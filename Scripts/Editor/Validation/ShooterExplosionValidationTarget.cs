using UnityEngine;

using PranaTactics.Combat;

namespace PranaTactics.Editor.Validation
{
    /// <summary>
    /// 爆風検証でユニットのColliderとプレイヤーIDを提供します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShooterExplosionValidationTarget : MonoBehaviour, IDamageable
    {
        public int PlayerId { get; set; }

        /// <summary>
        /// 検証ではResolverの命中通知を記録するため、直接適用を禁止します。
        /// </summary>
        public void ApplyDamage(int attackerPlayerId, float damage, bool playAnimation = true)
        {
            throw new System.InvalidOperationException("爆風ダメージは命中通知経由で適用してください。");
        }
    }
}
