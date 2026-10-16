<!-- Codex (GPT-6), 2026-10-16. Generated 20 area backgrounds for Stages 2–5 with the built-in image generator. -->
# 스테이지 2~5 영역별 배경 생성

각 스테이지의 1~5영역에 사용할 이미지를 1장씩, 총 20장 생성했다. 기존 `Assets/Sprites/Backgrounds/StageN_Background.png`를 테마 참조로 사용해 첫 영역 이미지를 생성하고, 같은 스테이지의 나머지 4장은 새 첫 영역 이미지를 참조했다.

| 스테이지 | 유지한 테마 | 생성 수 | 저장 폴더 |
|---|---|---:|---|
| 2 | 이끼·뿌리·덩굴과 청록색 물빛이 있는 동굴 | 5 | `Assets/@ExternalAssets/AreaBackgrounds/Stage2` |
| 3 | 현무암·주황색 용암·화산 원경 | 5 | `Assets/@ExternalAssets/AreaBackgrounds/Stage3` |
| 4 | 차가운 회청색 석조 던전·아치·거미줄 | 5 | `Assets/@ExternalAssets/AreaBackgrounds/Stage4` |
| 5 | 붉은 하늘·어두운 고딕 성채·원경의 번개 | 5 | `Assets/@ExternalAssets/AreaBackgrounds/Stage5` |

파일명은 `T_Stage{스테이지}_Area{영역}_{테마}.png`이다. 5영역은 같은 테마의 보스 공간을 위한 배경이며 보스 캐릭터 자체는 포함하지 않는다.

## 표시와 연결성

스테이지 1에서 수정한 것처럼 각 영역의 이미지를 카메라 화면 기준으로 사용하도록 구성했다. 전체 맵 길이나 세로 높이에 비례해 확대하는 것을 전제로 하지 않는다. 같은 스테이지 안에서 색감·조명·질감·원경 크기를 맞춰 인접 영역의 교차 전환에 사용한다. 이미지 끝이 픽셀 단위로 맞는 반복 타일 구성은 아니다.

이번 작업은 이미지 생성과 미리보기까지다. Unity 씬·카메라·배경 제어 스크립트·프리팹 연결은 변경하지 않았다. 씬에 적용할 때는 기존 `CameraZoneBackgroundManager`를 재사용하고, 각 씬의 실제 5개 CameraBoundsZone에 새 이미지를 연결해야 한다.

## 생성·검수

내장 이미지 생성 도구로 20개 별도 요청을 실행했다. 모든 프롬프트에 4K 3840×2160을 명시했지만, 실제 반환 원본은 각각 1672×941px이다. 원본을 확대하거나 이미지 편집으로 화소 수를 부풀리지 않았다.

20장 전체를 개별적으로 확인해 테마·색감·세부 묘사 크기를 검토했다. PNG 형식, 파일 존재 여부, 영역당 1장 및 스테이지당 5장, 실제 해상도와 고유한 파일 해시를 확인했다. 실제 게임 화면을 렌더링하거나 플레이 모드에서 검증한 결과는 아니다.

이미지 파일은 Git 제외 대상인 `Assets/@ExternalAssets`에 저장했다. 다른 PC에 전달할 때 해당 이미지와 자동 생성된 `.meta`도 함께 포함해야 한다.

- [20장 전체·영역 전환 미리보기](Stage2-5_AreaBackground_Preview.html)
- [파일별 경로·해상도·해시](Stage2-5_AreaBackground_Manifest_20261016.json)
- [실제 사용한 생성 프롬프트](Stage2-5_AreaBackground_Prompts_20261016.json)
