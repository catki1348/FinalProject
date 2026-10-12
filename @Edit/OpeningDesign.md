# OpeningDesign.md — 오프닝 화면 설계

## 1. 개요

게임 시작 시 보여주는 오프닝 일러스트 씬(`Assets/Scenes/Opening.unity`).
절벽 끝에 선 주인공이 뒷모습으로 서서 칼을 뽑아 들고 5스테이지의 마왕성을 가리킨다.

## 2. 구도 (카메라 기준)

```
        [          Stage5 (마왕성)          ]      ← 윗 단 (1~4 전체 위, 중앙)
   [ Stage1 ][ Stage2 ][ Stage3 ][ Stage4 ]        ← 아랫 단 (왼쪽부터 스테이지 순)
                      ▲ 칼끝이 Stage5(마왕성)를 향함
                    (플레이어, 뒷모습, 절벽 끝)
```

- 배경 패널: `Assets/Sprites/Backgrounds/Stage1~5_Background.png` (각 스테이지 테마 이미지)
- 아랫 단: Stage1~4를 X 오름차순(왼→오)으로 동일 폭·간격 없이 이어 붙임.
- 윗 단: Stage5를 1~4 패널 폭 전체의 가운데 위에 배치(마왕성이 화면 정중앙 위).
- 플레이어: 화면 하단 중앙, 카메라를 등지고 서 있음. 절벽 끝(바위 받침) 위.
- 포즈: 오른팔을 앞-위로 뻗어 뽑아 든 검이 Stage5 마왕성 방향을 가리킴.

### 2-1. 구도 v2 (현재 채택, 사용자 재지시)

- 플레이어: 화면 **좌하단**의 뾰족하게 솟은 절벽 끝에 서서, 뒷모습(3/4 각도)으로 오른쪽 위 대각선을 바라봄.
- 스테이지 5 테마 이미지: **우상단** (플레이어 기준 대각선 방향). 붉은 테두리 + 붉은 발광으로 강조.
- 스테이지 1~4: 스테이지 순으로 왼쪽→오른쪽, 한 칸씩 위로 올라가는 계단식으로 펼쳐 스테이지 5 쪽으로 이어짐.
- 미리보기: `@Edit/OpeningPreview/OpeningScreen_Preview_v2.png` (스크립트 `compose_opening_v2.py`). v1(`OpeningScreen_Preview.png`)은 이전 구도 기록용.

- 포즈 수정본 반영 미리보기: `@Edit/OpeningPreview/OpeningScreen_Preview_v3_SwordForward.png` (칼이 스테이지 5 방향을 겨눔).

### 2-2. 구도 v3 — 액자 배치 폐기, 연결된 자연 풍경 (플레이어 제외, 사용자 재지시)

- 스테이지 테마 이미지를 개별 패널(액자)로 두지 않고, 하나의 이어지는 풍경으로 VARCO 이미지 생성(4K 문구 포함, 플레이어 없음).
- 흐름: 좌하단(절벽 첨탑) → 1 숲/설산 → 2 이끼 동굴 → 3 용암 화산 → 4 어두운 악마 동굴 → 우상단 5 마왕성. 하늘색도 파랑→보라→진홍으로 연속 변화.
- 산출물: `@Edit/OpeningPreview/OpeningScene_Connected_A.png`, `OpeningScene_Connected_B.png` (둘 다 1376x768, 후보 2장).
- 플레이어는 이 배경 확정 후 좌하단 절벽 위에 합성/배치 예정.

### 2-3. 구도 v4 — 스테이지 5 중심(오른쪽 구석) + 나머지 테마가 감싸는 구조 (사용자 재지시)

- v3(A/B)는 컨셉은 맞으나 세부 요구와 달라 폐기: 스테이지 5가 **중심**이어야 하고, 이미지의 **가장 오른쪽 구석**에 놓이되, 스테이지 4→3→2→1이 스테이지 5를 **바깥으로 겹겹이 둘러싸야** 함.
- 재생성 산출물: `@Edit/OpeningPreview/OpeningScene_Surround_A.png`(동심 고리형), `OpeningScene_Surround_B.png`(자연스러운 지형 연결형). 둘 다 1376x768.
- 플레이어는 여전히 제외. 확정 후 좌하단 절벽 위에 합성.

### 2-4. 구도 v5 — 지형 방사형 구성 (사용자 스케치 기준, v4 폐기)

- v4의 "동심 고리로 물리적으로 감싸기"는 오해였음. 요구는 **지형적 구성이 스테이지 5를 감싸는 형태**.
- 사용자 스케치: 스테이지 5가 중심 지형(고원, 타원). 그 가장자리에서 뻗는 경계선(능선·절벽·강)으로 지형이 갈라짐.
  - 스테이지 1: 왼쪽~왼쪽 위(가장 넓음) / 스테이지 2: 왼쪽 아래 / 스테이지 3: 아래 중앙 / 스테이지 4: 오른쪽
  - 경계선: 고원 우상단→우측 위 가장자리, 고원 좌하단→왼쪽 아래 모서리(곡선), 고원 아래→하단, 고원 우하단→우측 아래 모서리
- 산출물(플레이어 제외, 1376x768):
  - `OpeningScene_Radial_Center.png` — 스케치 그대로 스테이지 5가 중앙
  - `OpeningScene_Radial_RightCorner.png` — 같은 방사형 구조에서 스테이지 5가 오른쪽 구석 쪽 (앞선 "오른쪽 구석" 요구 반영)
- 좌하단에 플레이어용 절벽(첨탑)을 둠.

### 2-5. 구도 v6 — 경계선 표현 제거 (v5 수정)

- 스케치의 구분선은 이해를 돕는 **가이드**였으며, 최종 이미지에 능선·절벽·협곡·강 같은 경계로 직접 표현하면 안 됨. (v5 `OpeningScene_Radial_*`는 경계 지형이 드러나 폐기)
- 지역 구분은 지형·식생·암석·빛·하늘색이 서서히 섞이며 바뀌는 방식으로만 표현. 지역 배치(스테이지 1 좌/좌상, 2 좌하, 3 하단, 4 우측, 5 중심)는 유지.
- 산출물(플레이어 제외, 1376x768): `OpeningScene_Blend_Center.png`(스테이지 5 중앙), `OpeningScene_Blend_RightCorner.png`(스테이지 5 오른쪽 구석 쪽).

## 3. 플레이어 3D 모델

### 3-1. Blender 포즈 변경 (재생성 없이 기존 모델 사용)

- 대상: `Knight_BackView_SwordRaised_4K.glb` → 결과: `Knight_BackView_SwordForward_4K.glb`
- 스크립트: `@Edit/OpeningPreview/repose_sword.py` (Blender 헤드리스, 인자: 입력/출력 GLB, 칼 상승각 기본 25°)
- 방식: 리깅이 없는 정적 메시라서, 칼+손(그립 중심 기준)만 강체 회전(약 67°)하고 주변은 smoothstep 감쇠로 몸에 이어 붙임. 전체 정점 23,537개 중 1,010개만 이동.
  몸통·방패·망토·투구·다리 등 나머지 형태는 변경하지 않음(`_Blender /` 수칙 15: 원래 형태 보존 최우선).
- 결과: 칼이 몸 앞쪽(-Y)으로 약 25° 위를 향함. 팔(전완)은 위로 든 원래 상태 유지.
- 미검증: 수칙 34(실루엣 일치율)는 미수행.

- 생성 도구: VARCO 3D (이미지 생성 → 이미지-to-3D, 텍스처 포함, 비-T포즈).
- 프롬프트 요점: 뒷모습 / 뿔 달린 강철 투구 / 은빛 갑옷 / 붉은 망토·스카프 / 갈색 가죽 / 오른팔로 검을 앞위로 겨눔 / 왼쪽 사자 문장 방패.
  (기존 2D 스프라이트 `idle_right.png`의 외형과 일치)
- 산출물: 정적 모델이므로 GLB로 다운로드, 핵심 키워드로 명명(예: `Knight_BackView_SwordRaised.glb`).
- Unity 반영: 씬에 직접 등록하지 않고 `@Prefab` 폴더에 `P_` 접두 프리팹으로 만든 뒤 그 프리팹을 사용 (수칙 20·23·40·47).

## 4. 씬 구성 (Opening.unity)

| 오브젝트 | 설명 |
|---|---|
| Main Camera | 원근 카메라, 플레이어 뒤쪽에서 패널을 바라봄 |
| Panels/Stage1~4 | 아랫 단 스프라이트 4장 |
| Panels/Stage5 | 윗 단 스프라이트 |
| Cliff | 플레이어가 선 절벽 끝(프리미티브 바위) |
| OpeningPlayer | VARCO 3D 모델 |
| Directional Light | 붉은 하늘 톤의 약한 역광 |

## 5. 상태

- 설계 작성: 완료
- 모델 생성: 완료 (4K 프롬프트 재생성본 `C:\Users\MBC-501-21\Documents\LHS\FinalProject_@Edit\OpeningPreview\Knight_BackView_SwordRaised_4K.glb`). 포즈 보완 여부 사용자 결정 대기
- **최종 확정(사용자): 버전 1 = `OpeningScene_Blend_Center.png` + 플레이어 배치.**
- 최종 합성 미리보기: `C:\Users\MBC-501-21\Documents\LHS\FinalProject_@Edit\OpeningPreview\OpeningScreen_Final_Preview.png` (스크립트 `compose_opening_v4.py`, 1920x1080, Unity 미사용). 칼 포즈 수정 모델에서 바위 받침을 제거하고 좌하단 바위 턱 위에 뒷모습 3/4 각도로 배치, 칼끝이 우상단 마왕성 쪽을 향함.
- 플레이어 위치 재배치(사용자 안내선 기준): 검은 타원=서 있을 위치·크기(몸통 중심 약 가로 31%, 세로 45%), 대각선=발밑 지면선. 결과 `OpeningScreen_Final_Preview_v2.png` (발 위치 1920x1080 기준 몸통 중심 x≈600, 발 y≈688, 키 380px; `compose_opening_v4.py <glb> <bg> <out> 674 688 380`. x=674는 칼이 오른쪽으로 뻗은 실루엣 중심 보정값). 이전 좌하단 배치는 `OpeningScreen_Final_Preview.png`.
- 절벽 재구성(사용자 안내선 기준): 검은 **삼각형 안내선 = 절벽 모양·위치**. 꼭짓점이 중앙 고원 쪽(가로 약 38%, 세로 약 55%), 밑변이 하단 왼쪽(0~32%), 윗면이 좌하단에서 비스듬히 올라오는 쐐기형 절벽. 플레이어(타원)는 그 윗면 끝 근처에 선다. 안내선 자체는 최종 이미지에 선으로 그리지 않고 실제 바위 지형으로만 표현.
  - 배경 재생성: `OpeningScene_Cliff_B.png` 채택 (후보 `OpeningScene_Cliff_A.png`는 절벽 윗면에 검은 직선이 그대로 그려져 폐기).
  - 합성 결과: `OpeningScreen_Final_Preview_v3.png` (`compose_opening_v4.py <glb> OpeningScene_Cliff_B.png <out> 674 712 380`). 발 y는 절벽 윗면에 닿도록 안내 위치(688)에서 712로 보정.
- 꼭짓점 정렬 + 플레이어 크기 보정: `align_cliff.py`로 배경을 가로 구간 리맵(왼쪽 22% 압축 / 오른쪽 21% 확장, 화면 가장자리 고정)하여 절벽 꼭짓점을 가로 약 49%→38%로 이동(`OpeningScene_Cliff_B_Aligned.png`). 안내선 비율(플레이어 키 ≈ 절벽 높이의 76%, 몸 폭 ≈ 밑변의 1/4)에 맞춰 플레이어 키 370px. 결과 `OpeningScreen_Final_Preview_v4.png` (`compose_opening_v4.py <glb> OpeningScene_Cliff_B_Aligned.png <out> 674 712 370`). 세로 꼭짓점 높이는 약 52%(안내 55%)로 근사.
- 플레이어 2배 확대 + 절벽 위 재배치: 키 370px → 740px(화면 높이의 약 2/3). 꼭짓점 근처 윗면은 폭이 좁아 커진 몸이 올라서기 어려워, 윗면이 더 넓은 절벽 중간쪽으로 이동(몸통 중심 가로 약 440px, 발 세로 약 800px, 1920x1080 기준). 결과 `OpeningScreen_Final_Preview_v5.png` (`compose_opening_v4.py <glb> OpeningScene_Cliff_B_Aligned.png <out> 590 800 740`; x=590은 칼이 뻗은 실루엣 보정값 포함).
- 포즈 재수정(현재 배치에 맞춤): 기존 포즈(팔을 위로 든 채 칼이 하늘을 향함)는 마왕성 방향과 맞지 않아 폐기. 마왕성이 플레이어 손과 거의 같은 높이이므로 **팔 전체를 앞으로 곧게 뻗고 칼이 팔과 일직선**이 되도록 변경.
  - 스크립트: `repose_arm.py <in.glb> <out.glb> [yaw 20°] [elevation 3°]` — 1단계: 전완+손+칼을 어깨 기준으로 강체 회전(어깨 근처는 가중치 0→1 램프로 견갑·몸통 보존, 약 50°), 2단계: 칼+손을 주먹 기준으로 회전해 칼을 팔과 일직선으로(약 80°). 정점 약 1,767개(팔·칼 섬)만 이동, 몸통·방패·망토·투구·다리 형태 유지(수칙 15). 입력은 원본 `Knight_BackView_SwordRaised_4K.glb`, 결과 `Knight_BackView_ArmExtended_4K.glb`, 5방향 확인 `Preview_Knight_BackView_ArmExtended_4K.png`.
  - 합성: `compose_opening_v4.py`를 몸 높이(발~뿔 끝, z 범위 기준) + 발 중심 기준으로 수정(자세가 바뀌어도 크기·위치 일정). 결과 `OpeningScreen_Final_Preview_v6.png` (`<glb> OpeningScene_Cliff_B_Aligned.png <out> 480 800 700`).
- 포즈 재수정 2(사용자 지시): 검이 **왼쪽 아래에서 오른쪽 위로 비스듬히** 스테이지 5를 가리키도록 변경. 마왕성 상부가 손보다 위에 있어 수평이 아닌 상승각 필요 → `repose_arm.py <in> <out> 20 22` (모델 상승각 22° ≈ 화면상 약 24°, 우측 20° 방향). 결과 `Knight_BackView_ArmDiagonal_4K.glb`, 합성 `OpeningScreen_Final_Preview_v7.png` (`compose_opening_v4.py <glb> OpeningScene_Cliff_B_Aligned.png <out> 480 800 700`). 직전 수평 버전은 `Knight_BackView_ArmExtended_4K.glb`/`OpeningScreen_Final_Preview_v6.png`.
- (이전) 초기 미리보기: `@Edit/OpeningPreview/OpeningScreen_Preview.png` (생성 스크립트 `compose_opening.py`, Unity 미사용)
- Unity 적용: 사용자 승인 대기 (수칙 9·11)
