using UnityEngine;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// 両ページでチーム識別色と英字名を統一します。
    /// </summary>
    public static class ResultTeamStyle
    {
        /// <summary>
        /// 暗色背景上のチーム識別色です。
        /// </summary>
        private static readonly Color[] s_colors =
        {
            new(0.42f, 0.86f, 0.9f), new(1.0f, 0.48f, 0.52f), new(0.98f, 0.77f, 0.38f),
            new(0.68f, 0.58f, 0.98f), new(0.45f, 0.87f, 0.63f), new(0.96f, 0.58f, 0.82f)
        };

        /// <summary>
        /// チームのアクセント色を返します。
        /// </summary>
        public static Color GetColor(int teamId)
        {
            return s_colors[Mathf.Abs(teamId % s_colors.Length)];
        }

        /// <summary>
        /// 自チームまたはローカルPlayerの行全体に、チーム色の明るい背景を設定します。
        /// </summary>
        /// <param name="teamId">
        /// 行に対応するチームIDです。
        /// </param>
        /// <param name="isHighlighted">
        /// 行全体を強調表示する場合は<c>true</c>です。
        /// </param>
        public static Color GetRowColor(int teamId, bool isHighlighted)
        {
            Color background = new Color(0.055f, 0.08f, 0.1f);
            return isHighlighted ? Color.Lerp(background, GetColor(teamId), 0.28f) : background;
        }

        /// <summary>
        /// チームIDを英字名へ変換します。
        /// </summary>
        public static string GetName(int teamId)
        {
            return teamId >= 0 && teamId < 26 ? ((char)('A' + teamId)).ToString() : teamId.ToString();
        }
    }
}
