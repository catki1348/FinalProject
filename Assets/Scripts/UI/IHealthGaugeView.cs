// 체력 게이지의 시각적 표현을 담당하는 컴포넌트가 구현하는 인터페이스
// 구현체를 교체하는 것만으로 게이지의 에셋/연출을 바꿀 수 있도록 갱신 로직과 분리
public interface IHealthGaugeView
{
    void SetRatio(float ratio); // 0~1, 현재체력/최대체력
    void SetTempHealth(float tempHealth, float remainingSeconds); // 완벽 방어 임시 체력(0이면 표시 끔)
}
