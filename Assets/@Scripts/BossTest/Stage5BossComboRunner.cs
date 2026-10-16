// Stage5BossComboRunner.cs - triggers Stage 5 boss attacks/combos in the TEST scene (keys + auto mode).
// AI attestation (rule 54): written by Claude Sonnet 5.5, 2026-10-15. Concept/preview test only (rule 51).
// Single role: choose and sequence attacks; playback is done by Stage5BossAttackPlayer.
// Keys: 1-3 = basic attack N1/N2/N3 alone, 4-6 = N1/N2/N3 + chain attacks 4+5, Tab = auto mode on/off.
// Design v3 (BossDesign_Stage5): five attacks; never the same pattern twice in a row; 1.0s pause between patterns.
using System.Collections;
using UnityEngine;

namespace Stage5BossTest
{
    public sealed class Stage5BossComboRunner : MonoBehaviour
    {
        [SerializeField] private Stage5BossAttackPlayer attackPlayer;
        [SerializeField] private Transform target;           // the dummy player
        [SerializeField] private float gapSeconds = 1f;
        [SerializeField] private float limitX = 14f;
        [SerializeField] private bool autoOnStart = true;    // boss starts attacking by itself; Tab toggles

        private static readonly KeyCode[] ComboKeys =
        { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6 };

        public string CurrentCombo { get; private set; } = "-";
        public bool AutoMode { get; private set; }

        private bool busy;
        private float startTime;
        private int lastCombo = -1;
        private int facing = -1;

        private void Start()
        {
            startTime = Time.time;
            AutoMode = autoOnStart;
            facing = FacingToTarget();
            attackPlayer.ShowIdle(facing);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab)) AutoMode = !AutoMode;
            if (!busy && !attackPlayer.IsPlaying) { HandleManualKeys(); if (!busy && AutoMode) PickAuto(); }
            if (!busy) { facing = FacingToTarget(); }
        }

        private int FacingToTarget() { return target.position.x < transform.position.x ? -1 : 1; }

        // Manual trigger keys: 1-3 basic attack alone, 4-6 basic attack + chain 4+5 (also usable while auto mode is on).
        private void HandleManualKeys()
        {
            for (int i = 0; i < ComboKeys.Length; i++)
                if (Input.GetKeyDown(ComboKeys[i])) { Begin(Stage5BossAttackDefs.Combos[i].Label, Stage5BossAttackDefs.Combos[i].Ids, Stage5BossAttackDefs.Combos[i].HasSpecial); return; }
        }

        // Auto mode: random pattern among the 6, never the same twice in a row.
        private void PickAuto()
        {
            int count = Stage5BossAttackDefs.Combos.Length;
            int n = Random.Range(0, count);
            if (n == lastCombo) n = (n + 1 + Random.Range(0, count - 1)) % count;
            lastCombo = n;
            Begin(Stage5BossAttackDefs.Combos[n].Label, Stage5BossAttackDefs.Combos[n].Ids, Stage5BossAttackDefs.Combos[n].HasSpecial);
        }

        private void Begin(string label, string[] ids, bool hasSpecial)
        {
            StartCoroutine(RunCombo(label, ids));
        }

        // Plays the attacks back to back, then waits the combo gap.
        private IEnumerator RunCombo(string label, string[] ids)
        {
            busy = true;
            CurrentCombo = label;
            for (int i = 0; i < ids.Length; i++)
            {
                AttackDef def = Stage5BossAttackDefs.Get(ids[i]);
                if (def == null) continue;
                facing = FacingToTarget();                              // direction is fixed from attack start to recovery end
                float tx = Mathf.Clamp(target.position.x, -limitX, limitX);
                yield return attackPlayer.Play(def, facing, tx, i > 0, i < ids.Length - 1);
            }
            yield return new WaitForSeconds(gapSeconds);
            CurrentCombo = "-";
            busy = false;
        }
    }
}
