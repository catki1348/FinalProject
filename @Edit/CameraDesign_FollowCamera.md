# 팔로우 카메라 구조 설계안

> 이 문서는 설계만 다룹니다. 실제 Unity 작업(스크립트 생성/수정, 씬 오브젝트 배치 등)은 `LHS_Custumer.md` 규칙 9, 11에 따라 사용자 승인 후 별도로 진행합니다.
> **구현 준비 목적의 문서**이므로, "구현해" 지시만 받으면 6항의 파일 목록대로 바로 작업을 시작할 수 있을 만큼 구체적으로 작성합니다.
> `LHS_Custumer.md`(작업 수칙), `MapEdit.md`(맵 제작 수칙) 참고 완료 — 관련 반영 사항:
> - `LHS_Custumer.md` 규칙 46: `.md` 파일은 `@Edit` 폴더에 보관 → 이 문서 위치가 이미 `@Edit/CameraDesign_FollowCamera.md`로 규칙에 부합.
> - `MapEdit.md` 규칙 2, 3: 스테이지당 1~5영역, 표기는 `스테이지-영역`(예: `1-1`~`1-5`) → 이하 본문에서 기존 `Section_n` 표현을 이 표기에 맞춰 정리.
> - `MapEdit.md` 규칙 4: 영역 시작(★)/골인(○) 지점 통과 시 다음 영역으로 전환 → 기존 `SectionEdgeTrigger`의 경계 통과 방식과 그대로 일치, 별도 수정 불필요.
> - `MapEdit.md` 규칙 5: 5영역(`1-5`)에는 보스존이 별도 존재 → 3.7항에 보스존 카메라 처리 방침 추가.

## 1. 요구사항 정리

1. 메인 카메라는 플레이어를 따라 이동한다(연속 추적, 구간 스냅 방식 아님).
2. 배경이 없는 구역(맵/배경 바깥)에 도달하면 그 방향으로의 카메라 이동은 멈춘다.
3. 플레이 화면 어디에도 여백(배경이 없는 빈 공간)이 비치면 안 된다.

## 2. 현재 구조 분석 및 충돌 지점

현재 프로젝트는 **"구간별 고정 카메라 + 스냅 전환"** 방식으로 되어 있어, 이번 요구사항(연속 팔로우)과 근본적으로 다릅니다.

| 기존 스크립트 | 현재 역할 | 새 요구사항과의 충돌 |
|---|---|---|
| [StageManager.cs](Assets/Scripts/Managers/StageManager.cs) | 구간 진입 시 카메라를 `sectionCameraAnchors[i]`로 코루틴 Lerp 이동(구간 내에서는 카메라 고정) | 구간 내 연속 추적이 없음 → 팔로우 요구사항과 정면 충돌 |
| [SectionEdgeTrigger.cs](Assets/Scripts/Managers/SectionEdgeTrigger.cs) | 경계 통과 시 `StageManager.MoveToSection(index)` 호출 | 구간 인덱스 개념 자체는 재사용 가능, 호출 대상만 교체 필요 |
| [BackgroundFitter.cs](Assets/Scripts/Stage/BackgroundFitter.cs) | 배경 스프라이트를 **카메라 화면 크기와 정확히 동일하게(여백 0, 여유 0)** 스케일 | 카메라가 고정일 때만 성립하는 방식. 카메라가 조금이라도 움직이면 배경 밖 여백이 즉시 노출됨 → **이 스크립트의 전제 자체가 팔로우 카메라와 양립 불가** |
| [OutOfBoundsTrigger.cs](Assets/Scripts/Stage/OutOfBoundsTrigger.cs) | 플레이어가 맵 밖으로 떨어지면 게임오버 | 그대로 유지 가능(카메라와 무관, 플레이어 낙사 판정용) |

➡ 결론: "여백 없이 배경 딱 맞춤"을 카메라가 아니라 **배경 크기 자체(고정)** 로 보장하던 방식에서, **배경보다 작은 뷰(카메라)가 배경 내부를 돌아다니다가 배경 경계에서 멈추는 방식(클램프)** 으로 패러다임을 바꿔야 합니다. 즉 각 구간의 배경 아트가 지금보다 **더 커야** 합니다(카메라가 움직일 여유 공간 확보).

## 3. 새 카메라 구조

### 3.1 핵심 개념

```
[Player] ──위치 참조──> [PlayerCameraFollow] ──클램프──> [CameraBoundsZone(현재 구간)]
                              │                                   ▲
                              └─ 매 프레임 카메라 위치 갱신          │
                                                        [SectionEdgeTrigger] 가 구간 진입 시 갱신
```

- **CameraBoundsZone**: 각 배경(구간)마다 하나씩 배치하는 "카메라가 갈 수 있는 배경의 실제 월드 범위" 마커. 배경 스프라이트의 실제 가로/세로 크기와 동일하게 배치.
- **PlayerCameraFollow**: 메인 카메라에 부착. 매 프레임 플레이어를 따라가되, 현재 `CameraBoundsZone` 범위를 벗어나지 않도록 카메라 중심 좌표를 클램프.
- 두 요소를 분리하는 이유(규칙 39, SRP): 카메라 스스로는 "어떻게 따라갈지"만 알고, "어디까지 갈 수 있는지"는 구간(레벨) 데이터가 갖도록 책임을 분리 — 레벨 디자이너가 배경을 바꿔도 카메라 스크립트를 건드릴 필요 없음.

### 3.2 CameraBoundsZone 설계

새 컴포넌트, 배경 오브젝트(`Background_Forest`)와 같은 위치에 배치하거나 별도 빈 오브젝트에 부착. 오브젝트 이름은 `MapEdit.md` 규칙 3의 표기를 따라 `CameraBounds_1-1` ~ `CameraBounds_1-5`(현재 씬의 `Section_1`~`Section_5` = 1스테이지 1~5영역에 각각 대응)로 명명.

```csharp
// 구간 배경의 실제 월드 크기(카메라가 이동 가능한 최대 범위)와, 이 구간에서 사용할 줌 값을 정의
public class CameraBoundsZone : MonoBehaviour
{
    [SerializeField] private Vector2 size;                  // 배경의 실제 가로/세로 크기(월드 유닛)
    [SerializeField] private float orthographicSize = 4f;   // 이 구간에서 사용할 카메라 확대 크기(구간마다 다르게 설정 - 확인 사항 3 반영)
    // center는 이 오브젝트의 transform.position 사용

    public Bounds WorldBounds => new Bounds(transform.position, size);
    public float OrthographicSize => orthographicSize;
}
```

- `size`는 배경 아트의 실제 크기와 반드시 일치해야 함(= 배경 스프라이트를 수동으로 이 크기에 맞춰 스케일).
- `orthographicSize`는 구간마다 다르게 설정 가능(5개 구간 줌 차이를 유지하기로 확정 — 5항 참고). 세로 추적 범위(`size.y`)와는 별개 값이므로, 줌이 커져도 세로 이동 가능 범위는 항상 `size` 기준으로 클램프됨.
- 기즈모(`OnDrawGizmos`)로 노란 사각형을 그려 씬 뷰에서 바로 확인 가능하게 설계(디자이너 편의).

### 3.3 PlayerCameraFollow 설계

메인 카메라에 부착. `StageManager`의 카메라 이동 책임을 이 스크립트가 대체.

```csharp
public class PlayerCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;              // 플레이어 Transform (Inspector 연결, Find 금지 - 규칙 40/41)
    [SerializeField] private Vector2 offset;                 // 필요 시 화면상 여유(look-ahead) 오프셋
    [SerializeField] private float followLerpSpeed = 8f;     // 값이 클수록 즉각 추적(오버슈트 없는 Lerp)
    [SerializeField] private float zoomLerpSpeed = 4f;        // 구간 전환 시 orthographicSize를 부드럽게 전환하는 속도
    [SerializeField] private CameraBoundsZone activeZone;    // 현재 구간(시작 구간으로 초기화)

    private Camera cam;

    private void Awake() => cam = GetComponent<Camera>();

    // 구간 경계를 넘을 때 SectionEdgeTrigger가 호출
    public void SetActiveZone(CameraBoundsZone zone) => activeZone = zone;

    private void LateUpdate()
    {
        // 위치: 플레이어를 연속 추적(가로/세로 모두) 후 현재 구간 범위로 클램프
        Vector3 desired = target.position + (Vector3)offset;
        desired.z = transform.position.z;

        Vector3 next = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followLerpSpeed * Time.deltaTime));
        next = ClampToZone(next, activeZone);

        transform.position = next;

        // 줌: 구간마다 다른 orthographicSize를 목표값으로 부드럽게 전환(위치 추적과 별개 처리)
        if (activeZone != null)
        {
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, activeZone.OrthographicSize, 1f - Mathf.Exp(-zoomLerpSpeed * Time.deltaTime));
        }
    }

    private Vector3 ClampToZone(Vector3 pos, CameraBoundsZone zone)
    {
        if (zone == null) return pos;

        Bounds b = zone.WorldBounds;
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        pos.x = ClampAxis(pos.x, b.min.x, b.max.x, halfW);
        pos.y = ClampAxis(pos.y, b.min.y, b.max.y, halfH);
        return pos;
    }

    // 배경 폭이 화면 폭보다 좁으면(구간이 작아 스크롤 여유가 없으면) 중앙 고정으로 자동 대체
    private float ClampAxis(float value, float min, float max, float half)
    {
        if (max - min <= half * 2f) return (min + max) * 0.5f;
        return Mathf.Clamp(value, min + half, max - half);
    }
}
```

- **오버슈트 방지**: `SmoothDamp` 대신 지수 감쇠 `Lerp`(`1 - e^-kt`)를 사용해 관성으로 인한 튐 없이 항상 목표를 향해서만 이동 → 클램프 경계를 순간적으로도 넘지 않음.
- **자동 축소 대응**: 특정 구간의 배경이 화면보다 작으면(스크롤 여유 없음) 자동으로 그 축은 중앙 고정 → 기존 "구간 고정 카메라" 방식과 완전히 동일하게 동작(하위 호환).
- **세로(Y) 추적 확정(5항 확인 완료)**: 가로뿐 아니라 세로도 플레이어를 따라감. 단, 각 구간의 세로 이동 가능 범위는 그 구간의 `CameraBoundsZone.size.y`로 고정(구간별 배경 높이를 그대로 유지) → 낙차가 큰 구간은 세로로도 따라가다가 그 구간 배경의 상/하단에서 멈추고, 낙차가 작은 구간은 자동으로 중앙 고정(3.3항 `ClampAxis`의 자동 축소 대응).

### 3.4 구간 전환 처리 (SectionEdgeTrigger 수정)

```csharp
public class SectionEdgeTrigger : MonoBehaviour
{
    [SerializeField] private CameraBoundsZone previousZone;
    [SerializeField] private CameraBoundsZone nextZone;
    [SerializeField] private PlayerCameraFollow cameraFollow;   // Inspector 연결

    private void OnTriggerEnter2D(Collider2D other)
    {
        Rigidbody2D playerBody = other.attachedRigidbody;
        if (playerBody == null || !playerBody.TryGetComponent<PlayerMovement>(out _)) return;

        cameraFollow.SetActiveZone(playerBody.linearVelocity.x >= 0f ? nextZone : previousZone);
    }
}
```

- 기존처럼 `StageManager.Instance.MoveToSection(index)` 호출 대신 카메라 컨트롤러에 활성 존만 갱신 → **스냅/보간 코루틴 불필요**(이미 추적 중이므로 자연스럽게 이어짐).
- **연속성 보장 조건**: 인접 구간의 `CameraBoundsZone`이 경계에서 딱 맞닿아 있어야 함(겹치거나 벌어지면 경계를 넘는 순간 카메라가 튐). 레벨 디자인 시 `Boundary_1_2`, `Boundary_2_3` 같은 경계 x좌표가 곧 두 Zone의 접점이 되도록 배치 필요.
- **구간별 줌 차이 유지(5항 확인 완료)**: 5개 구간의 `orthographicSize`를 통일하지 않고 구간마다 유지. `SetActiveZone` 호출 시 `StageManager`의 기존 `PanCameraTo` 코루틴처럼 위치를 스냅/팬 하지는 않고, **줌(`orthographicSize`)만** `PlayerCameraFollow.LateUpdate`(3.3항)에서 목표 구간 값으로 부드럽게 Lerp — 위치는 계속 연속 추적, 줌만 별도로 전환됨.

### 3.5 배경(BackgroundFitter) 처리 변경

- 기존 "카메라 화면에 정확히 맞춤(runtime auto-fit)" 로직은 팔로우 카메라와 양립 불가하므로 **폐기**.
- 대신 배경 스프라이트는 레벨 디자인 시점에 **해당 구간의 `CameraBoundsZone.size`와 같거나 그보다 살짝 크게** 수동 배치(또는 배경 자체가 `CameraBoundsZone.size`를 정의하는 기준이 되어도 됨).
- `BackgroundFitter.cs`는 삭제하거나, "배경을 CameraBoundsZone 크기에 맞춰 에디터에서 한 번 스케일해주는 에디터 유틸리티"로 역할을 바꾸는 두 가지 방안 중 선택 필요(6항 참고).

### 3.6 StageManager 역할 축소

- 카메라 이동/줌 책임은 `PlayerCameraFollow`로 이전.
- `StageManager`는 "5구간 진행 상태 관리 + 보스 처치 시 다음 스테이지 로드"라는 **게임 진행 매니저 본연의 역할만** 남기는 것을 권장(규칙 39: Manager는 상태 관리 전담).

### 3.7 보스존(1-5영역) 카메라 처리 (MapEdit.md 규칙 5 반영)

- `MapEdit.md` 규칙 5에 따라 5영역(`1-5`)에는 별도 보스존이 존재. 보스존도 일반 영역과 동일하게 `CameraBoundsZone` 하나로 취급하여 팔로우+클램프가 그대로 적용되도록 설계(별도 카메라 로직 불필요).
- 다만 보스전은 통상 플레이어가 좁은 구역 안에서 좌우로 크게 움직이므로, 보스존 배경(`CameraBoundsZone.size`)은 보스 아레나 전체 + 카메라 여유폭을 포함해 넉넉히 잡을 것을 권장. 필요 시 보스존만 `followLerpSpeed`를 낮춰(더 느긋하게 추적) 전투 중 화면 흔들림을 줄이는 옵션도 가능(현재 설계의 필드 값 조정만으로 대응, 구조 변경 불필요).

## 4. 여백 발생을 원천 차단하는 이중 안전장치

1. **1차: 클램프** — `PlayerCameraFollow`가 매 프레임 `CameraBoundsZone` 범위 밖으로 못 나가게 좌표 자체를 제한.
2. **2차: 존 접합** — 인접 구간 Zone이 경계에서 정확히 맞닿아, 구간이 바뀌는 순간에도 클램프 범위가 끊기지 않음.
3. 두 안전장치가 지켜지면 "배경이 없는 곳을 비추는" 상황은 구조적으로 발생 불가능(값이 애초에 그 좌표를 가질 수 없음).

## 5. 확인 필요 사항

1. ~~세로(Y) 추적 여부~~ → **확정: 세로도 추적한다.** 단 구간별 세로 이동 가능 범위(배경 높이, `CameraBoundsZone.size.y`)는 구간마다 그대로 유지(3.3항 반영 완료).
2. **BackgroundFitter 처리 방식**: 3.5항의 "삭제" vs "에디터 유틸리티로 전환" 중 선호 방식 확인 필요(아직 미확정).
3. ~~구간별 줌 차이 유지 여부~~ → **확정: 구간별 줌(`orthographicSize`)을 유지한다.** `CameraBoundsZone.orthographicSize` 필드와 `PlayerCameraFollow`의 줌 Lerp 로직으로 반영 완료(3.2, 3.3항).

## 6. 구현 시 생성/수정할 파일 목록 (구현 준비 체크리스트)

승인 후 아래 순서로 진행 예정:

**신규 스크립트**
1. `Assets/Scripts/Camera/CameraBoundsZone.cs`
2. `Assets/Scripts/Camera/PlayerCameraFollow.cs`

**기존 스크립트 수정**
3. `Assets/Scripts/Managers/SectionEdgeTrigger.cs` — 구간 인덱스 대신 `CameraBoundsZone` 참조 방식으로 변경
4. `Assets/Scripts/Managers/StageManager.cs` — 카메라 이동/줌 관련 코드 제거, 진행 상태 관리만 유지
5. `Assets/Scripts/Stage/BackgroundFitter.cs` — 5항 확인 결과에 따라 삭제 또는 역할 변경

**씬 작업 (Unity, 규칙 9/11에 따라 승인 후)**
6. `Stage1.unity`의 각 영역(`Section_1`~`Section_5` = `1-1`~`1-5`)에 `CameraBoundsZone` 오브젝트 배치(배경 실제 크기로 설정, `1-5`는 3.7항의 보스존 여유폭 반영)
7. 각 배경(`Background_Forest`) 스프라이트를 해당 구간 Zone 크기에 맞게 수동 스케일(현재의 "화면 딱 맞춤" 크기보다 확대)
8. `Main Camera`에 `PlayerCameraFollow` 부착 및 플레이어/시작 Zone 연결
9. `SectionEdgeTrigger` 5개의 참조를 `CameraBoundsZone`으로 교체
10. 규칙 24에 따라 `Test000` 임시 씬(또는 Stage1 복제본)에서 구간 이동/경계 클램프/여백 미발생 여부 테스트 후 정리

## 7. 다음 단계

이 설계안대로 구현 진행을 승인해주시면, 5항의 확인 사항에 대한 답변을 받은 뒤 규칙 9, 11에 따라 6항 순서로 실제 작업을 시작합니다.

## 8. 수정 이력: 물리 스텝과 카메라 추적 동기화 (2026-10-06)

- **증상**: 3-2에서 이동 발판(특히 승강형 엘리베이터)에 타면 캐릭터가 진동하듯 흔들림.
- **원인**: 플레이어와 이동 발판은 물리 스텝(0.02초, 초당 50회)마다 한 번씩만 위치가 바뀌는데(Rigidbody2D 보간 None), 카메라는 `LateUpdate`에서 매 렌더 프레임(실측 약 230fps) `Time.deltaTime`으로 부드럽게 따라갔습니다. 그래서 물리 스텝 사이 프레임마다 카메라만 움직여 화면상 캐릭터가 조금씩 밀리다가(+0.03씩), 다음 물리 스텝에 한 번에 0.1칸 튀는 톱니 모양 흔들림이 초당 50번 생겼습니다. 3-1·3-3 등 높이 10짜리 구간은 카메라 y가 고정이라 세로로는 드러나지 않았고, 카메라가 세로로 따라가는 3-2에서 크게 보였습니다.
- **수정**(`Scripts/Camera/PlayerCameraFollow.cs`): 물리 스텝이 진행된 프레임에서만, 그 사이 흐른 물리 시간(`Time.fixedTime` 차이)만큼 추적·줌을 반영하도록 바꿨습니다. 추적 속도(`followLerpSpeed`)와 줌 속도는 기존과 같습니다. 게임오버로 `timeScale`이 0이 되면 카메라도 멈춥니다.
- **검증**(3-2 Play 모드, 렌더 프레임 단위 239프레임 기록)
  - E1 탑승: 캐릭터는 그대로인데 카메라만 움직인 프레임 187 → 0, 화면상 캐릭터 위치 흔들림 최대 0.097 → 0.0001.
  - V1(대각선) 탑승: 캐릭터와 발판의 상대 위치 오차 0.0001 이하, 화면상 위치가 앞뒤로 흔들리는 경우 0건(카메라가 한 방향으로 따라붙는 움직임만 있음).
- 모든 스테이지가 같은 카메라 스크립트를 쓰므로, 가로로 걸을 때 생기던 같은 종류의 미세한 흔들림도 함께 사라집니다.
