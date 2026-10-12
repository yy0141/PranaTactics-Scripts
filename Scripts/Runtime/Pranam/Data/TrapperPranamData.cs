using UnityEngine;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// 罠の検知範囲、設置後の硬直と発動までの待機時間を保持します。
    /// </summary>
    [CreateAssetMenu(fileName = "TrapperPranamData", menuName = "Prana Tactics/Pranam/Trapper Pranam Data", order = 6)]
    public class TrapperPranamData : PranamData
    {
        /// <summary>
        /// 設置マスから隣接6方向へ検知するヘックス距離です。0は設置マスのみです。
        /// </summary>
        [Header("Trap")]
        [Min(0)]
        [SerializeField]
        private int _triggerRange = 1;

        /// <summary>
        /// 設置成功時に直ちに適用する硬直時間です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _recoveryTime = 1.0f;

        /// <summary>
        /// 敵の進入検知から罠の効果が発生するまでのゲーム内秒数です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _activationDelay;

        /// <summary>
        /// 味方に表示する設置マスの色です。
        /// </summary>
        [Header("Trap Highlight")]
        [SerializeField]
        private Color _placementHighlightColor = Color.red;

        /// <summary>
        /// 設置マスを除く起動範囲の表示色です。
        /// </summary>
        [SerializeField]
        private Color _rangeHighlightColor = new Color(1.0f, 0.55f, 0.55f, 1.0f);

        public int TriggerRange => Mathf.Max(0, _triggerRange);
        public float RecoveryTime => Mathf.Max(0.0f, _recoveryTime);
        public float ActivationDelay => Mathf.Max(0.0f, _activationDelay);
        public Color PlacementHighlightColor => _placementHighlightColor;
        public Color RangeHighlightColor => _rangeHighlightColor;
        protected override string RuntimeBehaviorName => nameof(TrapperBehavior);
    }
}
