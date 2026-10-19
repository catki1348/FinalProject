<!-- AI work record: Claude Sonnet 5.5, 2026-10-19. ToDo check of TODO_Progress_20261018.md against scene YAML, scripts and git. File based only: Unity MCP was disconnected, so nothing was compiled, opened or played. Overlay on the 10-18 document. -->
# 진척 현황 및 ToDo — 2026-10-19 (10-18 문서 점검)

[TODO_Progress_20261018.md](TODO_Progress_20261018.md)를 씬 YAML·스크립트·Git과 대조했다. **이 문서는 10-18 문서의 보정이다. 아래 보정을 적용한 상태로 10-18 문서의 남은 항목을 따른다.** Unity MCP가 끊겨 있어 컴파일·에디터 확인·재생은 하지 못했다.

## 1. 10-18 문서와 달라진 사실

| 10-18 문서 | 실제(10-19 확인) |
|---|---|
| P0 "Stage3·4는 WarpManager 테스트 시작이 비어 있음" | **5개 씬 모두 설정됨**: Stage1=`1-5`, Stage2=`2-5`, Stage3=`3-4`, Stage4=`4-5`, Stage5=`5-2`. 재생하면 그 영역에서 시작하고 배포 전 반드시 비워야 한다. |
| "OPTIONS·CREDITS 구현(둘 다 로그만)" | **OPTIONS는 구현됨**: `OptionsMenuUI`(배경음/효과음 볼륨 패널)가 `MainMenuManager`에 연결돼 있다. **CREDITS만** 아직 `Debug.Log`뿐이다. |
| 시계 NullReference "원인 미확정" | 씬 YAML 대조 결과 `PlatformCycleManager`의 시계 참조 6개(피해 3·점멸 3)는 모두 프리팹의 `DamageFoothold`/`BlinkingFoothold`를 가리키고 `externallyControlled=1`이다. 구조 문제는 아니다. **방어 코드를 추가했다**(아래 2절). |
| 테스트 씬·스크린샷 정리 | 확인: `Assets/Scenes`에 Test 씬 없음, `Assets/Screenshots` 없음. |
| BGM·환경음 연결 | 확인: Stage1~5 `stageBgm`·`stageAmbience` 모두 연결됨(fileID 8300000). |
| 보스5 구현 | 확인: `Stage5BossController`와 `DeployStage55.cs` 존재. 처치·엔딩 연결 실확인은 아직 전. |

## 2. 이번에 한 일

- `PlatformCycleManager`: 시계 목록의 항목이 null이어도 건너뛰고 첫 1회만 경고 로그를 남기도록 했다(`Apply` 세 루프, `WarnMissing`). 이전에는 null 하나가 시계 전체를 멈추는 NullReference였다. **컴파일은 확인하지 못했다**(Unity 연결 끊김). Unity가 연결되면 컴파일 오류와 5-1 재생 시 경고 로그 여부를 먼저 확인한다. 경고가 뜨면 원인은 참조 소실이므로 `Tools/Deploy/Stage 5-1 Five Segments`로 재배치한다.

## 3. 현재 상태

- 마지막 커밋 `261019`(10-19 00:50). 미커밋은 3건: `MalgunGothic SDF.asset`, `Stage4.unity`, `@Edit/Presentation_GameMaking.pptx`(신규). 커밋은 사용자 몫이다.
- 미구현 영역은 없다. 설계가 없는 영역도 없다.

## 4. 남은 항목 (10-18 문서 2~6절 기준, 위 보정 반영)

**P0**
- [ ] WarpManager 테스트 시작 5곳 비우기(위 표).
- [ ] 5-1 `PlatformCycleManager` 재생 확인(경고 로그·시계 동작). 낙사 복귀·공통 시계 연결 영향 확인.
- [ ] Stage5 새 타일: 플레이 중 스파이크 위치·플레이어가 몸체 윗면에 서는 모습 확인.

**P1 구현**
- [ ] **CREDITS** 구현(OPTIONS는 완료).
- [ ] 보스5: 그로기·사망 모션·VFX·음원·체력바·피격 넉백·테스트 판정 상자 제거·처치 후 종료점/엔딩 전환 실확인.
- [ ] Stage4 보스 전용 Idle/Walk/Groggy/Death, 최종 VFX·음원, 돌진 회피 높이(점프 1.39 vs 접촉 2.0) 확정.
- [ ] Stage3 보스 7패턴, Stage1·2 보스(2-5는 새 보스존) 전투·그로기·사망·종료점 검증.
- [ ] 오디오 실제 재생·볼륨·반복 검수, 점프음, 보스1·2 효과음.
- [ ] 무장해제 플레이어 나머지 행동, 방어 모션 이미지, 완벽 방어 이펙트 플레이 검증.
- [ ] 엔딩 최종 연출, 플레이어 슬라이드(경사) 상태 문서화.
- [ ] 5-2 미확정 질문(영역 1개 확정, P1 B구간 속도, 소요 시간).
- [ ] 이동형 발판 낙사 복귀, 영역 경계·4-4 질주·보스전 중 낙사 복귀 시나리오.

**플레이 검증(직접 조작)**
- [ ] 1-1, 1-4, 2-1, 2-3, 2-4, 3-1·3-3, 4-1~4-4, 5-1~5-4 완주와 영역 전환. 5-1은 WarpManager를 비운 뒤 시작.
- [ ] 5-3·5-4 이동형 승하차, 편도 이동형 첫 탑승 출발, 낙하 복귀, 화살·텔레포트 회피, 5-3→5-4→5-5 전환.
- [ ] 보스전 전체, 시작→엔딩 전체 통과.
- [ ] 배경: Stage2·3 영역 전환 플레이 확인, 해상도 한계(1672×941).

**정리**
- [ ] 맵 수칙 재검증(영역 겹침, 이동형 거리·시차, 대기 시간, Tiled 외형·콜라이더).
- [ ] 다른 PC 전달: `Assets/@ExternalAssets`는 Git 제외라 OneDrive/별도 백업 확인.

## 5. 주의

- 이 PC와 다른 PC가 OneDrive로 같은 프로젝트를 공유한다. 파일 수정 시각이 10-19 10:15로 일괄 갱신된 파일이 많은 것은 동기화·체크아웃 때문으로 보이며 내용 변경과 구분해서 봐야 한다.
- Unity MCP 연결이 끊긴 상태다(`unityMCP` 연결 종료). 에디터 작업이 필요하면 연결을 복구한 뒤 진행한다.
