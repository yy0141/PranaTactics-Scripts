using UnityEngine;

using PranaTactics.Combat;

namespace PranaTactics.Audio
{
    /// <summary>
    /// 確認音と、競合する音の一時減衰を設定します。
    /// </summary>
    [CreateAssetMenu(fileName = "CombatFeedback", menuName = "Prana Tactics/Audio/Combat Feedback Profile")]
    public sealed class CombatFeedbackProfile : ScriptableObject
    {
        /// <summary>
        /// 敵UnitのHPが減少したときの確認音です。
        /// </summary>
        [SerializeField]
        private SoundCue _unitHitCue;

        /// <summary>
        /// 敵の防御耐久値が減少したときの確認音です。
        /// </summary>
        [SerializeField]
        private SoundCue _defenseHitCue;

        /// <summary>
        /// 敵Unitを撃破したときの確認音です。
        /// </summary>
        [SerializeField]
        private SoundCue _eliminationCue;

        /// <summary>
        /// 通常確認音の最小開始間隔です。
        /// </summary>
        [SerializeField, Min(0.0f)]
        private float _hitInterval = 0.08f;

        /// <summary>
        /// 撃破音の最小間隔および通常音の抑制時間です。
        /// </summary>
        [SerializeField, Min(0.0f)]
        private float _eliminationInterval = 0.18f;

        /// <summary>
        /// 競合音の一時減衰を有効にします。
        /// </summary>
        [SerializeField]
        private bool _enableDucking = true;

        /// <summary>
        /// 通常確認音で自分の装飾音を下げるdB量です。
        /// </summary>
        [SerializeField, Range(0.0f, 12.0f)]
        private float _hitDuckDecibels = 3.0f;

        /// <summary>
        /// 撃破確認音で自分の装飾音を下げるdB量です。
        /// </summary>
        [SerializeField, Range(0.0f, 12.0f)]
        private float _eliminationDuckDecibels = 5.0f;

        public float HitInterval => Mathf.Max(0.0f, _hitInterval);
        public float EliminationInterval => Mathf.Max(0.0f, _eliminationInterval);
        public bool EnableDucking => _enableDucking;

        /// <summary>
        /// 攻撃結果に対応する確認音を取得します。
        /// </summary>
        public SoundCue GetCue(CombatFeedbackKind kind)
        {
            switch (kind)
            {
                case CombatFeedbackKind.UnitHit:
                    return _unitHitCue;
                case CombatFeedbackKind.DefenseHit:
                    return _defenseHitCue;
                case CombatFeedbackKind.Elimination:
                    return _eliminationCue;
                default:
                    return null;
            }
        }

        /// <summary>
        /// 攻撃結果に対応する減衰後の音量比率を取得します。
        /// </summary>
        public float GetDuckGain(CombatFeedbackKind kind)
        {
            if (!_enableDucking)
            {
                return 1.0f;
            }

            float decibels = kind == CombatFeedbackKind.Elimination
                ? _eliminationDuckDecibels
                : _hitDuckDecibels;
            return Mathf.Pow(10.0f, -decibels / 20.0f);
        }
    }
}
