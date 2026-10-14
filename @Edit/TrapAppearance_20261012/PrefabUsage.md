# 스테이지 테마 함정 프리팹

## 저장 위치

`Assets/@ExternalAssets/@Prefab/Trap/Themed/` 아래 Stage1_Forest, Stage2_Jungle, Stage3_Lava, Stage4_DemonCave, Stage5_DemonCastle 폴더. 각 폴더에 4개씩 총 20개.

| 프리팹 | 연결된 기능 | 배치 시 설정 |
|---|---|---|
| P_Trap_Switch_* | SwitchTrigger, 트리거 BoxCollider2D | 발판 위에 놓고 연결할 함정에서 참조 |
| P_Trap_Projectile_* | TrapProjectile, 기존 Rigidbody2D/충돌/피해/수명 설정 | 직접 놓기보다 발사기로 생성 |
| P_Trap_Launcher_* | ProjectileLauncher + LauncherAppearance, 수직 시점 이미지, 테마 Projectile | 외부 스위치를 switchTrigger에 연결. 기본 +X 발사, 루트/FirePoint가 총구 위치 |
| P_Trap_Arrow_* | Switch와 Launcher 중첩 프리팹, 내부 연결 완료 | 바로 배치 가능. 자식 Switch/Launcher의 위치를 조절 |
| P_Trap_Teleport_* | SwitchTrigger + SwitchTeleportTrap, 스위치 그림 + TeleportRune 그림 | startPoint 또는 customDestination, cameraFollow, destinationZone 지정 |

발사기는 0.5초 경고 후 발사, 스위치는 기존 3초 쿨다운을 유지한다. 텔레포트는 즉시 발동하며 기본 penaltyDamage=0이다. 효과음은 기존처럼 비어 있다. 텔레포트 룬 자식은 외관이고 접촉 판정은 루트 스위치에만 있다.

## 스프라이트와 배경

원본 PNG는 `Assets/Sprites/Traps/Themed/`에 보관하고, 사분면별 경계로 분리한 Sprite 자산 20개를 연결했다. 원본을 변형하지 않으며 `M_TrapWhiteKey` / `WhiteKeySprite.shader`가 흰 배경을 렌더링 때 투명 처리한다. 원본 그대로라 일부 외관은 사선 시점이다. 기본 Sprite 재질로 변경하면 흰 배경이 다시 보인다. Point 필터, mipmap 없음, 무압축 설정.

## 검증

- Unity 에디터에서 프리팹 20개 저장 확인.
- 모든 활성 SpriteRenderer의 Sprite/Material 연결 확인.
- 5개 발사기의 switchTrigger/projectilePrefab/firePoint/warningTarget 연결 확인, +X 방향 확인.
- 5개 텔레포트의 자기 SwitchTrigger 연결 확인, 스위치 트리거 확인.
- C# 콘솔 오류 없음, 셰이더 컴파일 오류 없음.
- 임시 Test982_ThemedTraps 씬에서 앞/뒤/좌/우/위 렌더 확인. 앞/뒤 외관과 배경 투명 처리 정상. 2D 평면이라 좌/우/위에서는 외관이 보이지 않는 것이 정상.
- 확인용 이미지 및 임시 씬 삭제. 실제 Stage 씬은 변경하지 않음.
- Play 모드의 피해/쿨다운/텔레포트 이동은 이번 작업에서 별도로 실행하지 않았다. 기존 동작 스크립트는 변경하지 않았다.

제작 코드: `Assets/@Scripts/Editor/BuildThemedTraps.cs`. 원본 프리팹을 읽어 새 프리팹을 생성하며, 동일한 출력 자산이 있으면 덮어쓰지 않고 중단한다.
