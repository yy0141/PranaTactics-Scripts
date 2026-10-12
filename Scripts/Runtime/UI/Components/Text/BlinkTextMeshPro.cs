using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

using PranaTactics.UI.Interaction;

namespace PranaTactics.UI
{
    /// <summary>
    /// 待機状態の間、TextMeshProUGUIの透明度を
    /// 指定された範囲内で往復させます。
    /// </summary>
    /// <remarks>
    /// 点滅処理はUnityのUpdateメッセージではなく、
    /// <see cref="IdleBehaviorBase"/>と
    /// <see cref="UIStateMachine"/>を通して更新されます。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BlinkTextMeshPro : IdleBehaviorBase
    {
        /// <summary>
        /// 透明度を減少させる方向を表す値です。
        /// </summary>
        private const float k_FadeOutDirection = -1.0f;

        /// <summary>
        /// 透明度を増加させる方向を表す値です。
        /// </summary>
        private const float k_FadeInDirection = 1.0f;

        /// <summary>
        /// 最初に待機状態へ登録された時点から
        /// 点滅を開始するかどうかを示します。
        /// </summary>
        [Header("Blink")]
        [FormerlySerializedAs("_playOnStart")]
        [SerializeField]
        private bool _playOnStart = true;

        /// <summary>
        /// 点滅中に適用する最小透明度です。
        /// </summary>
        [FormerlySerializedAs("_minAlpha")]
        [Range(0.0f, 1.0f)]
        [SerializeField]
        private float _minimumAlpha = 0.1f;

        /// <summary>
        /// 点滅中に適用する最大透明度です。
        /// </summary>
        [FormerlySerializedAs("_maxAlpha")]
        [Range(0.0f, 1.0f)]
        [SerializeField]
        private float _maximumAlpha = 1.0f;

        /// <summary>
        /// 1秒当たりに変化させる透明度です。
        /// </summary>
        [FormerlySerializedAs("_alphaChangeRate")]
        [Min(0.0f)]
        [SerializeField]
        private float _alphaChangeSpeed = 1.0f;

        /// <summary>
        /// 透明度を変更するTextMeshProUGUIです。
        /// </summary>
        private TextMeshProUGUI _targetText;

        /// <summary>
        /// 現在の透明度変更方向です。
        /// </summary>
        private float _fadeDirection = k_FadeOutDirection;

        /// <summary>
        /// 最初の待機状態への遷移を処理済みかどうかを示します。
        /// </summary>
        private bool _hasProcessedInitialEnter;

        /// <summary>
        /// TextMeshProUGUIへの参照を取得します。
        /// </summary>
        private void Awake()
        {
            CacheTextReference();
        }

        /// <summary>
        /// 待機状態へ遷移したとき、
        /// テキストの点滅を開始します。
        /// </summary>
        protected override void OnIdleEnter()
        {
            if (!_hasProcessedInitialEnter)
            {
                _hasProcessedInitialEnter = true;

                if (!_playOnStart)
                {
                    return;
                }
            }

            if (!CacheTextReference())
            {
                return;
            }

            base.OnIdleEnter();

            float currentAlpha =
                _targetText.color.a;

            _fadeDirection =
                currentAlpha <= _minimumAlpha
                    ? k_FadeInDirection
                    : k_FadeOutDirection;
        }

        /// <summary>
        /// 待機状態が有効な間、
        /// テキストの透明度を更新します。
        /// </summary>
        protected override void OnIdleUpdate()
        {
            BlinkText();
        }

        /// <summary>
        /// 待機状態が終了したとき、
        /// 点滅処理を停止します。
        /// </summary>
        protected override void OnIdleExit()
        {
            base.OnIdleExit();
        }

        /// <summary>
        /// テキストの透明度を最小値と最大値の間で変化させます。
        /// </summary>
        private void BlinkText()
        {
            if (_targetText == null ||
                _alphaChangeSpeed <= 0.0f)
            {
                return;
            }

            Color textColor =
                _targetText.color;

            float nextAlpha =
                textColor.a +
                _fadeDirection *
                _alphaChangeSpeed *
                Time.deltaTime;

            if (nextAlpha <=
                _minimumAlpha)
            {
                nextAlpha =
                    _minimumAlpha;

                _fadeDirection =
                    k_FadeInDirection;
            }
            else if (nextAlpha >=
                    _maximumAlpha)
            {
                nextAlpha =
                    _maximumAlpha;

                _fadeDirection =
                    k_FadeOutDirection;
            }

            textColor.a =
                nextAlpha;

            _targetText.color =
                textColor;
        }

        /// <summary>
        /// 自身または子オブジェクトから
        /// TextMeshProUGUIを取得します。
        /// </summary>
        /// <returns>
        /// TextMeshProUGUIを取得できた場合は<c>true</c>です。
        /// </returns>
        private bool CacheTextReference()
        {
            if (_targetText != null)
            {
                return true;
            }

            _targetText =
                GetComponent<TextMeshProUGUI>();

            if (_targetText == null)
            {
                _targetText =
                    GetComponentInChildren<TextMeshProUGUI>(
                        true);
            }

            if (_targetText != null)
            {
                return true;
            }

            Debug.LogWarning(
                $"{nameof(BlinkTextMeshPro)}: " +
                $"{gameObject.name}から" +
                $"{nameof(TextMeshProUGUI)}を取得できないため、" +
                "点滅処理を実行できません。",
                this);

            return false;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorから設定された値を有効範囲へ補正します。
        /// </summary>
        private void OnValidate()
        {
            _minimumAlpha =
                Mathf.Clamp01(
                    _minimumAlpha);

            _maximumAlpha =
                Mathf.Clamp01(
                    _maximumAlpha);

            if (_maximumAlpha <
                _minimumAlpha)
            {
                _maximumAlpha =
                    _minimumAlpha;
            }

            _alphaChangeSpeed =
                Mathf.Max(
                    0.0f,
                    _alphaChangeSpeed);
        }
#endif
    }
}
