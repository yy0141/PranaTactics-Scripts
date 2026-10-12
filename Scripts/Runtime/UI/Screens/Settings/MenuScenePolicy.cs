namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// シーンとプレイモードに応じてメニューからの遷移を制限します。
    /// </summary>
    public static class MenuScenePolicy
    {
        /// <summary>
        /// マルチプレイの選択画面と対戦中はメニューからの退出を禁止します。
        /// </summary>
        public static bool CanLeaveGame(string scene, bool isSoloPlay)
        {
            return isSoloPlay || (scene != "InGameScene" && scene != "CharacterSelectScene");
        }

        /// <summary>
        /// ロビーからのみチュートリアルを開始できます。
        /// </summary>
        public static bool CanStartTutorial(string scene)
        {
            return scene == "LobbyScene";
        }

        /// <summary>
        /// 指定されたローカルシーンだけでロビーへの復帰を許可します。
        /// </summary>
        /// <remarks>
        /// 現在のソロゲームはオンラインとInGameSceneを共有しているためモードも確認します。
        /// </remarks>
        public static bool CanReturnToLobby(string scene, bool isSoloPlay)
        {
            return CanLeaveGame(scene, isSoloPlay) &&
                (scene == "SettingScene" || scene == "TutorialScene" ||
                scene == "Solo_CharacterSelectScene" || scene == "Solo_InGameScene" ||
                (scene == "InGameScene" && isSoloPlay));
        }
    }
}
