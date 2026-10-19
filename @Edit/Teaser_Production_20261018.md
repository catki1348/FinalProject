<!-- AI work record: Claude Sonnet 5.5; 2026-10-18. Teaser trailer production: v1 (gameplay footage) and v2 (still images only, after user request). Baroque music via VARCO Sound. Unity game logic was not modified. -->
# 본게임 티저 영상 제작 기록 (2026-10-18)

## 버전

| 버전 | 파일 (`C:\Users\catkl\OneDrive\문서\LHS\컨셉\`) | 내용 |
|---|---|---|
| v1 | `FinalDungeon_Teaser_v1.mp4` (164MB) | 자동 플레이 영상 약 1분 56초 포함. 사용자 요청으로 대체됨(삭제하지 않고 보관). |
| **v2 (현행)** | `FinalDungeon_Teaser_Stills_v1.mp4` (146MB) | 플레이 영상 제거. 게임 장면을 캡처한 정지 이미지 16장 + 오프닝 일러스트로만 구성. 3분 0초, 1920×1080, 30fps. |
| 이미지 원본 | `Teaser_Stills\` (PNG 16장, 약 33MB) | v2에 쓴 캡처 이미지. 번호 순서가 영상 순서. |
| 음악 | `Epic_baroque_journey_1.mp3`, `_2.mp3` | VARCO Sound 생성(각 약 3분 18초). v1, v2 모두 2번 사용, 1번은 대안. |

음악은 직접 듣지 못했고 수치(라우드니스 -13.5 LUFS, 트루피크 -1.9 dBFS, 클리핑 없음, 마지막 5초 페이드 아웃)로만 검수했다.

## v2 구성 (3분)

| 구간 | 내용 |
|---|---|
| 인트로 (28초) | 오프닝 일러스트 레이어(`Assets/Sprites/Opening`)를 느린 줌으로 연출. 자막 "모든 길은 마왕성으로 이어진다" -> "그리고, 한 명의 기사가 나선다", 영웅 등장, 로고 |
| 스테이지 1 | 카드 "숲을 지나" + 베기 장면 + 점프 장면 |
| 스테이지 2 | 카드 "고대 유적을 넘어" + 화염 함정 위 베기 + 점프 |
| 방어 | 자막 "PERFECT BLOCK / 완벽 방어": 방어 자세와 완벽 방어 이펙트 2장 |
| 스테이지 3 | 카드 "용암 지대를 건너" + 점프(화염/석궁 함정, 용암 동굴) 2장 + 보스 화산 전갈 |
| 스테이지 4 | 카드 "악마의 동굴을 뚫고" + 베기 + 보스 눈먼 동굴 괴수 2장 |
| 스테이지 5 | 카드 "마왕성으로" + 공중 베기, 점프 + 최종 보스 쌍검 악마군주 2장 |
| 엔딩 (약 22초) | 오프닝 일러스트 + 로고 + "COMING SOON / 2D 액션 플랫포머", 페이드 아웃 |

각 이미지는 8.4초, 느린 줌, 크로스페이드 0.8초, 가장자리 비네팅. 음악의 변화 지점(약 10, 23, 63, 140, 160, 180초)에 맞춰 영웅 등장(11초), 로고(22.5초) 등을 배치했다.

## 제작 방법

1. **이미지 캡처** (Unity Play 모드): `Assets/@Scripts/Teaser/`의 도구. 게임 스크립트는 수정하지 않았다.
   - `TeaserBot.cs`: 오른쪽으로 달리며 점프/검 공격하는 자동 조작(리플렉션으로 입력 덮어쓰기). 플레이어는 무적 처리.
   - `TeaserCapture.cs`: 카메라를 1920×1080 RenderTexture로 렌더해 저장. 줌, 대상 추적, 카메라 구간 안으로 화면 제한. `Snapshot`으로 한 장 저장, `Begin`으로 연속 프레임 저장.
   - `TeaserStills.cs`: 검을 휘두르는 순간, 점프 정점, 일정 간격마다 자동으로 PNG 스냅샷. `BlockShowcase`는 방어 직후 피해를 줘 완벽 방어 이펙트가 퍼지는 순간을 촬영. `TeaserGuardHold`는 방어 상태를 표시 스크립트가 읽기 직전에 유지시키는 도우미(PlayerDefense가 매 프레임 방어를 해제하기 때문).
   - 스테이지별로 약 14장(보스 12장)을 자동 촬영한 뒤 컨택트 시트로 16장을 골랐다. 촬영은 줌 0.34~0.5, 추적 카메라로 주인공이 화면 높이의 약 25~30%가 되게 했다.
2. **음악**: sound.varco.ai 음악 모드(200크레딧). 프롬프트: 하프시코드, 현악, 파이프오르간, 바흐/비발디풍 대위법, D단조, 영웅적, 보컬 없음.
3. **합성**: ffmpeg(`imageio-ffmpeg` 내장)와 Python. 스크립트는 `@Edit/teaser/`: `make_cards.py`(스테이지 카드), `build_intro_end.py`(오프닝 레이어 연출 공용 함수), `build_still_teaser.py`(v2 조립), `still_sheet.py`(후보 컨택트 시트), `build_teaser.py`(v1 조립). 입력 폴더(`selected/`, `cards/`)는 스크립트 옆 기준이며 현재는 임시 폴더라 없다. 이미지 원본은 `LHS\컨셉\Teaser_Stills`에 있다.

## 알아 둘 점

- `WarpManager`의 테스트 시작 지점이 1-5(보스 구역)로 설정돼 있어 스테이지 로드 직후 보스 구역에서 시작한다. 이 설정은 바꾸지 않았다.
- 게임 오버가 한 번 발생하면 `Time.timeScale`이 0으로 남아 촬영이 정지 화면이 된다.
- 오프닝 씬은 오버레이 Canvas라 카메라 렌더에 안 잡혀, 원본 레이어 PNG로 직접 합성했다.
- 봇이 보스를 처치하면 다음 스테이지 씬으로 넘어간다(화산 전갈은 4장만 촬영됨).
- 스테이지 1/2 보스는 촬영하지 않았다. Stage4 1영역(수직 구간)과 2영역 배경은 단조로워 1장만 사용했다.
- 자막 문구(인트로 2줄, 스테이지 카드 5개, "완벽 방어", "2D 액션 플랫포머")는 제작자 초안이며 수정 가능하다.
- 컨셉 영감인 Sparkster 계열의 분위기만 참고했고 해당 게임의 이미지, 음악, 이름은 쓰지 않았다.
- Unity에 새로 만든 파일은 `Assets/@Scripts/Teaser/`의 스크립트 3개와 메타뿐이다. 필요 없으면 폴더째 삭제해도 게임에는 영향이 없다. 에디터 활성 씬 `Stage5`의 저장 안 된 변경은 건드리지 않았고 Play 모드는 종료했다.


---

<!-- AI work record: Claude Sonnet 5.5; 2026-10-19. Teaser re-shoot (v1_Reshoot): fixed auto-play bot (facing, walk animation), re-recorded gameplay in Unity Play Mode, reassembled video. -->
## 재촬영 (2026-10-19) — `FinalDungeon_Teaser_v1_Reshoot.mp4`

사용자 지적: v1의 플레이 영상에서 플레이어가 거꾸로 걷고, 진행 방향과 반대로 공격하고, 걷기 모션 없이 앞으로 이동한다.

### 원인
- `TeaserBot`이 실행 순서 1000에서 `moveInput`을 덮어썼다. `PlayerMovement.Update`(순서 0)가 매 프레임 실제 키 입력(0)으로 `moveInput`과 바라보는 방향(`Flip`)을 정하고, `PlayerSpriteAnimator`(순서 10)는 그 값을 먼저 읽으므로 애니메이터는 "이동 없음"으로 판단했다(걷기 모션 없음). 방향 전환(`Flip`)도 호출되지 않아 `FacingRight`가 초기값에 고정되었다(공격 방향이 진행 방향과 어긋남).

### 수정 (`Assets/@Scripts/Teaser/`)
- `TeaserBot`: 실행 순서 5(Movement 뒤, 애니메이터 앞)로 변경, 진행/대상 방향이 다르면 `Flip`을 직접 호출. 공격은 앞에 적이 있을 때만(없으면 5초 정도마다 한 번), 보스전 모드(`Target`)에서는 보스에게 다가가 간격을 유지하고 가끔 점프로 피하며 공격하지 않는다(맞은 보스가 그로기로 누워 있게 되므로).
- `TeaserCapture`: `BoundsProvider` 추가(카메라 구간이 바뀌어도 화면을 구간 안으로 제한).
- `TeaserDirector`(신규): 씬 로드 후 지정한 영역의 시작 지점에서 봇/촬영 도구를 붙이고 연속 프레임을 저장. 촬영 중 플레이어 무적/체력 보충, 보스전은 보스 체력을 최대로 유지. 게임 스크립트는 수정하지 않았다.

### 촬영 구성
스테이지 1, 2, 3, 5는 1영역, 스테이지 4는 3영역(1영역이 수직 낙하 구간이라 단조로워 교체)에서 각 18초(영상에는 16초 사용), 보스 3(화산 전갈), 4(눈먼 동굴 괴수), 5(쌍검 악마군주)는 5영역에서 14초(영상에는 12초 사용), 줌 0.55(보스 0.8), 1920×1080 30fps. 촬영 중에만 `StageManager.WarpPlayerTo`로 시작 영역을 지정했고 씬 파일의 `testStartZone`은 건드리지 않았다.

### 조립
`@Edit/teaser/build_teaser_reshoot.py`: 프레임 -> 클립, 스테이지/보스 자막, 스테이지 사이는 오른쪽으로 밀리는 슬라이드 전환(`xfade slideleft`), 보스/엔딩 앞은 페이드, 인트로(30초)와 엔딩(38초)은 v1에서 잘라 재사용, 음악은 `Epic_baroque_journey_2.mp3`. 총 2분 58.6초, 208MB. 작업 폴더(프레임)는 완료 후 삭제했다.

### 알아 둘 점
- 이전 PC에서 복사한 `@ExternalAssets`의 `.meta`가 어긋나 배경/보스가 안 나오던 문제는 사용자께서 원본 `.meta` 포함 폴더를 다시 옮겨 해결되었다(촬영은 그 이후에 진행).
- 에디터가 플레이 중 자동 컴파일되면 일시정지(Pause) 상태가 되어 촬영이 멈출 수 있다. 스크립트를 고친 뒤에는 Play를 껐다 켜고, 멈추면 `EditorApplication.isPaused = false`로 풀면 된다.
