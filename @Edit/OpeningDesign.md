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
- 미검증: 수칙 32(실루엣 일치율)는 미수행.

- 생성 도구: VARCO 3D (이미지 생성 → 이미지-to-3D, 텍스처 포함, 비-T포즈).
- 프롬프트 요점: 뒷모습 / 뿔 달린 강철 투구 / 은빛 갑옷 / 붉은 망토·스카프 / 갈색 가죽 / 오른팔로 검을 앞위로 겨눔 / 왼쪽 사자 문장 방패.
  (기존 2D 스프라이트 `idle_right.png`의 외형과 일치)
- 산출물: 정적 모델이므로 GLB로 다운로드, 핵심 키워드로 명명(예: `Knight_BackView_SwordRaised.glb`).
- Unity 반영: 씬에 직접 등록하지 않고 `@Prefab` 폴더에 `P_` 접두 프리팹으로 만든 뒤 그 프리팹을 사용 (수칙 20·23·38·45).

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
- 모델 생성: 완료 (4K 프롬프트 재생성본 `C:\Users\<현재 사용자명>\Documents\LHS\FinalProject_@Edit\OpeningPreview\Knight_BackView_SwordRaised_4K.glb`). 포즈 보완 여부 사용자 결정 대기
- **최종 확정(사용자): 버전 1 = `OpeningScene_Blend_Center.png` + 플레이어 배치.**
- 최종 합성 미리보기: `C:\Users\<현재 사용자명>\Documents\LHS\FinalProject_@Edit\OpeningPreview\OpeningScreen_Final_Preview.png` (스크립트 `compose_opening_v4.py`, 1920x1080, Unity 미사용). 칼 포즈 수정 모델에서 바위 받침을 제거하고 좌하단 바위 턱 위에 뒷모습 3/4 각도로 배치, 칼끝이 우상단 마왕성 쪽을 향함.
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
- **타이틀 "FINAL DUNGEON" (영문, 3D 입체 문자 이미지) 삽입**: VARCO 이미지 생성(4K 문구 포함, nano-banana-pro, 16:9, 초록 단색 배경, 후보 2장) → `Title_FinalDungeon_A.png`, `Title_FinalDungeon_B.png`(채택: 용암 균열이 더 선명하고 글자가 큼직). 두 줄 구성(FINAL / DUNGEON), 철자 확인 완료.
  - 합성: `compose_title.py <base> <title> <out> <x> <y> <width>` — 초록 배경 키잉 + 가장자리 초록 번짐 제거 + 약한 그림자. 좌상단은 플레이어 투구 뿔을 가려 폐기, **우하단 어두운 동굴 지대(x 1290, y 770, 폭 600px)** 채택.
  - 결과 `OpeningScreen_Final_WithTitle.png` (기반 `OpeningScreen_Final_Preview_v7.png`).
- **타이틀 정중앙 배치(사용자 지시)**: 화면 정중앙(가로·세로 모두)으로 이동. `compose_title.py ... -1 -1 600`(x/y에 -1을 주면 해당 축 중앙 정렬) → 위치 (660, 413), 크기 600x253px. 결과 `OpeningScreen_Final_WithTitle_Center.png`. 우하단 배치본 `OpeningScreen_Final_WithTitle.png`는 비교용으로 유지.
- **타이틀 표현 방식 변경 — 클래식 일본 콘솔 RPG(드래곤 퀘스트 계열) 로고 방식 참고(사용자 지시, 특정 로고 모방 없이 표현 방식만 차용)**: 기존 "돌+용암 균열" 표현 폐기. 새 표현: 윗줄 "FINAL"(작고 위로 살짝 아치) + 아랫줄 "DUNGEON"(크고 넓음), 날카로운 세리프의 굵은 글자, 금속 금색→주황 그라데이션 + 상단 흰노랑 하이라이트 + 하단 청동색, 강한 입체 베벨, 두꺼운 검정 외곽선 + 얇은 갈색 안쪽 외곽선. 장식·엠블럼 없음.
  - 생성: VARCO(4K 문구 포함, nano-banana-pro, 16:9, 초록 단색 배경) 후보 `Title_FinalDungeon_Classic_A.png`(깔끔) / `_B.png`(채택: 이중 외곽선·베벨·질감이 풍부). 철자 확인 완료.
  - 합성: `compose_title.py ... -1 -1 640`(정중앙, 640x270px) → `OpeningScreen_Final_WithTitle_Classic.png`. 이전 "돌+용암" 버전(`..._WithTitle_Center.png`, `Title_FinalDungeon_A/B.png`)은 비교용으로 유지.
- **타이틀 표현 방식 재수정 — 사용자 레퍼런스 이미지(드래곤 퀘스트 XI 타이틀 화면) 참고**: 이전 금색 단일톤 로고는 폐기. 레퍼런스에서 차용한 표현 요소(원본 에셋으로 제작, 특정 로고 모방 없음):
  1. 파란 광택 3D 블록 글자 + 두꺼운 금색 베벨 테두리 + 진한 남색 외곽선 + 흰 하이라이트, 아치형 밑선 → `Title_FinalDungeon_Blue_A.png`(채택) / `_B.png` (VARCO, 4K 문구, 초록 배경)
  2. 로고 뒤를 휘감는 반투명 분홍빛 용 실루엣(오라) → `Dragon_Aura_A.png`(채택) / `_B.png` (VARCO, 검은 배경, 스크린 블렌드로 합성, 머리가 오른쪽 위)
  3. 로고 아래 한글 제목 줄 + 얇은 가로선 + 부제 줄 (흰 글자 + 그림자, 맑은 고딕 Bold) — 문구는 **임시안**: 제목 "파이널 던전", 부제 "마왕성을 향한 마지막 여정"(변경 가능)
  4. 로고+한글 줄을 하나의 덩어리로 화면 정중앙 정렬(블록 높이 473px, 상단 y=303)
  5. 밝고 선명한 색감 (레퍼런스의 저작권 문구 줄은 사실과 다른 내용을 넣지 않기 위해 제외)
  - 합성: `compose_title_v2.py <base> <logo> <dragon> <out> 760 "<제목>" "<부제>"` → `OpeningScreen_Final_WithTitle_DQ.png`. 용 실루엣은 플레이어를 가리지 않도록 폭 1180px·불투명도 0.55로 오른쪽(중심 x≈1130)에 배치.
  - 이전 버전(`..._WithTitle_Classic.png` 금색 / `..._Center.png` 돌+용암)은 비교용으로 유지.
- **시안 10종 생성(사용자 지시)**: 기본 시안(`OpeningScreen_Final_WithTitle_DQ.png`: 좌하단 절벽 위 기사 + 우상단 마왕성 + 정중앙 제목 덩어리)의 틀은 유지하고 시안마다 변경점 1개씩 부여. 기존에 생성해 둔 에셋만 재사용(추가 크레딧 0). 스크립트 `compose_title_variants.py <에셋 폴더> <출력 폴더>`, 결과 폴더 `@Edit/OpeningPreview/Variants/` (`OpeningVariant_NN_*.png` 10장 + `OpeningVariants_ContactSheet.png` 번호 비교표).

  | # | 파일 | 변경점 |
  |---|---|---|
  | 01 | `01_logoB` | 로고 B형(곧은 밑선) |
  | 02 | `02_gold` | 금색 클래식 로고 |
  | 03 | `03_stone` | 어두운 돌·용암 로고 |
  | 04 | `04_dragonB` | 용 오라 B형(더 크고 진하게) |
  | 05 | `05_top` | 제목 블록 상단 배치(화면 높이 36% 지점) |
  | 06 | `06_bigLogo` | 로고 대형화(폭 760→960) |
  | 07 | `07_minimal` | 용 오라·한글 줄 제거(로고만) |
  | 08 | `08_blueDragon` | 푸른 용 오라 |
  | 09 | `09_duskSun` | 황혼 톤 + 비네팅 + 로고 뒤 햇살(선버스트) |
  | 10 | `10_press` | 하단 "PRESS START" 안내 추가 |
- **시안 10종 v2 (사용자 지시): 글자 크기 배열 반대 + 용 오라 제외.** 로고를 "FINAL"(크게) / "DUNGEON"(작게, FINAL 글자 높이의 약 55%) 구성으로 새로 생성(VARCO, 4K 문구, 초록 배경, nano-banana-pro, 스타일 3종×후보 2장 = 6장, 전부 철자 확인): `Title_FinalBig_Blue_A/B.png`(파랑·금), `Title_FinalBig_Gold_A/B.png`(클래식 금색), `Title_FinalBig_Stone_A/B.png`(돌·용암). 모든 시안에서 용 오라 제외. 틀(기사·절벽·마왕성·정중앙 제목 덩어리)은 유지, 시안마다 변경점 1개.
  - 스크립트 `compose_title_variants_v2.py <에셋 폴더> <출력 폴더>`, 결과 `@Edit/OpeningPreview/Variants2/` (`OpeningVariant_NN_*.png` 10장 + `OpeningVariants_ContactSheet.png`).

  | # | 파일 | 변경점 |
  |---|---|---|
  | 01 | `01_blueA` | 기본: 파랑·금 로고 A |
  | 02 | `02_blueB` | 파랑·금 로고 B |
  | 03 | `03_gold` | 클래식 금색 로고 |
  | 04 | `04_stone` | 돌·용암 로고 |
  | 05 | `05_top` | 제목 블록 상단 배치 |
  | 06 | `06_bigLogo` | 로고 대형화(폭 960) |
  | 07 | `07_minimal` | 한글 줄 제거(로고만) |
  | 08 | `08_duskGlow` | 황혼 톤 + 비네팅 + 은은한 글로우 |
  | 09 | `09_press` | 하단 "PRESS START" 추가 |
  | 10 | `10_band` | 제목 블록 뒤 반투명 어두운 띠 |
  - 이전 v1 시안(`Variants/`, DUNGEON 크게 + 용 오라)은 비교용으로 유지.
- **시안 10종 v3 (사용자 지시): v2는 1차 시안보다 글자가 두드러지지 않고 배치 모양이 약함 → 레퍼런스처럼 부채꼴(역사다리꼴) 배치.** 윗줄 "FINAL"은 매우 크고 넓게 무지개처럼 위로 휘고, 아랫줄 "DUNGEON"은 윗줄 폭의 약 55%로 좁게 모아 배치. 로고 재생성(VARCO, 4K 문구, 초록 배경, 스타일 3종×2장, 철자 확인): `Title_Fan_Blue_A/B.png`, `Title_Fan_Gold_A/B.png`, `Title_Fan_Stone_A/B.png`. 합성 기본 로고 폭 760→900px(06번은 1120px)로 키워 존재감 강화. 용 오라 제외 유지.
  - 스크립트 `compose_title_variants_v3.py`, 결과 `@Edit/OpeningPreview/Variants3/` (시안 10장 + `OpeningVariants_ContactSheet.png`). 변경점 구성은 v2 표와 동일(01 파랑금A, 02 파랑금B, 03 금색, 04 돌·용암, 05 상단, 06 대형, 07 로고만, 08 황혼+글로우, 09 PRESS START, 10 반투명 띠).
  - 확인된 문제: 06번(폭 1120)은 로고가 기사의 팔·칼과 마왕성 일부를 가림.
- **타이틀 배치 정밀화 (사용자 지시, 레퍼런스 DQ XI 타이틀)**: 윗단 글자는 레퍼런스 윗줄의 배치 형태(큰 아치, 양끝이 낮게 처짐, 글자가 원호 중심 방향으로 방사형 기울기, 가운데 글자가 가장 큼)를 따르고, 아랫단은 같은 원호 중심을 공유해 윗단 아래로 좁게 수렴하는 부채꼴. AI 생성은 정확한 배치를 맞출 수 없어 **Blender에서 글자를 한 자씩 3D로 직접 배치**.
  - `build_fan_logo.py <out.png> [blue|gold]`: 글자 = 남색 외곽 슬랩 + 베벨 금색 몸체 + 광택 파란 앞면(세로 그라데이션). 원호 중심 공통, 윗단 반경 9.0·글자 2.7·가운데 확대 14%, 아랫단 반경 7.4·글자 1.12(폭 약 55%). 글꼴 Georgia Bold. 투명 배경 렌더 → `FanLogo_Blue.png`, `FanLogo_Gold.png`.
  - 합성: `compose_fan_title.py <base> <logo> none <out> 980 "<제목>" "<부제>"` → `OpeningScreen_FanTitle_Blue.png`, `OpeningScreen_FanTitle_Gold.png` (정중앙, 용 오라 없음).
  - 주의: 레퍼런스 로고의 글꼴·형태를 복제한 것이 아니라 **배치 기하 구조만** 따른 원본 디자인(글꼴 다름, 100% 동일 형태 불가).
- **레퍼런스 윤곽 트레이싱 (사용자 지시, v6 아치 해석 수정)**: 레퍼런스 윗줄을 4배 확대·측정한 결과, 윗줄은 "무지개 아치"가 아니라 **양끝 글자(D·T)가 가장 크고 바깥으로 기울며, 가운데 글자가 낮은(윗선이 움푹한) 형태 + 아랫선은 거의 일직선 + 가운데 아래에 좁은 요소(XI)가 모이는 부채꼴**. 측정 비율(윗줄 폭 대비): 양끝 글자 높이 약 19%, 중앙 글자 약 11%, 양끝 기울기 약 15~25°.
  - 범위: 로고 그림·글자꼴 자체를 따라 그리는 것은 하지 않고(상표 로고), **윤곽(높이 분포·기울기·폭 비율)만 트레이싱**하여 직접 만든 글자(Georgia Bold)에 적용.
  - `build_fan_logo_v2.py`: 윗단 FINAL = 폭 12.0, 중앙 글자 높이 1.30·양끝 2.35, 양끝 기울기 24°, 공통 중심선 정렬(글자 폭에 비례한 간격). 아랫단 DUNGEON = 폭 6.6(윗단의 55%), 높이 0.62~0.95, 기울기 16°, 윗단 아래 중앙. 결과 `FanLogo2_Blue.png`, `FanLogo2_Gold.png`.
  - 합성: `compose_fan_title.py ... 1000 ...` → `OpeningScreen_FanTrace_Blue.png`, `OpeningScreen_FanTrace_Gold.png` (정중앙, 용 오라 없음). 이전 아치형(`OpeningScreen_FanTitle_*.png`)은 비교용 유지.
- **기울기·높이 차이 강화 (사용자 지시)**: `build_fan_logo_v3.py` — 윗단 FINAL: 중앙 글자 높이 1.30→1.05, 양끝 2.35→3.10, 기울기 24°→38°, 증가 지수 1.3→1.1(양끝 쪽에서 더 빨리 커짐). 아랫단 DUNGEON: 높이 0.60~1.15, 기울기 16°→26°, 윗단 아래로 간격 확대(zc -2.45). 결과 `FanLogo3_Blue/Gold.png`, 합성 `OpeningScreen_FanTrace2_Blue.png`, `OpeningScreen_FanTrace2_Gold.png` (이전 `FanTrace_*`는 비교용 유지).
  - 알려진 문제: 양끝 "F"가 기사 망토·칼 밑부분과 더 많이 겹치고, "L"은 마왕성 우측 벽을 가림.
- **2열 분리 폐지 → 한 줄 배치 (사용자 지시, 레퍼런스와 동일 구조)**: 레퍼런스처럼 "FINAL DUNGEON"을 **하나의 줄**(FINAL + DUNGEON, 레퍼런스의 DRAGON + QUEST에 해당)로 배치. `build_fan_logo_v4.py` — 12글자(+단어 사이 간격) 한 줄, 폭 20.0, 중앙 글자 높이 2.0 → 양끝 3.9(증가 지수 1.25), 양끝 기울기 30°, 글자를 세로로 늘림(SY 자동 계산 ≈ 2.0: 레퍼런스의 키 큰 좁은 글자 비율에 맞춤), 글자 간격 0.24em·단어 간격 0.30em. 결과 `FanLogo4_Blue/Gold.png`, 합성(로고 폭 1500px, 정중앙) `OpeningScreen_FanOneRow_Blue.png`, `OpeningScreen_FanOneRow_Gold.png`.
  - 알려진 문제: 폭이 넓어 왼쪽 "F"가 기사 몸통·망토를 가림, 한글 줄이 로고 폭에 비례해 커짐. 레퍼런스 중앙의 "XI"에 해당하는 요소는 넣지 않음(해당 요소 문구 미정).
- **기울기를 더 부드럽게 (사용자 지시)**: `build_fan_logo_v5.py` — 기울기를 위치에 선형 비례(`(2u-1)·30°`)하던 것을 **사인 곡선**(`26°·sin((2u-1)·π/2)`)으로, 높이를 거듭제곱 곡선 대신 **코사인 이즈**(`(1-cos(π|2u-1|))/2`, 중앙·양끝 기울기 0)로 변경 → 글자 사이 급격한 단차 제거. 높이 중앙 2.0 → 양끝 3.6(3.9에서 완화), 최대 기울기 26°(30°에서 완화). 결과 `FanLogo5_Blue/Gold.png`, 합성 `OpeningScreen_FanSmooth_Blue.png`, `OpeningScreen_FanSmooth_Gold.png` (정중앙, 로고 폭 1500px).
  - 이전 직선 기울기 버전(`FanOneRow_*`)은 비교용 유지.
- **안쪽 곡률 + 도형 트레이싱 삽입 (사용자 지시)**:
  1. **도형 트레이싱**: 레퍼런스 윗줄을 4배 확대(crop4x: 레퍼런스 x70~510, y60~210)하고 글자 가장자리의 꼭짓점을 잡아 다각형 작성 — 윗선 9점, 아랫선 8점(총 17점), 오차 약 ±4px(레퍼런스 픽셀). 꼭짓점 `TitleShape_vertices.json`, 도형 `TitleShape_polygon.png`, 겹침 검증 `TitleShape_overlay_check.png`, 스크립트 `trace_polygon.py`. 형태: 왼쪽 끝이 가장 높고 바깥으로 기울며 아래로 길게 처짐 / 윗선은 가운데로 움푹 / 오른쪽 끝은 위로 솟음 / 아랫선은 거의 일직선(오른쪽 끝에서 위로 올라감). 좌우 비대칭.
  2. **화면 안쪽으로 휘는 곡률**: 글자를 3D 오목 곡면(반경 17)에 배치 — 가운데 글자는 카메라에서 멀리, 양끝은 가까이 + 양끝 글자가 중앙을 향해 회전(yaw) + 원근 카메라(35mm, 거리 34) → 양끝 글자가 커지고 옆면 두께가 보임. `build_bowl_strip.py` → `BowlStrip_Blue/Gold.png`(곧은 글자열).
  3. **도형에 삽입**: `warp_to_shape.py` — 위선·아랫선 꼭짓점 곡선 + 비스듬한 좌우 변으로 Coons 패치 변형(곧은 글자열의 위→도형 윗선, 아래→도형 아랫선). 결과 `ShapeTitle_Blue/Gold.png`.
  4. **합성**: `compose_fan_title.py ... 1500 ...` → `OpeningScreen_ShapeTitle_Blue.png`, `OpeningScreen_ShapeTitle_Gold.png` (정중앙, 용 오라 없음).
  - 알려진 문제: 왼쪽 "F-I"가 도형 변형으로 좁게 눌려 겹침, 왼쪽 끝 글자가 기사 상반신을 가림, 한글 제목 줄이 로고 폭에 비례해 큼.
- **사용자 외곽선 안내선 기준 재작업 (사용자 지시)**: 사용자가 레퍼런스 위에 검은 선으로 외곽선 안내선을 그림(`images/5.jpg`). 이전의 눈대중 꼭짓점은 폐기하고 **안내선의 중심선 픽셀**에서 꼭짓점을 추출·정리(`extract_guide.py`로 검은 선 추출 후 열 단위 실측으로 정리, `trace_polygon_v2.py`).
  - 꼭짓점(안내 이미지 px, 윗선 11점·아랫선 9점): 윗선 (79,88),(101,73),(150,91),(210,106),(270,116),(330,116),(390,109),(436,100),(484,85),(502,79),(516,93) / 아랫선 (139,187),(150,185),(210,173),(270,160),(300,163),(360,169),(420,178),(437,191),(466,171). 좌우 변은 (79,88)→(139,187), (516,93)→(466,171)의 직선. `TitleShape_vertices.json`(×4 좌표) / 일치 검증 `TitleShape_overlay_check.png`(도형이 사용자 선 위에 겹침) / 도형 `TitleShape_polygon.png`.
  - 글자 삽입: `warp_to_shape.py`(Coons 패치)로 안쪽 곡률 글자열(`BowlStrip_*.png`)을 도형에 변형 → `ShapeTitle_Blue/Gold.png`, 합성(로고 폭 1500px, 정중앙, 용 오라 없음) `OpeningScreen_ShapeTitle2_Blue.png`, `OpeningScreen_ShapeTitle2_Gold.png`. 이전 눈대중 버전 `OpeningScreen_ShapeTitle_*`은 비교용 유지.
  - 알려진 문제: 왼쪽 끝 "F-I" 겹침·기사 가림, 오른쪽 끝 "N"이 위로 길게 늘어남(도형 오른쪽 끝이 날카롭게 솟는 형태 때문), 한글 줄이 큼.
- **글자 형태 인식성 개선 (사용자 지시)**: 도형(사용자 외곽선)은 그대로 두고 글자 가독성을 개선. 원인 3가지와 조치:
  1. 이웃 글자와 겹침(좁은 간격·두꺼운 몸체) → `build_bowl_strip_v3.py`: 글자 간격 0.24→0.46em, 몸체 두께 0.20→0.10, 앞면/외곽 깊이 간격 축소.
  2. 안쪽 곡률이 강해 양끝 글자의 옆면이 앞면을 가림 → 오목 곡면 반경 17→30(곡률 완화), 세로 늘림 SY 고정 1.25.
  3. 글자와 배경 경계가 흐림 → `warp_to_shape_v3.py`: 글자 바깥에 어두운 남색 외곽선(알파 팽창 9회) 추가.
  4. 도형이 높은 양끝에서 글자를 세로로만 늘림 → 매핑을 **종횡비 보존 매개화**(도형이 높은 구간에 글자 폭을 비례 배분, `ds/du = c·H(s)` 수치 적분)로 변경. (첫 시도는 방향이 반대여서 양끝 글자가 더 가늘어졌고, 수정 후 해결)
  - 결과 `ShapeTitle3_Blue/Gold.png`, 합성 `OpeningScreen_ShapeTitle3_Blue.png`, `OpeningScreen_ShapeTitle3_Gold.png`(로고 폭 1500px, 정중앙, 용 오라 없음). 직전 버전 `ShapeTitle2_*`/`OpeningScreen_ShapeTitle2_*`는 비교용 유지.
  - 남은 문제: 왼쪽 끝 "F-I"가 기사 상반신을 가림, 마지막 "N"은 도형 끝이 날카로워 기울어진 형태, 한글 줄이 큼.
- **글자 개별 설계 방식으로 전환 (사용자 지시: 양끝 글자 왜곡 과다 → 레퍼런스의 글자 설계 참고)**: 도형에 글자열을 억지로 변형하던 방식(`warp_to_shape*`) 폐기. 레퍼런스는 글자를 하나씩 따로 설계한 3D 블록 글자(왼쪽 아래로 두꺼운 금색 옆면, 글자별 크기·기울기 차이)이므로 같은 방식 적용. `build_traced_letters.py`: **글자는 변형하지 않고**(전 글자 동일 비율 SY 1.9) 사용자 외곽선 도형에서 읽은 값으로 글자별 크기·기울기·위치만 결정 — 도형 양끝 변의 기울기(좌 31.2°, 우 32.7°)를 양끝 글자 최대 기울기로, 도형의 위·아랫선 높이를 글자 크기(양끝 79·66px → 중앙 35px, 좌측이 더 큼)와 수직 중심 위치로 사용. 두께 0.30(금색 옆면), 카메라는 왼쪽 아래(-3,-44,-2.2) → 옆면이 왼쪽 아래로 보임. 결과 `TracedLetters_Blue/Gold.png`, 합성 `OpeningScreen_Traced_Blue.png`, `OpeningScreen_Traced_Gold.png` (정중앙, 로고 폭 1500px, 용 오라 없음). `ShapeTitle3_*`는 비교용 유지.
  - 남은 문제: 단어 사이 간격이 좁아 "FINAL DUNGEON"이 붙어 읽힘, 왼쪽 "F-I"가 기사 상반신을 가림, 글꼴은 Georgia Bold(레퍼런스의 거칠고 뾰족한 글자꼴과 다름).
- **안내선 준수 (사용자 지시: 개별 글자 방식은 유지하되 사용자 안내선을 넘지 않을 것)**: 직전 `TracedLetters_*`는 글자 크기·기울기를 도형 높이로만 정해 기울어진 글자 모서리와 옆면이 도형 밖으로 나감. `build_traced_letters_v2.py`로 수정:
  1. 글자마다 회전한 상자의 모서리·변 중점 + 왼쪽 아래 옆면(6px)이 **도형 안쪽 5px 여유**에 들어오는지 검사, 들어올 때까지 크기를 줄이고 위치를 ±14×±8px 범위에서 탐색.
  2. 정면 직교 카메라로 렌더(렌더 8px = 안내 이미지 1px)해 렌더 결과를 도형과 직접 대조 → 도형 밖 픽셀 비율 **0.008%**(안티앨리어싱 수준)를 스크립트가 출력. 검증 이미지 `TracedLetters_guide_check.png`(안내선 위에 글자 겹침).
  3. 옆면은 원근 대신 왼쪽 아래로 밀린 몸체 복사본(깊이 0.045em)으로 표현하여 검사 범위에 포함.
  - 결과 `TracedLetters2_Blue/Gold.png`, 합성 `OpeningScreen_Traced2_Blue.png`, `OpeningScreen_Traced2_Gold.png`. 여유(5px)를 두면서 글자가 직전보다 작아지고 "DUNGEON" 글자 간격이 벌어짐(여유값은 조절 가능). 이전 `OpeningScreen_Traced_*`(안내선 이탈)는 비교용으로 유지.
- **글자 간격 좁히고 크기 키움 (사용자 지시)**: `build_traced_letters_v3.py` — 글자 간격 0.42em→0.26em, 글자 높이 기준 확대(양끝 150/125→190/160, 중앙 62→80), 글자 비율 SY 2.0→1.7(덜 가늘게), 안내선 여유 5px→4px, 위치 탐색 ±14→±5px(글자가 이웃과 겹치지 않도록). 시도 중 간격 0.12em은 "F-I-N"이 겹쳐 폐기. 도형 밖 픽셀 비율 0.099%(안내선 준수 유지). 결과 `TracedLetters3_Blue/Gold.png`, 합성 `OpeningScreen_Traced3_Blue.png`, `OpeningScreen_Traced3_Gold.png`. 직전 `Traced2_*`는 비교용 유지.
  - 남은 문제: "DUNGEON" 앞쪽 D·U 간격이 벌어져 보임, 왼쪽 "F-I-N"이 기사 상반신 위에 걸침.
- **2차원 기울기 폐기 → 3차원 기울기 (사용자 지시)**: 평면 안에서 글자를 돌리던(롤) 방식을 모두 제거. `build_3d_letters.py` — 글자는 똑바로 세운 3D 블록 글자이고, 기울어 보이는 효과는 **3차원 회전**(① 오목 곡면 위 위치에 따른 좌우 회전(yaw), ② 글자판 전체를 앞으로 숙임(pitch 24°))과 **가까운 원근 카메라**(거리 15, 렌즈는 프레임 폭 450 안내 px에 맞춰 계산)로 만든다.
  - 안내선 준수: 글자마다 3개 층의 바운딩 박스 8점씩을 **카메라에 투영**(`world_to_camera_view`)해 도형 안쪽 3px 여유 안에 들어올 때까지 크기를 줄이고 위치 탐색(±4×±8px). 원근이 위치를 흐트러뜨려 투영 중심을 목표에 맞추는 3회 보정 추가. 렌더 결과 대조 도형 밖 픽셀 **0.0%**(`Letters3D_guide_check.png`).
  - 시행착오: 앞으로 42° 숙이고 두꺼운 몸체를 쓰면 몸체 윗면이 보여 지저분 → 숙임 24°·몸체 두께 0.26→0.09로 완화. 깊이 층 부호 오류로 외곽 층이 앞면을 가린 것도 수정.
  - 결과 `Letters3D_Blue/Gold.png`, 합성 `OpeningScreen_3DTilt_Blue.png`, `OpeningScreen_3DTilt_Gold.png`(정중앙, 용 오라 없음). 이전 `Traced3_*`는 비교용 유지.
  - 한계: 롤 없이 3D 회전만으로는 도형의 비스듬한 양끝 변에 큰 글자가 들어가지 못해 양끝 "F"·"N"이 작음. "I-N", "E-O"가 겹침.
- **양끝 글자 크기 확대 (사용자 지시)**: `build_3d_letters_v2.py` — ① 양끝으로 갈수록 앞으로 더 숙이고(숙임 20°+34°·smooth(u)) 좌우 회전 가중(+0.9) 증가(3D 기울기만 사용, 롤 없음), ② 양끝 글자의 높이 기준 상향(190/160 → 230/200), ③ 양끝 글자(F·I, N·O)만 위치 탐색 범위를 ±4 → ±16px로 확대해 도형의 비스듬한 바깥 변에서 안쪽으로 이동 가능하게 함. 원인: F·N이 도형 바깥 변에 걸려 작아졌음. 결과 `Letters3D2_Blue/Gold.png`, 합성 `OpeningScreen_3DTilt2_Blue.png`, `OpeningScreen_3DTilt2_Gold.png`. 도형 밖 픽셀 0.0%. 직전 `3DTilt_*`는 비교용 유지.
  - 한계: F·N이 직전보다 커졌으나 이웃 글자(I, O)와 겹침(F-I, O-N). 몸체 윗면이 일부 보임.
- **최종 확정 및 Unity 구현 (사용자 지시)**: 최종 시안 = `OpeningScreen_ShapeTitle3_Gold.png`(사용자 외곽선 도형에 맞춘 금색 로고) + ContactSheet의 **PRESS START** 시안(하단 문구). 이전 3D 기울기 시안들은 폐기.
  - **레이어 분리**: `export_opening_layers.py`로 배경/플레이어를 분리 내보내기, `ShapeTitle3_Gold.png`를 1500px로 잘라 로고 레이어화 → `Assets/Sprites/Opening/Opening_Background.png`(1920x1080), `Opening_Player.png`(기사+접지 그림자, 투명), `Opening_Title.png`(1500x403, 투명). 한글 제목·부제·PRESS START는 TMP 텍스트(맑은 고딕 SDF)로 구현.
  - **씬**: `Assets/Scenes/Opening.unity` — 캔버스(기준 1920x1080, 화면 크기 스케일) 위에 배경 → 플레이어 → 로고(좌상단 기준 (210,232)) → "파이널 던전"(y 675) → 가로선(y 724) → 부제(y 775) → PRESS START(y 930) → 검은 페이드. 빌드 설정 **맨 앞(인덱스 0)**에 등록. 생성 도구 `@Scripts/Editor/BuildOpeningScene.cs`(메뉴 Tools/Build Opening Scene, 재실행하면 같은 씬을 다시 만든다).
  - **동작**: `@Scripts/Opening/OpeningScreenController.cs` — PRESS START 알파를 사인 곡선으로 깜빡임(주기 2.2/초, 최소 알파 0.25), 시작 0.4초 후 아무 키/마우스/패드 버튼 입력 시 0.6초 검은 페이드 후 다음 씬(`nextSceneName`, 기본 `MainMenu`)으로 이동. 참조는 Inspector 연결(GameObject.Find 미사용), 싱글톤 미사용.
  - **검증**: 게임 뷰 1920x1080으로 플레이 실행 캡처 → 최종 시안과 레이아웃 일치 확인(캡처는 확인 후 삭제). 입력 전환은 자동 입력 수단이 없어 직접 눌러 보는 확인은 미수행.
  - 참고: 기사는 3D 모델을 렌더한 이미지 레이어로 사용(씬에 3D 모델을 직접 넣지 않았으므로 `@Prefab` 프리팹 규칙 대상 아님).
- **메인 메뉴를 첫 화면으로, 오프닝 배경 이미지 적용 (사용자 지시)**: 게임을 켜면 타이틀 글자·PRESS START 대신 **메인 메뉴**(START / OPTIONS / CREDITS)가 나오며, 메인 메뉴의 배경은 글자를 뺀 타이틀 이미지(= 타이틀 글자·한글·PRESS START가 없는 `Opening_Background.png` + 기사 `Opening_Player.png`)로 한다.
  - 적용 도구 `@Scripts/Editor/ApplyOpeningBackgroundToMainMenu.cs`(메뉴 Tools/Apply Opening Background To MainMenu): `MainMenu`의 기존 단색 `Background`에 풍경 스프라이트를 넣고 그 위에 `Player`(기사)와 `BackgroundDim`(검은 30% 막, 메뉴 글씨 가독성)을 쌓음. 기존 버튼·제목 구성은 변경하지 않음. 기존 Background 의 배율 1.98이 이미지를 2배 확대하던 문제를 배율 1로 수정. 재실행해도 중복 생성되지 않음.
  - **빌드 설정**: `MainMenu`를 맨 앞(인덱스 0)으로, `Opening` 씬은 목록에서 제거(씬 파일과 `OpeningScreenController`는 보존). 따라서 게임 시작 시 첫 화면 = 메인 메뉴. 직전의 "엔터 → Stage1" 오프닝 흐름은 현재 빌드에서 사용되지 않음(필요하면 `Opening`을 다시 맨 앞에 넣으면 됨).
  - 검증: 플레이 실행 캡처로 배경(풍경+기사)·메뉴 버튼 표시 확인(캡처 삭제).
  - 알려진 문제: "Dungeon" 제목 텍스트가 기사 투구와 겹침, 메뉴 버튼이 기사 일부를 가림(기존 메뉴 배치를 유지했기 때문).
- **메인 메뉴 제목을 FINAL DUNGEON 로고로 교체 (사용자 지시)**: `ApplyOpeningBackgroundToMainMenu.cs`에 `ReplaceTitleWithLogo` 추가 — `MenuLayout`의 `TitleText`("Dungeon") 글자는 끄고(레이아웃 칸은 유지해 버튼 위치 불변), 그 자식으로 `TitleLogo` 이미지(`Opening_Title.png`, 금색 도형 로고)를 둠. 크기 520x140(부모 배율 1.98 적용 시 화면 약 1030x277), 위로 12px. 처음 600x161은 로고 윗부분이 화면 밖으로 잘려 520으로 줄임. 재실행 시 중복 생성 없음. 플레이 캡처로 로고 전체 표시 확인(캡처 삭제).
  - 알려진 문제: 로고가 기사 투구·칼과 겹침, 한글 제목/PRESS START는 메인 메뉴에 없음(오프닝 씬 전용).
- **메뉴 버튼 글자를 타이틀과 같은 3D 글자로 (사용자 지시)**: START / OPTIONS / CREDITS를 타이틀(금색 3D)과 같은 스타일의 이미지로 교체. `@Edit/OpeningPreview/build_menu_words.py`(Blender 헤드리스) — Georgia Bold, 금색 그라데이션 앞면 + 왼쪽 아래로 쌓은 금색 옆면 + 남색 외곽 층, 정면 직교 카메라, 투명 PNG → `Assets/Sprites/Opening/Menu_Start.png`(581x145), `Menu_Options.png`(790x145), `Menu_Credits.png`(772x145). `ApplyOpeningBackgroundToMainMenu.cs`에 `ReplaceLabelWithImage` 추가: 각 라벨 텍스트(StartLabel/OptionsLabel/CreditsLabel)는 끄고 자식 `Art` 이미지(420x60 로컬, 비율 유지)로 표시. 버튼 모양·동작(MainMenuManager)은 변경 없음. 플레이 캡처로 확인(삭제).
  - 한계: 버튼 배경은 여전히 기본 파랑/회색 사각형이라 금색 글자와 어울림은 평범함. 타이틀과 달리 곧은(휘지 않은) 글자.
- **메뉴 버튼 배경 타이틀 톤 — 미리보기만 (사용자 지시, Unity 미적용)**: `@Edit/OpeningPreview/mockup_menu_buttons.py`로 실제 게임 화면 레이아웃(로고 1030x277 @ (445,14), 버튼 x 366~1554, y 318/564/810, 각 높이 198)을 그대로 합성한 `MenuMockup_TitleTone.png`. 버튼 판 = 짙은 남색 세로 그라데이션 + 바깥 남색 선(4px) + 금색 베벨 테두리(6px, 왼쪽 위 밝음/오른쪽 아래 어두움) + 안쪽 남색선·얇은 금선 + 둥근 모서리(14px). 선택(호버) 상태의 START는 더 밝은 파랑 + 상단 광택, 나머지는 어두운 남색. 승인 시 Unity 적용(버튼 Image 스프라이트 교체, 9-slice) 예정.
- **버튼 배경 타이틀 톤 Unity 적용 (사용자 승인)**: `@Edit/OpeningPreview/export_button_plates.py`로 미리보기와 같은 그림 코드의 판 스프라이트 3종 내보내기 → `Assets/Sprites/Opening/Button_Normal.png`(짙은 남색), `Button_Highlight.png`(밝은 파랑), `Button_Pressed.png`(어둡게), 각 1200x200(버튼 600x100과 같은 비율). `ApplyOpeningBackgroundToMainMenu.cs`의 `ApplyButtonPlate`가 3개 버튼의 Image 스프라이트를 교체하고 기존 파랑/회색 색 틴트를 흰색으로 제거, 전환 방식을 **SpriteSwap**(평소 Normal / 호버·선택 Highlight / 눌림·비활성 Pressed)으로 변경. 버튼 동작·위치는 그대로. 플레이 캡처로 확인(평소 3개 모두 남색, START를 선택 상태로 만들면 밝은 파랑으로 교체됨; 캡처 삭제).
  - 미확인: 실제 마우스 호버·클릭 입력으로의 확인은 하지 못함(선택 상태로 대체 확인).
- **메뉴 크기 30% 감소 — 미리보기만 (사용자 지시, Unity 미적용)**: `mockup_menu_buttons.py <스프라이트 폴더> <출력> 0.7`(세 번째 인자 = 메뉴 배율, 기본 1.0). 메뉴 중심 (960,663) 기준으로 버튼 판·간격·글자를 0.7배(버튼 1188x198 → 832x139, 메뉴 전체 높이 690 → 483) → `MenuMockup_Scale70.png`. 로고는 그대로. 메뉴가 줄면서 로고 아래~메뉴 사이에 약 130px 빈 공간이 생기고 기사가 가려지지 않음. Unity 적용 시 `MenuLayout`의 localScale 1.98 → 1.386(≈1.98×0.7).
- **메뉴 70% 축소 Unity 적용 (사용자 승인)**: `ApplyOpeningBackgroundToMainMenu.cs`에 `ApplyMenuScale` 추가 — `MenuLayout` 배율 1.98 → 1.386(×0.7). 배율은 레이아웃 중심(화면 y=540) 기준으로 줄어 버튼 묶음이 위로 올라가므로, 미리보기와 같이 버튼 묶음 중심이 y=663에 오도록 `anchoredPosition`을 약 -37 보정. 로고(`TitleLogo`)는 `MenuLayout`의 자식이라 같이 줄어들기 때문에 `sizeDelta = 1030x277 / 1.386`, 화면 좌표 (960, 927.5)로 직접 지정해 크기·위치를 그대로 유지. 재실행해도 동일 결과(절대값 기준). 플레이 캡처로 미리보기(`MenuMockup_Scale70.png`)와 일치 확인(캡처 삭제): 버튼 약 832x139, 기사 망토·몸통이 가려지지 않음.
  - 미확인: 마우스 호버·클릭 입력 확인은 하지 못함, 다른 화면 비율(예: 16:10, 4:3)에서의 확인은 하지 않음(캔버스는 기준 1920x1080, 높이/너비 0.5 매치).
- **선택된 메뉴에 금색 오라 이펙트 (사용자 지시)**:
  - 스프라이트: `@Edit/OpeningPreview/export_menu_aura.py` → `Assets/Sprites/Opening/Menu_Aura.png`(1440x320 = 판 1200x200 + 가로 120/세로 60 패딩). 판 둘레 라운드 사각형 거리장 기준: 바깥은 지수 감쇠 금색 글로우(+얇고 밝은 테두리 5px), 안쪽은 테두리를 따라 옅은 반짝임, 테두리에 가까울수록 흰빛 도는 금색.
  - 동작 `@Scripts/Opening/MenuSelectionAura.cs`: 선택(키보드 이동·마우스 호버) 시 오라가 서서히 켜지고(초당 6) 알파가 0.55~1로 맥동(1.4Hz), 크기도 최대 2.5% 맥동. 선택이 풀리면 서서히 꺼짐. 마우스가 올라오면 해당 버튼을 선택 상태로 만들어 한 번에 하나만 켜짐. 오라 참조는 Inspector 연결(Find 미사용).
  - 적용: `ApplyOpeningBackgroundToMainMenu.cs`에 `ApplySelectionAura`(각 버튼의 첫 자식 `Aura` 이미지 + 컴포넌트 연결, 오라 이미지는 raycast 제외) / `SetFirstSelected`(EventSystem 시작 선택 = START) 추가. 재실행해도 중복 생성 없음.
  - 검증: 플레이 캡처 — 시작 시 START에 금색 오라+밝은 판, 선택을 OPTIONS로 바꾸면 오라가 이동(캡처 삭제). 맥동 애니메이션과 마우스 호버 입력은 정지 캡처로는 확인하지 못함.
- **선택 커서가 없는 메뉴에는 오라 없음 (사용자 지시, 수정)**: 기존에는 선택/해제 이벤트(OnSelect/OnDeselect)로만 켜고 껐고, 오라 이미지의 저장 상태(알파 1)가 켜져 있어 이벤트를 놓치거나 실행 전 편집 화면에서는 선택과 무관하게 보일 여지가 있었음. 수정:
  1. `MenuSelectionAura.cs`: 이벤트 방식 폐기 → **매 프레임 `EventSystem.current.currentSelectedGameObject == 이 버튼`인 경우에만** 켜기(선택이 없거나 다른 버튼이면 서서히 꺼짐). `OnDisable`에서 즉시 소거. 마우스 호버 시 선택시키는 `OnPointerEnter`만 유지.
  2. `ApplyOpeningBackgroundToMainMenu.cs`: 씬에 저장되는 오라 이미지를 항상 알파 0·비활성으로 저장(`ApplySelectionAura`) → 실행 전에도 보이지 않음.
  - 검증(플레이): 시작 시 START만 알파 0.58(켜짐)·나머지 0.00(꺼짐), 선택을 해제(null)하면 3개 모두 0.00·비활성, CREDITS를 선택하면 캡처에 CREDITS만 오라 표시(캡처 삭제).
- 환경 변경 후 `.glb`와 최종 합성 이미지(v1~v7)가 git 미추적이라 사라져 있어, VARCO의 동일 모델(`...SwordRaised_4K.glb`)을 다시 받고 `repose_arm.py`(20°, 22°)와 `compose_opening_v4.py`로 v7을 재현함(정점 수·각도 동일 확인).
- (이전) 초기 미리보기: `@Edit/OpeningPreview/OpeningScreen_Preview.png` (생성 스크립트 `compose_opening.py`, Unity 미사용)
- Unity 적용: 사용자 승인 대기 (수칙 9·11)
