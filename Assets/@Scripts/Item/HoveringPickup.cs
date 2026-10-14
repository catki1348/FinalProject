using UnityEngine;

// 드랍된 자리에서 위아래로 일렁이며 제자리 호버링하는 연출 - 획득 로직과 분리했다(규칙 35 SRP).
// 중력/리지드바디를 쓰지 않고 Transform만 움직이므로, 몬스터가 낙사해도 아이템은 사망 위치에 머문다.
public class HoveringPickup : MonoBehaviour
{
    [SerializeField] private float amplitude = 0.15f; // 위아래 진폭(월드 유닛)
    [SerializeField] private float frequency = 1.5f;  // 초당 왕복 횟수

    private Vector3 origin;  // 호버링의 중심(스폰된 위치)
    private float phase;     // 여러 아이템이 동시에 같은 박자로 흔들리지 않도록 하는 시작 위상

    // 스폰된 위치를 중심점으로 기억(이후 이 점을 기준으로만 움직임)
    private void Start()
    {
        origin = transform.position;
        phase = Random.value * Mathf.PI * 2f;
    }

    // 사인파로 중심점 기준 상하 움직임만 적용(새 객체 할당 없음 - 규칙 39)
    private void Update()
    {
        float offset = Mathf.Sin(Time.time * frequency * Mathf.PI * 2f + phase) * amplitude;
        transform.position = new Vector3(origin.x, origin.y + offset, origin.z);
    }
}
