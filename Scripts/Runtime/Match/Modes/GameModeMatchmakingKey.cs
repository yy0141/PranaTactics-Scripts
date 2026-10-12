using System;

namespace PranaTactics.Match.Modes
{
    /// <summary>
    /// 通信サービス共通のモード別マッチング識別子を生成します。
    /// </summary>
    public static class GameModeMatchmakingKey
    {
        /// <summary>
        /// 既存モードの識別子を維持し、追加モードの検索先を分離します。
        /// </summary>
        public static string Create(string baseKey, string modeId)
        {
            if (string.IsNullOrWhiteSpace(baseKey))
            {
                throw new ArgumentException("検索先の識別子が必要です。", nameof(baseKey));
            }

            if (string.IsNullOrWhiteSpace(modeId))
            {
                throw new ArgumentException("ゲームモードIDが必要です。", nameof(modeId));
            }

            return modeId == GameModeRegistry.k_DefaultModeId ? baseKey : baseKey + "_" + modeId;
        }
    }
}
