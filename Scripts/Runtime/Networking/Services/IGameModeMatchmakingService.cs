namespace PranaTactics.Networking
{
    public interface IGameModeMatchmakingService
    {
        /// <summary>
        /// 指定ルールと人数構成を共有する試合の参加または作成を要求します。
        /// </summary>
        void JoinModeRoom(int partyCount, int partySize, string gameModeId);
    }
}
