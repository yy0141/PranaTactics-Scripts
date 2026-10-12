using UnityEngine;

using PranaTactics.Pranam;

namespace PranaTactics.Prana
{
    /// <summary>
    /// 散開後に直進し、着弾地点で球状の爆風を発生させるイラプト弾です。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Erupt : ShooterPranaBase
    {
        /// <summary>
        /// 爆発設定を確定し、共通の散開位置を設定します。
        /// </summary>
        /// <remarks>
        /// 通信で受信した発射時の設定を優先します。設定がない生成経路でも、
        /// Prefabに登録された兵装データから複製し、通常弾への暗黙の切り替えを防ぎます。
        /// </remarks>
        protected override void OnShooterInitialized()
        {
            base.OnShooterInitialized();

            if (Context == null)
            {
                return;
            }

            if (Context.Explosion == null)
            {
                if (Definition is not ShooterPranamData data || data.Explosion == null)
                {
                    Debug.LogError("イラプトの爆発設定データが設定されていません。", this);
                    DestroyLocally();
                    return;
                }

                Context.Explosion = data.Explosion.CreateSnapshot();
            }

            if (!TrySetCommonScatterPosition())
            {
                Debug.LogWarning("イラプトの散開位置を設定できません。", this);
                DestroyLocally();
            }
        }

        /// <summary>
        /// 直進弾の移動を更新し、対象へ脅威を通知します。
        /// </summary>
        protected override void Update()
        {
            base.Update();

            if (IsInitialized && Context != null)
            {
                SendThreat(false);
            }
        }

        /// <summary>
        /// ユニット、シールド、壁に加え、地面などの実体Colliderへの着弾も爆発対象にします。
        /// </summary>
        protected override bool CanExplodeOnCollider(Collider hitCollider)
        {
            return base.CanExplodeOnCollider(hitCollider) ||
                (!hitCollider.isTrigger && hitCollider.GetComponentInParent<PranaBase>() == null);
        }
    }
}
