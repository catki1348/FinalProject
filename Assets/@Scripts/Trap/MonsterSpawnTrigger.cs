using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 플레이어가 영역에 머무는 동안 일정 주기로 몬스터를 반복 스폰하는 함정.
// 이 함정이 만든 생존 몬스터가 blockAliveAtLeast 마리 이상이면 그 회차는 건너뛴다.
// 영역을 벗어나면 스폰만 멈추고 이미 스폰된 몬스터는 그대로 남는다(추격 범위 제한은 몬스터 프리팹의 감지 영역이 담당).
// 경로형 패턴(PathPatrol 계열)은 생성 시 path 참조가 비므로 지원하지 않는다.
// 사용법: 영역용 트리거 콜라이더(Is Trigger)와 함께 붙이고, oneShot을 false로 두며,
// monsterPrefab / spawnPoints를 Inspector에서 연결한다.
public class MonsterSpawnTrigger : ZoneEventTrigger
{
    [SerializeField] private MonsterStats monsterPrefab;     // 스폰할 몬스터 프리팹(구간별 감지 영역 변형)
    [SerializeField] private Transform[] spawnPoints;        // 스폰 지점(비행종은 공중, Monster.md 규칙 10)
    [SerializeField] private Transform spawnRoot;            // 생성 몬스터를 모아둘 부모(비워두면 씬 루트)
    [SerializeField] private int monstersPerWave = 3;        // 한 회차에 스폰하는 마리 수(지점이 모자라면 순환)
    [SerializeField] private float waveInterval = 5f;        // 회차 간격(초)
    [SerializeField] private int blockAliveAtLeast = 10;     // 생존 몬스터가 이 수 이상이면 스폰 건너뜀
    [SerializeField] private float corpseLifetime = 2f;      // 사망 후 오브젝트가 남는 시간(초)

    private readonly List<MonsterStats> alive = new List<MonsterStats>(); // 이 함정이 스폰한 생존 몬스터
    private WaitForSeconds intervalWait;                                  // 규칙 39: 매 주기 새로 할당하지 않도록 캐싱
    private Coroutine spawnLoop;
    private float lastWaveTime = float.NegativeInfinity;                  // 입퇴장 반복으로 주기를 우회하지 못하게 기록

    protected override Color GizmoColor => Color.red;

    protected override void Awake()
    {
        base.Awake();
        intervalWait = new WaitForSeconds(waveInterval);
    }

    // 플레이어가 영역에 들어오면 스폰 루프 시작(이미 돌고 있으면 무시)
    protected override void OnPlayerEnterZone(Rigidbody2D playerBody)
    {
        if (spawnLoop == null && CanSpawn()) spawnLoop = StartCoroutine(SpawnLoop());
    }

    // 플레이어가 영역을 벗어나면 스폰만 멈춘다(몬스터는 유지)
    private void OnTriggerExit2D(Collider2D other)
    {
        Rigidbody2D body = other.attachedRigidbody;
        if (body == null || !body.TryGetComponent<PlayerMovement>(out _)) return;
        StopSpawning();
    }

    private void OnDisable()
    {
        StopSpawning();
    }

    private void StopSpawning()
    {
        if (spawnLoop == null) return;
        StopCoroutine(spawnLoop);
        spawnLoop = null;
    }

    // 필수 참조가 비어 있으면 스폰하지 않는다(OnValidate 경고와 짝)
    private bool CanSpawn()
    {
        return monsterPrefab != null && spawnPoints != null && spawnPoints.Length > 0
            && monstersPerWave > 0 && waveInterval > 0f;
    }

    // 진입 즉시 한 번, 이후 waveInterval마다 반복. 직전 회차 이후 간격이 남아 있으면 그만큼 기다린 뒤 시작한다
    private IEnumerator SpawnLoop()
    {
        float remaining = waveInterval - (Time.time - lastWaveTime);
        if (remaining > 0f) yield return new WaitForSeconds(remaining); // 진입 시 1회만 할당

        while (true)
        {
            SpawnWave();
            lastWaveTime = Time.time;
            yield return intervalWait;
        }
    }

    // 한 회차: 생존 수 정리 -> 상한 검사 -> monstersPerWave마리 생성
    private void SpawnWave()
    {
        alive.RemoveAll(m => m == null); // 씬 정리 등으로 사라진 항목 제거
        if (alive.Count >= blockAliveAtLeast) return; // 근처 몬스터가 상한 이상이면 건너뜀

        for (int i = 0; i < monstersPerWave; i++)
        {
            Transform point = spawnPoints[i % spawnPoints.Length];
            MonsterStats monster = Instantiate(monsterPrefab, point.position, Quaternion.identity, spawnRoot);
            alive.Add(monster);
            monster.OnDeath += () => HandleMonsterDeath(monster); // 몬스터별 클로저(스폰 시점에만 할당)
        }
    }

    // 사망: 생존 목록에서 제거하고 corpseLifetime 뒤 오브젝트를 정리
    private void HandleMonsterDeath(MonsterStats monster)
    {
        alive.Remove(monster);
        if (monster != null) Destroy(monster.gameObject, corpseLifetime);
    }

    // Inspector 값 오류를 제작 시점에 알려주는 경고
    private void OnValidate()
    {
        if (monsterPrefab == null) Debug.LogWarning($"{name}: monsterPrefab이 비어 있습니다.", this);
        if (spawnPoints == null || spawnPoints.Length == 0) Debug.LogWarning($"{name}: spawnPoints가 비어 있습니다.", this);
        if (monstersPerWave <= 0) Debug.LogWarning($"{name}: monstersPerWave는 1 이상이어야 합니다.", this);
        if (waveInterval <= 0f) Debug.LogWarning($"{name}: waveInterval은 0보다 커야 합니다.", this);
        if (blockAliveAtLeast <= 0) Debug.LogWarning($"{name}: blockAliveAtLeast는 1 이상이어야 합니다(0이면 스폰되지 않음).", this);

        if (monsterPrefab == null) return;
        if (monsterPrefab.GetComponent<PathPatrolPattern>() != null || monsterPrefab.GetComponent<FlyingPathPatrolPattern>() != null)
            Debug.LogWarning($"{name}: 경로형 패턴 몬스터({monsterPrefab.name})는 path 참조가 유실되어 지원하지 않습니다.", this);
        if (monsterPrefab.GetComponent<FlyingChasePattern>() == null)
            Debug.LogWarning($"{name}: {monsterPrefab.name}에 FlyingChasePattern이 없습니다(추격형이 아님).", this);
        if (monsterPrefab.GetComponent<MonsterItemDropper>() != null)
            Debug.LogWarning($"{name}: {monsterPrefab.name}에 MonsterItemDropper가 있습니다. 반복 스폰 몬스터는 아이템을 드랍하지 않아야 하므로(회복/무장 복귀 파밍 방지) 제거하세요.", this);
    }

    // 씬 뷰에서 스폰 지점을 빨간 와이어 구로 표시
    private void OnDrawGizmosSelected()
    {
        if (spawnPoints == null) return;
        Gizmos.color = Color.red;
        foreach (Transform p in spawnPoints) if (p != null) Gizmos.DrawWireSphere(p.position, 0.3f);
    }
}
