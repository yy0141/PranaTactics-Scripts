using System;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// 陣営と生存状態に応じたキャラクターアイコンの不透明度を計算します。
    /// </summary>
    public static class TeamRosterAppearance
    {
        /// <summary>
        /// 味方のHPを50〜100%の不透明度へ変換し、死亡時は陣営を問わず50%にします。
        /// </summary>
        public static float CalculateOpacity(bool isAlly, bool isDead, float healthRate)
        {
            if (isDead)
            {
                return 0.5f;
            }

            return isAlly ? 0.5f + 0.5f * Math.Max(0.0f, Math.Min(1.0f, healthRate)) : 1.0f;
        }
    }
}
