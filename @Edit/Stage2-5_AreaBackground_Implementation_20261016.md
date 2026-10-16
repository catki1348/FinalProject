<!-- AI work record: Claude Sonnet 5.5, 2026-10-16. Completed the interrupted GPT/Codex session "맵 설계 N-1" task: apply the 20 generated area backgrounds to Stage2~5 scenes. Prefabs and ApplyStageAreaBackgrounds.cs were authored by Codex (GPT-6) earlier that day. -->
# 스테이지 2~5 영역별 배경 Unity 적용

중단된 GPT 세션(맵 설계 N-1)의 마지막 지시 "유니티에 반영해"를 이어서 완료했다. 이미지 20장·프리팹 4종·적용 함수는 해당 세션이 만들어 두었고, 씬 적용·저장이 빠져 있었다.

## 적용 결과

| 씬 | 프리팹 | 영역 연결 | 저장 |
|---|---|---|---|
| Stage2 | P_Stage2_AreaBackgrounds | 2-1~2-5 (2-3은 CameraBounds_2-3 영역) | 완료 |
| Stage3 | P_Stage3_AreaBackgrounds | 3-1~3-5 | 완료 |
| Stage4 | P_Stage4_AreaBackgrounds | 4-1~4-5 | 완료 |
| Stage5 | P_Stage5_AreaBackgrounds | 5-1~5-5 (5-3·5-4 확장 영역 포함) | 완료 |

방식은 Stage1과 같다. `ApplyStageAreaBackgrounds.Apply`가 StageManager의 영역 5개를 `CameraZoneBackgroundManager`에 연결하고, 기존 `ZoneBackground`·`ContinuousStageBackground`·`BackgroundFitter`와 영역 SpriteRenderer는 비활성화한다.

## 검증 (임시 씬 Test081·Test082, 종료 후 삭제)

- Stage4: 시작 시 4-1 배경만 활성(알파 1), 카메라 화면 17.8×10을 배경 18.5×10.4가 덮음. 4-3으로 이동하면 4-3 배경으로 전환, 5개 배경 모두 화면 포함 확인.
- Stage5: 5-5 영역으로 이동하면 5-5 배경만 활성, 화면 포함 확인.
- 두 플레이 모두 콘솔 오류 0.
- Stage2·3은 같은 함수·같은 프리팹 구조로 적용 저장을 확인했으나 플레이 검증은 하지 않았다.

## 한계

- 이미지 원본은 4K를 요청했지만 실제 1672×941이다. 카메라 크기 기준 표시로 도트 확대 문제를 줄였으며, 영역 간은 약 0.45초 교차 전환이고 이미지 양끝이 픽셀 단위로 이어지는 타일은 아니다.
- 이 세션이 만들던 임시 검증 씬 Test070~073은 앞서 정리했다(적용 결과에는 영향 없음).
