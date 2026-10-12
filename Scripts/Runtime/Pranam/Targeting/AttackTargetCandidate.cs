using PranaTactics.Unit;

namespace PranaTactics.Pranam.Targeting
{
    public readonly struct AttackTargetCandidate
    {
        public UnitBehavior Unit { get; }

        public float SquaredDistance { get; }

        public AttackTargetCandidate(UnitBehavior unit, float squaredDistance)
        {
            Unit = unit;
            SquaredDistance = squaredDistance;
        }
    }
}
