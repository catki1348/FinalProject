<!-- AI work record: Claude Sonnet 5.5; 2026-10-17. Disabled the physics slope slide via a default-off switch. Unity not run, compile not verified. -->
# 물리 슬라이드 비활성화 직전 백업 (2026-10-17)

| 백업 파일 | 원래 위치 |
|---|---|
| `PlayerMovement.cs.bak.cs` | `Assets/Scripts/Player/PlayerMovement.cs` |
| `SlopeSlideCheck.cs.bak.cs` | `Assets/@Scripts/Player/Editor/SlopeSlideCheck.cs` |

## 변경

- `PlayerMovement`: `slopeSlideAllowed`(기본 꺼짐) 추가. `SetSlopeSlideEnabled`가 `enabled && slopeSlideAllowed`만 적용한다. 슬라이드 계산 코드와 `StageManager.enableSection2SlopeSlide`(Stage1 씬에서 켜져 있음)는 그대로 두었으므로 씬 수정 없이 꺼진다.
- `SlopeSlideCheck`: 임시 컴포넌트에서만 스위치를 켜고 검사하므로 확인 도구는 그대로 통과해야 한다(미실행).

## 다시 켜기

플레이어 프리팹(`Player_Warrior`)의 `PlayerMovement > Slope Slide Allowed`를 체크한다. 또는 두 백업 파일을 되돌린다.
