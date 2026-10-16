<!-- Codex (GPT-6), 2026-10-16. Applied the approved continuous Stage 1 forest panorama. -->
# 스테이지 1 연속 숲 배경 적용

- 적용 씬: `Assets/Scenes/Stage1.unity`.
- 원본 이미지: `Assets/@ExternalAssets/ForestBackground/T_Stage1_Continuous_Forest.png` (1672×941px).
- 프리팹: `Assets/@ExternalAssets/@Prefab/P_Stage1_Continuous_Forest.prefab`.
- 배치 컴포넌트: `Assets/@Scripts/Stage/ContinuousStageBackground.cs`.
- 재적용 도구: `Assets/@Scripts/Editor/ApplyStage1ContinuousBackground.cs`의 `PreparePrefab()` 및 `Apply(Scene)`.

1~5영역이 한 장의 파노라마를 같은 월드 좌표와 균일한 배율로 공유한다. 영역 사이에서 텍스처가 재시작되거나 별도로 확대되지 않는다. 이미지의 가로세로 비율을 보존하며 전체 영역, 카메라 여유 범위와 추가 2유닛을 포함한다. 최대 기본 화면비는 21:9이며 더 넓은 실행 화면에서도 범위를 확장한다. 구간 경계가 바뀌면 배경 범위도 갱신된다.

기존 5개 ZoneBackground와 전역 BackgroundFitter의 동작 및 해당 SpriteRenderer를 비활성화했다. CameraBoundsZone과 경계 연결은 유지했다. 기존 공유 이미지와 다른 스테이지의 배경은 수정하지 않았다. 새 배경은 URP 2D Unlit 재질과 Point 필터를 사용한다.

## 검증

- 기존 Test000을 보존하고 별도의 Test061 씬에서 배치·저장 검증 후 임시 씬 삭제.
- 5영역 전체 포함 확인. 4:3·16:9·21:9 화면비 및 각 영역의 가장자리·중앙 조합 135개 카메라 화면 포함 확인.
- 배경 이외 기존 컴포넌트 1,125개 비교: 변경 0개.
- 임시 씬의 5개 영역 렌더링 및 정면·후면·좌·우·상단 렌더링 확인. 2D 스프라이트의 측면과 상단은 면적이 없어 보이지 않는다. 검사 이미지는 확인 후 삭제.
- Stage1 저장본을 별도로 열어 5개 영역 참조, 기존 배경 비활성화, 새 이미지 연결과 전체 범위 포함 재확인.
- 플레이 이동·전투 전체를 재시험한 것은 아니다. 검증 범위는 배경의 배치·저장·렌더링과 화면 범위다.

생성 시 4K를 요청했으나 반환된 원본은 1672×941px이다. 큰 맵 전체로 확대하므로 플레이 화면에서 원본 픽셀이 크게 보이는 해상도 제한이 있다.

`Assets/@ExternalAssets`는 현재 Git 제외 경로다. 다른 PC로 전달할 때 이미지·프리팹과 각 `.meta` 파일도 함께 복사해야 씬의 연결이 유지된다.

[영역별 배경 미리보기](Stage1_Background_Unity_Preview.html) · [검증 결과](Stage1_Background_Validation_20261016.json)
