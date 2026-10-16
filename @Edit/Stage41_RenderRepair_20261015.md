<!-- Codex (GPT-6), 2026-10-15. User-authorized visual repair in Stage4-1; original dungeon prefab pixels reused, 22 offscreen render probes passed. -->
# 4-1 발판 부분 유실 표시 수정

Stage4의4-1 발판22개를 수정했다. 오브젝트 자체는 모두 존재했고 Renderer도 활성 상태였다. 기존 반복 스프라이트는 원본 이미지의(226,300,562,456) 영역을 사용했으나, 외곽의 투명 픽셀이 포함돼 발판 표면과 하단 일부가 빠져 보일 수 있었다. 샘플링 시 반복 중앙 영역의 y=300 행은402픽셀 모두 투명했고 y=750 행도364픽셀이 투명했다.

기존 `Platform_Stage4_Dungeon` 프리팹 이미지의 내부 사각형(320,330,370,370)을 참조하는 전용 FullRect Sprite 에셋을 만들었다. 이 영역의 원본 최소 알파는 약0.984이며0.95 미만 픽셀은0이다. 이미지 픽셀·공유 텍스처·다른 영역 프리팹은 수정하지 않았다. PPU740으로 기본 높이0.5를 맞추고 좌우 Border32픽셀 및 Tiled/Continuous를 적용했다.

전용 변형 `Assets/@ExternalAssets/@Prefab/Stage4_1/P_Stage41_VisibleDungeon.prefab`을 원본에서 만들어22개 인스턴스의 연결을 교체했다. 호환되는 추가 컴포넌트·자식 함정·오버라이드는 보존했다. 교체 전후 위치·Scale·Collider 크기/offset/활성·재질 및 모든 기믹/자식 컴포넌트 직렬화 상태가 동일함을 검사한 후 저장했다. 새 SpriteTilingProperty는 외형 메타데이터로 구분했다.

`FixStage41Rendering.VerifyPixels()`는 빈 번호의 임시 Test001 씬을 만들고 각 실제 SpriteRenderer의 스프라이트·재질·타일링·크기를 투명 배경에 렌더링했다.22개 모두 가장자리2픽셀을 제외한 영역에서 알파230/255 미만 픽셀0, Collider 치수 일치, 전용 프리팹 연결을 확인했다. 긴 일반 발판·짧은 디딤돌·소멸·이동·피해 발판을 모두 포함했다. 결과: `Stage41_RenderRepair_20261015.txt`.

정면·후면·좌·우·상단·사선6방향도 메모리 프리뷰로 확인했다. 이전 단일 검수 스크린샷은 확인 후 삭제했다. Test001과.meta를 삭제하고 기존 Test000은 보존했다. Stage4 본 씬에 임시 카메라·조명·프로브가 남지 않았음을 확인했다.

배치기 `DeployStage4-1.cs`도 전용 프리팹·스프라이트를 사용하도록 바꿔 재배치 시 투명 외곽이 있는 원본 반복 스프라이트로 돌아가지 않게 했다. 전체 맵 재배치나 다른 영역 변경은 수행하지 않았다. 기능·물리 상태가 동일한 외형 수정이므로 전체 전투 완주 테스트는 반복하지 않았다.

새 Editor 코드는 Assets/@Scripts/Editor, 프리팹·스프라이트 에셋은 @Prefab, 문서는 @Edit에 저장했다. 사전·사후 수칙을 검수했으며 Unity 작업은 사용자의 수정 지시 범위에서 수행했다. Git 커밋·푸시는 없다.
