# 플레이어 방어 애니메이션 / 완벽 방어 이펙트 제작 기록 (컨셉)

작업 AI: Claude Sonnet 5.5 (claude-sonnet-5-5), 작성일: 2026-10-14.
분류: 컨셉 작업(수칙 51). Unity/본 게임에는 적용하지 않았다(수칙 9, 11). 설계 기준: `PlayerDesign_Defense.md`.

## 방어 모션 (오른쪽 기준 4프레임)

기준 외형: `Assets/Sprites/Warrior/Transparent/idle_right.png`를 VARCO GPT_Work_Room 플로우에 200x300 JPEG 축소본으로 업로드해 사용했다(2.3MB 원본을 직접 전달할 수 없어 화질은 낮다).
모델 gpt-image-2-medium, 3:4, 프롬프트에 4K resolution 명시, 실제 출력 1152x1536. Run All 미사용, 노드 지정 동시 실행(수칙 13).

| 파일 | 역할 |
|---|---|
| guard_1_start | 진입: 방패가 가슴 높이까지 올라오는 중 |
| guard_2_raised | 진입 완료: 방패를 얼굴/가슴 앞으로 완전히 내민 자세 |
| guard_3_holdA | 유지 A |
| guard_4_holdB | 유지 B (방패 약간 위, 망토 흔들림만 다름) |

1차 생성(각 프레임 독립 생성)은 프레임 2만 합격이었고 1·3·4는 크기와 몸 각도가 달라 폐기했다. 합격한 프레임 2를 직접 입력으로 1·3·4를 다시 생성해 크기, 발 위치, 외형을 맞췄다. 폐기물과 해당 노드는 삭제했다(수칙 29).

저장: `@Edit/hk2_gen/guard_final/`. 프롬프트: `guard_prompts_v1.json`(1차). 

## 완벽 방어 이펙트 (3프레임, 방향 무관)

gpt-image-1.5-medium, 1:1, 4K resolution 명시, 실제 출력 1024x1024. 흰색/금색 + 약간의 하늘색 불꽃. 캐릭터와 방패는 포함하지 않았다. 실제 알파 투명 PNG이며(알파 0~255, 모서리 완전 투명) 어두운 배경 합성으로 확인했다.

| 파일 | 역할 |
|---|---|
| perfect_block_1_flash | 충격 번쩍임(8갈래 별 모양) |
| perfect_block_2_ring | 확장되는 충격파 링 |
| perfect_block_3_sparks | 흩어지는 불꽃 |

저장: `@Edit/hk2_gen/guard_fx_final/`. 연결 지점은 설계서의 `PlayerDefense.OnBlocked(true)`이다.

## 배경 투명 처리 (2026-10-14, Claude Sonnet 5.5)

기존 절차를 그대로 사용했다: VARCO 분리 마스크(gpt-image-2-medium, 기존 `mask_prompt.txt` 기반, 4K resolution 명시, 노드 지정 동시 실행) → `apply_workflow_mask.ps1 -ClearWhiteExterior` → `clean_fringe.ps1 -KeepLargestComponent`. PowerShell 실행 정책 때문에 두 스크립트는 `-ExecutionPolicy Bypass`를 해당 프로세스에만 지정해 실행했다(시스템 설정 변경 없음).

- 결과: `guard_alpha_final/` (4장, 1152x1536 RGBA). 마스크: `guard_masks/`. 흰 배경 원본: `guard_final/`.
- 검증: 유지된 픽셀의 RGB 변경 0, 어두운 배경 합성으로 갑옷/방패/검/망토 외곽 확인. 프레임 2~4는 캔버스 가장자리 불투명 픽셀 0. 프레임 1은 검 끝이 왼쪽 가장자리에 닿아 불투명 22픽셀이 있다(검 끝이 캔버스에 걸려 있음, 잘림 가능성).
- 프레임 4는 발 위치가 다른 프레임보다 약 57px 위(바운딩 박스 하단 1115 vs 약 1172)이다. 발 기준 피벗 설정 시 자동으로 맞는다.
- 크기 보정(PPU)과 발 피벗, Unity 임포트는 아직 하지 않았다.

## PPU 보정 / 발 피벗 (2026-10-14, Claude Sonnet 5.5, Unity 미적용)

- 피벗: 부츠(브라운 영역) 밑면을 접지선으로, 부츠 좌우 범위의 중앙을 X로 계산했다. 프레임 1~4의 PivotX 0.4219/0.4245/0.4245/0.4223, PivotY 0.2461/0.2428/0.2402/0.2806. 프레임 4는 발이 캔버스에서 높아 PivotY가 크다. 왼쪽(반전 사용 시) PivotX는 1-X. 프레임 1·3은 검 끝이 접지선보다 10px 아래에 있다(무시).
- 투구 길이: 열 프로파일로 측정해 프레임 1~4가 101/102/101/101px로 거의 같다(4프레임의 크기 일관성 확인).
- A안(기존 투구 기준, 175px@PPU100): PPU 57.7~58.3. 그러나 이 방어 모션은 머리가 작고 다리가 길게 생성되어 머리~발 높이가 13.5~13.7유닛이 되어 idle(9.41)의 약 1.45배 거인이 된다. 달리기 8.2~8.7, 공격 9.45~9.69, 피격 9.15.
- B안(머리~발 높이 기준, 목표 9.0유닛, 4장 공통 PPU 87.472): 머리~발 8.94~9.05유닛으로 기존 모션과 체격이 비슷하다. 투구는 idle보다 작게 보인다. **권장: B안.**
- 비교 이미지(같은 월드 스케일, Unity 밖에서 합성): `Player_Guard_Scale_Compare.png`. 수치 전체: `guard_import_settings.json`(A/B 병기), `guard_feet_measure.json`.
- 기존 투구 기준 수칙과 달라지는 이유: 이 규칙은 웅크린 자세가 커지는 것을 막기 위한 것이었는데, 방어 모션은 자세가 아닌 몸 비율이 달라서 적용하면 오히려 크기가 어긋난다.

## B안 확정 (2026-10-14, Claude Sonnet 5.5)

사용자가 B안(머리~발 높이 기준, PPU 87.472 공통)을 선택했다. 확정 설정: `guard_import_settings_B_final.json`(PPU, 피벗, 좌우 반전 피벗). Unity에는 적용하지 않았다.

## Unity 임포트 (2026-10-14, Claude Sonnet 5.5, 사용자 명시 지시)

- 위치: `Assets/Sprites/Warrior/HeroKnightGuard/` (방어 4장), `.../Effects/` (이펙트 3장). 모두 3MB 미만이라 `@ExternalAssets`로 옮기지 않았다.
- 방어 4장: Sprite(Single), PPU 87.472, 프레임별 커스텀 피벗(B안 확정값), Point 필터, 무압축, 밉맵 없음, FullRect, 최대 2048. 읽어 본 결과 피벗 픽셀 y가 접지선과 일치(1158/1163/1167/1105), 크기 13.17x17.56유닛.
- 이펙트 3장: Sprite(Single), PPU 256(4x4유닛), 중앙 피벗, Point 필터, 무압축. PPU 256은 임의의 초기값이며 방패 크기에 맞춰 조정할 수 있다.
- 임포트와 설정만 했다. 씬/프리팹 연결은 하지 않았다: `Player_Warrior` 프리팹의 `Guard Right/Left Frames`, `PlayerDefenseFeedback`, 이펙트 재생 연결은 본 게임 적용(수칙 51)이므로 별도 지시 전까지 보류.
- Unity 콘솔에 이번 작업 관련 오류 없음(기존 씬의 EventSystem 중복 경고만 있음). 플레이 테스트는 하지 않았다.

## 본 게임 연결 (2026-10-14, Claude Sonnet 5.5, 사용자 명시 지시)

- `PlayerSpriteAnimator`(Assets/Scripts/Player): 방어 필드 추가 및 표시 분기 이식. 우선순위는 피격 > 방어 > 대시 > 공격 > 상자 열기 > 이동. 무장해제 중에는 표시하지 않는다. 컨셉 초안은 4프레임을 계속 순환했으나, 설계에 맞춰 진입 프레임(앞 2장)을 한 번 재생한 뒤 유지 프레임(나머지)을 반복하도록 개선했다. 방어를 새로 시작하면 진입부터 다시 재생한다.
- 왼쪽: `guardLeftFrames`에도 같은 4장을 넣고 `guardLeftFlipX=true`로 좌우 반전 표시(발 피벗 기준 반전). 설정값: `guardEntryFrames=2`, `guardFramesPerSecond=8`(진입 0.25초, 유지는 A/B를 초당 8프레임으로 교대), `guardVisualOffsetY=0`.
- `Player_Warrior.prefab`(@ExternalAssets/@Prefab, 모든 스테이지 공통 원본)에 `guardRightFrames`/`guardLeftFrames` 4장을 할당했다. 다시 읽어 값 확인. `@ExternalAssets`는 .gitignore 대상이라 git diff에는 나타나지 않는다.
- `PlayerStats`: `SetDefenseAttackBonus` 추가(기본 0, 공격력 계산 결과는 이전과 동일). `Assets/@Scripts/Player/PlayerDefense.cs` 신규 추가(컨셉 초안 이식, 헤더 주석에 작성 AI 표기).
- 컨셉의 PlayerHealth/PlayerCombat/PlayerStats는 본 게임이 이후 변경(드랍 조건, Rearm 등)되어 덮어쓰지 않고 필요한 부분만 이식했다.
- **현재 상태: 표시 코드와 프레임은 연결됐지만 방어 모션은 아직 나오지 않는다.** `PlayerDefense`를 프리팹에 붙이지 않았기 때문이다. 붙이면 좌클릭 유지가 방어 상태가 되는데, 검 공격이 같은 좌클릭이라 입력이 겹치고(설계: 검 공격을 X키로 이동), 피해 차단은 `PlayerHealth.TakeDamage`가 `TryBlock/AbsorbDamage`를 호출해야 적용된다. 이는 조작과 전투를 바꾸는 별도 작업이다.
- Unity 컴파일 후 콘솔 오류 0건. 플레이 테스트는 하지 않았다.

## PlayerDefense 프리팹 부착 (2026-10-14, Claude Sonnet 5.5, 사용자 명시 지시)

- `Player_Warrior.prefab` 루트에 `PlayerDefense`를 추가했다(perfectWindowFrames=3, buffDuration=5). 방어 프레임 4장 연결은 유지. 콘솔 오류 0건, 플레이 테스트 미실행.
- 영향: 좌클릭을 누르는 동안 방어 상태가 되어 방어 모션이 표시된다. 표시 우선순위가 공격보다 높아, 좌클릭 검 공격은 판정은 그대로 일어나지만 화면에는 베기 대신 방어 모션이 나온다.
- 아직 연결되지 않은 것: 피해 차단(`PlayerHealth.TakeDamage`의 `TryBlock`/`AbsorbDamage` 호출)이 없으므로 방어 중에도 피해를 받고 완벽 방어 버프/임시 체력은 발생하지 않는다. 이펙트 재생과 사운드(`PlayerDefenseFeedback`)도 미연결.

## 방어 입력을 우클릭으로 변경 (2026-10-14, Claude Sonnet 5.5, 사용자 지시)

- `PlayerDefense`의 입력을 마우스 0(좌클릭)에서 1(우클릭)으로 바꿨다(`GuardMouseButton` 상수). 컴파일 후 콘솔 오류 0건, 상수값 1 확인, 플레이 테스트 미실행.
- 효과: 좌클릭 검 공격과 입력이 겹치지 않아, 앞서 기록한 "공격이 방어 모션으로 보이는 문제"가 해소된다. 검 공격을 X키로 옮기는 설계안은 필요 없어졌다(설계 문서에 변경 표기).
- 우클릭의 기존 용도인 `PlayerCombat.PerformSpecialAction`은 내용이 비어 있는 확장 지점이고 다른 곳에서 쓰이지 않아 충돌은 없다. 다만 나중에 특수 액션을 구현하면 우클릭이 겹치므로 입력을 다시 나눠야 한다.

## 피해 차단 / 완벽 방어 이펙트·사운드 연결 (2026-10-14, Claude Sonnet 5.5, 사용자 지시)

**피해 차단** (`Assets/@Test/Scripts/Combat/PlayerHealth.cs`)
- `TakeDamage` 맨 앞에서 `PlayerDefense.TryBlock(amount)`를 호출해 방어 중이면 피해 전부를 차단하고(무적 시간 미소모), 차단하지 못한 피해는 방어력 경감 후 `AbsorbDamage`로 임시 체력부터 차감한다. 전부 흡수되면 실제 체력/피격 연출 없음. 무장해제 전환 시 `ClearBuff()`로 버프 제거.
- `PlayerDefense`는 `GetComponent`로 캐싱하고 없으면 건너뛴다(null 허용). 같은 `PlayerHealth`를 쓰는 `P_Player_HeroKnightTest` 등 방어 없는 캐릭터는 동작 변화가 없다. 컨셉 초안과 달리 `RequireComponent`와 `amount<=0` 조기 반환은 넣지 않았다(기존 동작 보존).
- 임시 오브젝트로 시나리오 7가지 검증, 모두 기대값 일치(편집 모드, 씬 미변경, 임시 오브젝트 삭제): ①방어 없이 피격 100->75 ②방어 직후 완벽 방어: 체력 유지, 공격력 10->12, 임시 체력 15 ③해제 후 피격: 경감 25 중 15를 임시 체력이 흡수, 실제 10 ④일반 방어: 차단, 버프 변화 없음 ⑤연속 완벽 방어: 공격력 12 유지(중첩 없음), 임시 체력 30으로 교체 ⑥임시 체력이 전부 흡수하면 실제 피해 없음 ⑦무장해제 시 공격력 10, 임시 체력 0 복구.

**이펙트/사운드** (`Assets/@Scripts/Player/PlayerDefenseFeedback.cs` 신규)
- `OnGuardChanged`/`OnBlocked` 구독. 완벽 방어 시 방패 앞에 이펙트 3프레임(번쩍임 -> 링 -> 불꽃, 15fps, 0.2초)을 재생하고 방향에 따라 x 위치를 반전한다. 효과음은 `SoundManager.PlaySfx`로 재생한다.
- `Player_Warrior.prefab`: `Visual/PerfectBlockEffect`(SpriteRenderer, 평소 꺼짐, 정렬 +1) 추가, 루트에 `PlayerDefenseFeedback` 부착 후 이펙트 3장 연결. 이펙트 위치 (3.5, 7.0)은 방패 앞 추정값이며 `perfectEffectOffset`으로 조정한다(루트 스케일 0.13이라 로컬 단위).
- **효과음은 슬롯만 연결**: 프로젝트에 방패 막기용 클립이 없어(`SFX_Player_*`는 점프/검 휘두르기뿐) `guardStartSfx`/`normalBlockSfx`/`perfectBlockSfx`는 비어 있어 무음이다. `generate_audio`(fal.ai 외부 유료 호출)는 지시 없이 쓰지 않았다.
- 이펙트의 실제 화면 표시(위치/크기/정렬)는 플레이 모드로 확인하지 않았다. 컴파일 오류 0건.

## 방패 효과음 (VARCO Sound, 2026-10-14, Claude Sonnet 5.5, 사용자 지시)

- sound.varco.ai에서 프로젝트 3개 생성(프로젝트당 후보 3개, 음악 Off, 크레딧 50씩 3회): `shield rise`(방어 시작), `shield thud`(일반 막기), `perfect parry`(완벽 막기). 후보 1번씩 다운로드(사용자 승인). 후보 2·3과 프로젝트는 사이트에 보존돼 있다.
- 원본은 모노 48kHz 16bit WAV이고 같은 소리가 여러 번 반복되는 4.4/2.6/9.0초 길이였다. 첫 번째 소리만 잘라(5ms 페이드 인, 끝 페이드 아웃) 피크 -3dBFS로 정규화했다. 원본: `@Edit/hk2_gen/guard_sfx_raw/`.
- 결과: `Assets/@ExternalAssets/Sound/SFX_Player_ShieldRaise.wav`(0.83초), `SFX_Player_ShieldBlock.wav`(0.46초), `SFX_Player_PerfectBlock.wav`(0.72초). `Player_Warrior.prefab`의 `PlayerDefenseFeedback`에 `guardStartSfx`/`normalBlockSfx`/`perfectBlockSfx`로 연결, 다시 읽어 확인. 콘솔 오류 0건.
- 소리는 직접 듣지 못했다. 파형 분석(길이, 반복 구간, 레벨)으로만 검수했으므로 청취 확인과 교체가 필요하다. 후보 2·3을 쓰려면 사이트에서 다시 받아야 한다.
- 다운로드 절차 메모: 사이트의 다운로드 버튼은 Downloads 폴더에 `*.tmp`(완전한 WAV)로만 저장된다. 이를 복사해 쓰고 임시 파일은 삭제했다(수칙 53).

## 미완료 / 결정 필요

- 방패 효과음 청취 검수(필요 시 후보 2·3 교체), 플레이 모드에서 이펙트 위치/크기 확인.
- 버프 UI(`OnBuffChanged` 구독)는 설계상 선택 사항으로 미구현.
- 우클릭 특수 액션을 구현할 때 입력 재분배 필요.
- (방어 입력 정리는 완료)(검 공격 X키 이동 여부), `PlayerHealth` 피해 차단 연결, `PlayerDefenseFeedback`/완벽 방어 이펙트 재생(`OnBlocked(true)`).
- 왼쪽 방향은 기존 방식대로 오른쪽 이미지를 좌우 반전해 쓰면 검/방패 든 손도 반전된다.
- Unity 임포트, `Guard Right/Left Frames` 연결, 재생 속도 설정은 수칙 9·11에 따라 지시 전까지 하지 않았다.
- 프레임 1~4의 재생(진입 2장 후 유지 2장 반복)은 `PlayerSpriteAnimator`의 Guard Frames 재생 방식에 맞게 연결 시 확인이 필요하다.
