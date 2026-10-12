using UnityEngine;
using UnityEngine.Serialization;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// 継続効果を持つオプション型プラーナムに共通する、
    /// リソース消費間隔と使用後の硬直時間を保持します。
    /// </summary>
    public abstract class OptionPranamData : PranamData
    {
        /// <summary>
        /// オプションの維持中にリソースを消費する時間間隔です。
        /// </summary>
        [Header("Timing")]
        [Min(0.0f)]
        [SerializeField]
        private float _consumptionInterval;

        /// <summary>
        /// オプション終了後に次の行動が可能になるまでの硬直時間です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _recoveryTime;

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
