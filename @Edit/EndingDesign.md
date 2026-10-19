# EndingDesign.md — 게임 클리어 화면 · 엔딩 씬 설계/구현 (2026-10-19)

## 1. 요구
5-5 보스를 클리어하면 **게임 클리어 화면**과 **엔딩 씬**이 출력되게 한다. (MapEdit.md 규칙 9, 10: 5-5의 종료점 도달 시 일반 스테이지 클리어 대신 엔딩 계열 화면.)

## 2. 흐름

```
5-5 보스 처치 (Boss.OnBossDefeated)
  → BossZoneEndPoint 활성화 (기존, 보스 처치 후에만 통과 가능)
  → 플레이어가 종료점 도달 → StageManager.OnZone5EndPointReached()
  → [stageIndex ≥ 5]  GameClearManager.TriggerGameClear()        ← 신규: 게임 클리어 화면 (오버레이, 게임 정지)
  → 엔터(1.8초 뒤부터) 또는 14초 경과 → EndingManager.TriggerEnding()   ← 변경: 검게 페이드 아웃 후 "Ending" 씬 로드
  → Ending 씬: (영상/연출) → THE END → 엔터 → 검게 페이드 → **크레딧**(CreditsUI) → MainMenu
```
1~4 스테이지는 기존대로 `StageClearManager`(STAGE CLEAR) → 다음 스테이지. 5-5만 위 흐름을 탄다.

## 3. 게임 클리어 화면 (`GameClearManager`, 오버레이)
- 어두운 막(알파 0.8)이 0.7초에 걸쳐 내려오고, 0.35초 뒤 금색 3D **GAME CLEAR** 글자(폭 1300)가 1.25배 → 1배로 자리잡으며 나타남.
- 1.1초부터 한글 문구 "마왕을 쓰러뜨리고 던전을 정복했다!" 페이드 인, 1.6초부터 **PRESS ENTER**가 완전 투명 ↔ 진한 상태로 깜빡임(메뉴·오프닝과 같은 방식).
- 화면이 떠 있는 동안 `Time.timeScale = 0`, 모든 연출은 unscaled 시간.
- 싱글톤은 매니저 클래스에만(규칙 37), UI는 StageClearManager처럼 코드로 구성 → 어느 씬에서도 동작.

## 4. 엔딩 씬 (`Assets/Scenes/Ending.unity`, `EndingSceneController`)
1. 검은 화면(2초) → **새벽빛 풍경 + 절벽 위 기사**가 나타나며 전체 40초에 걸쳐 8% 천천히 확대.
2. 아래 어두운 띠에 이야기 문구가 한 줄씩 나타났다 사라짐(엔터: 현재 문구 건너뛰기). 문구(**임시안**, Inspector에서 수정):
   1. 마왕은 쓰러졌다.
   2. 어둠에 잠겨 있던 땅 위로, 오랜만에 새벽빛이 번져 간다.
   3. 숲을 지나, 이끼 낀 동굴을 지나, 용암과 어둠의 땅을 넘어 — 기사의 긴 여정은 이곳에서 끝났다.
   4. 기사는 칼을 거두고, 밝아 오는 절벽 위에서 조용히 지평선을 바라보았다.
3. 마지막: 배경이 어두워지고 **FINAL DUNGEON 로고 → THE END → "플레이해 주셔서 감사합니다" → PRESS ENTER**(깜빡임).
4. 엔터 → 1초 검게 페이드 → `MainMenu`로 복귀(timeScale 1 복구).
- 엔딩 음악은 `endingBgm` 칸(비어 있으면 무음). 시작 시 기존 BGM·환경음은 정지.
- 씬 파일에는 카메라와 컨트롤러만 있고 UI는 코드가 만든다(씬 YAML을 손으로 쓸 때의 위험 최소화, 이미지는 Resources 로 로드).

## 5. 구현 파일
| 구분 | 파일 |
|---|---|
| 신규 | `Assets/@Scripts/Ending/GameClearManager.cs` — 게임 클리어 화면 |
| 신규 | `Assets/@Scripts/Ending/EndingSceneController.cs` — 엔딩 씬 연출 |
| 신규 | `Assets/@Scripts/Ending/EndingUi.cs` — 공용 UI 도우미(이미지 로드, 텍스트·이미지 생성, 입력) |
| 신규 | `Assets/@Scripts/Editor/EnsureEndingSceneInBuild.cs` — 에디터가 열릴 때마다 Ending 씬이 빌드 설정에 있는지 확인·추가 |
| 변경 | `Assets/Scripts/Managers/EndingManager.cs` — "THE END" 글자만 띄우고 멈추던 것을 "페이드 아웃 → Ending 씬 로드"로 변경 |
| 변경 | `Assets/Scripts/Managers/StageManager.cs` — 5-5에서 `GameClearManager.TriggerGameClear()` 호출 |
| 신규 씬 | `Assets/Scenes/Ending.unity` (+ 빌드 설정 등록: `ProjectSettings/EditorBuildSettings.asset`) |
| 이미지 | `Assets/Resources/Ending/` — `GameClear_Title.png`(2306x288), `TheEnd_Title.png`(1591x277), `Ending_Background.png`(새벽빛 풍경 1920x1080), `Ending_Knight.png`, `Ending_Logo.png` |
| 생성 도구 | `@Edit/Ending/build_ending_images.py`(이미지 생성), `mockup_ending_screens.py`(미리보기) |
| 미리보기 | `LHS/@Preview/Ending_20261019/Mock_GameClear.png`, `Mock_Ending_Story.png`, `Mock_Ending_Final.png` |

## 6. 결정 사항·이유
- **글자 방식**: TMP 글꼴 에셋을 씬에 저장하지 않고, 윈도우 맑은 고딕을 쓰는 UI.Text(+Outline)로 구성. 코드로 만드는 오버레이에서 글꼴 참조를 들고 있을 곳이 없기 때문. 윈도우 외 플랫폼으로 빌드할 때는 글꼴 지정이 필요.
- **이미지 방식**: PNG 를 Resources 에 넣고 `Texture2D → RawImage` 로 표시 → 스프라이트 임포트 설정(.meta) 없이 동작.
- **엔딩 배경**: 오프닝 풍경의 폭풍 붉은 하늘을 호박색 새벽빛으로 보정(번개 등 구조는 그대로). VARCO 재생성은 크레딧 사용이라 하지 않음. 마음에 안 들면 새 배경 이미지로 교체 가능.
- **보스 BGM**: 5-5 보스 음악 종료/전환은 기존 보스 코드 담당, 엔딩 씬은 시작 시 BGM·환경음을 모두 끈다.

## 7. 검증 상태
- 컴파일 검사: 새 스크립트 + EndingManager/GameManager/SoundManager 를 Unity 라이브러리 참조 상태로 `dotnet build` → 오류 0·경고 0. Unity 에디터 콘솔에도 컴파일 오류 없음.
- **Unity MCP 재연결 후 에디터에서 검증(2026-10-19)**: 
  - 처음에는 `Ending` 이 빌드 설정에서 빠져 있었고(에디터가 메모리 상태로 덮어씀), 강제 새로고침·재컴파일 후 `EnsureEndingSceneInBuild` 가 실행되어 인덱스 6 으로 등록됨.
  - 손으로 쓴 `Ending.unity` 가 정상 로드(카메라 + EndingSceneController 부착).
  - 엔딩 씬 플레이: 풍경·기사·하단 띠·문구 페이드, 마지막 화면(FINAL DUNGEON 로고 → THE END → 감사 문구 → PRESS ENTER 깜빡임) 캡처 확인.
  - `GameClearManager.TriggerGameClear()` 호출: GAME CLEAR 오버레이(글자·한글 문구·PRESS ENTER) 표시 확인, 14초 자동 진행 후 검은 페이드 → `Ending` 씬 재로드, `timeScale` 1 복구 확인(= GameClear → EndingManager → LoadScene 사슬 동작).
  - 발견·수정한 문제: 이미지가 기본 임포트 설정(2의 거듭제곱 크기로 변환)이라 로고 등의 가로세로 비율이 틀어짐 → 5장 모두 `npotScale = None`·비압축·밉맵 끔으로 변경(.meta 에 저장), 원본 크기 유지 확인.
- **직접 확인하지 못한 것**: 실제 키보드 엔터 입력으로의 진행(엔터 호출 경로는 자동 진행 경로와 같은 코드), 실제 Stage5 에서 보스를 처치하고 종료점을 통과하는 전체 흐름, 엔딩 마지막의 엔터 → MainMenu 복귀.
- 엔딩 4번째 문구는 한 줄에 다 안 들어가 두 줄로 줄바꿈됨(띠 높이 250 안에서 정상 표시).

## 8. 테스트 방법 (Unity 에서)
1. `Assets/Scenes/Ending.unity` 를 열어 바로 플레이 → 엔딩 연출 확인(엔터로 건너뛰기, 마지막에 MainMenu 복귀).
2. 게임 클리어 화면: Stage5 에서 보스 처치 없이 확인하려면 플레이 중 인스펙터/콘솔에서 `GameClearManager.Instance.TriggerGameClear()` 호출(또는 `Boss.TakeDamage`로 보스 처치 후 종료점 통과).
3. 빌드 설정에 `Ending` 이 없으면 메뉴 Tools/Ensure Ending Scene In Build.

## 9. 크레딧 (2026-10-19 추가)
- 엔딩 씬이 끝나면(마지막 장면에서 엔터, 또는 영상 도중 엔터로 건너뛰기) 검게 페이드된 뒤 **크레딧**이 올라가고, 크레딧이 끝나면 메인 메뉴로 돌아간다.
- 크레딧 화면은 메인 메뉴의 CREDITS 버튼이 쓰는 `CreditsUI`(검은 배경, 아래→위 스크롤, 제작자 LHS / 프로그래밍 GPT·CLAUDE / 음악 VARCO SOUND / 아트 디자인 VARCO 3D / 최종 감수 LHS)를 그대로 재사용 → 내용을 한 곳(`CreditsUI.Groups`)에서만 고치면 두 화면에 함께 반영.
- 조작: 아무 키(스페이스 제외) = 건너뛰기, 스페이스 누르는 동안 3배속(`CreditsUI` 기존 동작).
- 코드: `CreditsUI`에 `Closed` 이벤트 추가(닫힐 때 한 번 발행). `EndingSceneController.LeaveToNextScene()`이 크레딧을 만들어 열고, `Closed`를 기다린 뒤 크레딧 오브젝트를 파괴하고 `MainMenu`를 로드. `creditsBgm` 칸(비우면 엔딩 음악이 이어짐).
- 검증(에디터): 엔딩 → 나가기 경로 호출 → 크레딧(LHS … VARCO 3D)이 올라가는 화면 캡처 확인 → 크레딧 닫기 → `MainMenu` 로드, `timeScale` 1, 엔딩용 크레딧 오브젝트 정리(메인 메뉴 자체 크레딧만 남음). 실제 키 입력과 끝까지 스크롤되어 자동 종료되는 경로는 직접 확인하지 못함(둘 다 같은 `Close()`를 호출).

## 부록. 일시정지·게임오버·스테이지 클리어 글자 (2026-10-19)
- 글자는 게임클리어와 같은 금색 3D 블록 글자 이미지로 통일: `Assets/Resources/Ending/GameOver_Title.png`, `StageClear_Title.png`, `Pause_Title.png` (`@Edit/Ending/build_overlay_titles.py`, Blender 헤드리스, `build_ending_images.py`의 `render_word` 재사용).
- `GameOverManager`/`StageClearManager`: 기존 흰색 기본 글꼴 글자를 위 이미지(`EndingUi.CreatePicture`)로 교체.
- `PauseManager`: 어둠막 위에 금색 테두리 + 남색 판의 "퍼즈창" 하나에 제목 이미지, 버튼 3개(계속하기/영역 다시 하기/종료하기), ESC 안내를 모아 표시.
- 미확인: Unity 연결이 끊겨 컴파일·화면 확인을 하지 못함.
- 퍼즈창 디자인 변경(메인 메뉴와 동일 방식): 어둠막 + 금색 PAUSE 글자 + 판 버튼(`Button_Normal/Highlight/Pressed`) + 금색 3D 단어(RESUME / RESTART / EXIT) + 선택 오라(`MenuSelectionAura` 재사용). 스프라이트는 `Assets/Resources/PauseMenu/`(메인 메뉴 스프라이트 사본 + `build_menu_word_exit.py <out> <WORD>` 로 만든 RESUME/RESTART).
