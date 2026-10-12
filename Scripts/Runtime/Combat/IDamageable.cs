namespace PranaTactics.Combat
{
    public interface IDamageable
    {
        int PlayerId
        {
            get;
        }

        /// <summary>
        /// 指定された攻撃元から受けたダメージを適用します。
        /// </summary>
        /// <param name="attackerPlayerId">
        /// ダメージを与えたプレイヤーの識別子です。
        /// </param>
        /// <param name="damage">
        /// 適用するダメージ量です。
        /// </param>
        /// <param name="playAnimation">
        /// ダメージ適用時のアニメーションを再生する場合は<c>true</c>、
        /// 再生しない場合は<c>false</c>です。
        /// </param>
        void ApplyDamage(
            int attackerPlayerId,
            float damage,
            bool playAnimation = true);
    }
}
