<!-- AI work record: Claude Sonnet 5.5; 2026-10-17. Backed up and removed the player slide/slope animation support code. Unity not run, compile not verified. -->
# 플레이어 슬라이드 애니메이션 부분 백업 (2026-10-17)

제거 직전의 원본 3개를 보관한다. 확장자 `.bak.cs`는 `@Backup`이 `Assets` 밖이라 컴파일에 포함되지 않는다.

| 백업 파일 | 원래 위치 |
|---|---|
| `PlayerSpriteAnimator.cs.bak.cs` | `Assets/Scripts/Player/PlayerSpriteAnimator.cs` |
| `PlayerMovement.cs.bak.cs` | `Assets/Scripts/Player/PlayerMovement.cs` |
| `SlopeSlideCheck.cs.bak.cs` | `Assets/@Scripts/Player/Editor/SlopeSlideCheck.cs` |

## 제거한 것 (애니메이션 지원 코드만)

- `PlayerSpriteAnimator`: 이동 모션 조건의 `|| movement.IsSliding`. 이제 이동 입력이 있을 때만 이동 모션을 쓰므로, 입력 없이 미끄러지는 동안에는 대기 포즈가 보인다.
- `PlayerMovement`: `groundPoint` 필드와 대입 2곳, 공개 API `GroundNormal`, `GroundPoint`, `SlopeSpeed`, `TryGetVisualGround`, `IsMovingUphill`, `IsMovingDownhill`. 모두 경사 애니메이션 발 정렬/등반 판정 전용이었고 이동 물리에서는 쓰이지 않았다.
- `SlopeSlideCheck`(Tools > Checks > 1-2 Slope Slide): 위 API를 쓰던 확인 2개(실제 표면 조회, 오르막/내리막 포즈 판정). 나머지 확인(양방향 하강, 속도 상한, 브레이크, 평지 종료, 영역 초기화, 점프 속도 보존)과 점프 확인용 환경 설정은 그대로다.

## 그대로 둔 것 (이동 물리의 슬라이드)

`IsSliding`, `ApplySlopeSlide`, 슬라이드 수치 필드, `SetSlopeSlideEnabled`, `StageManager.enableSection2SlopeSlide`. 슬라이드 이동 자체는 계속 동작한다. 그림 에셋 `Assets/Sprites/Warrior/HeroKnightSlope`도 건드리지 않았다.

## 복원

세 파일을 원래 위치로 되돌려 복사하고 파일명에서 `.bak.cs`를 `.cs`로 바꾼다.
