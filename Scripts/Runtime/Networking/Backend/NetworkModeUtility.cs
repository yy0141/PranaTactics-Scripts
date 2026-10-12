using UnityEngine;

using PranaTactics.Match;

namespace PranaTactics.Networking
{
    public enum NetworkMode
    {
        Local = 0,

        Photon = 1,

        Eos = 2
    }

    /// <summary>
    /// 現在のゲームルールから、
    /// 使用するネットワーク通信方式を判定します。
    /// </summary>
    public static class NetworkModeUtility
    {
        /// <summary>
        /// PhotonとEOSが同時に有効になっていることを
        /// ログへ出力済みかどうかを示します。
        /// </summary>
        private static bool s_HasLoggedBackendConflict;

        public static NetworkMode CurrentMode
        {
            get
            {
                return ResolveCurrentMode();
            }
        }

        public static bool IsPhoton
        {
            get
            {
                return CurrentMode == NetworkMode.Photon;
            }
        }

        public static bool IsEos
        {
            get
            {
                return CurrentMode == NetworkMode.Eos;
            }
        }

        public static bool IsLocal
        {
            get
            {
                return CurrentMode == NetworkMode.Local;
            }
        }

        public static bool IsOnlineMode
        {
            get
            {
                NetworkMode currentMode = CurrentMode;

                return currentMode == NetworkMode.Photon ||
                    currentMode == NetworkMode.Eos;
            }
        }

        /// <summary>
        /// 現在のゲームルールからネットワーク通信方式を判定します。
        /// </summary>
        /// <returns>
        /// 現在使用するネットワーク通信方式です。
        /// </returns>
        private static NetworkMode ResolveCurrentMode()
        {
            GameRuleManager gameRuleManager =
                GameRuleManager.Instance;

            if (gameRuleManager == null ||
                gameRuleManager.CurrentRule == null)
            {
                ResetConflictLogState();

                return NetworkMode.Local;
            }

            GameRuleData currentRule =
                gameRuleManager.CurrentRule;

            bool usesPhoton = currentRule.UsePhoton;
            bool usesEos = currentRule.UseEos;

            if (usesPhoton && usesEos)
            {
                LogBackendConflict();

                return NetworkMode.Eos;
            }

            ResetConflictLogState();

            if (usesEos)
            {
                return NetworkMode.Eos;
            }

            if (usesPhoton)
            {
                return NetworkMode.Photon;
            }

            return NetworkMode.Local;
        }

        /// <summary>
        /// PhotonとEOSが同時に有効になっていることを
        /// 一度だけエラーログへ出力します。
        /// </summary>
        private static void LogBackendConflict()
        {
            if (s_HasLoggedBackendConflict)
            {
                return;
            }

            Debug.LogError(
                $"{nameof(NetworkModeUtility)}: " +
                $"{nameof(GameRuleData.UsePhoton)}と" +
                $"{nameof(GameRuleData.UseEos)}が両方有効です。" +
                "通信方式が競合するため、EOSを優先します。");

            s_HasLoggedBackendConflict = true;
        }

        /// <summary>
        /// ネットワーク設定の競合が解消された場合に、
        /// 競合ログの出力状態をリセットします。
        /// </summary>
        private static void ResetConflictLogState()
        {
            s_HasLoggedBackendConflict = false;
        }
    }
}
