using UnityEngine;
using UnityEngine.Serialization;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// 攻撃補助型プラーナムの攻撃範囲、攻撃性能、
    /// 弾速および照準時間を保持します。
    /// </summary>
    [CreateAssetMenu(
    fileName = "AttackOptionPranamData",
    menuName = "Prana Tactics/Pranam/Option/Attack Option Pranam Data",
    order = 6)]
    public sealed class AttackOptionPranamData : OptionPranamData, IHitSlowdownPranamData
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
        /// 攻撃判定を生成する角度です。
        /// </summary>
        [Header("Attack Range")]
        [Min(0.0f)]
        [SerializeField]
        private float _angle;

        /// <summary>
        /// 攻撃判定の直径または攻撃可能距離です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _diameter;

        /// <summary>
        /// プラーナムの攻撃力です。
        /// </summary>
        [Header("Combat Performance")]
        [Min(0.0f)]
        [SerializeField]
        private float _attackPower;

        /// <summary>
        /// プラーナムの防御力です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _defensePower;

        /// <summary>
        /// 生成する弾または攻撃オブジェクトの移動速度です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _bulletSpeed;

        /// <summary>
        /// 攻撃の照準が完了するまでに必要な秒数です。
        /// </summary>
        [Header("Timing")]
        [Min(0.0f)]
        [SerializeField]
        private float _aimTime;

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

        public float DefensePower
        {
            get
            {
                return _defensePower;
            }
        }

        public float BulletSpeed
        {
            get
            {
                return _bulletSpeed;
            }
        }

        public float AimTime
        {
            get
            {
                return _aimTime;
            }
        }
    }
}
