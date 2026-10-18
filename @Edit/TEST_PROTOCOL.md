# MM_TestStage 자동 테스트 설계서

`@Test` 폴더에서 진행하는 반복 플레이 테스트를 위한 표준 절차서. 이 문서가 있는 한, 사용자는 "테스트해줘" 수준의 짧은 지시만으로도 아래 절차 전체가 실행되는 것을 기대할 수 있다. 세부 범위·방법을 매번 다시 설명할 필요가 없도록 하는 것이 이 문서의 목적이다.

## 0. 트리거

다음과 같은 지시는 이 문서의 절차 전체를 실행하라는 뜻으로 해석한다.
- "테스트 진행해봐", "게임 테스트해줘", "정상 작동하는지 확인해줘"
- "테스트 씬 최신화해줘" (5장만 실행)
- 그 외 문맥상 `MM_TestStage` 플레이 검증을 요구하는 모든 표현

## 1. 원칙

1. **최신 설계 기준.** 매 테스트는 그 시점의 본게임(Stage1 등) 최신 상태를 기준으로 한다. 지난 테스트에서 본 구조를 그대로 믿지 말고, 실행 시점에 항상 다시 확인한다(3장).
2. **필드는 1-1(Stage1 → Section_1) 기준.** 별도 지시가 없는 한 재현 대상은 Stage1의 `Sections/Section_1`이다. 다른 스테이지/섹션을 원하면 사용자가 명시적으로 지정한다.
3. **LHS_Custumer.md 규칙 준수.**
   - 규칙 9·11: Unity를 사용하는 모든 작업은 실행 전 사용자 확인이 필요하다. 단, 이 문서가 있으므로 확인은 "지금부터 아래 표준 절차대로 진행해도 될까요?" 한 번의 예/아니오로 충분하다 — 범위를 다시 협의하는 여러 번의 질문을 반복하지 않는다.
   - 규칙 22: 확인용 스크린샷은 확인 직후 완전히 삭제한다.
   - 규칙 24는 `Test000`류의 1회성 임시 씬 규칙이며 `MM_TestStage`에는 적용하지 않는다(영구 보존 대상). 씬 자체를 삭제하지 않는다.
   - 규칙 40: 새로 작성/수정하는 코드에는 기능 설명 주석을 남긴다.
4. **교차 의존성 주의.** `Assets/@Test/Scripts/Combat/PlayerHealth.cs`는 현재 본게임 Player 프리팹에도 그대로 사용되고 있는 것이 확인됨(2026-09-16 기준). `@Test/Scripts` 아래 스크립트를 수정하기 전에는 본게임 Player가 그 스크립트를 참조하고 있는지 먼저 확인한다(`Grep`으로 프리팹/씬에서 클래스명 사용 여부 확인). 필요 시 본게임에 영향 없는 별도 이름으로 분리한다.

## 2. 대상 씬 / 산출물 위치

- 테스트 씬: `Assets/@Test/Scenes/MM_TestStage.unity`
- 스크립트: `Assets/@Test/Scripts/{Player,Combat,Enemy,Stage}/`
- 프리팹: `Assets/@Test/Prefabs/` (`P_` 접두사)
- 지형은 가능한 한 `Assets/@Prefab/Platform_*.prefab`(본게임과 동일 프리팹)을 그대로 재사용한다. 새 지형 프리팹을 만들지 않는다.
- 플레이어/적/투사체 등 아직 정식 아트가 없는 오브젝트는 Unity 기본 도형(캡슐/큐브/스피어/실린더)으로 대체한다. 정식 스프라이트가 생기면 교체하되, 이번 설계 범위에는 포함하지 않는다.

## 3. 매 실행 시 최신 설계 파악 (동기화 조사)

Unity 작업을 시작하기 전에 아래를 확인해 "무엇이 달라졌는지" 파악한다.

1. `git status --porcelain` / `git diff --stat`로 마지막 테스트 이후 `Assets/Scripts`, `Assets/Scenes/Stage1.unity`, `Assets/@Prefab/*.prefab` 변경 여부 확인.
2. Stage1 씬에서 `Sections/Section_1`(1-1)의 하위 구조를 `get_hierarchy`(read-only)로 조회해 플랫폼 배치·좌표·레이어를 그대로 기록.
3. 현재 Player 프리팹에 붙어 있는 컴포넌트 목록을 확인(예: PlayerMovement, PlayerCombat, PlayerDash, PlayerEquipment, PlayerHealth, PlayerSpriteAnimator 등). 새로 추가된 컴포넌트가 있으면 4장의 테스트 플레이어에도 상응하는 최소 기능을 반영할지 판단한다(스프라이트 애니메이션처럼 시각적 요소는 제외, 이동/전투/생존에 영향을 주는 로직만 반영).
4. `Assets/Scripts/Managers`, `Assets/Scripts/Stage` 아래 새로 추가된 매니저/트리거(예: GameOverManager, OutOfBoundsTrigger)가 있는지 확인하고, 필드 안전장치로 필요한 것은 테스트 씬에도 동일하게 배치한다.
5. 새로 발견된 항목은 이 문서 8장(변경 이력)에 한 줄로 추가한다.

### 참고: 2026-09-16 기준 Section_1(1-1) 스냅샷

재현 시 출발점으로 삼되, 반드시 위 3장 절차로 재조회하여 최신 값으로 갱신할 것.

| 오브젝트 | 위치 | 스케일 | 비고 |
|---|---|---|---|
| StartPlatform | (2, 0) | (4, 1) | 시작 지점, layer 8(Ground) |
| SteppingStone | (7, 0) | (1, 1) | |
| HillPlatform | (11.5, 0.8) | (3, 1) | 단차 점프 필요 |
| FlatAfterHill | (19, 0) | (8, 1) | |
| LongFlat | (31, 0) | (9, 1) | |
| FinalRaised | (39.25, 0.8) | (2.5, 1) | 종료 지점 단차 |
| Player 스폰 | (0.5, 1.2) | - | Section_1 진입점 |

## 4. 테스트 필드 재현 절차

1. `MM_TestStage.unity`를 로드한다(플레이 모드 중이면 4번 질문처럼 먼저 확인/대기).
2. 기존 지형 오브젝트를 정리하고, 3장에서 조사한 Section_1 레이아웃과 동일한 좌표·스케일로 `Platform_Ground`/`Platform_Bridge` 등 본게임과 동일한 프리팹을 배치한다(단차 구간은 `HillPlatform`/`FinalRaised`처럼 y를 올려서 재현).
3. `P_MM_Player`(캡슐), `P_MM_Enemy`(큐브, 필요 수만큼), `P_MM_Projectile`(스피어), `Goal`(실린더+트리거), `OutOfBoundsTrigger`(맵 전체 폭을 덮는 하단 트리거), `CameraFollow`(Main Camera)를 배치하거나 기존 것을 재사용한다.
4. 본게임에 새로 추가된 안전장치/매니저(3-4 참고)를 동일하게 연동한다.
5. 씬을 저장한다.

## 5. 최신화만 필요한 경우 (전체 재구성 없이 동기화만)

"테스트 씬 최신화해줘"처럼 필드 재구성 없이 시스템만 맞추면 되는 요청은 4장을 생략하고 3장에서 발견한 신규 매니저/트리거/컴포넌트만 기존 `MM_TestStage`에 추가·반영한다.

## 6. 자동 검증 절차

Unity 에디터가 포커스를 잃으면 `Update()` 기반 프레임이 거의 진행되지 않는 경우가 있었다(관찰됨, 2026-09-16). 따라서 검증은 아래 두 방식을 함께 사용한다.

**A. 결정적 물리 검증 (기본, Update 프레임에 의존하지 않음)**
```csharp
var prevMode = Physics2D.simulationMode;
Physics2D.simulationMode = SimulationMode2D.Script;
for (int i = 0; i < N; i++) Physics2D.Simulate(1f/50f);
Physics2D.simulationMode = prevMode;
```
이 방식으로 낙하/착지, 발사체-적 충돌, 골 트리거, 맵 밖 추락을 직접 검증한다. `PlayerMovement`의 `groundCheck`/`groundLayer`/`groundCheckRadius`는 리플렉션으로 읽어 `Physics2D.OverlapCircle`을 동일 조건으로 직접 호출해 접지 판정 로직 자체를 검증한다(`IsGrounded` 프로퍼티 값은 `Update()`가 돌지 않으면 갱신되지 않으므로 신뢰하지 않는다).

**B. 실제 플레이 관찰 (보조)**
포커스 등 환경에 따라 실시간 프레임이 정상 진행되는 경우도 있다. 이때는 `Time.frameCount`/`Time.time`이 실제로 증가하는지 먼저 확인한 뒤, 진행되고 있다면 그 상태의 관찰 결과(우발적으로 드러나는 버그 포함)도 유효한 근거로 채택한다.

**확인 항목 체크리스트**
- [ ] 플레이 모드 진입 시 콘솔 오류 0건
- [ ] 중력/착지: 스폰 후 첫 지형에 정상 착지
- [ ] 이동 로직 경로(입력→속도 적용)가 예외 없이 동작
- [ ] 사격→피해→적 사망(체력 0 도달) 정상
- [ ] 목표(Goal) 트리거 정상 (`STAGE CLEAR` 로그 등)
- [ ] 맵 밖 추락 시 낙사 복귀 정상 동작(체력 35 손실 + 마지막 안전 발판 복귀, 체력 35 이하면 GameOverManager 게임오버) — [SystemDesign_FallRecovery.md](SystemDesign_FallRecovery.md)
- [ ] 3장에서 새로 반영한 항목이 있다면 그것도 별도 확인

## 7. MCP 툴 파라미터 주의사항 (환경 이슈, 2026-09-16 관찰)

일부 세션에서 `manage_scene`/`read_console`/`manage_camera`/`execute_code` 등의 툴이 선언된 optional 파라미터를 생략하면 `"expected nonoptional, received undefined"` 오류를 낸다. 이 증상이 나타나면:
- 사용하지 않는 파라미터도 전부 명시적으로 채워서 호출한다.
- 타입이 있는 필드(enum/array/int)에는 문자열 `"null"`을 넣지 말고 그 타입에 맞는 중립값을 넣는다(예: enum은 유효한 값 중 하나, array는 `[]`/기본값, string은 `""`).
- `view_target`처럼 실제로 조회를 시도하는 필드는 빈 문자열이 아니라 존재하는 오브젝트 이름(`"Main Camera"` 등)을 넣는다.
- 매 세션 시작 시 이 증상이 재현되는지는 다를 수 있으므로, 정상적으로 optional 파라미터 생략이 되면 이 워크어라운드 없이 진행한다.

## 8. 정리 및 보고

1. 플레이 모드 종료.
2. 검증용으로 찍은 스크린샷 파일(.png/.meta)은 규칙 22에 따라 즉시 삭제.
3. 씬이 의도치 않게 dirty 상태면 원상태로 되돌리거나(저장 전 상태 확인 후) 의도한 변경만 저장.
4. 사용자에게는 위 체크리스트 결과와, 실행 중 새로 발견한 사실(버그, 본게임과의 차이 등)을 중심으로 간결히 보고한다.

## 9. 변경 이력

- 2026-09-16: 최초 작성. Section_1(1-1) 스냅샷 기록, `PlayerHealth.cs` 교차 의존성 확인, MCP 파라미터 워크어라운드 기록.
