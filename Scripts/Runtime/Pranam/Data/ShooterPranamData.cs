using UnityEngine;
using UnityEngine.Serialization;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// 射出型プラーナムの攻撃範囲、弾道性能、
    /// 追尾間隔および使用時の時間設定を保持します。
    /// </summary>
    [CreateAssetMenu(
    fileName = "ShooterPranamData",
    menuName = "Prana Tactics/Pranam/Shooter Pranam Data",
    order = 2)]
    public sealed class ShooterPranamData : PranamData, IHitSlowdownPranamData
    {
        /// <summary>
        /// 着弾爆発の設定です。無効の場合は通常の射出弾として動作します。
        /// </summary>
        [Header("Impact Explosion")]
        [SerializeField]
        private ShooterExplosionSettings _explosion = new();

        public ShooterExplosionSettings Explosion => _explosion;

        /// <summary>
        /// ユニットへの命中時に適用する移動速度の減速率です。
        /// </summary>
        [Header("Hit Slowdown")]
        [Range(0.0f, 1.0f)]
        [SerializeField]
        private float _hitSlowdownRatio;

        /// <summary>
        /// 命中時の減速が持続するゲーム内秒数です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _hitSlowdownDurationSeconds = 1.0f;

        public float HitSlowdownRatio => Mathf.Clamp01(_hitSlowdownRatio);

        public float HitSlowdownDurationSeconds => Mathf.Max(0.0f, _hitSlowdownDurationSeconds);

        /// <summary>
        /// 攻撃可能範囲を表す扇形の角度です。
        /// </summary>
        [Header("Attack Range")]
        [Range(0.0f, 360.0f)]
        [SerializeField]
        private float _angle;

        /// <summary>
        /// 攻撃可能距離または攻撃範囲の直径です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _diameter;

        /// <summary>
        /// 弾または攻撃オブジェクトの攻撃力です。
        /// </summary>
        [Header("Combat Performance")]
        [Min(0.0f)]
        [SerializeField]
        private float _attackPower;

        /// <summary>
        /// 発射された弾の移動速度です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _bulletSpeed;

        /// <summary>
        /// 弾道が拡散する最大角度です。
        /// </summary>
        [Header("Projectile")]
        [Range(0.0f, 180.0f)]
        [SerializeField]
        private float _spreadAngle;

        /// <summary>
        /// 追尾弾が進行方向を更新する時間間隔です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _trackingInterval;

        /// <summary>
        /// 攻撃可能になるまでに必要な照準時間です。
        /// </summary>
        [Header("Timing")]
        [Min(0.0f)]
        [SerializeField]
        private float _aimTime;

        /// <summary>
        /// 攻撃後に次の行動が可能になるまでの硬直時間です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _recoveryTime;

        public float Angle
        {
            get
            {
                return _angle;
            }
        }

        public float Diameter
        {
            get
            {
                return _diameter;
            }
        }

        public float AttackPower
        {
            get
            {
                return _attackPower;
            }
        }

        public float BulletSpeed
        {
            get
            {
                return _bulletSpeed;
            }
        }

        public float SpreadAngle
        {
            get
            {
                return _spreadAngle;
            }
        }

        public float TrackingInterval
        {
            get
            {
                return _trackingInterval;
            }
        }

        public float AimTime
        {
            get
            {
                return _aimTime;
            }
        }

        public float RecoveryTime
        {
            get
            {
                return _recoveryTime;
            }
        }
    }
}
