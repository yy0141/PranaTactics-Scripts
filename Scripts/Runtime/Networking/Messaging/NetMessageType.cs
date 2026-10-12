namespace PranaTactics.Networking
{
    public enum NetMessageType
    {
        None = 0,

        Ping = 1,

        Pong = 2,

        GameStart = 3,

        TeamAssigned = 4,

        CharacterSelected = 5,

        MoveRouteRequest = 6,

        MoveRouteConfirmed = 7,

        FireRequest = 8,

        PranamSpawned = 9,

        DamageApplied = 10,

        PhaseChanged = 11,

        GameFinished = 12,

        BoardTrap = 13
    }
}
