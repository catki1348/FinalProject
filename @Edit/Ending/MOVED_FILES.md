<!-- AI work record: Claude Sonnet 5.5; 2026-10-19. Moved large ending files out of git-tracked project paths to comply with LHS_Custumer rules 43 and 48 (original ending production was done by another session). -->
# 엔딩 제작 큰 파일 이동 기록

git이 "큰 파일 감지(10MB 초과)"로 경고한 파일을 수칙에 맞게 옮겼다.

| 파일 | 이전 위치 | 새 위치 | 근거 |
|---|---|---|---|
| `FinalDungeon_Ending.mp4` (소리 포함 26MB) | `@Edit/Ending/` | `C:\Users\<현재 사용자명>\Documents\LHS\FinalProject_@Edit\Ending\` | 수칙 48 (3MB 이상 산출물) |
| `Ending_silent.mp4` (소리 없는 영상 25MB) | `@Edit/Ending/` | 위와 같은 폴더 | 수칙 48 |
| `bgm/Ending_BGM_BaroqueElegance_1.mp3` (대안 곡 6.9MB) | `@Edit/Ending/bgm/` | 위 폴더의 `bgm\` | 수칙 48 |
| `keyframes/k1.png` (9MB 이상 키프레임) | `@Edit/Ending/keyframes/` | 위 폴더의 `keyframes\` | 수칙 48 |
| 게임에서 재생하는 엔딩 영상 (소리 없는 영상, 위 `Ending_silent.mp4`와 내용 동일) | `Assets/StreamingAssets/Ending/FinalDungeon_Ending.mp4` | `Assets/@ExternalAssets/Ending/FinalDungeon_Ending.mp4` | 수칙 43 (`Assets` 안 10MB 초과) |

## 경로 수정
- `EndingSceneController`: `StreamingAssets` 경로(문자열)로 영상을 불러오던 방식을 `VideoClip endingVideo`(Inspector 연결)로 바꿨다. `@ExternalAssets`는 StreamingAssets 밖이라 URL로 접근할 수 없기 때문이다.
- `Assets/Scenes/Ending.unity`: 컨트롤러의 `endingVideo`에 위 영상을 연결했다.
- 빈 `Assets/StreamingAssets` 폴더와 메타 파일은 삭제했다.
- 키프레임을 쓰는 제작 스크립트(`build_ending.py` 등)를 다시 돌릴 때는 `keyframes\k1.png`를 위 `LHS` 폴더에서 `@Edit/Ending/keyframes/`로 임시로 복사한 뒤 실행하고, 끝나면 다시 `LHS`로 옮긴다.
