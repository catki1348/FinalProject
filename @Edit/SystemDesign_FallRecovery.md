<!-- AI work record: Claude Sonnet 5.5, 2026-10-17. System design for the fall recovery (35 damage + return to the last stood platform). Design only; no scripts or scenes were changed (LHS rules 9/11). Based on OutOfBoundsTrigger.cs, GameOverManager.cs, PlayerHealth.cs, StageManager.cs, PlatformCore/PlatformBehaviour, SuperRunManager/TimedRunManager as of 2026-10-17. -->
# 낙사 복귀 시스템 설계 — 체력 35 손실 + 마지막으로 밟은 발판으로 복귀

작성일: 2026-10-17
상태: **구현 완료(2026-10-17, 사용자 요청).** 임시 씬 검증 4건 통과, 직접 조작 플레이 검증 전. 구현 결과는 9절.

## 1. 변경 요약

| | 현재 | 변경 후 |
|---|---|---|
| 낙사선(`OutOfBoundsTrigger`) 접촉 | 즉시 `GameOverManager.TriggerGameOver()` | **체력 35 손실 → 마지막으로 밟은 안전한 발판 위로 복귀** |
| 체력이 35 이하일 때 | (해당 없음) | 35를 잃으면 0이 되므로 **게임오버**(기존 사망 경로) |
| 시간 기반 구간(4-4, 5-4) 중 낙사 | 게임오버로 씬이 끝나 구간 상태가 초기화됨 | 씬이 유지되므로 **구간을 명시적으로 초기화**해야 한다(5절) |

기존 맵 전용 낙하 복귀 영역(`TeleportTrap`: 3-4 고공 구간, 4-1 고리, 5-4 P3 복귀 센서)은 **그대로 유지**한다. 그 영역이 먼저 닿으면 지정된 발판으로 피해 없이 돌아가고, 그 아래 낙사선이 이 시스템의 대상이다.

## 2. 기준 수치

| 항목 | 값 | 근거 |
|---|---|---|
| 낙사 피해 | **35 (고정)** | 사용자 지시. 방어력·방어 버프·피격 무적과 무관하다 |
| 플레이어 기본 체력 | 100 | `PlayerHealth.baseHealth`. 3번째 낙사에서 사망(100 → 65 → 30 → 사망) |
| 복귀 후 무적 | 1.0초 | 제안값. 복귀 직후 몬스터·함정에 연속으로 맞는 것을 막는다 |
| 기록 최소 접지 시간 | 0.2초 | 제안값. 점프 중 스치기만 한 발판은 기록하지 않는다 |
| 복귀 위치 가장자리 여유 | 0.6 (발판 반폭×0.8보다 크지 않게) | 가장자리에 복귀해 다시 떨어지는 것을 막는다 |
| 기록 가능한 최소 발판 폭 | 1.5 | 디딤돌(폭 1.0~1.2)에는 안전하게 서 있기 어렵다 |
| 낙사 처리 재진입 방지 | 0.5초 | 낙사선이 여러 개 겹치거나 복귀 직후 같은 선에 닿는 중복 처리를 막는다 |

## 3. "마지막으로 밟은 발판" 기록 규칙

### 3.1 무엇을 기록하나
- 플레이어가 **접지**(`PlayerMovement.IsGrounded`)이고, 발 아래 Ground 레이어 콜라이더가 `PlatformCore`를 가진 발판일 때, 같은 발판 위에 **0.2초 이상 연속으로 서 있으면** 그 발판을 "마지막 발판"으로 기록한다. 새로 기록되면 이전 기록을 덮어쓴다(스택 없음).
- 기록 내용: 발판(Transform), 서 있던 위치의 발판 기준 로컬 오프셋(복귀 위치 계산용), 발판의 카메라 영역(`CameraBoundsZone`).

### 3.2 기록에서 제외하는 발판 (위험하거나 사라질 수 있음)
| 제외 대상 | 이유 |
|---|---|
| `DamageFoothold`(피해형, 공유 시계 포함) | 복귀하자마자 피해를 받을 수 있다 |
| `BlinkingFoothold`, `DisappearingFoothold` | 복귀 시점에 사라져 있거나 곧 사라진다 |
| `SequentialFallingPlatform` | 시간 기반 낙하 발판은 이미 떨어졌을 수 있다 |
| `RunwayDamage`가 붙은 질주 바닥 | 접지 중 계속 체력이 줄어든다 |
| 폭 1.5 미만 | 안전하게 서 있을 수 없다 |
| 비활성(콜라이더 꺼짐) 발판 | 복귀 시점에 통과된다 |

이 발판들 위에 서 있는 동안에는 기록이 갱신되지 않고, **그 이전에 기록된 안전한 발판**이 유지된다. 따라서 위험 발판 구간에서 떨어지면 그 구간 직전의 안전대로 돌아간다(예: 5-4 낙하 코스에서 떨어지면 코스 앞의 긴 안전 발판).

### 3.3 이동형 발판
이동형(`MovingPlatform`)은 기록 가능하다. 월드 좌표가 아니라 **발판 Transform + 발판 기준 오프셋**을 저장하고, 복귀 시 그 발판의 **현재 위치**에서 계산한다. 복귀한 플레이어는 이동형의 기존 탑승 처리(`riders`)로 곧바로 발판과 함께 움직인다. 다만 복귀 직후 발판이 화면 밖으로 이동 중일 수 있으므로 카메라를 복귀 위치로 즉시 맞춘다(5절).

### 3.4 기록이 없을 때
스폰 직후 낙사하는 등 기록이 없거나 기록된 발판이 파괴·비활성이면 **현재 영역의 시작 지점**(`ZoneStartPoint`)으로 복귀한다.

## 4. 낙사 처리 순서

`OutOfBoundsTrigger`는 플레이어를 감지하면 게임오버를 직접 부르지 않고 `FallRecoveryManager.HandleFall()`을 호출한다.

1. **중복 방지**: 마지막 처리 후 0.5초 안이면 무시한다.
2. **체력 35 손실**: `PlayerHealth.TakeEnvironmentalDamage(35)`. 환경 피해 경로라 방어력 경감·피격 무적 소모·피격 모션이 없고(기존 질주 바닥 피해와 같은 경로), 무장해제 상태에서는 무장해제 체력에서 깎인다.
3. **사망 확인**: 이 피해로 체력이 0이 되면 `PlayerHealth`가 기존대로 `Die()` → 게임오버를 처리한다. 복귀 단계는 건너뛴다.
4. **복귀 위치 확정**: 3절 기록을 다시 검증(발판 존재, 활성, 위험 상태 아님)해 위치를 정한다. 발판 윗면 중앙 쪽으로 가장자리 여유를 적용하고, 플레이어 발이 표면에 닿도록 +0.03을 더한다. 검증에 실패하면 이전 기록 없이 영역 시작 지점을 쓴다.
5. **이동**: 플레이어 `Rigidbody2D.position`을 옮기고 속도를 0으로 한다. 대시 중이면 취소한다. 이동형 부모는 `MovingPlatform`이 다음 물리 스텝에 스스로 정리한다(`TeleportTrap`과 같은 방식).
6. **영역·카메라**: 복귀 위치가 속한 카메라 영역을 찾아 `StageManager`의 영역을 갱신(`CurrentArea`가 달라지면 `OnAreaChanged` 발행)하고 카메라를 `SnapTo`로 즉시 맞춘다. 낙사 직전에 다음 영역으로 넘어갔더라도 복귀 위치 기준으로 영역이 맞춰진다.
7. **복귀 후 무적**: 1.0초(`PlayerHealth.GrantInvulnerability`). 보스 연계 특공(무적 관통)은 이 보호 대상이 아니다.
8. **연출**: 짧은 화면 깜박임 또는 페이드(0.3초), 기존 텔레포트 효과음 재사용. 체력 UI는 `OnHealthChanged`로 자동 갱신된다.
9. **이벤트 발행**: `FallRecoveryManager.OnRespawned(position)`. 시간 기반 구간 관리자가 구독해 초기화한다(5절).

## 5. 다른 시스템과의 상호작용

| 시스템 | 문제 | 설계 |
|---|---|---|
| `SuperRunManager`(4-4), `TimedRunManager`(5-4 P4·P5) | 낙하 시계가 시작된 뒤 낙사하면 발판이 이미 떨어져 있고 `attempted`가 켜져 있어 **재도전이 불가능**해진다(기존에는 게임오버로 씬이 다시 시작되어 가려져 있었다) | `OnRespawned`에서 해당 구간을 초기화한다: 낙하 발판 `ResetSupport()`, `attempted/running=false`, 최대 가속 부여 해제, 전투 그룹 비활성. 시작선을 다시 지나면 시계가 처음부터 시작한다 |
| `RunwayEncounterManager`(4-3, 5-3 분노) | 게임오버가 아니라 분노·몬스터 상태가 유지된다 | 변경 없음. 분노 스택은 낙사로 사라지지 않는다. 영역을 벗어나거나 사망·재시작 시 기존 규칙대로 초기화 |
| `PlatformCycleManager`(2-1, 5-1 공유 시계) | 복귀 위치가 시계 구간 밖일 수 있다 | 변경 없음. 구간 밖이면 기존 이탈 규칙(안전 상태로 초기화)이 적용된다 |
| `GameOverManager.OnGameOver` 구독자 | 낙사에서는 호출되지 않는다 | 낙사 때 임시 부여를 풀어야 하는 시스템은 `OnRespawned`로 처리한다. 기존 구독은 사망·게임오버용으로 유지 |
| 몬스터 그룹·함정·아이템 | 복귀 시 재생성하면 처치한 적이 부활하고 보상이 중복된다 | **재생성·재활성 없음**(5-4 낙하 복귀 규칙 승계) |
| `TeleportTrap` 낙하 복귀 영역 | 낙사선 위에서 먼저 동작한다 | 우선 적용. 이 영역은 피해 0으로 지정 발판에 돌려보낸다(변경 없음) |
| 보스 아레나(1-5 ~ 5-5) | 아레나 바닥 아래 낙사선 | 같은 처리. 보스 전투와 보스 체력·패턴은 유지한다 |
| 방어 임시 체력(완벽 방어 버프) | 환경 피해가 임시 체력을 먼저 소모하는가 | `TakeEnvironmentalDamage`는 임시 체력과 무관하게 실제 체력을 깎는다(확정 질문 8절) |

## 6. 구현 변경 목록 (미적용)

씬과 플레이어 프리팹은 바꾸지 않는다. 낙사선은 기존 `OutOfBoundsTrigger` 컴포넌트를 그대로 쓴다.

1. **신규 `FallRecoveryManager`**(매니저, 규칙 35): 마지막 발판 기록·낙사 처리·`OnRespawned` 이벤트. `GameOverManager`처럼 첫 사용 시 자동 생성. 플레이어는 `PlayerHealth.Awake`에서 등록(`DropConditionManager.Register`와 같은 방식). 규칙 36·37에 따라 `Find` 없이 등록 방식으로 참조한다.
2. **`OutOfBoundsTrigger`**: `GameOverManager.TriggerGameOver()` 호출을 `FallRecoveryManager.HandleFall()`로 교체. 인스펙터 옵션 `instantGameOver`(기본 꺼짐)로 특정 낙사선만 즉사로 둘 수 있게 한다.
3. **`PlayerHealth`**: `GrantInvulnerability(seconds)` 추가(`lastHitTime` 조정). 나머지는 기존 `TakeEnvironmentalDamage` 사용.
4. **`StageManager`**: `TryWarpToTestPlatform`의 후반(영역 판정·이동·카메라 스냅·`OnAreaChanged`)을 공용 `WarpPlayerTo(Vector2)`로 분리해 복귀가 재사용.
5. **`SuperRunManager`, `TimedRunManager`**: `OnRespawned` 구독과 구간 초기화 메서드 추가.
6. 문서 갱신: `CameraDesign_FollowCamera.md`의 낙사 설명(이 문서로 연결, 반영함), 각 맵 문서의 "낙사 = 게임오버" 표현은 구현 후 일괄 정리(2026-10-17 완료: 3-2, 3-4, 4-4, 5-4, 전체 배치 문서, 테스트 프로토콜, 4-4 구현 기록).

## 7. 검증 계획 (적용 후)

| 항목 | 기대 결과 |
|---|---|
| 일반 발판에서 낙사 | 체력 −35, 마지막 발판 윗면(가장자리 안쪽)에 복귀, 카메라 즉시 이동, 1초 무적 |
| 체력 35 이하에서 낙사 | 게임오버 |
| 피해·점멸·소멸·낙하 발판 위에 있다가 낙사 | 그 직전의 안전한 발판으로 복귀 |
| 이동형 발판에서 낙사 | 발판의 현재 위치 위로 복귀하고 함께 이동 |
| 5-4 낙하 코스에서 낙사 | 코스 앞 안전대로 복귀, 낙하 발판 복원, 시작선을 다시 지나면 시계 재시작 |
| 4-4 질주에서 낙사 | 같은 방식으로 구간 초기화 |
| 영역 경계에서 낙사 | 복귀 위치 영역으로 `CurrentArea`·카메라가 맞춰짐 |
| 연속 3회 낙사 | 100 → 65 → 30 → 게임오버 |
| 낙사선 중복 접촉 | 처리 1회만 |
| 보스 전투 중 낙사 | 보스 상태 유지, 플레이어만 복귀 |
| 기록이 없을 때 낙사 | 영역 시작 지점으로 복귀 |

## 8. 확정이 필요한 항목 (기본값으로 설계함)

1. 체력이 35 이하일 때 낙사하면 **사망**한다(0까지 깎음). 대안은 체력 1을 남기는 방식이다.
2. 낙사 피해는 **방어력·방어 임시 체력과 무관한 고정 35**다.
3. 복귀 후 **무적 1.0초**와 접지 **0.2초** 기록 조건, 발판 폭 **1.5** 최소값은 제안값이다.
4. 위험 발판 구간에서 낙사하면 그 구간 **직전의 안전한 발판**으로 돌아간다(위험 발판 위로 복귀하지 않음).

## 9. 구현 결과 (2026-10-17)

<!-- AI work record: Claude Sonnet 5.5, 2026-10-17. Implemented the fall recovery system; verified in a temporary scene (Test090, deleted) with Unity MCP. -->
확정 질문 4개는 8절의 기본값 그대로 구현했다(체력 35 이하 낙사는 사망, 고정 35, 무적 1.0초·접지 0.2초·폭 1.5, 위험 발판 구간은 직전 안전 발판).

| 파일 | 변경 |
|---|---|
| `Assets/@Scripts/Stage/FallRecoveryManager.cs` | **신규.** 마지막 안전 발판 기록(`FixedUpdate`), 낙사 처리(`HandleFall`), 복귀 위치 계산, 검은 화면 깜박임 연출, `OnRespawned` 이벤트 |
| `Assets/Scripts/Stage/OutOfBoundsTrigger.cs` | 게임오버 대신 `HandleFall` 호출. `instantGameOver` 옵션(기본 꺼짐) |
| `Assets/@Test/Scripts/Combat/PlayerHealth.cs` | 매니저 등록/해제, `GrantInvulnerability(seconds)` 추가 |
| `Assets/Scripts/Player/PlayerDash.cs` | `CancelDash()` 추가(복귀 시 진행 중인 대시 종료) |
| `Assets/Scripts/Managers/StageManager.cs` | `WarpPlayerTo(position, notifyAlways)`(영역 판정·이동·카메라 스냅·영역 변경 이벤트)와 `TryGetCurrentAreaStart` 추가. 기존 `TryWarpToTestPlatform`이 같은 메서드를 쓰도록 정리 |
| `Assets/@Scripts/Stage/SuperRunManager.cs`, `TimedRunManager.cs` | `OnRespawned` 구독: 낙하 발판 복원, 시도 상태·전투 그룹·가속 초기화 |

씬·프리팹은 변경하지 않았다(낙사선은 기존 `OutOfBoundsTrigger`를 그대로 사용, 플레이어는 `PlayerHealth.Awake`에서 등록).

### 검증 (임시 씬 Test090, 삭제함)
| 항목 | 결과 |
|---|---|
| 일반 발판에서 낙사 | 체력 100 → 65, 서 있던 위치(가장자리 안쪽)로 복귀, 카메라 즉시 이동, 시간 정상 |
| 피해 발판 위에 있다가 낙사 | 피해 발판은 기록되지 않고 직전 안전 발판으로 복귀 |
| 체력 35 이하에서 낙사 | 체력 0, 게임오버 오버레이, 복귀 없음 |
| 5-4 낙하 코스에서 낙사 | 낙하 발판 21개 전부 복원, 구간 시도 상태 초기화, 코스 앞 안전 발판으로 복귀(체력 65 → 30) |

미검증: 이동형 발판에서의 복귀, 영역 경계에서의 낙사, 4-4 질주(`SuperRunManager`) 초기화, 보스 전투 중 낙사, 효과음(설계의 텔레포트 효과음은 미연결 — 연결 지점은 `HandleFall`), 직접 조작 플레이.
