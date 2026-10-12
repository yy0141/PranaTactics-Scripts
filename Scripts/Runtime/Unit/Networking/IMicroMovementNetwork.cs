using PranaTactics.Unit.Movement;

namespace PranaTactics.Unit.Networking
{
    public interface IMicroMovementNetwork
    {
        void PublishMicroMovement(MicroMovementSnapshot snapshot);
    }
}
