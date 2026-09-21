// 몬스터 패턴 스크립트가 구현해야 하는 최소 인터페이스
// MonsterController가 구체적인 패턴 타입을 몰라도 패턴을 실행할 수 있도록 분리한다(Monster.md 규칙 2, 4)
public interface IMonsterPattern
{
    // 패턴이 매 프레임 수행할 행동. MonsterController.Update()가 몬스터가 살아있는 동안만 호출한다.
    void UpdatePattern();
}
