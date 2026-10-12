using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// ユニットの現在HPと、遅れて減少するダメージ表示を管理します。
    /// </summary>
    /// <remarks>
    /// 通常のHPゲージを更新した後、
    /// ダメージ表示用ゲージを遅れて追従させます。
    ///
    /// アニメーションを有効にした場合は、
    /// ゲージ更新と同時にUIを振動させます。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HealthGauge : MonoBehaviour
    {
        /// <summary>
        /// 現在HPを表示する画像です。
        /// </summary>
        [Header("Gauge")]
        [FormerlySerializedAs("healthImage")]
        [SerializeField]
        private Image _healthImage;

        /// <summary>
        /// ダメージ量を遅れて表示する画像です。
        /// </summary>
        [FormerlySerializedAs("burnImage")]
        [SerializeField]
        private Image _damageImage;

        /// <summary>
        /// HPゲージが目標値へ変化する時間です。
        /// </summary>
        [Header("Animation")]
        [FormerlySerializedAs("duration")]
        [Min(0.0f)]
        [SerializeField]
        private float _animationDuration = 3.0f;

        /// <summary>
        /// HPゲージの更新完了後、
        /// ダメージ表示ゲージが動き始めるまでの時間です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _damageAnimationDelay = 0.5f;

        /// <summary>
        /// ダメージ表示ゲージのアニメーション時間を、
        /// HPゲージのアニメーション時間に対する倍率で指定します。
        /// </summary>
        [Range(0.0f, 1.0f)]
        [SerializeField]
        private float _damageDurationMultiplier = 0.5f;

        /// <summary>
        /// ゲージ更新時にUIを振動させる強さです。
        /// </summary>
        [Header("Shake")]
        [FormerlySerializedAs("strength")]
        [Min(0.0f)]
        [SerializeField]
        private float _shakeStrength = 20.0f;

        /// <summary>
        /// 振動中に方向を変更する回数です。
        /// </summary>
        [FormerlySerializedAs("vibrate")]
        [Min(1)]
        [SerializeField]
        private int _shakeVibrato = 100;

        /// <summary>
        /// 現在設定されているHP割合です。
        /// </summary>
        private float _currentRate = 1.0f;

        /// <summary>
        /// HPゲージを更新するTweenです。
        /// </summary>
        private Tween _healthTween;

        /// <summary>
        /// ダメージ表示ゲージを更新するTweenです。
        /// </summary>
        private Tween _damageTween;

        /// <summary>
        /// UIの振動を制御するTweenです。
        /// </summary>
        private Tween _shakeTween;

        /// <summary>
        /// 振動させるUIのRectTransformです。
        /// </summary>
        private RectTransform _rectTransform;

        /// <summary>
        /// 振動開始直前の位置です。レイアウト確定後の位置を毎回保存します。
        /// </summary>
        private Vector3 _shakeOrigin;

        /// <summary>
        /// 振動前の位置を復元する必要があるかどうかを示します。
        /// </summary>
        private bool _hasShakeOrigin;

        public float CurrentRate
        {
            get
            {
                return _currentRate;
            }
        }

        /// <summary>
        /// 必要なコンポーネントを取得し、
        /// 現在のゲージ値を初期状態として保存します。
        /// </summary>
        private void Awake()
        {
            _rectTransform =
                transform as RectTransform;

            if (_healthImage != null)
            {
                _currentRate =
                    Mathf.Clamp01(
                        _healthImage.fillAmount);
            }
        }

        /// <summary>
        /// コンポーネントが無効化されたとき、
        /// 実行中のTweenを停止します。
        /// </summary>
        private void OnDisable()
        {
            KillTweens();
        }

        /// <summary>
        /// HPゲージを指定された割合へ更新します。
        /// </summary>
        /// <param name="value">
        /// 設定するHP割合です。
        /// 0から1の範囲へ補正されます。
        /// </param>
        /// <param name="animation">
        /// アニメーションを再生する場合は<c>true</c>です。
        /// </param>
        public void SetGauge(
            float value,
            bool animation = true)
        {
            if (!ValidateImages())
            {
                return;
            }

            float targetRate =
                Mathf.Clamp01(
                    value);

            KillTweens();

            _currentRate =
                targetRate;

            if (!animation ||
                _animationDuration <= 0.0f)
            {
                ApplyGaugeImmediately(
                    targetRate);

                return;
            }

            PlayGaugeAnimation(
                targetRate);
        }

        /// <summary>
        /// ゲージをアニメーションなしで指定された値へ設定します。
        /// </summary>
        /// <param name="value">
        /// 設定するHP割合です。
        /// </param>
        public void SetGaugeImmediately(
            float value)
        {
            SetGauge(
                value,
                false);
        }

        /// <summary>
        /// HPゲージとダメージ表示ゲージの
        /// アニメーションを開始します。
        /// </summary>
        /// <param name="targetRate">
        /// アニメーション後のHP割合です。
        /// </param>
        private void PlayGaugeAnimation(
            float targetRate)
        {
            _healthTween =
                _healthImage
                    .DOFillAmount(
                        targetRate,
                        _animationDuration)
                    .SetEase(
                        Ease.OutQuad)
                    .OnComplete(
                        () =>
                        {
                            _healthTween =
                                null;

                            PlayDamageGaugeAnimation(
                                targetRate);
                        });

            PlayShakeAnimation();
        }

        /// <summary>
        /// ダメージ表示ゲージを遅れて目標値へ変更します。
        /// </summary>
        /// <param name="targetRate">
        /// アニメーション後のHP割合です。
        /// </param>
        private void PlayDamageGaugeAnimation(
            float targetRate)
        {
            if (_damageImage == null ||
                !isActiveAndEnabled)
            {
                return;
            }

            float damageDuration =
                _animationDuration *
                _damageDurationMultiplier;

            if (damageDuration <= 0.0f)
            {
                _damageImage.fillAmount =
                    targetRate;

                return;
            }

            _damageTween =
                _damageImage
                    .DOFillAmount(
                        targetRate,
                        damageDuration)
                    .SetDelay(
                        _damageAnimationDelay)
                    .SetEase(
                        Ease.OutQuad)
                    .OnComplete(
                        () =>
                        {
                            _damageTween =
                                null;
                        });
        }

        /// <summary>
        /// ゲージ更新時の振動アニメーションを開始します。
        /// </summary>
        private void PlayShakeAnimation()
        {
            if (_rectTransform == null ||
                _shakeStrength <= 0.0f)
            {
                return;
            }

            float shakeDuration =
                _animationDuration *
                0.5f;

            if (shakeDuration <= 0.0f)
            {
                return;
            }

            _shakeOrigin = _rectTransform.anchoredPosition3D;
            _hasShakeOrigin = true;

            _shakeTween =
                _rectTransform
                    .DOShakeAnchorPos(
                        shakeDuration,
                        _shakeStrength,
                        _shakeVibrato)
                    .OnComplete(RestoreShakePosition)
                    .OnKill(RestoreShakePosition);
        }

        /// <summary>
        /// 振動の完了・中断時に元の位置を復元し、連続被弾による位置ずれの蓄積を防ぎます。
        /// </summary>
        private void RestoreShakePosition()
        {
            if (_hasShakeOrigin && _rectTransform != null)
            {
                _rectTransform.anchoredPosition3D = _shakeOrigin;
            }

            _hasShakeOrigin = false;
            _shakeTween = null;
        }

        /// <summary>
        /// 両方のゲージを即座に指定された値へ変更します。
        /// </summary>
        /// <param name="targetRate">
        /// 設定するHP割合です。
        /// </param>
        private void ApplyGaugeImmediately(
            float targetRate)
        {
            _healthImage.fillAmount =
                targetRate;

            _damageImage.fillAmount =
                targetRate;
        }

        /// <summary>
        /// 実行中のすべてのTweenを停止します。
        /// </summary>
        private void KillTweens()
        {
            _healthTween?.Kill();
            _damageTween?.Kill();
            _shakeTween?.Kill();

            _healthTween =
                null;

            _damageTween =
                null;

            _shakeTween =
                null;
        }

        /// <summary>
        /// HPゲージに必要な画像が設定されているか検証します。
        /// </summary>
        /// <returns>
        /// 両方の画像が設定されている場合は<c>true</c>です。
        /// </returns>
        private bool ValidateImages()
        {
            if (_healthImage != null &&
                _damageImage != null)
            {
                return true;
            }

            Debug.LogError(
                $"{nameof(HealthGauge)}: " +
                "HPゲージまたはダメージ表示ゲージが設定されていません。",
                this);

            return false;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorに設定されたアニメーション値を検証します。
        /// </summary>
        private void OnValidate()
        {
            _animationDuration =
                Mathf.Max(
                    0.0f,
                    _animationDuration);

            _damageAnimationDelay =
                Mathf.Max(
                    0.0f,
                    _damageAnimationDelay);

            _damageDurationMultiplier =
                Mathf.Clamp01(
                    _damageDurationMultiplier);

            _shakeStrength =
                Mathf.Max(
                    0.0f,
                    _shakeStrength);

            _shakeVibrato =
                Mathf.Max(
                    1,
                    _shakeVibrato);
        }
#endif
    }
}
