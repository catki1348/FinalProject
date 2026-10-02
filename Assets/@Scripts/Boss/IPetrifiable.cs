// 석화(일정 시간 행동 불가) 상태이상을 받을 수 있는 대상이 구현하는 인터페이스.
// 공격 측(StoneSerpentController의 석화 광선)이 플레이어의 구체 타입을 몰라도 되도록 IDamageable처럼 분리한다.
public interface IPetrifiable
{
    // duration초 동안 석화. 이미 석화 중이면 남은 시간을 더 긴 쪽으로 갱신한다
    void Petrify(float duration);
}
