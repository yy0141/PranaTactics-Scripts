using UnityEngine;
using UnityEngine.Serialization;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// 近接攻撃型プラーナムの攻撃範囲、性能および
    /// 使用時の時間設定を保持します。
    /// </summary>
    [CreateAssetMenu(
    fileName = "AttackerPranamData",
    menuName = "Prana Tactics/Pranam/Attacker Pranam Data",
    order = 1)]
    public sealed class AttackerPranamData : PranamData, IHitSlowdownPranamData
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
        /// 攻撃動作の速度です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _attackSpeed;

        /// <summary>
        /// 攻撃の照準が完了するまでに必要な秒数です。
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

        public float AttackSpeed
        {
            get
            {
                return _attackSpeed;
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
