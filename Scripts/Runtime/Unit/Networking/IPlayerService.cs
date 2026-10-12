namespace PranaTactics.Unit.Networking
{
    public interface IPlayerService
    {
        int LocalPlayerId
        {
            get;
        }

        int GetTeamId(int playerId);

        string GetCharacterName(int playerId);
    }
}
