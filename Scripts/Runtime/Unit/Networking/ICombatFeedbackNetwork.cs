using PranaTactics.Combat;

namespace PranaTactics.Unit.Networking
{
    public interface ICombatFeedbackNetwork
    {
        void PublishCombatFeedback(CombatFeedbackResult result);
    }
}
