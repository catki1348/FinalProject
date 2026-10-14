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

## 2026-10-14 재개 기록 (CLAUDE_Work_Room2)

작업 플로우: VARCO `CLAUDE_Work_Room2` (flowId 3aa4abe3-dd0c-43a6-b858-dc54b3ed8204). 기존 URL 워크플로우의 입력 문제는 MCP `upload_image`/`set_node_input`으로 우회했다.

구성: 외형 참조 ImageInput(NoEquip idle_right 축소본) + 원본 프레임 ImageInput → EditImage(nano-banana-pro, 1:1) sourceImage/reference/referenceText 연결. 프롬프트는 위 공통 프롬프트를 "원본 자세·위치·크기 유지, 갑옷/투구/뿔/망토/검/방패 제거" 위주로 변형.

- 주의: `@Edit/hk2_gen/download_identity.ps1`의 run_1~ URL은 이름과 달리 돌진 자세 원본이라 자세 기준으로 쓰면 안 된다. 실제 `Assets/Sprites/Warrior/HeroKnightTest/*.png`를 축소(공통 크롭, 흰 배경, 8bpp, 약 10KB)해 업로드해야 한다. 업로드는 base64 직접 전달이라 원본 크기(1~2MB)는 불가.
- 축소본: `@Edit/disarmed_gen/tiny/run_1~8.png` (run 공통 크롭 상자 x57 y156 w1057 h1168, 0.2배).
- 파일럿 run_1: 자세/방향/발 위치 일치, 외형 일치. 결과 `@Edit/disarmed_gen/pilot2_run_1.png`(1024x1024, 흰 배경). 무기 팔 자세는 원본과 약간 다름(맨손 재해석).
- 미완료: run_2~8, attack_1~8, dash, damage 좌우, idle/jump/pickup, 투명 배경 분리, 크기/피벗 정렬, 게임 연결.

### 2026-10-14 진행 결과 (이동·공격)

- 결과 폴더: `@Edit/disarmed_gen/out/` (run_1~8 완료, attack_1·2·3·6·7·8 완료). 1024x1024(이동)/1200x896(공격), 흰 배경, 아직 투명 분리·정렬 전.
- 이동 run_1~8: 다리 위상·방향·외형 일치 확인(`out/contact_run_all.png`). 무기 팔은 맨손으로 재해석됨.
- 공격: "공격 위상"만 지시하면 모두 주먹 뻗기로 바뀌므로 프레임별로 팔 위치를 명시한 프롬프트를 사용해야 한다(attack_1 첫 시도가 이 문제). 현재 프레임별 TextInput 노드가 플로우에 있다.
- **업로드 주의**: `upload_image`는 base64를 직접 옮기는 방식이라 1~3바이트가 틀려 PNG CRC가 깨지는 경우가 있었다(run_6/7, attack_4/5 4건이 서버에서 "Task failed"로 반복 실패한 원인). 업로드 후 반드시 다시 내려받아 `cmp`로 원본과 대조한다. 같은 파일을 다시 올려도 같은 위치가 틀리면 해시가 동일하므로, 그땐 크기를 조금 바꿔 다시 만든다(attack_5 → `tiny/attack_5b.png`).
- 남은 프레임: attack_4(소스 업로드 검증 완료 `.../6cbe26fe546a0422d684701b923717fc.png`), attack_5(`.../8fc72d4828860dfcba7b2608c7ebf49e.png`, 검증 완료). 플로우의 attack_5 소스 노드(8fff843b)는 아직 손상 파일을 가리키므로 위 URL로 교체 필요. 편집기가 "Run is being prepared" 상태로 잠겨 있어 탭 새로고침이 필요하다.
- 미완료: dash, damage 좌우, idle, jump, pickup, 투명 분리, 크기/피벗 정렬, 게임 연결.

### 2026-10-14 공격 8프레임 완료

- attack_1~8 모두 `@Edit/disarmed_gen/out/attack_N.png`에 있음(attack_1은 `attack_1_v2` 사본). 비교: `out/contact_attack_all.png`.
- 위상 순서(준비→머리 위 최고점→전방 상승→수평 뻗기→아래 타격→낮은 후속→복귀 서기)가 원본과 일치. 외형 일관.
- 프레임마다 캔버스 안 인물 크기가 다르다(특히 attack_2는 크고, attack_7·8은 정면 서기라 키가 큼). 투명 분리 후 머리 크기/발 피벗 기준으로 재정렬 필요.
- 편집기 잠금은 탭 새로고침으로 해소됨. 플로우에 사용자 추가로 보이는 노드(a34e84b3, cd53c7b5, bd0698bf)가 있으니 건드리지 않는다.
- 남은 프레임: dash, damage 좌우, idle(좌우), jump(좌우), pickup(좌우 각 2장).
