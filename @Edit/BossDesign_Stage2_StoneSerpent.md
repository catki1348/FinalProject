# 보스 설계 - 2 : 스테이지 2 보스 Stone Serpent (돌 뱀)

> 2026-10-02 작성. 스테이지 1 보스(Ancient Treant Guardian, `BossController.cs`)의 기본 구조를 계승하고
> 일반 공격 2종 + 특수 공격 2종으로 확장한 설계.
> **2026-10-02 Unity 적용 완료**(사용자 요청). 에셋·스크립트·프리팹 생성, 플레이어 프리팹에 석화 컴포넌트 추가, Stage2 2-5 보스존 배치 및 임시 씬 플레이 테스트까지 완료(아래 7장).
> 적용 전 원본 패키지(Unity `Assets` 구조 그대로):
> `C:\Users\<현재 사용자명>\Downloads\LHS\컨셉\@Boss_Stage2_Serpent\UnityReady\Assets\`

원본 테마 이미지: `Assets/Sprites/Bosses/Stage2_Transition/05_StoneSerpent_pixel.png`(보스 컨셉 생성 때 최종 채택된 도트 버전)

---

## 1. 컨셉

숲과 동굴의 경계에 사는 거대한 돌 비늘 뱀. 이끼 낀 녹색 돌판 비늘, 크림색 마디 배, 머리·목의 돌가시 볏, 붉은 눈.
몸 아래쪽은 똬리를 튼 채 바닥에 붙어 있고 목을 S자로 세운 자세가 기본. 죽으면 몸 전체가 회색 돌로 굳는다.

| 항목 | 스테이지 1 (트렌트) | 스테이지 2 (돌 뱀) |
|---|---|---|
| 체력(Boss.cs `maxHealth`) | 100 | **130** (권장) |
| 이동 속도 | 2.5 | 2.2 (원거리 패턴이 있어 조금 느리게) |
| 일반 공격 | 양손 주먹질 1종 | **물기 + 꼬리치기 2종** |
| 특수 공격 | 점프 내려찍기 1종 | **독 브레스 + 석화 광선 2종 (번갈아 사용)** |
| 그로기/사망 | 누적 30 / 단일 20 → 3초 그로기(클립은 마지막 자세 1초 유지 후에만 반복), 사망 시 충돌 해제 | 동일 계승 |

## 2. 행동 패턴 (`StoneSerpentController.cs`)

기본 흐름은 트렌트와 같다: 플레이어가 16 이내면 추적(발판 끝에서 정지) → 사거리에 맞는 공격 → 대기.
행동 우선순위: **특수 공격(쿨다운 됐고 거리 2.5 이상) > 일반 공격(쿨다운 됐고 사거리 안) > 추적 > 대기**

| 구분 | 이름 | 사거리 | 피해 | 판정 | 회피 방법 | 클립 / 길이 |
|---|---|---|---|---|---|---|
| 일반 1 | 물기 | 2.4 이내 | 12 | 전방 2.6, 발밑 0.8~2.6 높이 상자 (0.35초에 1회) | 뒤로 빠지기 / 아래(0.8 미만)로는 안 닿음 | `Boss_Bite` 0.7초 |
| 일반 2 | 꼬리치기 | 3.4 이내 | 10 | 전방 3.6, 높이 1.0 낮은 상자 (0.4초에 1회) | **점프** | `Boss_TailSwipe` 0.8초 |
| 특수 1 | 독 브레스 | 7 이내 | 14 × 틱 (1초 간격, 분사 1.2초 → 최대 2틱) ※ 플레이어 피격 무적 1초에 맞춤 | 입 앞 6.5 × 높이 2.2 상자(입보다 1.2 아래 중심) | 사거리 밖으로 도망 / 보스 뒤로 돌아가기 | `Boss_PoisonBreath` 1.9초 |
| 특수 2 | 석화 광선 | 16(감지 거리) 이내, 광선 14 | 12 + **석화 1.5초** | 눈에서 조준 방향으로 두께 0.5 원형 캐스트, 첫 대상 1명. 바닥에 막힘 | 충전(0.8초) 중 조준 고정(발사 0.25초 전) 직후 이동 | `Boss_PetrifyGaze` 1.4초 |

- 일반 공격 선택: 물기 사거리(2.4) 안이면 물기/꼬리치기 50:50, 그보다 멀고 3.4 이내면 꼬리치기. 공용 쿨다운 1.2초.
- 특수 공격 선택: 첫 사용은 전투 시작 4초 후, 이후 7초마다. 독 브레스 ↔ 석화 광선 번갈아 사용. 독 브레스 차례인데 플레이어가 7보다 멀면 석화 광선으로 대체.
- 석화 광선 조준: 바라보는 방향 기준 위아래 최대 35°까지만 꺾임(발판 위/아래의 플레이어도 노림, 뒤쪽은 안 쏨).
- 그로기/사망 시 진행 중이던 공격, 이펙트(브레스/광선), 남은 애니메이션 트리거를 모두 정리한다.

### 석화 상태이상 (`IPetrifiable` / `PlayerPetrify.cs`)
- 광선이 맞은 대상에 `IPetrifiable`이 있으면 `Petrify(1.5초)` 호출(공격 측은 플레이어 타입을 모름 - `IDamageable`과 같은 방식).
- `PlayerPetrify`: 석화 동안 `PlayerMovement`/`PlayerCombat`/`PlayerDash`를 꺼서 이동·공격·대시 불가, 좌우 속도 0 고정(중력 낙하는 유지), 스프라이트 회색. 피해는 그대로 받음(무적 아님). 기존 플레이어 스크립트는 **수정하지 않음**.
- 시작/해제를 `OnPetrifyChanged` 이벤트로 알림(UI·사운드 연결용, 규칙 36).

## 3. 애니메이션

### 스프라이트 프레임 (18장 + VFX 2장)
워크플로우(`gpt-image-1.5-medium` EditImage)로 생성. 원본 컨셉 → 옆모습 기준 프레임(01) 생성 → 01을 소스로 나머지 17장을 병렬 생성(규칙 13).
모든 프레임은 **같은 캔버스 1432×1198 px**에, 똬리 바닥 중심을 같은 피벗 **(0.5, 0.006678)** 에 맞춰 정렬했다(프레임 전환 시 몸이 미끄러지지 않음). 모두 오른쪽을 보는 옆모습.
임포트 설정은 트렌트와 동일: Sprite(Single), PPU 100, Filter **Point**, 압축 없음, Custom 피벗.

| 번호 | 파일 | 용도 |
|---|---|---|
| 01–02 | `01_idle_1`, `02_idle_2` | 대기(숨쉬기, 혀 날름) |
| 03–04 | `03_move_1`, `04_move_2` | 이동(똬리를 앞으로 밀었다 당기며 미끄러짐) |
| 05–06 | `05_groggy_1`, `06_groggy_2` | 그로기(목이 축 늘어지고 머리가 흔들림) |
| 07–08 | `07_bite_windup`, `08_bite_strike` | 물기: 목을 움츠림 → 앞으로 내지름 |
| 09–10 | `09_tail_windup`, `10_tail_swipe` | 꼬리치기: 가시 돌뭉치 꼬리를 뒤로 치켜듦 → 앞쪽 바닥을 쓸어냄 |
| 11–12 | `11_breath_charge`, `12_breath_fire` | 독 브레스: 머리를 젖히고 입에 독이 차오름 → 앞으로 뿜기 시작 |
| 13–14 | `13_gaze_charge`, `14_gaze_fire` | 석화 광선: 두 눈에 황금빛이 모임 → 눈에서 섬광 발사 |
| 15–18 | `15_death_1` ~ `18_death_4` | 사망: 치명타에 비명 → 힘 빠짐 → 머리가 바닥에 쓰러짐 → 전신이 회색 돌로 굳음 |
| VFX | `VFX/vfx_poison_breath` (1323×550) | 독 가스 줄기. 피벗 왼쪽 끝 중앙(0, 0.5) → 입 위치에 두면 앞으로 뻗음 |
| VFX | `VFX/vfx_petrify_beam` (1428×140) | 석화 광선. 피벗 왼쪽 끝 중앙(0, 0.5), 스크립트가 길이/각도에 맞춰 늘림·회전 |

### 클립 (60 샘플레이트, 스프라이트 교체 커브)

| 클립 | 키 프레임(초) | 길이 | 루프 |
|---|---|---|---|
| `Boss_Idle` | 01@0 → 02@0.5 | 1.0 | O |
| `Boss_Walk` | 03@0 → 04@0.25 | 0.5 | O |
| `Boss_Groggy` | 05@0 → 06@0.3 | 0.6 | O |
| `Boss_Bite` | 07@0 → **08@0.35(타격)** | 0.7 | X |
| `Boss_TailSwipe` | 09@0 → **10@0.4(타격)** | 0.8 | X |
| `Boss_PoisonBreath` | 11@0 → **12@0.5(분사 시작, 1.2초 지속)** | 1.9 | X |
| `Boss_PetrifyGaze` | 13@0 → **14@0.8(발사)** | 1.4 | X |
| `Boss_Death` | 15@0 → 16@0.25 → 17@0.5 → 18@0.8 (마지막 프레임 유지) | 1.1 | X |

스크립트의 타격 시각(`biteHitTime` 등)과 클립 키 시각이 일치하도록 맞춰 두었다. 클립을 바꾸면 스크립트 값도 같이 바꿀 것.

### Animator Controller `Boss_Stage2_StoneSerpentAnimator`
트렌트 컨트롤러와 같은 구조.

| 파라미터 | 타입 | 전환 |
|---|---|---|
| `IsWalking` | Bool | Idle ⇄ Walk |
| `IsGroggy` | Bool | AnyState → Groggy (true), Groggy → Idle (false) |
| `Bite` / `TailSwipe` / `PoisonBreath` / `PetrifyGaze` | Trigger | AnyState → 각 공격 상태, 클립 끝(Exit Time 1)에서 → Idle |
| `Die` | Trigger | AnyState → Death (Death에서 나가는 전환 없음) |

모든 전환 Duration 0(도트 스프라이트라 블렌딩 없음), 기본 상태 Idle.

## 4. Unity 적용 방법 (요청 시 진행 - 규칙 9·11)

1. `UnityReady\Assets\` 아래 폴더를 프로젝트 `Assets\`에 그대로 복사(.meta 포함 → GUID가 이미 연결되어 있어 클립·컨트롤러 참조가 바로 유지됨).
   - `Assets/Sprites/Bosses/Stage2_Transition/StoneSerpent/` (프레임 18 + VFX 2)
   - `Assets/Animations/Boss/Stage2_StoneSerpent/` (클립 8 + 컨트롤러)
   - `Assets/@Scripts/Boss/StoneSerpentController.cs`, `IPetrifiable.cs`, `Assets/@Scripts/Player/PlayerPetrify.cs` (규칙 44)
2. 프리팹 `Boss_Stage2_StoneSerpent`를 `@Prefab`에 생성(규칙 23, 트렌트 프리팹과 같은 구성):

```
Boss_Stage2_StoneSerpent  (Layer: 트렌트와 동일 9)
├─ Rigidbody2D      Mass 50, Gravity 3, Freeze Rotation Z, Interpolate
├─ BoxCollider2D    Size (2.2, 2.8), Offset (0, 1.4)
├─ Boss             maxHealth 130
├─ StoneSerpentController
│    animator = Visual의 Animator / visual = Visual
│    targetLayer = Default / groundLayer = Ground
│    breathVfx = Visual/BreathVfx / gazeBeamVfx = GazeBeamVfx
├─ Visual           Scale (0.35, 0.35, 1)  SpriteRenderer(01_idle_1, Order 5) + Animator(Boss_Stage2_StoneSerpentAnimator)
│   └─ BreathVfx    LocalPos (3.29, 5.71), Rot Z -18, Scale (1.4, 1.15)  SpriteRenderer(vfx_poison_breath, Order 6, 비활성)
│                   ※ Visual의 자식이라 좌우 반전을 같이 따라감
└─ GazeBeamVfx      SpriteRenderer(vfx_petrify_beam, Order 6, 비활성)
                    ※ Visual의 형제. 위치/회전/길이는 스크립트가 매번 설정(좌우 반전 영향 없음)
```

   - Visual 스케일 0.35 기준 보스 키 약 3.3 유닛(트렌트 약 3.5와 비슷). `mouthOffset (1.15, 2.0)`, `eyeOffset (0.6, 3.0)`도 이 스케일에서 실측한 값 - 스케일을 바꾸면 같은 비율로 조정. 선택 시 씬 뷰 기즈모(빨강 물기 / 주황 꼬리 / 초록 브레스 / 노랑 눈·광선)로 확인 가능.
3. 플레이어 프리팹 루트에 `PlayerPetrify` 추가, `tintTarget`에 플레이어 SpriteRenderer 지정(없으면 석화는 피해만 들어감).
4. Stage2 2-5 보스존에 배치하고 `BossZoneEndPoint`와 연결(1-5와 동일 - `Boss.OnBossDefeated` 정적 이벤트로 자동 연결).
5. 테스트는 임시 씬 `Test00X`에서 진행 후 삭제(규칙 24).

## 5. 검증 / 남은 확인 사항

- 스크립트 3종: 프로젝트의 `Assembly-CSharp.csproj` 참조(UnityEngine 등)로 임시 컴파일 검사 → **오류 0**. 경고는 `OverlapBoxNonAlloc` 폐기 예정 경고뿐(기존 `BossController.cs`와 같은 방식이라 통일 유지).
- 프레임 정렬: 01번 프레임을 반투명으로 겹친 검수 시트로 18장 모두 똬리 위치가 피벗에 맞는 것을 확인(`preview_sheet.png`).
- Unity 플레이 테스트 결과는 7장 참고. 아래는 실제 화면에서 계속 지켜볼 항목:
  - 08(물기)은 머리를 앞으로 내미느라 생성 단계에서 몸이 다른 프레임보다 약간 작게 그려졌다 - 화면에서 튀어 보이면 해당 프레임만 재생성 또는 스케일 보정.
  - 03↔04(이동) 루프, 15→16→17(사망) 사이의 자세 변화가 자연스러운지.
  - 독 브레스 VFX 위치/크기(입에서 시작하는지), 석화 광선 각도.

## 6. 생성 기록 (워크플로우)

- 기준 프레임: 1차(painterly 스타일·갈색 눈으로 원본과 달라 폐기) → 2차 2장 중 원본 돌 비늘·붉은 눈이 살아난 쪽(v2a) 채택.
- 나머지 17장 + VFX 2장 병렬 생성 → 전부 성공. 검수에서 2장 재생성(규칙 21):
  - `10_tail_swipe`: 꼬리가 몸 뒤쪽으로 휘둘러져 전방 판정과 불일치 → 전방 바닥 쓸기로 재생성
  - `03_move_1`: 똬리가 완전히 풀려 `04`와 형태 차이가 커 이동 루프가 튐 → 똬리 유지한 채 앞으로 기운 자세로 재생성
- 원본/후보 이미지: `@Boss_Stage2_Serpent\_work\` (`raw` 원본, `retry` 재생성 후보, `frames_src` 채택본, `norm2` 정렬본)
- 사용 크레딧: 약 520 (이미지 1장 20 × 26장: 기준 프레임 3 + 본 생성 19 + 재생성 후보 4)
- 워크플로우 노드: x 42000~43800 구역(ImageInput `fe9f8464…`이 기준 프레임 v2a). 재생성 시 해당 TextInput 프롬프트만 수정해 재사용.

## 7. Unity 적용 결과 (2026-10-02)

### 생성/변경한 에셋
| 구분 | 경로 |
|---|---|
| 스프라이트 18 + VFX 2 | `Assets/Sprites/Bosses/Stage2_Transition/StoneSerpent/` (+ `VFX/`) |
| 클립 8 + 컨트롤러 | `Assets/Animations/Boss/Stage2_StoneSerpent/` |
| 스크립트 | `Assets/@Scripts/Boss/StoneSerpentController.cs`, `IPetrifiable.cs`, `Assets/@Scripts/Player/PlayerPetrify.cs` |
| 보스 프리팹(신규) | `Assets/@ExternalAssets/@Prefab/Boss_Stage2_StoneSerpent.prefab` (트렌트 프리팹과 같은 위치·구성, Layer 9, Rigidbody2D 값은 트렌트에서 복사) |
| 플레이어 프리팹(변경) | `Player_Warrior.prefab`에 `PlayerPetrify` 추가(`tintTarget` = Visual) — Stage1/2가 모두 이 프리팹 사용 |
| 씬(변경) | `Stage2.unity` Section_5(2-5) |

### 2-5 보스존 배치 (Section_5 위치 (228, -60), 1-5와 같은 구조)
기존 2-5는 임시 상태(길이 2 발판 1개 + `Boss` 컴포넌트만 붙은 빈 오브젝트 `Boss_2-5`)였음 → 빈 보스는 삭제하고 아래처럼 구성.

| 오브젝트 | 위치 | 비고 |
|---|---|---|
| Platform_Stage2_Jungle_2-5 | (228, -60.5), 스케일 (14, 0.5) | 기존 발판 인스턴스를 길이 28 직선 발판 1개로 늘림(규칙 13). 충돌 X 214~242, 윗면 y = -60 |
| ZoneStartPoint_2-5 (★) | (217, -59) | 왼쪽 끝 |
| Boss_2-5 | (236, -59.98) | `Boss_Stage2_StoneSerpent` 프리팹, 체력 130 |
| BossZoneEndPoint_2-5 (○) | (240.5, -59), 트리거 1×2 | 처치 전 비활성 |
| ArenaWall_Left/Right_2-5 | x = 213.5 / 242.5, 1×12 | 보이지 않는 벽 |
| OutOfBoundsTrigger_2-5 | (228, -67.5), 40×2 | 2-5 전용 낙사 트리거(공용 트리거 y=-20은 2-5를 덮지 못함) |
| Section_5 CameraBoundsZone | size (20,15) → **(30,15)** | 아레나 폭에 맞춤 |

### 플레이 테스트 (임시 씬 `Test000` = Stage2 복사본, `WarpManager.testStartZone = "2-5"`, 테스트 후 삭제 - 규칙 24)
- 2-5 스폰 → 거리 19(감지 밖)에서 대기 → 접근 시 추적, 플레이어 쪽으로 좌우 반전 정상.
- 근접: 물기/꼬리치기가 무작위로 번갈아 나오고, 타격 시점이 클립 프레임 전환(0.35초/0.4초)과 일치. 실제 피해 7/5(플레이어 방어력 5 반영).
- 거리 6: 독 브레스(충전 0.5초 → 분사 1.2초, 2회 명중 9+9) → 7초 뒤 석화 광선(플레이어 쪽 아래 25° 조준, 명중 시 피해 + 석화 → 1.5초 후 해제).
- 피해 25 → 즉시 그로기(진행 중이던 브레스 이펙트 꺼짐) → 3초 후 회복.
- 처치 → 사망 애니메이션 후 회색 돌 모습으로 정지, 몸 충돌 해제, 종료점 활성화 → 종료점 통과 → 스테이지 클리어 → **Stage3 로드**까지 확인. 콘솔 에러/경고 0.

### 테스트 중 수정한 점
1. **독 브레스 피해가 거의 안 들어감**: 플레이어에게 피격 후 1초 무적 + 방어력 5가 있어, 0.3초 간격 4피해 틱은 1회·1피해만 적용됨 → 틱 간격을 무적 시간과 같은 1초로, 틱 피해를 14로 조정(분사 중 최대 2회).
2. **석화 광선 피해 8 → 12**: 방어력 5를 빼면 3밖에 안 들어가서 상향(실피해 7).
3. **독 구름이 플레이어 머리 위로 떠 보임**(판정은 바닥 높이): BreathVfx를 앞쪽 아래로 18° 기울여 판정 범위와 화면을 일치시킴.