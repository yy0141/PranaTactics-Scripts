namespace PranaTactics.Unit.Movement
{
    public interface IMicroMovementParticipant
    {
        int MovementPlayerId { get; }
        long OccupiedCell { get; }
        long DestinationCell { get; }
        bool IsMovementAlive { get; }
        bool IsAdvancing { get; }
        float SecondsToArrival { get; }
        void AdvanceMovement(float seconds);
        void CompleteMovement();
        void BlockMovement();
    }
}
