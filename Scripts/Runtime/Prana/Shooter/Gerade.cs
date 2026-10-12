using UnityEngine;

namespace PranaTactics.Prana
{
    /// <summary>
    /// 散開後、指定された対象へ向かって直進する
    /// 標準的な射出プラーナです。
    /// </summary>
    /// <remarks>
    /// 生成地点から現在位置の外側へ散開した後、
    /// 発射時点の対象位置から決定した方向へ直線移動します。
    ///
    /// 移動中は対象ユニットへ非追尾攻撃として
    /// 脅威情報を通知します。
    /// </remarks>
    public sealed class Gerade : ShooterPranaBase
    {
        /// <summary>
        /// Shooter系プラーナの共通初期化完了後に、
        /// 散開位置を設定します。
        /// </summary>
        protected override void OnShooterInitialized()
        {
            base.OnShooterInitialized();

            if (Context == null)
            {
                return;
            }

            if (!TrySetCommonScatterPosition())
            {
                Debug.LogWarning(
                    $"{nameof(Gerade)}: " +
                    "散開位置を設定できません。",
                    this);

                return;
            }

        }

        /// <summary>
        /// プラーナの散開、照準待機および直進移動を更新し、
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
