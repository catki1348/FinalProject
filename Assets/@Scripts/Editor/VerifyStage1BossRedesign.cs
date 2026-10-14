using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

// 임시 TestNNN 씬에서만 수행하는 1-5 통합 검증. 본게임 저장 데이터에는 테스트 상태를 남기지 않는다.
// OpenAI Codex (GPT-6), 2026-10-14.
public static class VerifyStage1BossRedesign
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [Serializable] public class Result { public string check; public bool pass; public string detail; }
    private static void Set(object target, string name, object value) { target.GetType().GetField(name, Private).SetValue(target, value); }
    private static void Call(object target, string name) { target.GetType().GetMethod(name, Private).Invoke(target, null); }

    // 타격/충돌 결과를 실제 컴포넌트와 Physics2D로 확인하고 원래 시뮬레이션 모드로 복구한다.
    public static string Run()
    {
        var scene = SceneManager.GetActiveScene();
        if (!Application.isPlaying || !scene.name.StartsWith("Test")) throw new InvalidOperationException("Use a temporary TestNNN scene in play mode");
        var results = new List<Result>();
        Action<string, bool, string> record = (name, pass, detail) => results.Add(new Result { check = name, pass = pass, detail = detail });
        var boss = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Boss>()).Single();
        var controller = boss.GetComponent<BossController>(); controller.enabled = false;
        boss.GetComponentInChildren<Animator>().enabled = false;
        var slam = boss.GetComponent<BossArenaGroundSlam>();
        var floor = slam.ArenaFloor;
        var player = GameManager.Instance.PlayerTransform;
        foreach (var behaviour in player.GetComponents<MonoBehaviour>())
            if (!(behaviour is PlayerHealth) && !(behaviour is PlayerStats) && !(behaviour is PlayerEquipment) && !(behaviour is PlayerPlatformDrop)) behaviour.enabled = false;
        var body = player.GetComponent<Rigidbody2D>(); var collider = player.GetComponent<Collider2D>();
        var drop = player.GetComponent<PlayerPlatformDrop>();
        var health = player.GetComponent<PlayerHealth>();
        var bonus = boss.GetComponent<BossHeadProximityDamage>();
        var ledges = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BossArenaLedge>()).ToArray();
        var bossBody = boss.GetComponent<Rigidbody2D>();
        bossBody.bodyType = RigidbodyType2D.Kinematic;
        bossBody.position = new Vector2(floor.bounds.center.x, floor.bounds.max.y);
        Physics2D.SyncTransforms();
        float offset = body.position.y - collider.bounds.min.y;
        var oldMode = Physics2D.simulationMode; Physics2D.simulationMode = SimulationMode2D.Script;
        Action<int> simulate = count => { for (int i = 0; i < count; i++) { Physics2D.SyncTransforms(); foreach (var l in ledges) l.UpdatePlayerCollision(); Call(drop, "FixedUpdate"); Physics2D.Simulate(0.02f); } };
        Action<float, float> place = (x, foot) => { drop.Restore(); body.position = new Vector2(x, foot + offset); body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms(); };
        try
        {
            record("30 ledges", ledges.Length == 30, "actual prefab instances");
            foreach (var ledge in ledges)
            {
                var surface = ledge.GetComponent<Collider2D>().bounds;
                place(surface.center.x, surface.max.y + 0.25f); simulate(25);
                var movement = player.GetComponent<PlayerMovement>();
                var check = (Transform)movement.GetType().GetField("groundCheck", Private).GetValue(movement);
                int mask = ((LayerMask)movement.GetType().GetField("groundLayer", Private).GetValue(movement)).value;
                bool grounded = Physics2D.OverlapCircle(check.position, 0.1f, mask) != null;
                record("landing " + ledge.name, Mathf.Abs(collider.bounds.min.y - surface.max.y) < 0.06f && grounded,
                    "foot=" + collider.bounds.min.y + ";top=" + surface.max.y + ";grounded=" + grounded);
            }

            // 안쪽 격자를 한 층씩 80% 점프 속도로 실제 물리 상승한다.
            foreach (int side in new[] { -1, 1 })
            {
                var spine = ledges.Where(l => (l.transform.position.x - floor.bounds.center.x) * side > 0)
                    .GroupBy(l => Mathf.RoundToInt((l.GetComponent<Collider2D>().bounds.max.y - floor.bounds.max.y) / 0.8f))
                    .OrderBy(g => g.Key).Select(g => g.OrderBy(l => l.transform.position.x).First()).ToArray();
                float priorTop = floor.bounds.max.y;
                float priorX = spine[0].transform.position.x;
                foreach (var next in spine)
                {
                    var surface = next.GetComponent<Collider2D>().bounds;
                    place(priorX, priorTop + 0.015f);
                    body.linearVelocity = new Vector2((surface.center.x - priorX) / 0.36f, 7.2f);
                    simulate(18); body.linearVelocity = new Vector2(0, body.linearVelocity.y); simulate(12);
                    record("80% ascent " + next.name, Mathf.Abs(collider.bounds.min.y - surface.max.y) < 0.06f, "foot=" + collider.bounds.min.y + ";target=" + surface.max.y);
                    priorX = surface.center.x; priorTop = surface.max.y;
                }
            }

            // 단차 0.8: 100% 점프(속도 9)로도 한 번에 두 단(1.6)은 올라갈 수 없고 한 단만 오른다.
            Func<int, Collider2D> tierLedge = t => ledges.Where(l => Mathf.Abs(l.GetComponent<Collider2D>().bounds.max.y - floor.bounds.max.y - 0.8f * t) < 0.03f)
                .OrderBy(l => l.transform.position.x).Last().GetComponent<Collider2D>();
            for (int k = 0; k <= 3; k++)
            {
                float startTop = k == 0 ? floor.bounds.max.y : tierLedge(k).bounds.max.y;
                float startX = k == 0 ? tierLedge(1).bounds.center.x : tierLedge(k).bounds.center.x;
                place(startX, startTop + 0.015f); simulate(3);
                body.linearVelocity = new Vector2(0, 9f);
                float maxFoot = collider.bounds.min.y;
                for (int i = 0; i < 70; i++) { simulate(1); maxFoot = Mathf.Max(maxFoot, collider.bounds.min.y); }
                float skipTop = floor.bounds.max.y + 0.8f * (k + 2);
                record("full jump cannot skip from tier " + k, maxFoot < skipTop - 0.1f, "apexFoot=" + maxFoot + ";twoTierTop=" + skipTop);
            }

            // 검 판정(attackPoint 반경)이 몸통/머리 판정에 닿는지 단별로 확인한다. 머리는 5단에서만 닿아야 한다.
            var combat = player.GetComponent<PlayerCombat>();
            var attackPoint = (Transform)combat.GetType().GetField("attackPoint", Private).GetValue(combat);
            float attackRange = (float)combat.GetType().GetField("attackRange", Private).GetValue(combat);
            int enemyMask = ((LayerMask)combat.GetType().GetField("enemyLayer", Private).GetValue(combat)).value;
            var bodyHit = boss.GetComponent<Collider2D>();
            var headHit = boss.GetComponentsInChildren<Collider2D>().Single(c => c.name == "HeadHurtbox");
            var bossVisual = boss.GetComponentInChildren<Animator>().transform;
            bossBody.bodyType = RigidbodyType2D.Kinematic;
            foreach (int tier in new[] { 1, 2, 3, 4, 5 })
            {
                var surface = tierLedge(tier).bounds;
                place(surface.center.x, surface.max.y + 0.015f); simulate(2);
                bossBody.position = new Vector2(surface.center.x + 2.2f, floor.bounds.max.y);
                bossVisual.localScale = new Vector3(-Mathf.Abs(bossVisual.localScale.x), bossVisual.localScale.y, 1);
                Physics2D.SyncTransforms();
                var hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyMask);
                bool head = hits.Contains(headHit), bodyTouched = hits.Contains(bodyHit);
                record("sword reach tier " + tier, bodyTouched && head == (tier == 5), "body=" + bodyTouched + ";head=" + head);
            }
            bossVisual.localScale = new Vector3(Mathf.Abs(bossVisual.localScale.x), bossVisual.localScale.y, 1);

            // 보스가 모든 상층을 통과하여 최하단 바닥에 내려앉는다. 플레이어는 떨어진 위치로 옮긴다.
            place(floor.bounds.min.x + 2, floor.bounds.max.y + 0.015f);
            var summit = ledges.OrderByDescending(l => l.transform.position.y).First().GetComponent<Collider2D>();
            bossBody.bodyType = RigidbodyType2D.Dynamic; bossBody.gravityScale = 3;
            bossBody.position = new Vector2(summit.bounds.center.x, summit.bounds.max.y + 0.2f);
            bossBody.linearVelocity = Vector2.zero; Physics2D.SyncTransforms(); simulate(70);
            record("boss passes all tiers", Mathf.Abs(boss.GetComponent<Collider2D>().bounds.min.y - floor.bounds.max.y) < 0.06f,
                "foot=" + boss.GetComponent<Collider2D>().bounds.min.y);
            record("actual floor contact", boss.GetComponent<Collider2D>().IsTouching(floor), "Collider2D.IsTouching arenaFloor");
            bossBody.bodyType = RigidbodyType2D.Kinematic;
            bossBody.position = new Vector2(floor.bounds.center.x, floor.bounds.max.y); Physics2D.SyncTransforms();

            // 거리 경계/중간값 및 환경 피해를 검사한다.
            foreach (float distance in new[] { 0.5f, 0.75f, 1f, 2f, 3f, 4f, 6f })
            {
                Vector2 targetCenter = (Vector2)bonus.HeadAnchor.position + Vector2.right * distance;
                body.position += targetCenter - (Vector2)collider.bounds.center; Physics2D.SyncTransforms();
                float expected = 1 + Mathf.Clamp01((4 - distance) / 3.25f);
                int expectedDamage = Mathf.FloorToInt(10 * expected + 0.5f);
                int actual = bonus.Calculate(new DamageContext(10, collider));
                record("head distance " + distance, actual == expectedDamage, "damage=" + actual);
            }
            record("missing attacker base damage", bonus.Calculate(new DamageContext(10, null)) == 10, "damage=10");
            int hp = boss.CurrentHealth; boss.TakeDamage(10); record("environment no bonus", hp - boss.CurrentHealth == 10, "delta=" + (hp - boss.CurrentHealth));
            body.position += (Vector2)bonus.HeadAnchor.position - (Vector2)collider.bounds.center; Physics2D.SyncTransforms();
            hp = boss.CurrentHealth; boss.TakeDamage(new DamageContext(10, collider));
            record("context damage once", hp - boss.CurrentHealth == 20, "delta=" + (hp - boss.CurrentHealth));

            // 지면 양끝/중앙(보스 몸통 폭 2.4 때문에 보스 바로 옆)의 동일 피해와 한 번만 피해를 확인한다.
            int expectedLoss = Mathf.Max(1, 25 - player.GetComponent<PlayerStats>().FinalDefense);
            foreach (float x in new[] { floor.bounds.min.x + 1, floor.bounds.center.x + 2.5f, floor.bounds.max.x - 1 })
            {
                place(x, floor.bounds.max.y + 0.015f); simulate(3);
                Set(health, "currentHealth", 100); Set(health, "lastHitTime", -999f);
                slam.BeginWarning(); Set(slam, "warningStarted", Time.time - 1); bool activated = slam.Activate();
                Call(slam, "FixedUpdate"); int after = health.CurrentHealth; Set(health, "lastHitTime", -999f); Call(slam, "FixedUpdate");
                record("whole floor x=" + x, activated && 100 - after == expectedLoss && health.CurrentHealth == after,
                    "damage after defense=" + (100 - after) + ";duplicate delta=" + (after - health.CurrentHealth)); slam.Cancel();
            }
            foreach (int tier in new[] { 1, 2, 3, 4, 5 })
            {
                var surface = ledges.First(l => Mathf.Abs(l.GetComponent<Collider2D>().bounds.max.y - floor.bounds.max.y - 0.8f * tier) < 0.03f).GetComponent<Collider2D>().bounds;
                place(surface.center.x, surface.max.y + 0.015f); simulate(3);
                Set(health, "currentHealth", 100); Set(health, "lastHitTime", -999f);
                slam.BeginWarning(); Set(slam, "warningStarted", Time.time - 1); slam.Activate(); Call(slam, "FixedUpdate");
                record("safe tier " + tier, health.CurrentHealth == 100 && !slam.IsInHazard(collider.bounds), "hp=" + health.CurrentHealth); slam.Cancel();
            }
            slam.BeginWarning(); record("minimum warning", !slam.Activate(), "activation rejected before 0.8 seconds");
            Set(slam, "warningStarted", Time.time - 1); slam.Activate(); Set(slam, "activeUntil", Time.time - 0.01f); Call(slam, "FixedUpdate");
            record("attack expires", !slam.IsActive && !slam.IsWarning, "state cleared");

            // 최상단 발판에서 아래층으로 하강하고 충돌 무시를 복구한다.
            var top = summit.bounds;
            place(top.center.x, top.max.y + 0.015f); simulate(3);
            bool dropped = drop.TryDrop(); simulate(25);
            float finalFoot = collider.bounds.min.y;
            record("drop to lower tier", dropped && finalFoot < top.max.y - 0.4f && finalFoot > floor.bounds.max.y, "foot=" + finalFoot);
            drop.Restore(); record("drop restore", !player.GetComponent<PlayerMovement>().GroundingSuppressed, "grounding enabled");

            // 보스 처치가 기존 종료점 이벤트를 정확히 연결하는지 마지막에 확인한다.
            var exit = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BossZoneEndPoint>()).Single();
            record("exit locked", !exit.GetComponent<Collider2D>().enabled, "before boss death");
            boss.TakeDamage(1000);
            record("death unlocks exit", boss.IsDead && exit.GetComponent<Collider2D>().enabled, "after boss death");
        }
        finally { drop.Restore(); slam.Cancel(); Physics2D.simulationMode = oldMode; }
        return JsonUtility.ToJson(new Report { results = results.ToArray(), passed = results.Count(r => r.pass), failed = results.Count(r => !r.pass) }, true);
    }

    // 하단 공격(뿌리 쓸기)·주먹의 판정 높이 분담을 플레이어 높이별로 확인한다. 임시 TestNNN 씬 플레이 모드 전용.
    public static string RunLowAttack()
    {
        var scene = SceneManager.GetActiveScene();
        if (!Application.isPlaying || !scene.name.StartsWith("Test")) throw new InvalidOperationException("Use a temporary TestNNN scene in play mode");
        var results = new List<Result>();
        Action<string, bool, string> record = (name, pass, detail) => results.Add(new Result { check = name, pass = pass, detail = detail });
        var boss = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Boss>()).Single();
        var controller = boss.GetComponent<BossController>(); controller.enabled = false;
        boss.GetComponentInChildren<Animator>().enabled = false;
        var floor = boss.GetComponent<BossArenaGroundSlam>().ArenaFloor;
        var player = GameManager.Instance.PlayerTransform;
        foreach (var behaviour in player.GetComponents<MonoBehaviour>())
            if (!(behaviour is PlayerHealth) && !(behaviour is PlayerStats) && !(behaviour is PlayerEquipment)) behaviour.enabled = false;
        var body = player.GetComponent<Rigidbody2D>(); var collider = player.GetComponent<Collider2D>();
        var health = player.GetComponent<PlayerHealth>();
        var bossBody = boss.GetComponent<Rigidbody2D>(); bossBody.bodyType = RigidbodyType2D.Kinematic;
        bossBody.position = new Vector2(floor.bounds.center.x, floor.bounds.max.y); Physics2D.SyncTransforms();
        controller.GetType().GetField("target", Private).SetValue(controller, player);
        controller.GetType().GetMethod("Face", Private).Invoke(controller, new object[] { true }); // 주먹 판정은 바라보는 방향(오른쪽)으로 확인
        float offset = body.position.y - collider.bounds.min.y;
        float footY = floor.bounds.max.y;
        // 발 높이(보스 발 기준)와 수평 거리로 배치하고, 한 번의 판정 후 피해 여부를 돌려준다.
        Func<float, float, string, bool> hitBy = (rise, dx, which) =>
        {
            body.position = new Vector2(bossBody.position.x + dx, footY + rise + offset); body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms();
            Set(health, "currentHealth", 100); Set(health, "lastHitTime", -999f);
            if (which == "low") controller.GetType().GetMethod("DealLowDamage", Private).Invoke(controller, new object[] { 1f, footY });
            else Call(controller, "DealPunchDamage");
            return health.CurrentHealth < 100;
        };
        var isLow = controller.GetType().GetMethod("IsTargetLow", Private);
        Func<float, bool> low = rise => { body.position = new Vector2(bossBody.position.x + 3f, footY + rise + offset); Physics2D.SyncTransforms(); return (bool)isLow.Invoke(controller, null); };
        record("floor is low target", low(0f), "feet 0");
        record("tier1 is low target", low(0.8f), "feet 0.8");
        record("tier2 is not low target", !low(1.6f), "feet 1.6");
        record("low hits floor (near)", hitBy(0f, 1.8f, "low"), "dx 1.8");
        record("low hits floor (far end)", hitBy(0f, 4.3f, "low"), "dx 4.3 within reach 4.6");
        record("low misses beyond reach", !hitBy(0f, 5.5f, "low"), "dx 5.5");
        record("low misses behind boss", !hitBy(0f, -3f, "low"), "dx -3");
        record("low hits tier1", hitBy(0.8f, 3f, "low"), "feet 0.8 < height 0.9");
        record("low misses tier2", !hitBy(1.6f, 3f, "low"), "feet 1.6");
        record("low dodged by jump apex", !hitBy(1.1f, 3f, "low"), "feet 1.1 (jump apex 1.376)");
        record("punch misses floor", !hitBy(0f, 3f, "punch"), "box starts at 1.4");
        record("punch hits tier2", hitBy(1.6f, 3f, "punch"), "feet 1.6");
        record("punch hits tier4", hitBy(3.2f, 3f, "punch"), "feet 3.2");
        record("punch misses tier5", !hitBy(4f, 3f, "punch"), "feet 4.0");
        Set(health, "currentHealth", 100); Set(health, "lastHitTime", -999f);
        body.position = new Vector2(bossBody.position.x + 3f, footY + offset); Physics2D.SyncTransforms();
        controller.GetType().GetMethod("DealLowDamage", Private).Invoke(controller, new object[] { 1f, footY });
        int afterOne = health.CurrentHealth;
        record("low single damage", 100 - afterOne == Mathf.Max(1, 18 - player.GetComponent<PlayerStats>().FinalDefense), "hp=" + afterOne);
        var vfx = boss.GetComponent<BossRootSweepVfx>();
        vfx.ShowWarning(footY, 1f, 4.6f, 0.9f); var warn = boss.transform.Find("RootSweepWarning").GetComponent<SpriteRenderer>();
        record("warning visible with sprite", warn.enabled && warn.sprite != null, "enabled=" + warn.enabled);
        vfx.Cancel(); record("cancel clears warning", !warn.enabled, "enabled=" + warn.enabled);
        var clip = boss.GetComponentInChildren<Animator>().runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == "Boss_LowAttack");
        record("low attack clip present", clip != null && clip.length > 1.2f, clip == null ? "missing" : "length=" + clip.length);
        return JsonUtility.ToJson(new Report { results = results.ToArray(), passed = results.Count(r => r.pass), failed = results.Count(r => !r.pass) }, true);
    }
    [Serializable] private class Report { public int passed, failed; public Result[] results; }
}
