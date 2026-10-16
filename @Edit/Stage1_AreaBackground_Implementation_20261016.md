<!-- Codex (GPT-6), 2026-10-16. Replaced the oversized shared panorama with five generated forest area images. -->
# 스테이지 1 영역별 배경 재생성·적용

## 변경 이유와 표시 방식

기존 한 장의 배경을 전체 스테이지 폭에 맞춰 확대한 방식은 플레이 화면에서 원본의 작은 부분만 보이므로 픽셀이 크게 보였다. 영역마다 한 장씩 새로 생성하고, 각 배경을 카메라 화면 크기에 맞춰 표시하도록 교체했다. 1-2처럼 높이가 큰 구간도 전체 높이에 이미지를 늘리지 않으므로 같은 문제가 반복되지 않는다. 플레이어가 어느 위치로 이동하든 해당 영역의 배경이 화면 전체를 덮는다.

기존 녹색 숲·덩굴·나무·먼 산의 컨셉을 유지했다. 1-1 새 이미지를 나머지 4장 생성의 기준으로 사용하여 팔레트, 조명, 원경 밀도와 묘사 크기를 맞췄다. 영역마다 구도가 다른 이미지 1장씩을 사용한다. 연결성은 같은 숲의 분위기와 약 0.45초 교차 전환으로 제공하며, 이미지 양끝이 픽셀 단위로 이어지는 타일 구성은 아니다.

`CameraZoneBackgroundManager`는 실제 `PlayerCameraFollow.ActiveZone`을 읽어 현재 배경을 선택한다. 일반 이동·역방향 이동·보스 영역 순간이동에 같은 경로가 적용된다. 카메라 이동·줌 처리 뒤 배경을 갱신하고, 이미지 비율을 보존하며 4% 여유를 둔다. 카메라 위치에 따른 작은 패닝을 적용한다. 전환 중에도 바탕이 투명해지지 않도록 레이어별 알파를 계산한다. 프레임마다 새 오브젝트나 배열을 생성하지 않는다.

## 적용 파일

- 씬: `Assets/Scenes/Stage1.unity`
- 새 이미지 5장: `Assets/@ExternalAssets/ForestBackground/T_Stage1_Area1_Forest.png` ~ `T_Stage1_Area5_Forest.png`
- 프리팹: `Assets/@ExternalAssets/@Prefab/P_Stage1_AreaBackgrounds.prefab`
- 배경 제어: `Assets/@Scripts/Stage/CameraZoneBackgroundManager.cs`
- 적용 도구: `Assets/@Scripts/Editor/ApplyStage1AreaBackgrounds.cs`
- 카메라: `Assets/Scripts/Camera/PlayerCameraFollow.cs`에 현재 영역의 읽기 전용 속성 추가. 이동·줌 코드는 유지.
- [영역별·전환 미리보기](Stage1_AreaBackground_Preview.html)
- [사용한 생성 프롬프트](Stage1_AreaBackground_Prompts_20261016.json)
- [검증 수치](Stage1_AreaBackground_Validation_20261016.json)

이전 `ContinuousStageBackground` 및 기존 영역별 배경은 Stage1에서 비활성화했다. 이전 단일 파노라마 적용 도구 대신 새 `ApplyStage1AreaBackgrounds`를 사용해야 한다. 다른 스테이지의 배경 에셋은 수정하지 않았다.

## 검증 범위

- 기존 Test000을 보존하고 Test062에서 검증. 검증 후 임시 씬과 검사 스크린샷 삭제.
- 카메라 위치·화면비 135조합: 모든 배경이 화면 전체를 덮음.
- 정방향·역방향·1↔5 전환 포함 132상태: 배경 투명 구멍 없음, 최종 영역 이미지 선택 일치.
- 각 영역에 서로 다른 이미지 1장씩 총 5장 연결 확인.
- 1080px 높이 화면과 4:3·16:9·21:9에서 원본 1픽셀의 투영 크기 최대 약 1.57px. 기존 과도한 확대 제거.
- 배경 외 기존 컴포넌트 1,126개 비교: 변경 0개.
- 영역별 실제 씬 렌더링 5개 및 정면·후면·좌·우·상단 확인. 2D 평면의 측면·상단은 면적이 없어 보이지 않음.
- 플레이 모드에서 실제 카메라 구간을 보스 영역으로 변경한 뒤 배경이 자동으로 5영역 이미지로 전환되고 화면을 덮는 것을 확인. 콘솔 오류 0개.

생성은 내장 이미지 생성 도구를 사용했다. 모든 프롬프트에 4K를 명시했으나 실제 반환 파일은 각각 1672×941px이며, 단순 업스케일로 해상도를 부풀리지 않았다. 게임에서는 화면 기준 표시로 원본의 세부 묘사를 사용한다.

이미지·프리팹은 Git 제외 경로인 `Assets/@ExternalAssets`에 있으므로 다른 PC로 전달할 때 해당 파일과 `.meta` 파일도 함께 복사해야 한다.
