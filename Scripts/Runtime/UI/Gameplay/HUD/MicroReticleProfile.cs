using UnityEngine;

using PranaTactics.Combat;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// 通常レティクルの画像、大きさ、通常色と命中時の表示設定を保持します。
    /// </summary>
    [CreateAssetMenu(fileName = "MicroReticleProfile", menuName = "Prana Tactics/UI/Micro Reticle Profile")]
    public sealed class MicroReticleProfile : ScriptableObject
    {
        [SerializeField]
        private Sprite _sprite;

        /// <summary>
        /// 透明な余白を含む画像全体の表示サイズです。
        /// </summary>
        [Tooltip("画像全体（透明な余白を含む）の表示サイズです。細い線のSpriteは小さすぎると見えなくなります。付属の十字画像は64×64が目安です。")]
        [SerializeField]
        private Vector2 _size = new Vector2(6f, 6f);

        [SerializeField]
        private Color _normalColor = Color.white;

        [SerializeField]
        private Color _hitColor = Color.red;

        [SerializeField, Min(0f)]
        private float _hitDuration = 0.2f;

        /// <summary>
        /// 防御命中のマーカー色です。
        /// </summary>
        [SerializeField]
        private Color _defenseColor = new Color(0.3f, 0.85f, 1.0f);

        /// <summary>
        /// 撃破マーカーの色です。
        /// </summary>
        [SerializeField]
        private Color _eliminationColor = new Color(1.0f, 0.55f, 0.15f);

        /// <summary>
        /// 防御命中の表示時間です。
        /// </summary>
        [SerializeField, Min(0.0f)]
        private float _defenseDuration = 0.2f;

        /// <summary>
        /// 撃破マーカーの表示時間です。
        /// </summary>
        [SerializeField, Min(0.0f)]
        private float _eliminationDuration = 0.7f;

        /// <summary>
        /// 共通照準の左右に配置するMain／Subマーカーの間隔です。
        /// </summary>
        [SerializeField, Min(0.0f)]
        private float _feedbackSideOffset = 18.0f;

        public Sprite Sprite => _sprite;

        public Vector2 Size => new Vector2(Mathf.Max(0f, _size.x), Mathf.Max(0f, _size.y));

        public Color NormalColor => _normalColor;

        public Color HitColor => _hitColor;

        public float HitDuration => Mathf.Max(0f, _hitDuration);

        public float FeedbackSideOffset => Mathf.Max(0.0f, _feedbackSideOffset);

        /// <summary>
        /// 結果ごとのマーカー色を取得します。
        /// </summary>
        public Color GetFeedbackColor(CombatFeedbackKind kind)
        {
            if (kind == CombatFeedbackKind.Elimination)
            {
                return _eliminationColor;
            }

            return kind == CombatFeedbackKind.DefenseHit ? _defenseColor : _hitColor;
        }

        /// <summary>
        /// 結果ごとのマーカー表示時間を取得します。
        /// </summary>
        public float GetFeedbackDuration(CombatFeedbackKind kind)
        {
            if (kind == CombatFeedbackKind.Elimination)
            {
                return Mathf.Max(0.0f, _eliminationDuration);
            }

            return kind == CombatFeedbackKind.DefenseHit ? Mathf.Max(0.0f, _defenseDuration) : HitDuration;
        }
    }
}
