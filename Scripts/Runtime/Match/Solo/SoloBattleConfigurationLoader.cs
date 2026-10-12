using PranaTactics.Match.Solo.Stages;

namespace PranaTactics.Match.Solo
{
    /// <summary>
    /// ソロ編成のJSONアセットを読み込みます。
    /// </summary>
    public static class SoloBattleConfigurationLoader
    {
        /// <summary>
        /// 同じ試合の生成・集計・配色で共有する設定を読み込みます。
        /// </summary>
        public static SoloBattleConfiguration Load()
        {
            return SoloStageCatalog.Load().Stages[0].LoadConfiguration();
        }
    }
}
