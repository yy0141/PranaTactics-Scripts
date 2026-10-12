using UnityEngine;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// 設置地点で爆発する罠の威力、減衰、演出と命中時の減速を保持します。
    /// </summary>
    [CreateAssetMenu(fileName = "ExplosiveTrapperPranamData",
        menuName = "Prana Tactics/Pranam/Explosive Trapper Pranam Data", order = 7)]
    public sealed class ExplosiveTrapperPranamData : TrapperPranamData, IHitSlowdownPranamData
    {
        /// <summary>
        /// 爆心での最大攻撃力です。
        /// </summary>
        [Header("Explosion")]
        [Min(0.0f)]
        [SerializeField]
        private float _attackPower = 300.0f;

        /// <summary>
        /// Eruptと共通の爆風とVFXの設定です。
        /// </summary>
        [SerializeField]
        private ShooterExplosionSettings _explosion = new();

        /// <summary>
        /// 命中した敵の移動速度を減速する割合です。
        /// </summary>
        [Header("Hit Slowdown")]
        [Range(0.0f, 1.0f)]
        [SerializeField]
        private float _hitSlowdownRatio = 0.75f;

        /// <summary>
        /// 命中時の減速が続く秒数です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _hitSlowdownDurationSeconds = 1.0f;

        public float AttackPower => Mathf.Max(0.0f, _attackPower);
        public ShooterExplosionSettings Explosion => _explosion;
        public float HitSlowdownRatio => Mathf.Clamp01(_hitSlowdownRatio);
        public float HitSlowdownDurationSeconds => Mathf.Max(0.0f, _hitSlowdownDurationSeconds);
    }
}
