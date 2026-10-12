using UnityEngine;

using PranaTactics.GameFlow;
using PranaTactics.Match.Modes;
using PranaTactics.Networking;
using PranaTactics.Settings;

namespace PranaTactics.UI.Screens.GamePlayMode
{
    /// <summary>
    /// プレイモードの接続確認と選択状態の保存を共有します。
    /// </summary>
    public static class PlayModeSelection
    {
        public static bool IsSoloPlay => GameFlowManager.Instance != null
            ? GameFlowManager.Instance.IsSoloPlay
            : SettingsManager.Current.General.IsSoloPlay;

        public static bool CanPlayOnline =>
            !GameBuildSettings.IsSoloOnly &&
            Application.internetReachability != NetworkReachability.NotReachable &&
            NetworkServiceLocator.Online != null && NetworkServiceLocator.Online.IsConnected;

        public static PlayCategory Category => IsSoloPlay ? PlayCategory.Local : PlayCategory.Online;

        public static string GameModeId
        {
            get
            {
                string selectedId = GameFlowManager.Instance != null
                    ? GameFlowManager.Instance.GameModeId
                    : SettingsManager.Current.General.GameModeId;
                return GameModeRegistry.Catalog.TryGet(selectedId, Category, out GameMode mode)
                    ? mode.Id : GameModeRegistry.k_DefaultModeId;
            }
        }

        /// <summary>
        /// 選択可能なモードをゲーム状態と保存設定に反映します。
        /// </summary>
        public static bool TrySelect(PlayCategory category, string gameModeId)
        {
            if ((LobbyUIManager.Instance != null && LobbyUIManager.Instance.IsMatchmaking) ||
                (category == PlayCategory.Online && !CanPlayOnline) ||
                !GameModeRegistry.Catalog.TryGet(gameModeId, category, out GameMode mode))
            {
                return false;
            }

            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.SetGameMode(category, mode.Id);
            }

            SettingsManager.Current.General.IsSoloPlay = category == PlayCategory.Local;
            SettingsManager.Current.General.GameModeId = mode.Id;
            SettingsManager.Save();
            return true;
        }

        /// <summary>
        /// 回線が利用可能なら次回選択に備えて再接続を要求します。
        /// </summary>
        public static void TryReconnect()
        {
            if (GameBuildSettings.IsSoloOnly ||
                Application.internetReachability == NetworkReachability.NotReachable)
            {
                return;
            }

            try
            {
                NetworkServiceLocator.Online?.Connect();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
