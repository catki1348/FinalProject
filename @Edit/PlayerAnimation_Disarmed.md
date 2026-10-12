# 무장해제 애니메이션 1:1 제작 기록

2026-10-12. 상태: 원본 조사 완료, VARCO 생성 및 게임 적용 미완료.

워크플로우: https://3d.varco.ai/customWorkflow/49642cd6-4c2d-4dce-a5c2-88d90a5638d1

## 현재 본 게임 기준

`Assets/@ExternalAssets/@Prefab/Player_Warrior.prefab`는 HeroKnightTest 이동 8장과 공격 8장, 대쉬 1장, 좌우 피격을 사용한다. 이동은 Walk 상태를 새 Run 클립으로 대체한 12fps 반복이다. runKey=0이므로 일반 이동이 이 반복을 사용한다. 경사 전용 모션은 이전 사용자 지시로 비활성화되어 있어 이번 제작 대상에 포함하지 않는다.

| 행동 | 무장 기준 | 무장해제 현재 문제 | 제작/연결 목표 |
|---|---|---|---|
| 대기 | Transparent/idle_right, idle_left | NoEquip 정지 그림 | 좌우 외형 및 크기/발 위치 검수 |
| 이동 | HeroKnightTest/run_1~8, 12fps | NoEquip/walk 좌우 정지 그림 | 동일 순서/타이밍의 전용 8프레임, 양방향 반복 |
| 달리기 분기 | runRight/runLeft | 걷기 그림 재사용 | 전용 달리기 연결; 현재 입력 설정은 유지 |
| 점프 | Transparent/jump 좌우 | NoEquip/jump 좌우 | 원본 자세와 방향/표시 크기 대응 검수 |
| 공격 | HeroKnightTest/attack_1~8, 0.5초 | 무장해제 시 표시 분기 제외 | 같은 진행률의 8프레임 맨손 동작, 무기/방패/검 궤적 제거 |
| 대쉬 | HeroKnightTest/dash_1, 0.2초 | 실제 대쉬 중 이동/대기 표시 | 같은 돌진 자세의 전용 무장해제 그림 |
| 피격 | HeroKnightTest/damage 좌우, 0.35초 | TriggerDamage와 체력 분기에서 제외 | 무장해제 피격도 전용 좌우 그림으로 표시 |
| 아이템 습득 | Transparent/pickup 좌우 각 2장, 0.6초 | NoEquip/box 좌우 정지 그림 | 같은 웅크림→습득 2단계, 양방향 |

## 생성 원칙

- 외형 기준: NoEquip/idle_right의 갈색 머리, 드러난 얼굴, 붉은 긴 튜닉, 갈색 벨트/바지/부츠. 기사 갑옷/투구/망토/검/방패를 남기지 않는다.
- 각 무장 원본 프레임을 자세 기준으로 직접 참조하고 NoEquip 대기를 외형 참조로 사용한다. 앞 생성물을 연속적으로 원본 삼아 외형이 변하는 방식은 피한다.
- 공통 프롬프트: `4K resolution. Create one disarmed version of the supplied armed animation frame. Match the exact body pose, limb positions, facing, camera, movement phase and composition of the pose reference. Use the identity reference's brown-haired bareheaded man, red long tunic, brown leather belt, dark brown trousers and boots, same pixel art shading and proportions. Remove all armor, helmet, sword, shield and weapon trails. Reconstruct natural empty hands, sleeves and exposed face. Single full-body character, no clipping, no text, no ground or shadow. Plain white background for segmentation. Preserve consistent body scale across the sequence.`
- 공격의 팔 궤적은 무장 원본의 준비/전방 타격/후속/복귀 순서를 따르되 맨손 관절이 자연스럽도록 검수한다. 게임 피해량/판정 시점은 애니메이션 작업으로 변경하지 않는다.
- VARCO 분리 마스크로 투명 배경을 만들고 기존 apply_workflow_mask/clean_fringe 작업을 참조해 흰 테두리와 잔여 조각을 검사한다.
- 머리 크기와 발 피벗을 기준으로 PPU 및 표시 오프셋을 맞춘다. 전체 키만으로 웅크림/점프 프레임을 확대하지 않는다.
- PlayerSpriteAnimator의 동일 우선순위(피격→대쉬→공격→습득→점프→이동→대기)에 무장해제별 자산을 선택하도록 연결한다. 기존 무장해제 체력 전환 조건은 유지한다.
- PlayerHeroKnightMotionTest의 LateUpdate 방향 보정과 새 무장해제 그림의 flipX가 충돌하지 않도록 검증한다.

## 이번 세션 접근 결과

VARCO 원본 워크플로우가 브라우저에서 열렸다. WebMCP 제공 도구는 없으며 Codex 문서 연결 세션도 없다. UI에 실행 중인 노드 2개가 표시된다. 새 EditImage 노드 `e7dd4c4c-9000-4eae-b772-6430bc846020`가 추가되었으나 원본 입력은 비어 있다. 이미지 입력 클릭 및 이후 화면 캡처가 CDP 시간 초과로 실패해 새 이미지 생성은 실행하지 않았다.

게임 스크립트/프리팹/PNG는 이번 작업으로 수정하지 않았다. 완료를 위해 VARCO 입력/실행 제어 복구, 프레임 제작과 검수, 본 게임/테스트 프리팹 연결, Unity 컴파일 및 실제 양방향 동작 검증이 필요하다.

### 재확인 결과

사용자의 재확인 요청 후 같은 워크플로우를 점검했다. DOM 조회, 화면 캡처, 노드 이동은 정상 응답한다. 추가해 둔 EditImage 노드를 화면 안으로 이동했다. 그러나 Source Image의 Add Image 버튼을 눌러도 입력창이 나타나지 않고, 파일 선택 이벤트를 기다리는 방식도 10초 후 시간 초과로 실패했다. WebMCP 도구는 여전히 없다. 실행 중 노드 2개 표시가 다시 나타났으므로 생성 작업 완료 여부는 확정할 수 없다. 새 플레이어 이미지 생성 및 게임 적용은 여전히 미완료다.
