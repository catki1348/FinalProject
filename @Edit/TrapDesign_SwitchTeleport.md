# 스위치 연동 텔레포트 함정 설계안

> 이 문서는 설계만 다룹니다. 스크립트 작성, 씬 오브젝트·프리팹 배치 등 Unity 작업은 `LHS_Custumer.md` 규칙 9, 11에 따라 사용자 승인 후 별도로 진행합니다.
> 기반 문서: `TrapDesign_SwitchZoneMonster.md`(개정 2판, 13항 구현 현황). 이 문서는 그 6항(구간 진입 텔레포트)의 **발동 방식만 확장**합니다.

## 1. 요구사항

1. 텔레포트 트랩은 **맵에 배치된 스위치 트랩(`SwitchTrigger`)을 밟으면 작동**한다. 별도의 발동 영역 박스를 두지 않는다.
2. 효과는 별도로 지정하지 않으면 **플레이어를 시작점으로 날려버린다**(진행 상황을 잃는 벌칙형).
3. 효과를 별도로 지정하는 설계(예: 지정 지점 이동)는 같은 컴포넌트의 목적지 필드만 바꿔서 지원한다.

## 2. 기존 구조와 달라지는 점

| 항목 | 기존 `TeleportTrap`(구간 진입형) | 신규 `SwitchTeleportTrap`(스위치형) |
|---|---|---|
| 발동 | 자기 트리거 영역에 플레이어 진입 | 연결된 `SwitchTrigger.OnActivated` 구독 |
| 반복 | `oneShot`/`retriggerCooldown` | 스위치의 `oneShot`/`cooldown`을 그대로 사용(중복 로직 없음) |
| 목적지 | `destination` 필수 | 기본 = **시작점**, 지정 시 그 지점 |
| 콜라이더 | 필요(발동 영역) | 불필요(스위치가 감지) |

기존 `TeleportTrap`과 3-4의 텔레포트 5개(낙하 포착형)는 **수정하지 않습니다.**
스위치의 발동 감지는 이미 `SwitchTrigger`가 하므로 새 컴포넌트는 "누가 밟았는가 → 어디로 보낼 것인가"만 담당합니다(규칙 39).

## 3. 시작점의 정의

기본 목적지는 **플레이어가 해당 영역(Zone)에 처음 들어올 때 서는 지점**, 곧 그 영역의 `ZoneStartPoint`입니다.
`StageManager.MovePlayerToStartOfCurrentArea`가 사망 후 재시작과 영역 진입에 같은 지점을 사용하므로 기준이 일치합니다.

- 인스펙터에 `ZoneStartPoint startPoint`를 연결(`Find` 금지, 규칙 40).
- 별도 지정이 필요하면 `Transform customDestination`을 채운다. 값이 있으면 `startPoint`보다 우선한다.
- 3-1 등 신규 영역은 영역마다 `ZoneStartPoint`가 이미 있으므로 새 오브젝트가 필요 없다.

## 4. 스위치 이벤트 확장 (기존 파일 수정 1건)

현재 `SwitchTrigger.OnActivated`는 인자가 없어서, 누가 밟았는지(= 보낼 플레이어)를 알 수 없습니다.
기존 `ProjectileLauncher`를 건드리지 않기 위해 **이벤트를 하나 추가**합니다(기존 이벤트와 동작은 그대로).

```csharp
// SwitchTrigger.cs — 추가분
// 스위치를 밟은 플레이어의 Rigidbody2D를 함께 전달(텔레포트 등 플레이어에게 작용하는 함정용)
public event Action<Rigidbody2D> OnActivatedByPlayer;

// OnTriggerEnter2D 안, 기존 OnActivated?.Invoke(); 바로 아래
OnActivatedByPlayer?.Invoke(body);
```

## 5. 신규 스크립트: `SwitchTeleportTrap`

위치: `Assets/@Scripts/Trap/SwitchTeleportTrap.cs`

```csharp
// SwitchTrigger를 밟은 플레이어를 영역 시작점(또는 지정 지점)으로 날려버리는 함정. 예고 없이 즉시 발동한다.
// 이동 방식은 TeleportTrap과 같다(rb.position 직접 이동 + 속도 초기화 + 카메라 즉시 이동).
public class SwitchTeleportTrap : MonoBehaviour
{
    [SerializeField] private SwitchTrigger switchTrigger;       // 이 함정을 발동시키는 스위치
    [SerializeField] private ZoneStartPoint startPoint;         // 기본 목적지(각 영역의 시작점)
    [SerializeField] private Transform customDestination;       // 지정 시 startPoint보다 우선
    [SerializeField] private CameraBoundsZone destinationZone;  // 목적지가 속한 카메라 구간
    [SerializeField] private PlayerCameraFollow cameraFollow;
    [SerializeField] private int penaltyDamage = 0;             // 0이면 피해 없음, 필요 시 인스펙터에서 조절
    [SerializeField] private AudioClip teleportSfx;
}
```

핵심 동작:
1. 스위치 접촉 → `OnActivatedByPlayer(body)` 수신 → **예고 없이 즉시** 이동.
2. `body.position = Destination`, `linearVelocity = 0`, `cameraFollow.SnapTo(Destination, destinationZone)`.
3. 스위치 쿨다운이 재발동을 막으므로 별도 루프 방지 코드가 필요 없다.
4. 한 스위치에 투사체 발사기와 텔레포트를 **함께** 연결할 수 있다(이벤트 멀티캐스트).

## 6. 설계 결정과 근거

| 결정 | 이유 |
|---|---|
| 기존 `TeleportTrap` 비수정, 신규 클래스 | 3-4에 이미 5개가 배치·검증되어 있어 회귀 위험을 피함 |
| 쿨다운·1회성은 스위치에 위임 | 같은 기능을 두 곳에 두지 않기 위함(SRP, 규칙 39) |
| 예고 없음 | 사용자 확정(즉시 발동) |
| 벌칙 피해 기본 0 | `[SerializeField] penaltyDamage`로 함정마다 조절(사용자 확정) |
| 목적지는 `ZoneStartPoint` | 사망/영역 진입 시 시작 위치와 일치해 플레이어가 예측 가능 |
| 스위치 외형 구분 | 투사체 스위치(노랑)와 구분되도록 텔레포트 스위치는 별도 색(자홍) 프리팹 사용을 권장 |

## 7. 맵 배치 규칙

1. 텔레포트 스위치는 **우회 불가능한 곳이 아니라 "밟을지 말지 고를 수 있는 곳"**에 둔다. 동선 중앙 발판 한가운데에 두면 회피 수단이 없어 불합리하다.
2. 스위치는 발판 위 서는 면 + 0.7(기존 스위치와 동일), 발판 끝에서 1.5유닛 이상 안쪽, 정상 동선에서 점프로 넘을 수 있는 위치.
3. 시작점과 가까운 앞부분(첫 1/4)에는 배치하지 않는다. 날려 보내도 손실이 거의 없어 함정이 의미를 잃는다.
4. 동일 영역에서 텔레포트 스위치는 **영역당 2개 이내**를 권장(과하면 진행 불가능에 가까워짐).
5. 투사체 스위치와 같은 발판에 두지 않는다(회피 동작이 겹쳐 불가피한 피해가 됨).
6. 텔레포트 스위치 주변 2유닛 안에는 몬스터 활동 영역을 두지 않는다(몬스터에 밀려 의도치 않게 밟히는 것을 방지).

## 8. 구현 시 생성/수정할 파일 (승인 후)

- **신규**: `Assets/@Scripts/Trap/SwitchTeleportTrap.cs`
- **수정**: `Assets/@Scripts/Trap/SwitchTrigger.cs`(4항, 이벤트 1개 추가)
- **프리팹**(`@Prefab/Trap/`): `P_Trap_TeleportSwitch`(`P_Trap_Switch` 변형, 자홍색, 쿨다운 3초) + `SwitchTeleportTrap` 부착. 프리팹 단독으로는 씬 참조(`cameraFollow`, `startPoint`, `destinationZone`)를 가질 수 없으므로 배치 후 연결한다.
- 수정 없음: `TeleportTrap`, `ZoneEventTrigger`, `ProjectileLauncher`, `PlayerCameraFollow`, `StageManager`.

## 9. 검증 체크리스트 (규칙 24: 임시 테스트 씬에서 수행 후 정리)

- [ ] 스위치 접촉 → 즉시 시작점으로 이동, 낙하/점프 속도가 0으로 초기화된다.
- [ ] 카메라가 보간 없이 시작 영역으로 즉시 이동한다(렌더 프레임 단위 확인).
- [ ] 쿨다운 3초 동안 같은 스위치 재접촉은 무시된다. 스위치가 시작점 근처여도 루프가 없다.
- [ ] 쿨다운 중 재접촉이 이중 이동을 일으키지 않는다.
- [ ] 이동형 발판 탑승 중 발동해도 부모/`riders`가 정리된다(3-4 문서에서도 미확인 항목).
- [ ] `customDestination` 지정 시 시작점 대신 그 위치로 이동한다.
- [ ] 한 스위치에 발사기와 텔레포트를 함께 연결했을 때 둘 다 동작한다.
- [ ] 기존 투사체 함정과 3-4 텔레포트 5개의 동작이 변하지 않는다.
- [ ] 무적 시간·체력 0 상태·대시 중 발동 시 비정상 동작이 없다.

## 10. 확정 사항 (사용자 답변)

1. 시작점은 **각 영역의 시작점**(`ZoneStartPoint`).
2. 맵별 설치 수량은 **설계 시 사용자가 지정**한다(이 문서는 수량을 정하지 않음).
3. **예고 없이** 발동한다.
4. 피해는 기본 0이며 `penaltyDamage`(`SerializeField`)로 조절한다.

## 11. 구현 현황

- 구현됨: `SwitchTeleportTrap.cs`, `SwitchTrigger.OnActivatedByPlayer` 추가(스크립트 파일만).
- 미진행(Unity 작업 승인 대기): `P_Trap_TeleportSwitch` 프리팹, 씬 배치·연결, 임시 씬 검증.

## 12. 기존 텔레포트 변환 (3-4, 2026-10-12)

- `P_Trap_Teleport` 프리팹: `TeleportTrap` 제거 → `SwitchTrigger`(쿨다운 3초, 반복형) + `SwitchTeleportTrap` 부착. 기존 BoxCollider2D는 스위치 감지 영역으로 그대로 사용.
- `Stage3.unity`의 `Trap_3-4_T1`~`T5`: 위치·영역 크기·목적지(`Destination` 자식, 구간 시작 발판)는 그대로 두고 `customDestination`으로 연결(별도 지정이므로 영역 시작점 대신 사용). `destinationZone=Section_4`, `cameraFollow=Main Camera`, `penaltyDamage=0`.
- 낙하하는 플레이어가 스위치 영역에 닿으면 발동하므로 게임플레이는 동일하고, 발동 방식만 스위치 연동으로 통일됨. 예고 없음.
- 미검증: 플레이 테스트(낙하 → 복귀, 쿨다운, 카메라 이동)는 아직 하지 않았다.

