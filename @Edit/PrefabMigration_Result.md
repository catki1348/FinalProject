# 공용 프리팹 연결 결과

백업: @Backup/PrefabMigration_20261008_172358

- Assets/Scenes/MainMenu.unity: 연결 0, 전용 프리팹 교체 0, 미연결 0
- Assets/Scenes/SampleScene.unity: 연결 0, 전용 프리팹 교체 0, 미연결 0
- Assets/Scenes/Stage1.unity: 연결 0, 전용 프리팹 교체 0, 미연결 0
- Assets/Scenes/Stage2.unity: 연결 19, 전용 프리팹 교체 18, 미연결 0
- Assets/Scenes/Stage3.unity: 연결 27, 전용 프리팹 교체 0, 미연결 0
- Assets/Scenes/Stage4.unity: 연결 11, 전용 프리팹 교체 0, 미연결 0
- Assets/Scenes/Stage5.unity: 연결 11, 전용 프리팹 교체 0, 미연결 0

이전 완료분: Stage1 연결 14개 (백업 @Backup/PrefabMigration_20261008_171608).
총 연결 82개, 전용 박쥐 프리팹 교체 18개. 기존 컴포넌트의 직렬화 설정과 참조 보존 검사 통과.
게임 빌드 씬의 Missing Script: 0개. 플레이 모드 테스트는 수행하지 않음. 기존 Test000 씬은 유지.

후속 수정: Stage2 2-3 박쥐 18마리의 경로 참조가 저장 후 비어 있었음. 이전 보존 검사는 저장 전 메모리 비교에 한정되어 불충분했음.
각 Path_Monster_2-3_13~30을 FlyingChasePattern에 연결하여 복구. 감지 영역 8×4, 이동 속도 기존 1.2 유지. 저장 후 재로드 참조 확인 및 임시 씬의 순찰→추적→복귀 명령 검사 18마리 통과.
SharedPrefabMigration은 교체 전 ObjectReference를 별도로 보관하고 SerializedProperty로 재기록하도록 보완.
