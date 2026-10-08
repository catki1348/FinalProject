using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 메뉴에서 다시 실행할 수 있는 슬라이드 계산 확인. 실제 맵 플레이 확인과는 별개다.
public static class SlopeSlideCheck
{
    [MenuItem("Tools/Checks/1-2 Slope Slide")]
    public static void Run()
    {
        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        int index = 0;
        while (System.IO.File.Exists("Assets/Scenes/Test" + index.ToString("000") + ".unity") ||
               UnityEngine.SceneManagement.SceneManager.GetSceneByName("Test" + index.ToString("000")).IsValid()) index++;
        scene.name = "Test" + index.ToString("000");
        try
        {
            var go = new GameObject("SlideCheck");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            var movement = go.AddComponent<PlayerMovement>();
            movement.enabled = false;
            Set(movement, "rb", body);
            var apply = typeof(PlayerMovement).GetMethod("ApplySlopeSlide", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (float angle in new[] { -30f, 30f, -75f, 75f })
            {
                movement.SetSlopeSlideEnabled(true);
                Set(movement, "groundNormal", (Vector2)(Quaternion.Euler(0, 0, angle) * Vector2.up));
                body.linearVelocity = Vector2.zero;
                apply.Invoke(movement, null);
                Require(movement.IsSliding && body.linearVelocity.y < 0f, "Slope must slide downhill");
                for (int i = 0; i < 300; i++) apply.Invoke(movement, null);
                Require(body.linearVelocity.magnitude <= 12.001f, "Slide speed cap");
                float beforeBrake = body.linearVelocity.magnitude;
                Set(movement, "moveInput", -Mathf.Sign(body.linearVelocity.x));
                apply.Invoke(movement, null);
                Require(body.linearVelocity.magnitude < beforeBrake, "Opposite input must brake");
                Set(movement, "moveInput", 0f);
                Set(movement, "groundNormal", Vector2.up);
                for (int i = 0; i < 100; i++) apply.Invoke(movement, null);
                Require(!movement.IsSliding, "Flat ground must end slide");
            }
            movement.SetSlopeSlideEnabled(false);
            Require(!movement.IsSliding, "Zone exit must clear slide");
            // U자 곡선 바닥에서 왼쪽 급경사 연장선이 아닌 발밑의 실제 평면을 반환한다.
            var ground = new GameObject("CurveGround");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ground, scene);
            ground.layer = 8;
            var curve = ground.AddComponent<PolygonCollider2D>();
            curve.points = new[] { new Vector2(-2, 2), new Vector2(-0.5f, 0), new Vector2(0.5f, 0), new Vector2(2, 2), new Vector2(2, -1), new Vector2(-2, -1) };
            var box = go.AddComponent<BoxCollider2D>();
            box.offset = Vector2.up * 0.5f;
            go.transform.position = Vector3.up * 0.2f;
            Set(movement, "bodyCollider", box);
            Set(movement, "groundLayer", (LayerMask)(1 << 8));
            Physics2D.SyncTransforms();
            RaycastHit2D surface;
            Require(movement.TryGetVisualGround(out surface) && Mathf.Abs(surface.point.y) < 0.01f && surface.normal.y > 0.99f,
                "Curve bottom must use the actual surface under the visual");
            // 충돌 해석이 수직 속도를 0으로 만들어도 경사 모션 판정은 이동 계산 결과를 유지한다.
            Set(movement, "isGrounded", true);
            Set(movement, "hasWalkableGround", true);
            body.linearVelocity = Vector2.zero;
            foreach (float angle in new[] { -30f, 30f })
            {
                Vector2 normal = Quaternion.Euler(0, 0, angle) * Vector2.up;
                Set(movement, "groundNormal", normal);
                Set(movement, "slopeSpeed", -Mathf.Sign(normal.x) * 5f);
                Require(movement.IsMovingUphill && !movement.IsMovingDownhill, "Climb pose must survive solver velocity changes");
                Set(movement, "slopeSpeed", Mathf.Sign(normal.x) * 5f);
                Require(movement.IsMovingDownhill && !movement.IsMovingUphill, "Slide pose must survive solver velocity changes");
            }
            movement.SetSlopeSlideEnabled(true);
            Set(movement, "isJumping", true);
            Set(movement, "isGrounded", true);
            Set(movement, "slideAirborne", true);
            body.linearVelocity = new Vector2(5f, 9f);
            typeof(PlayerMovement).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(movement, null);
            Require(body.linearVelocity == new Vector2(5f, 9f), "Jump must retain launch velocity");
            Debug.Log("SlopeSlideCheck: both directions, speed cap, brake, flat exit, zone reset and jump passed.");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
        }
    }

    // 기존 이동 컴포넌트의 내부 계산만 호출해 테스트용 런타임 API를 추가하지 않는다.
    private static void Set(PlayerMovement target, string field, object value)
    {
        typeof(PlayerMovement).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

