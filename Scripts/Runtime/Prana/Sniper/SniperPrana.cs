using UnityEngine;

namespace PranaTactics.Prana
{
    /// <summary>
    /// 対象へ向かって直線移動する標準的な狙撃プラーナです。
    /// </summary>
    /// <remarks>
    /// ズーム後の発射時点で確定した進行方向を使用します。
    /// 発射後は対象を追尾せず、確定した方向へ直進します。
    /// </remarks>
    public sealed class SniperPrana : SniperPranaBase
    {
        /// <summary>
        /// 発射側が確定して通信した回転から進行方向を設定します。
        /// </summary>
        /// <remarks>
        /// 発射時点の敵位置からSniperBehaviorが計算した方向を維持します。
        /// 受信後の敵位置を再参照しないため、通信遅延で射線が変化しません。
        /// </remarks>
        protected override void OnSniperInitialized()
        {
            base.OnSniperInitialized();
            TargetPosition = transform.position + transform.forward;
            Direction = transform.forward;
        }

        /// <summary>
        /// 狙撃プラーナの移動を更新し、
        /// 対象へ非追尾攻撃として脅威を通知します。
        /// </summary>
        protected override void Update()
        {
            base.Update();

            if (!IsInitialized ||
                Context == null)
            {
                return;
            }

            SendThreat(
                false);
        }
    }
}
