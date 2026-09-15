// 피해를 받을 수 있는 대상이 구현하는 최소 인터페이스(공격 측이 구체 타입을 몰라도 되도록 분리)
public interface IDamageable
{
    void TakeDamage(int amount);
}
