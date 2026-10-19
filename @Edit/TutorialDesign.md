# TutorialDesign.md — 튜토리얼 메뉴 · 튜토리얼 씬 설계/구현 (2026-10-19)

## 1. 요구
메인 메뉴에서 START 아래에 **TUTORIAL** 메뉴를 만들고, 이 메뉴로 **튜토리얼 씬**에 들어가 게임의 **기본 조작법**을 익힐 수 있게 한다.

## 2. 메뉴
- 메인 메뉴 순서: START → **TUTORIAL** → OPTIONS → CREDITS → EXIT. 금색 3D 글자(`Menu_Tutorial.png`), 타이틀 톤 버튼 판, 선택 오라는 다른 버튼과 동일(OptionsButton 을 복제해 만든다).
- 클릭 → `MainMenuManager.OnClickTutorial()` → `GameManager.Instance.LoadScene("Tutorial")`.
- 버튼이 5개가 되어 메뉴 묶음 배율을 0.95, 위치 y -94 로 조정(첫 버튼은 로고 아래 화면 위에서 약 410px, EXIT 아래 여백 확보). 처음 1.15 배율에서는 EXIT 가 화면 밖으로 잘려 수정함.

## 3. 튜토리얼 씬 (`Assets/Scenes/Tutorial.unity`)
- Stage1 을 복사해 만든 뒤 스테이지 내용만 걷어내고 코스를 새로 놓았다 → **실제 게임과 같은 플레이어·카메라·HUD·낙사 처리·일시정지**를 그대로 쓰므로 조작 감각이 본 게임과 같다.
- 코스(왼쪽 → 오른쪽, x 좌표): 시작 2 · 이동 6 · 계단 발판 21~26(점프) · 대시 36 · 허수아비 47(공격) · 방어 훈련 장치 62 · 도착 표지 76. 평평한 바닥(x -5~95) + 계단 발판 2단(높이 0.78, 1.58). 바닥 위 각 구간에 금색 안내 글자("1. 이동" 등).
- 카메라 구간 110x14 한 칸, 낙사선은 바닥 아래.

## 4. 단계(TutorialManager, 화면 위 안내 패널)
| 단계 | 안내 | 완료 조건 |
|---|---|---|
| 1 이동 | A/D 또는 ←/→ | 누적 이동 거리 6 |
| 2 점프 | Space | 계단 발판 위 도착 표지 근처에서 발이 발판 높이 이상 |
| 3 대시 | Left Shift | `PlayerDash.IsDashing` 이 한 번이라도 true |
| 4 공격 | 마우스 좌클릭 | 허수아비를 3번 맞힘(`TutorialDummy`, 검 판정이 닿는 적 레이어·키네마틱·트리거) |
| 5 방어 | 마우스 우클릭 유지 | 훈련 장치(`TutorialGuardTrainer`: 빨간 빛 깜빡임 1초 → 충격파 반복, 피해 4)의 공격을 2번 막음. 터지기 직전에 누르면 완벽 방어(`PlayerDefense.OnBlocked`) |
| 완료 | Esc 일시정지 · E 상자 열기 안내 | ENTER → 메인 메뉴 |
- 단계를 마치면 "완료!"(1.3초)를 보여준 뒤 다음 안내로 바뀐다. 방어 단계 완료 시 완벽 방어 횟수도 칭찬 문구에 표시.
- 언제든 **Tab** 으로 나가면 메인 메뉴로 복귀(0.6초 검게 페이드).
- 허수아비/훈련 장치는 체력이 없고 피해는 낮아(방어 장치 4) 튜토리얼 중 죽지 않는다. 훈련 장치는 플레이어가 6.5 이내일 때만 작동.

## 5. 구현 파일
| 구분 | 파일 |
|---|---|
| 신규 | `Assets/@Scripts/Tutorial/TutorialManager.cs` · `TutorialDummy.cs` · `TutorialGuardTrainer.cs` |
| 신규(에디터 도구) | `@Scripts/Editor/BuildTutorialScene.cs`(Tools/Build Tutorial Scene, 실행하면 Stage1 에서 새로 복사해 처음부터 다시 만듦) · `AddTutorialButtonToMainMenu.cs`(Tools/Add Tutorial Button To MainMenu, 여러 번 실행해도 안전) |
| 변경 | `Assets/Scripts/UI/MainMenuManager.cs` — `tutorialButton` 칸과 `OnClickTutorial` |
| 신규 씬 | `Assets/Scenes/Tutorial.unity`(빌드 설정에 등록) |
| 이미지 | `Assets/Sprites/Opening/Menu_Tutorial.png`(917x145, `@Edit/Ending/build_menu_word_exit.py ... TUTORIAL`) |
- UI 는 코드로 구성(EndingUi 재사용): 글꼴 참조를 씬에 저장하지 않는다. 참조는 Inspector 연결, `GameObject.Find`·문자열 비교 없음.

## 6. 검증(에디터, Unity MCP)
- 컴파일 오류 0, 메인 메뉴 5개 버튼 모두 화면 안에 표시(캡처).
- 메뉴 TUTORIAL 클릭 → `Tutorial` 씬 로드 확인, 나가기 → `MainMenu` 복귀·timeScale 1 확인.
- 단계 진행 전부 확인: 이동(위치 이동으로 거리 충족) → 점프(발판 위 배치) → 대시(상태 표시) → 공격(실제 `TryPerformSwordAttack` 3회가 허수아비에 3번 적중) → 방어(`SetGuard`+`DamageDelivery`로 차단 2번, 체력 변화 없음, 완벽 방어 2회 집계) → 완료 화면.
- 안내 패널: 첫 렌더에서 방어 안내문이 줄바꿈으로 글자와 겹쳐 패널 높이(250)·글자 크기·문구를 줄여 수정.
- **직접 확인하지 못한 것**: 실제 키보드·마우스 입력으로 하는 이동/점프/대시/공격/방어 전 과정(입력 경로 대신 같은 상태를 만드는 호출로 확인), 방어 훈련 장치가 스스로 예고→충격파를 내는 모습의 캡처(로직은 코드 검토와 차단 경로 확인으로 갈음), 점프 단계의 실제 점프 높이로 계단 2단 오르기 가능 여부(발판 높이차 0.8 은 기존 1-1 발판 간격과 같음).
