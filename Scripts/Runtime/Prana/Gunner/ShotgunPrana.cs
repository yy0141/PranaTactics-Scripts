namespace PranaTactics.Prana
{
    /// <summary>
    /// 散弾型プラーナムによって生成される、
    /// 拡散方向へ直進する射撃プラーナです。
    /// </summary>
    /// <remarks>
    /// 生成時に対象方向へ拡散角度を適用し、
    /// 移動中は対象ユニットへ非追尾弾として脅威を通知します。
    /// </remarks>
    public sealed class ShotgunPrana : GunnerPranaBase
    {
        /// <summary>
        /// 基底クラスによる初期化完了後に、
        /// 対象方向へ散弾用の拡散を適用します。
        /// </summary>
        protected override void OnInitialized()
        {
            base.OnInitialized();

            if (Context == null)
            {
                return;
            }

            _direction =
                GetTargetDirection(
                    Context.TargetPlayerId,
                    Context.SpreadAngle);
        }

        /// <summary>
        /// プラーナの移動を更新し、
        /// 対象へ非追尾攻撃として脅威を通知します。
        /// </summary>
        protected override void Update()
        {
            base.Update();

            if (!IsInitialized)
            {
                return;
            }

            SendThreat(
                false);
        }
    }
}
