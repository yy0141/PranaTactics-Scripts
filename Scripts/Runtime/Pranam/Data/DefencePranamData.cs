using UnityEngine;
using UnityEngine.Serialization;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// 防御型プラーナムの防御性能、継続消費間隔および
    /// 使用後の硬直時間を保持します。
    /// </summary>
    [CreateAssetMenu(
    fileName = "DefencePranamData",
    menuName = "Prana Tactics/Pranam/Defence Pranam Data",
    order = 5)]
    public sealed class DefencePranamData : PranamData
    {
        /// <summary>
        /// 攻撃を防ぐことができる防御力です。
        /// </summary>
        [Header("Defense Performance")]
        [Min(0.0f)]
        [SerializeField]
        private float _defensePower;

        /// <summary>
        /// 防御状態の維持中にリソースを消費する時間間隔です。
        /// </summary>
        [Header("Timing")]
        [Min(0.0f)]
        [SerializeField]
        private float _consumptionInterval;

        /// <summary>
        /// 防御終了後に次の行動が可能になるまでの硬直時間です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _recoveryTime;

        public float DefensePower
        {
            get
            {
                return _defensePower;
            }
        }

        public float ConsumptionInterval
        {
            get
            {
                return _consumptionInterval;
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
