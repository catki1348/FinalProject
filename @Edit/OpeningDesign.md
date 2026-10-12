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
- 모델 생성: 완료 (4K 프롬프트 재생성본 `@Edit/OpeningPreview/Knight_BackView_SwordRaised_4K.glb`). 포즈 보완 여부 사용자 결정 대기
- 오프닝 화면 미리보기: 완료 — `@Edit/OpeningPreview/OpeningScreen_Preview.png` (생성 스크립트 `compose_opening.py`, Unity 미사용)
- Unity 적용: 사용자 승인 대기 (수칙 9·11)
