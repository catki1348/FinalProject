// Stage5TestDummyPlayer.cs - a simple stand-in player for the Stage 5 boss test scene (no connection to the real player).
// AI attestation (rule 54): written by Claude Sonnet 5.5, 2026-10-15. Concept/preview test only (rule 51).
// Single role: move/jump with the same numbers as the real player (speed 8, jump height ~1.39), take damage from the
// boss hit windows, and report events. Damage is only logged (the real health system is not touched).
using System;
using UnityEngine;

namespace Stage5BossTest
{
    public sealed class Stage5TestDummyPlayer : MonoBehaviour
    {
        [SerializeField] private Stage5BossAttackPlayer attackPlayer;
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float jumpHeight = 1.39f;
        [SerializeField] private float gravity = 29.43f;      // gravity scale 3 * 9.81, same as the real player
        [SerializeField] private float height = 1.29f;
        [SerializeField] private float halfWidth = 0.3f;
        [SerializeField] private float invincibleSeconds = 1f;
        [SerializeField] private int maxHp = 100;
        [SerializeField] private float limitX = 14f;

        public int Hp { get; private set; }
        public int MaxHp => maxHp;
        public event Action<string> Log;            // text lines for the HUD

        private float velocityY;
        private float invincibleTimer;

        private void Awake() { Hp = maxHp; }

        private void OnEnable()
        {
            attackPlayer.HitClosed += HandleHitClosed;
        }

        private void OnDisable()
        {
            attackPlayer.HitClosed -= HandleHitClosed;
        }

        // A hit window closed without touching the player -> the player dodged it.
        private void HandleHitClosed(ActiveHit hit)
        {
            if (!hit.Consumed) Log?.Invoke("DODGED  " + hit.Label);
        }

        private void Update()
        {
            Move();
            CheckHits();
            if (Input.GetKeyDown(KeyCode.R)) { Hp = maxHp; Log?.Invoke("HP restored"); }
            if (invincibleTimer > 0f) invincibleTimer -= Time.deltaTime;
            body.color = (invincibleTimer > 0f && Mathf.FloorToInt(invincibleTimer * 12f) % 2 == 0)
                ? new Color(1f, 1f, 1f, 0.35f) : new Color(0.3f, 0.85f, 1f, 0.9f);
        }

        // A/D (or arrows) to move, W/Up to jump.
        private void Move()
        {
            Vector3 p = transform.position;
            float h = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;
            p.x = Mathf.Clamp(p.x + h * moveSpeed * Time.deltaTime, -limitX, limitX);
            bool grounded = p.y <= 0f && velocityY <= 0f;
            if (grounded && (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))) velocityY = Mathf.Sqrt(2f * gravity * jumpHeight);
            velocityY -= gravity * Time.deltaTime;
            p.y += velocityY * Time.deltaTime;
            if (p.y <= 0f) { p.y = 0f; velocityY = 0f; }
            transform.position = p;
        }

        // Overlap test of the player's box against every open damage window (each window hits at most once).
        private void CheckHits()
        {
            Vector3 p = transform.position;
            Rect box = Rect.MinMaxRect(p.x - halfWidth, p.y, p.x + halfWidth, p.y + height);
            for (int k = 0; k < Stage5BossAttackPlayer.MaxHits; k++)
            {
                ActiveHit h = attackPlayer.Hits[k];
                if (!h.Open || h.Consumed || !h.Rect.Overlaps(box)) continue;
                h.Consumed = true;
                if (invincibleTimer > 0f && !h.PiercesIFrames) { Log?.Invoke("BLOCKED (i-frames)  " + h.Label); continue; }
                Hp = Mathf.Max(0, Hp - h.Damage);
                invincibleTimer = invincibleSeconds;
                Log?.Invoke("HIT  " + h.Label + "  -" + h.Damage + "  (HP " + Hp + ")" + (Hp == 0 ? "  DEAD - press R" : ""));
            }
        }
    }
}
