using System.Collections;
using UnityEngine;

// 밟으면 일정 시간 후 사라지고, 일정 시간 뒤 다시 나타나는 발판 타입(무너지는 발판, 어떤 발판 외형 프리팹에든 붙여 사용)
public class DisappearingFoothold : PlatformBehaviour
{
    [SerializeField] private float vanishDelay = 0.4f;  // 밟은 뒤 사라지기까지 걸리는 시간
    [SerializeField] private float respawnDelay = 2f;    // 사라진 뒤 다시 나타나기까지 걸리는 시간(0 이하면 영구 소멸)

    private Coroutine vanishRoutine;

    // 한 번 밟으면 발을 중간에 떼도 사라짐이 확정되는 방식(전형적인 무너지는 발판 특성)
    protected override void OnPlayerStand(Rigidbody2D playerBody)
    {
        vanishRoutine ??= StartCoroutine(VanishAfterDelay());
    }

    private IEnumerator VanishAfterDelay()
    {
        yield return new WaitForSeconds(vanishDelay);
        SetPlatformActive(false);

        if (respawnDelay > 0f)
        {
            yield return new WaitForSeconds(respawnDelay);
            SetPlatformActive(true);
        }

        vanishRoutine = null;
    }
}
