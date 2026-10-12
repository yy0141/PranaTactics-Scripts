using UnityEngine;
using UnityEngine.Serialization;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// 銃撃型プラーナムの射程、攻撃性能、連射性能および
    /// 使用時の時間設定を保持します。
    /// </summary>
    [CreateAssetMenu(
    fileName = "GunnerPranamData",
    menuName = "Prana Tactics/Pranam/Gunner Pranam Data",
    order = 3)]
    public sealed class GunnerPranamData : PranamData, IHitSlowdownPranamData
    {
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
        /// 1発あたりの攻撃力です。
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
        /// 1回の攻撃で発射する弾数です。
        /// </summary>
        [Header("Firing")]
        [Min(1)]
        [SerializeField]
        private int _bulletCount = 1;

        /// <summary>
        /// 連続する弾を発射する時間間隔です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _fireInterval;

        /// <summary>
        /// 弾道が拡散する最大角度です。
        /// </summary>
        [Range(0.0f, 180.0f)]
        [SerializeField]
        private float _spreadAngle;

        /// <summary>
        /// 1行動力あたりに発射できる弾数です。
        /// </summary>
        [Min(1)]
        [SerializeField]
        private int _bulletsPerActionPoint = 1;

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

        public int BulletCount
        {
            get
            {
                return _bulletCount;
            }
        }

        public float FireInterval
        {
            get
            {
                return _fireInterval;
            }
        }

        public float SpreadAngle
        {
            get
            {
                return _spreadAngle;
            }
        }

        public int BulletsPerActionPoint
        {
            get
            {
                return _bulletsPerActionPoint;
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
