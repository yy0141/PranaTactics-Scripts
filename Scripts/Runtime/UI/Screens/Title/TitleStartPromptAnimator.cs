using UnityEngine;

namespace PranaTactics.UI.Screens.Title
{
    /// <summary>
    /// タイトルの開始案内を実時間で滑らかに明滅させます。
    /// </summary>
    /// <remarks>
    /// 開始案内は操作状態を持たないため、UIの待機状態登録には依存しません。
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class TitleStartPromptAnimator : MonoBehaviour
    {
        /// <summary>
        /// 明滅が一周する実時間の秒数です。
        /// </summary>
        [SerializeField]
        [Min(0.1f)]
        private float _cycleSeconds = 3.6f;

        /// <summary>
        /// 最も暗い時点の不透明度です。
        /// </summary>
        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float _minimumAlpha = 0.2f;

        /// <summary>
        /// 最も明るい時点の不透明度です。
        /// </summary>
        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float _maximumAlpha = 1.0f;

        /// <summary>
        /// 開始案内だけの不透明度を管理します。
        /// </summary>
        private CanvasGroup _canvasGroup;

        /// <summary>
        /// 表示開始前の不透明度です。
        /// </summary>
        private float _originalAlpha;

        /// <summary>
        /// 現在の周期内の実経過時間です。
        /// </summary>
        private float _elapsedTime;

        /// <summary>
        /// 案内を明るい状態から表示します。
        /// </summary>
        private void OnEnable()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _originalAlpha = _canvasGroup.alpha;
            _elapsedTime = 0.0f;
            ApplyAlpha();
        }

        /// <summary>
        /// ゲーム速度に影響されずに明滅を進めます。
        /// </summary>
        private void Update()
        {
            _elapsedTime = Mathf.Repeat(
                _elapsedTime + Time.unscaledDeltaTime,
                Mathf.Max(0.1f, _cycleSeconds));
            ApplyAlpha();
        }

        /// <summary>
        /// 周期に応じた滑らかな不透明度を反映します。
        /// </summary>
        private void ApplyAlpha()
        {
            float phase = _elapsedTime / Mathf.Max(0.1f, _cycleSeconds);
            float brightness = 0.5f + 0.5f * Mathf.Cos(phase * Mathf.PI * 2.0f);
            float minimumAlpha = Mathf.Clamp01(_minimumAlpha);
            float maximumAlpha = Mathf.Clamp(_maximumAlpha, minimumAlpha, 1.0f);
            _canvasGroup.alpha = Mathf.Lerp(minimumAlpha, maximumAlpha, brightness);
        }

        /// <summary>
        /// 非表示時に元の不透明度へ戻します。
        /// </summary>
        private void OnDisable()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = _originalAlpha;
            }
        }
    }
}
