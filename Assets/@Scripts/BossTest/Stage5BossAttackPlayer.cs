// Stage5BossAttackPlayer.cs - plays one Stage 5 boss attack (sprite frames + timing + lunge + hit windows) in the TEST scene.
// AI attestation (rule 54): written by Claude Sonnet 5.5, 2026-10-15. Concept/preview test only (rule 51).
// Single role: animation playback and hit-window bookkeeping. It never reads input and never touches the player (rule 35);
// other scripts subscribe to its events / read its Hits array (rule 36).
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Stage5BossTest
{
    // A damage window currently open (or a warning box) in world coordinates. Pooled: allocated once in Awake.
    public sealed class ActiveHit
    {
        public Rect Rect;
        public int Damage;
        public HitHeight Height;
        public string Label;
        public bool Open;
        public bool Consumed;
        public bool PiercesIFrames;   // chain attack 4/5 hits ignore the 1s invincibility so 40 + 40 can total 80
    }

    public sealed class Stage5BossAttackPlayer : MonoBehaviour
    {
        [Serializable] public struct NamedSprite { public string name; public Sprite sprite; }

        [SerializeField] private List<NamedSprite> sprites = new List<NamedSprite>(); // "IDLE", "N1_1" ... (assigned by the setup script)
        [SerializeField] private SpriteRenderer visual;   // the body sprite
        [SerializeField] private SpriteRenderer ghost;    // afterimage of the previous frame
        [SerializeField] private float pixelsPerUnit = 270f;
        [SerializeField] private float shakeAmount = 0.03f;
        [SerializeField] private float idleLead = 0.16f;  // neutral pose before the first attack of a combo
        [SerializeField] private float minImpactHold = 0.11f;

        public const int MaxHits = 4;
        public readonly ActiveHit[] Hits = new ActiveHit[MaxHits];      // damage windows
        public readonly ActiveHit[] Warnings = new ActiveHit[MaxHits];  // danger-zone previews before each window

        public event Action<AttackDef> AttackStarted;
        public event Action<AttackDef> AttackEnded;
        public event Action<ActiveHit> HitOpened;
        public event Action<ActiveHit> HitClosed;

        public AttackDef Current { get; private set; }
        public bool IsPlaying { get; private set; }

        private readonly Dictionary<string, Sprite> map = new Dictionary<string, Sprite>();
        private readonly Rect[] rects = new Rect[MaxHits];

        private void Awake()
        {
            for (int i = 0; i < MaxHits; i++) { Hits[i] = new ActiveHit(); Warnings[i] = new ActiveHit(); }
            for (int i = 0; i < sprites.Count; i++) map[sprites[i].name] = sprites[i].sprite;
            if (ghost != null) ghost.enabled = false;
        }

        // Shows the neutral pose facing left (-1) or right (+1). Sprites are drawn facing right.
        public void ShowIdle(int facing)
        {
            if (visual == null || !map.TryGetValue("IDLE", out Sprite idle)) return;
            visual.sprite = idle;
            visual.flipX = facing < 0;
            visual.transform.localPosition = Vector3.zero;
            if (ghost != null) ghost.enabled = false;
        }

        // Plays one attack as a coroutine. chained: skip the neutral lead-in; hasNext: skip the recovery (next attack follows).
        // targetX is the player position locked at the start (used by the Wing Dive).
        public IEnumerator Play(AttackDef def, int facing, float targetX, bool chained, bool hasNext)
        {
            Current = def; IsPlaying = true;
            AttackStarted?.Invoke(def);
            float startX = transform.position.x;
            float y = transform.position.y, z = transform.position.z;
            BuildRects(def, facing, startX, targetX);

            if (!chained)
            {
                ShowIdle(facing);
                yield return new WaitForSeconds(idleLead);
            }

            Sprite prev = chained ? visual.sprite : null;                       // chained: afterimage continues from the previous attack
            float prevOffset = chained ? visual.transform.localPosition.x : 0f;
            int first = chained ? def.ChainSkip : 0;                            // chained attacks skip their anticipation pose
            float travelTotal = 0f;
            if (def.TravelToTarget) for (int i = 1; i < def.ImpactIndex; i++) travelTotal += def.Holds[i];
            float travelElapsed = 0f;

            for (int i = first; i < def.Frames.Length; i++)
            {
                if (!map.TryGetValue(def.Frames[i], out Sprite sp)) { Debug.LogWarning("Missing sprite " + def.Frames[i]); continue; }
                float offset = def.OffsetsPx[i] / pixelsPerUnit * facing;
                float hold = def.Holds[i];
                visual.sprite = sp; visual.flipX = facing < 0;

                bool hitFrame = false;
                for (int k = 0; k < def.Hits.Length; k++)    // open damage windows / warnings for this frame
                {
                    HitDef hd = def.Hits[k];
                    bool warn = i >= def.WarnFrom && i < hd.FrameIndex;
                    SetSlot(Warnings[k], k, hd, def, warn, false);
                    if (hd.FrameIndex == i)
                    {
                        hitFrame = true;
                        SetSlot(Hits[k], k, hd, def, true, true);
                        HitOpened?.Invoke(Hits[k]);
                    }
                }
                if (hitFrame) hold = Mathf.Max(hold, minImpactHold);

                if (ghost != null)   // afterimage trail on the fast frames
                {
                    bool showGhost = i >= def.ImpactIndex - 1 && i > 0 && prev != null;
                    ghost.enabled = showGhost;
                    if (showGhost)
                    {
                        ghost.sprite = prev; ghost.flipX = facing < 0;
                        ghost.color = new Color(1f, 1f, 1f, 0.22f);
                        ghost.transform.localPosition = new Vector3(prevOffset, 0f, 0f);
                    }
                }

                float t = 0f;
                while (t < hold)
                {
                    float dt = Time.deltaTime;
                    t += dt;
                    float shake = (hitFrame && t < 0.09f) ? ((Mathf.FloorToInt(t / 0.045f) % 2 == 0) ? shakeAmount : -shakeAmount) : 0f;
                    visual.transform.localPosition = new Vector3(offset + shake, 0f, 0f);
                    if (def.TravelToTarget && i >= 1 && i < def.ImpactIndex && travelTotal > 0f)
                    {
                        travelElapsed += dt;
                        float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(travelElapsed / travelTotal));
                        transform.position = new Vector3(Mathf.Lerp(startX, targetX, p), y, z);
                    }
                    yield return null;
                }
                if (def.TravelToTarget && i == def.ImpactIndex - 1) transform.position = new Vector3(targetX, y, z);

                for (int k = 0; k < def.Hits.Length; k++)   // close this frame's windows
                {
                    Warnings[k].Open = false;
                    if (Hits[k].Open && def.Hits[k].FrameIndex == i) { Hits[k].Open = false; HitClosed?.Invoke(Hits[k]); }
                }
                prev = sp; prevOffset = offset;
            }

            if (ghost != null) ghost.enabled = false;
            if (!hasNext)
            {
                yield return new WaitForSeconds(0.09f);
                ShowIdle(facing);
            }
            IsPlaying = false;
            AttackEnded?.Invoke(def);
        }

        // Computes each hit box in world space once per attack (facing mirrors front boxes).
        private void BuildRects(AttackDef def, int facing, float startX, float targetX)
        {
            for (int k = 0; k < def.Hits.Length && k < MaxHits; k++)
            {
                HitDef hd = def.Hits[k];
                float x0, x1;
                if (hd.AtTarget) { x0 = targetX + hd.XMin; x1 = targetX + hd.XMax; }
                else if (hd.Symmetric) { x0 = startX - hd.XMax; x1 = startX + hd.XMax; }
                else if (facing > 0) { x0 = startX + hd.XMin; x1 = startX + hd.XMax; }
                else { x0 = startX - hd.XMax; x1 = startX - hd.XMin; }
                rects[k] = Rect.MinMaxRect(x0, hd.YMin, x1, hd.YMax);
            }
        }

        // Fills one pooled slot (hit window or warning) and sets it open/closed.
        private void SetSlot(ActiveHit slot, int k, HitDef hd, AttackDef def, bool open, bool resetConsumed)
        {
            slot.Rect = rects[k]; slot.Damage = hd.Damage; slot.Height = hd.Height; slot.Label = def.Label; slot.PiercesIFrames = def.IsSpecial;
            slot.Open = open;
            if (resetConsumed) slot.Consumed = false;
        }
    }
}
