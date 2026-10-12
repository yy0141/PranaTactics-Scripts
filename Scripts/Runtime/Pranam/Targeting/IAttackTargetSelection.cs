using System.Collections.Generic;

using PranaTactics.Unit;

namespace PranaTactics.Pranam.Targeting
{
    public interface IAttackTargetSelection
    {
        void SelectTargets(
            IReadOnlyList<AttackTargetCandidate> candidates,
            ICollection<UnitBehavior> selectedTargets);
    }
}
