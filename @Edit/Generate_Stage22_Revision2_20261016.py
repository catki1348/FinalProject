"""Codex (GPT-6), 2026-10-16. Design-only: explicit 80% jump and safe disappearing-chain timing.

Reads scene YAML as text for placement evidence; never opens Unity or writes game assets.
"""
import copy
import hashlib
import importlib.util
import json
import math
import re
from pathlib import Path
import numpy as np

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
STEM = 'Stage22_Revision2_20261016'
ORDER = 'N1 N2 N3 MP1 N4 MP2 N5 MP3 N6 MP4 N7 MP5 N8 D1 D2 D3 N9 N10'.split()


def load_module(name, filename):
    """Reuse pure coordinate helpers without running the historical generators."""
    spec = importlib.util.spec_from_file_location(name, HERE/filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def saved_scene_evidence(previous):
    """Resolve direct Section_2 prefab-instance overrides and compare them to the old plan."""
    scene = ROOT/'Assets/Scenes/Stage2.unity'
    raw = scene.read_bytes()
    text = raw.decode('utf-8-sig')
    docs = {int(m.group(2)): (int(m.group(1)), m.group(3)) for m in
            re.finditer(r'^--- !u!(\d+) &(\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)', text, re.M | re.S)}
    section_go = next(k for k, (typ, body) in docs.items()
                      if typ == 1 and re.search(r'^  m_Name: Section_2$', body, re.M))
    section_transform = next(k for k, (typ, body) in docs.items() if typ == 4 and
                             re.search(rf'^  m_GameObject: \{{fileID: {section_go}\}}$', body, re.M))
    def vec(body, key):
        m = re.search(rf'^  {key}: \{{x: ([^,]+), y: ([^,]+), z: ([^}}]+)\}}', body, re.M)
        return [float(v) for v in m.groups()]
    parent_body = docs[section_transform][1]
    parent_x, parent_y, _ = vec(parent_body, 'm_LocalPosition')
    assert vec(parent_body, 'm_LocalScale') == [1, 1, 1]
    found = []
    for _, (typ, body) in docs.items():
        if typ != 1001 or not re.search(rf'^    m_TransformParent: \{{fileID: {section_transform}\}}$', body, re.M):
            continue
        props = re.findall(r'propertyPath: ([^\n]+)\n      value: ([^\n]*)', body)
        values = dict(props)
        name = values.get('m_Name', '')
        if name not in ORDER:
            continue
        found.append(dict(id=name, x=round(parent_x+float(values['m_LocalPosition.x']), 6),
                          centerY=round(parent_y+float(values['m_LocalPosition.y']), 6),
                          prefabGuid=re.search(r'm_SourcePrefab: .*guid: ([a-f0-9]+)', body).group(1)))
    assert len(found) == 18, 'Saved Section_2 does not expose exactly 18 reviewed platforms'
    old = {p['id']: p for p in previous['platforms']}
    for item in found:
        p = old[item['id']]
        item['matches20261015Position'] = abs(item['x']-p['x']) < .002 and abs(item['centerY']-(p['top']-p['height']/2)) < .002
    return dict(scene='Assets/Scenes/Stage2.unity', sha256=hashlib.sha256(raw).hexdigest().upper(),
                method='read_only_serialized_text_no_editor', sectionLocalOrigin=[parent_x, parent_y],
                platforms=found, allPositionsMatchPrevious=all(p['matches20261015Position'] for p in found))


def connections(platforms, player, positions, jump):
    """Sample landing windows with both 80% move speed and 80% jump launch velocity.

    Include the semi-implicit Euler height correction for a 0.02-second fixed step.
    Require 0.15 units of horizontal contact, rather than accepting a grazing pixel.
    """
    starts = np.arange(0, 60, .1)[:, None]
    air = np.arange(0, 1.81, .02)[None, :]
    result = []
    by_id = {p['id']: p for p in platforms}
    for a_id, b_id in zip(ORDER, ORDER[1:]):
        a, b = by_id[a_id], by_id[b_id]
        ax, ay = positions(a, starts)
        bx, by = positions(b, starts+air)
        py = ay+.015+jump*air-.5*player['gravity']*(air*air+.02*air)
        delta = py-by
        mask = np.zeros(len(starts), dtype=bool)
        first = None
        for speed in np.arange(.8, 6.401, .2):
            px = ax+a['width']/2-.08+speed*air
            overlap = np.minimum(px+player['playerWidth']/2, bx+b['width']/2)-np.maximum(px-player['playerWidth']/2, bx-b['width']/2)
            cross = (delta[:, :-1] >= 0) & (delta[:, 1:] <= 0) & (overlap[:, 1:] >= .15) & (air[:, 1:] > .1)
            available = cross.any(axis=1)
            if first is None and available.any():
                i, k = np.argwhere(cross)[0]
                first = dict(startTime=round(float(starts[i, 0]), 4), speed=round(float(speed), 4),
                             flight=round(float(air[0, k+1]), 4), overlap=round(float(overlap[i, k+1]), 4),
                             landingCenter=float(px[i, k+1]), targetLeft=float((bx-b['width']/2)[i, k+1]))
            mask |= available
        best = current = 0
        for v in mask:
            current = 0 if v else current+1
            best = max(best, current)
        result.append(dict(pair=f'{a_id}->{b_id}', reachable=bool(mask.any()),
                           availableStartSamples=int(mask.sum()), totalStartSamples=len(starts),
                           longestUnavailableSampleRun=round(best*.1, 3), witness=first))
    return result


def main():
    previous = json.loads((HERE/'MapDesign_Stage2-2_Revision.json').read_text(encoding='utf-8'))
    player = json.loads((HERE/'Stage22_Audit_20261015.json').read_text(encoding='utf-8'))
    helper = load_module('revision1_helpers', 'Generate_Stage2-2_Revision.py')
    pos = load_module('audit_positions', 'Audit_Stage2-2.py').positions
    evidence = saved_scene_evidence(previous)
    by_id = {p['id']: copy.deepcopy(p) for p in previous['platforms']}
    right = by_id['N8']['x']+by_id['N8']['width']/2
    for i, name in enumerate(['D1', 'D2', 'D3'], 1):
        p = by_id[name]
        p.update(width=2.6, x=round(right+2.4+1.3, 6), top=round(by_id['N8']['top']+.65*i, 6), vanish=.8, respawn=2)
        right = p['x']+p['width']/2
    by_id['N9']['x'] = round(right+2.4+by_id['N9']['width']/2, 6)
    by_id['N9']['top'] = round(by_id['N8']['top']+2.6, 6)
    by_id['N10']['x'] = round(by_id['N9']['x']+(previous['platforms'][-1]['x']-next(p['x'] for p in previous['platforms'] if p['id']=='N9')), 6)
    by_id['N10']['top'] = by_id['N9']['top']
    start = copy.deepcopy(previous['start'])
    goal = dict(x=round(by_id['N10']['x']+.97, 6), y=round(by_id['N10']['top']+.525, 6))
    platforms = [by_id[n] for n in ORDER]
    for p in platforms:
        p['center'] = dict(x=p['x'], y=round(p['top']-p['height']/2, 6))
        p['rendererSize'] = p['colliderSize'] = [p['width'], p['height']]
    baseline = connections(previous['platforms'], player, pos, 7.2)
    pairs = connections(platforms, player, pos, 7.2)
    assert all(p['reachable'] for p in pairs), [p for p in pairs if not p['reachable']]
    chain = ['N8', 'D1', 'D2', 'D3', 'N9']
    skips = []
    for i in range(5):
        for j in range(i+2, 5):
            a, b = by_id[chain[i]], by_id[chain[j]]
            gap = b['x']-b['width']/2-(a['x']+a['width']/2)
            rise = b['top']-a['top']
            bound = helper.dash_reach_upper(rise, player)
            conservative = bound+.85 if bound else 0
            skips.append(dict(pair=f'{a["id"]}->{b["id"]}', gap=round(gap, 6), rise=round(rise, 6),
                              maximumBuffDashConservativeReach=round(conservative, 6),
                              clearance=round(gap-conservative, 6), blocked=gap>conservative))
    assert all(p['blocked'] for p in skips)
    # A conservative post-landing traversal budget includes 0.15 support, an edge launch, and 0.12 input reserve.
    traversal = (2.6+player['playerWidth']/2-.15-.08)/6.4
    timing = dict(worstWalkSeconds=round(traversal, 6), inputReserve=.12, requiredSeconds=round(traversal+.12, 6), vanishDelay=.8)
    assert timing['requiredSeconds'] < .8
    route, distances = helper.canonical_route(by_id, start['x'], goal['x'])
    distance = dict(**distances, totalUnits=round(sum(distances.values()), 6), scale=round(sum(distances.values())/8, 6))
    assert 1 <= distance['scale'] <= 100
    for p in platforms:
        assert p['x']-p['width']/2 >= 50 and p['x']+p['width']/2 <= 172
        assert -8 <= p['top']-p['height'] <= p['top'] <= 8
    second_dash = .2+math.sqrt(2*(9**2/(2*player['gravity'])-1.3)/player['gravity']) < .6
    assert second_dash
    data = dict(schema='stage22_revision2_20261016', status='design_only_not_applied',
                implementationEvidence=None, basedOn='MapDesign_Stage2-2_Revision.json',
                internalAttribution=dict(agent='Codex', model='GPT-6', date='2026-10-16'),
                start=start, goal=goal, camera=dict(x0=50,x1=172,y0=-8,y1=8), downstreamAdditionalShiftX=0,
                bossCenter=dict(x=236.025,y=-60), unitsPerScale=8, platforms=platforms,
                canonicalRoute=route, distance=distance,
                validation=dict(kind='offline_model_not_Unity_physics', moveSpeed=6.4,jumpLaunchVelocity=7.2,
                                integrationStep=.02,departureEdgeInset=.08, minimumHorizontalContact=.15,
                                baselineFailures=[p['pair'] for p in baseline if not p['reachable']],
                                adjacentPairs=pairs,skipPairs=skips,noSecondDashBeforeSkipPlane=second_dash,
                                disappearingTimingBudget=timing,unityPlaytested=False), savedSceneEvidence=evidence)
    (HERE/f'MapDesign_{STEM}.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
    write_doc(data)
    write_svg(data)
    print(json.dumps(dict(baselineFailures=data['validation']['baselineFailures'], adjacentPairs=len(pairs),
                          blockedSkips=len(skips), minimumSkipClearance=min(p['clearance'] for p in skips),
                          timing=timing,distance=distance,currentSceneMatches20261015=evidence['allPositionsMatchPrevious']),ensure_ascii=False))


def write_doc(d):
    """Emit one current specification with explicit design-only status and reproducible coordinates."""
    lines=['# 2-2 최신 수정 설계 — 정글 짐, 2차 수정',
           '<!-- Codex (GPT-6), 2026-10-16. Design-only revision: conservative 80% movement+jump, skip prevention, and disappearing traversal time. -->',
           '', '**상태: 설계 완료 / Unity 미적용.** 이 문서와 같은 날짜의 JSON이 최신 설계 기준이다. 2026-10-15의 적용·검증 기록은 이전 버전의 이력이며 이번 설계의 적용 증거로 사용하지 않는다.',
           '', '## 수정 이유', '',
           '사용자가 최신 설계 미적용 상태를 지적했다. 파일에 남아 있는 이전 완료 표시와 새 설계 적용 상태를 구분한다. 기존 생성기가 과거 검증 메타데이터의 존재만으로 새 산출물까지 적용 완료로 표시하는 방식도 최신 설계에는 사용하지 않는다.',
           '', '이전 검사는 수평 속도만 80%(6.4), 점프 초기 속도는 기본값 9였다. 이번에는 MapEdit 14번의 여유를 보수적으로 해석해 수평 속도 상한 6.4와 점프 초기 속도 7.2를 동시에 적용한다. 이는 기존 수칙 문구를 변경하는 것이 아니라 이 설계의 검증 조건을 명확히 강화한 것이다.',
           '', '## 유지·변경 명세', '',
           '| 항목 | 최신 기준 |', '|---|---|',
           '| 구성 | 일반 10 · 이동형 5 · 소멸형 3, 총 18 |',
           '| 이동형 | 기존 5개 경로·속도 2·시작 지연 0·경유 대기 0 유지 |',
           '| 소멸 구간 틈 | 가장자리 3.8 → **2.4** |',
           '| 소멸 발판 폭 | 1.6 → **2.6** |',
           '| 매 착지면 상승 | 0.4 → **0.65** |',
           '| 소멸 / 복구 대기 | 0.4 → **0.8초** / 2초 유지 |',
           '| 외형 | 부모 포함 Scale 1, Tiled/Continuous, 명시적 동일 크기 콜라이더 |',
           '| 영역 | X=[50,172], Y=[-8,8], 추가 후속 영역 이동 0 |',
           '', '틈을 줄여 낮아진 점프 성능으로도 착지할 수 있게 하고, 폭과 상승량을 함께 늘려 중간 발판 생략을 막는다. 넓어진 발판에서 다음 이륙점까지 이동할 시간을 확보하도록 소멸 지연을 0.8초로 늘린다. 단순히 폭만 늘리고 기존 0.4초를 유지하면 낮은 속도에서 이탈 전에 소멸할 수 있으므로 타이밍도 함께 변경한다.',
           '', '## 배치표', '',
           '전부 월드 좌표이며 Y는 충돌 윗면이다. 중심 Y=윗면−두께/2. 일반·이동 두께 1.05, 소멸 두께 1.0을 유지한다. 이동형 좌표는 첫 경유점이다.', '',
           '| ID | 유형 | 중심 X | 표면 Y | 폭 |', '|---|---|---:|---:|---:|']
    for p in d['platforms']:
        lines.append(f'| {p["id"]} | '+{'normal':'일반','moving':'이동','disappearing':'소멸'}[p['kind']]+f' | {p["x"]:.3f} | {p["top"]:.3f} | {p["width"]:.3f} |')
    lines += ['',f'시작점은 ({d["start"]["x"]:.3f},{d["start"]["y"]:.3f}), 종료 마커는 ({d["goal"]["x"]:.3f},{d["goal"]["y"]:.3f})이다. 종료점의 다음 영역 참조는 2-3으로 유지한다. N9·N10은 이전 수정안보다 X -2.6, 표면 Y +1.0 이동한다.',
              '', '## 이동 경로 및 프리팹', '',
              'MP1 수평 왕복: (84,0.525)↔(90,0.525). MP2 수직 왕복: (96,0.525)↔(96,3.725). MP3 대각 왕복: (102,3.725)↔(108,1.225). MP4 폐순환: (114,1.225)→(118,1.225)→(118,3.725)→시작. MP5 대각 왕복: (124,3.725)↔(131,2.025). 경유점 좌표는 표면 기준이며 실제 경로 중심은 Y−0.525이다.',
              '', '기존 `Assets/@ExternalAssets/@Prefab/Stage2_2/`의 일반·이동·소멸 전용 프리팹을 재사용하는 것으로 설계한다. 소멸 지연은 소멸 전용 프리팹 원본에서 변경하고, 씬에는 크기·배치·외부 경로 참조만 둔다. 이 설계 작업에서는 해당 에셋을 변경하지 않았다. 모든 경로는 발판의 자식이 아닌 외부 오브젝트로 연결한다. 최장 편도 약 7.204유닛, MP4 전체 순환 약 11.217유닛으로 이동 한계 56유닛 이하다. 이동형 사이에 고정 발판이 있어 연속 이동형 쌍에 대한 차등 속도·지연 규칙은 적용 대상이 없다.',
              '', '## 거리·영역', '',
              f'1척도=기본 이동 8유닛이다. 기준 동선은 **{d["distance"]["totalUnits"]:.6f}유닛 / 척도 {d["distance"]["scale"]:.6f}**이다. 고정 발판 걷기·점프 직선 연결·이동형 편도 탑승을 합하며 대기·점프 포물선·복귀 탑승은 제외한다. 세부 꼭짓점과 분해는 JSON에 기록했다.',
              '', '카메라 및 영역 경계는 이전 수정안의 [50,172]를 그대로 예약한다. 2-3=[172,433], 2-4=[433,504], 하단 보스 중심=(236.025,-60)으로 추가 이동은 없다. 이미 이동한 후속 영역에 +12를 다시 더하지 않는다. 이후 적용 시 기존 영역 경계를 먼저 확인하고, 그때도 경계가 이 기준과 다르면 차이만 조정한다.',
              '', '## 계산 검증', '',
              f'- 이전 수정안에서 강화 조건을 통과하지 못한 연결: {", ".join(d["validation"]["baselineFailures"])}.',
              '- 새 배치는 인접 17쌍 모두 도달 가능한 출발 시점이 있다. 수평 상한 6.4, 점프 초기 속도 7.2, 중력 29.43, 물리 스텝 0.02초의 반암시적 적분 높이를 사용했다. 가장자리 안쪽 0.08에서 출발하고 수평 접촉 0.15유닛 이상을 요구했다. 출발 시점 600개와 속도 0.8~6.4를 검사했다.',
              f'- 소멸 구간 생략 6쌍은 최대 가속과 공중 대시 상한을 포함해 차단됐다. 최소 생략 간격 7.4, 최소 보수적 여유 {min(v["clearance"] for v in d["validation"]["skipPairs"]):.6f}유닛이다. 기본 점프 9, 이동 10, 대시 거리 1.125·지속 0.2초와 수직 속도 초기화, 추가 0.85유닛 여유를 반영했다. 다음 대시 쿨다운 전에 착지면을 지나므로 2회 대시는 성립하지 않는다.',
              f'- 소멸 발판의 보수적 지상 이동 시간 {d["validation"]["disappearingTimingBudget"]["worstWalkSeconds"]:.6f}초 + 입력 여유 0.12초 = {d["validation"]["disappearingTimingBudget"]["requiredSeconds"]:.6f}초로 소멸 지연 0.8초 이내다. 이 항목은 실제 연속 플레이 검사가 아닌 이탈 시간 예산이다.',
              '- 발판 수, 카메라 범위 포함, 척도 1~100, 생략 차단, 이탈 시간 예산을 생성 코드에서 assert로 검사했다.',
              f'- 출발 시점 샘플에서 최대 연속 도달 불가 구간은 약 {max(v["longestUnavailableSampleRun"] for v in d["validation"]["adjacentPairs"]):.1f}초다. 경로·속도 보존 조건에서 측정한 값이며 탑승 대기 없이 항상 통과할 수 있다는 뜻은 아니다.',
              '', '**계산 모델 통과를 Unity 적용 완료나 실제 물리 통과로 표시하지 않는다.** 이동형의 모든 출발 위상이 가능한 것은 아니며 고정 발판에서 탑승 시점을 선택한다. 실제 접촉·경사 모서리·점프 입력·대시 프레임 순서와 소멸 구간 연속 완주는 적용 후 임시 Test 씬에서 확인해야 한다.',
              '', '## 현재 저장 파일과 적용 판정', '',
              f'현재 `Stage2.unity`를 Unity 실행 없이 텍스트로 읽어 Section_2의 직접 프리팹 인스턴스 18개를 확인했다. 이전 2026-10-15 설계 좌표와의 일치 여부: **{d["savedSceneEvidence"]["allPositionsMatchPrevious"]}**. 이번 최신 좌표·타이밍은 적용하지 않았다. 관측한 파일 SHA256은 `{d["savedSceneEvidence"]["sha256"]}`이다. 이 파일 검사는 현재 Unity 편집기의 저장되지 않은 상태를 판정하지 않는다.',
              '', '이전 보고서는 그 날짜의 검사 이력으로 보존한다. 최신 적용 완료는 이 JSON의 버전·해시와 실제 씬의 수치 및 검사 기록을 대조한 뒤에만 판정한다. 과거 완료 메타데이터가 있다는 이유만으로 최신 상태를 적용 완료로 자동 변경하지 않는다.',
              '', '기존 `DeployStage22Revision.cs`는 이전 JSON 파일을 읽고 소멸 지연 0.4초를 프리팹 생성 함수에 고정해 두었다. 따라서 기존 배치 메뉴를 다시 실행하는 것만으로 이 최신 설계가 적용되지는 않는다. 후속 구현에서는 이 버전의 JSON을 읽도록 연결하고, 소멸 시간도 JSON의 `vanish`·`respawn`을 사용하도록 고쳐야 한다. 이 작업에서는 배치 코드와 게임 에셋을 수정하지 않았다.',
              '', '## 적용 시 필수 확인', '',
              '1. 18개와 전용 프리팹 원본·외부 경로·Scale/Tiled/콜라이더를 확인한다.',
              '2. 기본 성능 및 수평 6.4·점프 7.2에서 실제 인접 착지를 검사한다.',
              '3. N8→D1→D2→D3→N9를 실제 시간으로 연속 통과하고 0.8초 소멸·2초 복구를 확인한다.',
              '4. 최대 가속·대시의 생략 시도 6쌍과 의도치 않은 낙하 우회를 검사한다.',
              '5. 시작 착지, 종료점의 실제 2-3 전환, 카메라·배경·보스 영역과의 분리를 확인한다.',
              '6. 저장 후 다시 열어 최신 버전 수치를 재검사하고 임시 씬을 삭제한다.',
              '', f'- [정밀 배치·모델·저장 파일 대조 데이터](MapDesign_{STEM}.json)',
              f'- [전체 및 소멸 구간 확대도](MapPreview_{STEM}.svg)',
              f'- [재생성 코드](Generate_{STEM}.py)']
    (HERE/f'MapDesign_{STEM}.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')


def write_svg(d):
    """Generate a native SVG diagram, distinctly marked as a non-applied design."""
    colors={'normal':'#80c99c','moving':'#78b9ff','disappearing':'#f4b65f'}
    out=['<svg xmlns="http://www.w3.org/2000/svg" width="1440" height="740" viewBox="0 0 1440 740">',
         '<!-- Codex (GPT-6), 2026-10-16. Offline design diagram, not a Unity screenshot. -->',
         '<rect width="1440" height="740" fill="#111b26"/>',
         '<style>text{font-family:Segoe UI,Malgun Gothic,sans-serif;fill:#e8f0f8}</style>',
         '<text x="40" y="45" font-size="27" font-weight="700">2-2 최신 수정 설계 · 정글 짐</text>',
         '<text x="40" y="78" font-size="18">설계 완료 / Unity 미적용 · 일반 10 / 이동 5 / 소멸 3 · 수평 6.4 + 점프 7.2 모델 검사</text>']
    for inset in [False,True]:
        sx,x0,base=(12.5,68,275) if not inset else (39,132,630)
        plats=d['platforms'] if not inset else [p for p in d['platforms'] if p['id'] in ['N8','D1','D2','D3','N9','N10']]
        if inset:
            out += ['<line x1="40" y1="345" x2="1400" y2="345" stroke="#314359"/>',
                    '<text x="40" y="390" font-size="23">소멸 구간 · 틈 2.4 / 폭 2.6 / 상승 0.65 / 소멸 0.8초</text>']
        for p in plats:
            x=40+(p['x']-x0)*sx;y=base-p['top']*(22 if not inset else 30)
            if p['points']:
                points=' '.join(f'{40+(q["x"]-x0)*sx:.2f},{base-q["y"]*22:.2f}' for q in p['points'])
                if p['loop']==0:
                    q=p['points'][0];points+=f' {40+(q["x"]-x0)*sx:.2f},{base-q["y"]*22:.2f}'
                out.append(f'<polyline points="{points}" fill="none" stroke="#78b9ff" stroke-dasharray="5 4" stroke-width="2"/>')
            out.append(f'<rect x="{x-p["width"]*sx/2:.2f}" y="{y:.2f}" width="{p["width"]*sx:.2f}" height="{p["height"]*14:.2f}" rx="2" fill="{colors[p["kind"]]}"/>')
            out.append(f'<text x="{x:.2f}" y="{y-10:.2f}" text-anchor="middle" font-size="16">{p["id"]}</text>')
        if inset:
            for a,b in zip(plats[:4],plats[1:5]):
                left=40+(a['x']+a['width']/2-x0)*sx;right=40+(b['x']-b['width']/2-x0)*sx;y=base-min(a['top'],b['top'])*30+35
                out.append(f'<path d="M {left:.2f} {y:.2f} H {right:.2f}" stroke="#b4c4d7"/>')
                out.append(f'<text x="{(left+right)/2:.2f}" y="{y+20:.2f}" text-anchor="middle" font-size="16">2.4</text>')
    out += [f'<text x="40" y="691" font-size="19">기준 동선 {d["distance"]["totalUnits"]:.3f}유닛 = 척도 {d["distance"]["scale"]:.3f} · 인접 17쌍 / 생략 차단 6쌍 모델 통과</text>',
            '<text x="40" y="724" font-size="16">실제 물리·연속 통과는 적용 후 검증 · 기존 적용 기록은 2026-10-15 버전의 이력</text>','</svg>']
    (HERE/f'MapPreview_{STEM}.svg').write_text('\n'.join(out),encoding='utf-8')


if __name__=='__main__':
    main()
