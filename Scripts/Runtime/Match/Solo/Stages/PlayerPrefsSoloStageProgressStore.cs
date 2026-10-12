using UnityEngine;

namespace PranaTactics.Match.Solo.Stages
{
    /// <summary>
    /// ステージ識別子ごとのクリア記録を端末へ永続保存します。
    /// </summary>
    public sealed class PlayerPrefsSoloStageProgressStore : ISoloStageProgressStore
    {
        private const string k_KeyPrefix = "PranaTactics.Solo.Elimination.Clear.";

        /// <summary>
        /// 保存されたクリア状態を取得します。
        /// </summary>
        public bool IsCleared(string stageId)
        {
            return PlayerPrefs.GetInt(k_KeyPrefix + stageId, 0) == 1;
        }

        /// <summary>
        /// クリア状態を保存し、次回起動にも反映します。
        /// </summary>
        public void SaveClear(string stageId)
        {
            PlayerPrefs.SetInt(k_KeyPrefix + stageId, 1);
            PlayerPrefs.Save();
        }
    }
}
