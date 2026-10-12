using UnityEngine;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// 被ダメージ時の画面端の色、範囲、表示時間を保持します。
    /// </summary>
    [CreateAssetMenu(fileName = "DamageVignetteProfile", menuName = "Prana Tactics/UI/Damage Vignette Profile")]
    public sealed class DamageVignetteProfile : ScriptableObject
    {
        [SerializeField]
        private Color _color = new Color(1f, 0f, 0f, 0.24f);

        [SerializeField, Range(0.01f, 0.45f)]
        private float _edgeWidth = 0.15f;

        [SerializeField, Min(0f)]
        private float _holdDuration = 0.08f;

        [SerializeField, Min(0.01f)]
        private float _fadeDuration = 0.6f;

        public Color Color => _color;

        public float EdgeWidth => Mathf.Clamp(_edgeWidth, 0.01f, 0.45f);

        public float HoldDuration => Mathf.Max(0f, _holdDuration);

        public float FadeDuration => Mathf.Max(0.01f, _fadeDuration);
    }
}
