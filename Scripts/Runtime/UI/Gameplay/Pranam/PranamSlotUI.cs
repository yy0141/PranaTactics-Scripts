using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

using PranaTactics.Pranam;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// 戦闘画面に表示するPranamスロットの
    /// アイコン、名称、選択状態およびクールダウンゲージを管理します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PranamSlotUI : MonoBehaviour
    {
        /// <summary>
        /// Pranamのアイコンを表示する画像です。
        /// </summary>
        [Header("Display")]
        [FormerlySerializedAs("_icon")]
        [SerializeField]
        private Image _iconImage;

        /// <summary>
        /// アイコンの背景画像です。
        /// </summary>
        [FormerlySerializedAs("_iconBackground")]
        [SerializeField]
        private Image _iconBackgroundImage;

        /// <summary>
        /// Pranamの名称を表示するテキストです。
        /// </summary>
        [FormerlySerializedAs("_text")]
        [SerializeField]
        private TextMeshProUGUI _nameText;

        /// <summary>
        /// 兵装名の右横に消費行動力を表示します。
        /// </summary>
        [SerializeField]
        private TextMeshProUGUI _actionPointCostText;

        /// <summary>
        /// 非選択状態で使用する透明度です。
        /// </summary>
        [Header("Selection")]
        [Range(0.0f, 1.0f)]
        [SerializeField]
        private float _inactiveAlpha = 0.5f;

        /// <summary>
        /// スロットの枠として使用する画像です。
        /// </summary>
        [SerializeField]
        private TacticalHudGraphic _frameImage;

        /// <summary>
        /// 選択中に表示するPranamの射程オブジェクトです。
        /// </summary>
        private GameObject _pranamRange;

        /// <summary>
        /// このスロットに対応するPranamです。
        /// </summary>
        private PranamBase _pranam;

        /// <summary>
        /// 現在設定されているクールダウン時間です。
        /// </summary>
        private float _cooldownDuration;

        /// <summary>
        /// クールダウン開始後の経過時間です。
        /// </summary>
        private float _cooldownElapsedTime;

        /// <summary>
        /// 現在スロットが選択されているかどうかを示します。
        /// </summary>
        private bool _isSelected;

        public bool IsSelected
        {
            get
            {
                return _isSelected;
            }
        }

        /// <summary>
        /// 背景ゲージの参照を取得し、使用可能な状態で表示します。
        /// </summary>
        private void Awake()
        {
            CacheReferences();

            SetGaugeRate(1.0f);
        }

        /// <summary>
        /// クールダウンゲージを更新します。
        /// </summary>
        private void Update()
        {
            UpdateCooldownGauge();
        }

        /// <summary>
        /// Pranamスロットの表示内容を初期化します。
        /// </summary>
        /// <param name="pranamRange">
        /// 選択中に表示するPranamの射程オブジェクトです。
        /// </param>
        /// <param name="icon">
        /// スロットへ表示するPranamアイコンです。
        /// </param>
        /// <param name="displayName">
        /// スロットへ表示するPranam名です。
        /// </param>
        /// <param name="actionPointCost">
        /// 兵装を1回使用する際の消費行動力です。
        /// </param>
        /// <param name="isDefault">
        /// 初期選択状態にする場合は<c>true</c>です。
        /// </param>
        public void Initialize(
            GameObject pranamRange,
            Sprite icon,
            string displayName,
            int actionPointCost,
            bool isDefault)
        {
            _pranamRange =
                pranamRange;

            _pranam =
                _pranamRange != null
                    ? _pranamRange.GetComponent<PranamBase>()
                    : null;

            SetIcon(
                icon);

            if (_actionPointCostText != null)
            {
                _actionPointCostText.text = Mathf.Max(0, actionPointCost).ToString();
            }

            if (_nameText != null)
            {
                _nameText.text =
                    displayName ??
                    string.Empty;
            }

            if (_pranamRange != null)
            {
                _pranamRange.SetActive(
                    isDefault);
            }

            SetSelected(
                isDefault);
        }

        /// <summary>
        /// スロットに表示するアイコンを設定します。
        /// </summary>
        /// <param name="icon">
        /// 表示するSpriteです。
        /// <c>null</c>の場合はアイコンを非表示にします。
        /// </param>
        public void SetIcon(
            Sprite icon)
        {
            if (_iconImage == null)
            {
                return;
            }

            bool hasIcon =
                icon != null;

            _iconImage.sprite =
                icon;

            if (_iconImage.gameObject.activeSelf !=
                hasIcon)
            {
                _iconImage.gameObject.SetActive(
                    hasIcon);
            }
        }

        /// <summary>
        /// スロットを選択状態または非選択状態へ変更します。
        /// </summary>
        /// <param name="isSelected">
        /// 選択状態にする場合は<c>true</c>です。
        /// </param>
        public void SetSelected(
            bool isSelected)
        {
            _isSelected =
                isSelected;

            _frameImage?.SetHighlighted(isSelected);

            SetAlpha(
                isSelected
                    ? 1.0f
                    : _inactiveAlpha);

            if (_pranamRange != null &&
                _pranamRange.activeSelf !=
                isSelected)
            {
                _pranamRange.SetActive(
                    isSelected);
            }

            if (!isSelected &&
                _pranam != null)
            {
                _pranam.Exit();
            }
        }

        /// <summary>
        /// スロットを選択状態へ変更します。
        /// </summary>
        public void Enter()
        {
            SetSelected(
                true);
        }

        /// <summary>
        /// スロットを非選択状態へ変更します。
        /// </summary>
        public void Exit()
        {
            SetSelected(
                false);
        }

        /// <summary>
        /// スロットを構成するUI要素の透明度を変更します。
        /// </summary>
        /// <param name="alpha">
        /// 設定する透明度です。
        /// 0から1の範囲へ補正されます。
        /// </param>
        public void SetAlpha(
            float alpha)
        {
            float validAlpha =
                Mathf.Clamp01(
                    alpha);

            SetGraphicAlpha(
                _frameImage,
                validAlpha);

            SetGraphicAlpha(_actionPointCostText, validAlpha);

            SetGraphicAlpha(
                _nameText,
                validAlpha);

            SetGraphicAlpha(
                _iconImage,
                validAlpha);

            SetGraphicAlpha(
                _iconBackgroundImage,
                validAlpha);
        }

        /// <summary>
        /// 指定された時間でクールダウンゲージの更新を開始します。
        /// </summary>
        /// <param name="duration">
        /// クールダウンが完了するまでの秒数です。
        /// </param>
        public void CountGauge(
            float duration)
        {
            _cooldownDuration =
                Mathf.Max(
                    0.0f,
                    duration);

            _cooldownElapsedTime =
                0.0f;

            if (_cooldownDuration <= 0.0f)
            {
                SetGaugeRate(
                    1.0f);

                return;
            }

            SetGaugeRate(
                0.0f);
        }

        /// <summary>
        /// クールダウンを終了し、
        /// ゲージを最大値へ戻します。
        /// </summary>
        public void CompleteGauge()
        {
            _cooldownDuration =
                0.0f;

            _cooldownElapsedTime =
                0.0f;

            SetGaugeRate(
                1.0f);
        }

        /// <summary>
        /// 経過時間に応じて背景ゲージの割合を更新します。
        /// </summary>
        private void UpdateCooldownGauge()
        {
            if (_cooldownDuration <= 0.0f)
            {
                return;
            }

            _cooldownElapsedTime +=
                Time.deltaTime;

            float gaugeRate =
                Mathf.Clamp01(
                    _cooldownElapsedTime /
                    _cooldownDuration);

            SetGaugeRate(
                gaugeRate);

            if (gaugeRate < 1.0f)
            {
                return;
            }

            _cooldownDuration =
                0.0f;

            _cooldownElapsedTime =
                0.0f;
        }

        /// <summary>
        /// クールダウンゲージを指定された割合へ変更します。
        /// </summary>
        /// <param name="rate">
        /// ゲージの割合です。
        /// 0から1の範囲へ補正されます。
        /// </param>
        private void SetGaugeRate(
            float rate)
        {
            _frameImage?.SetFill(rate);
        }

        /// <summary>
        /// 指定されたGraphicのRGB値を維持し、
        /// 透明度だけを変更します。
        /// </summary>
        /// <param name="graphic">
        /// 透明度を変更するUI要素です。
        /// </param>
        /// <param name="alpha">
        /// 設定する透明度です。
        /// </param>
        private static void SetGraphicAlpha(
            Graphic graphic,
            float alpha)
        {
            if (graphic == null)
            {
                return;
            }

            Color color =
                graphic.color;

            color.a =
                alpha;

            graphic.color =
                color;
        }

        /// <summary>
        /// Inspectorで設定されていないUI参照を可能な範囲で取得します。
        /// </summary>
        private void CacheReferences()
        {
            if (_frameImage == null)
            {
                _frameImage =
                    GetComponent<TacticalHudGraphic>();
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorに設定された値と参照を検証します。
        /// </summary>
        private void OnValidate()
        {
            _inactiveAlpha =
                Mathf.Clamp01(
                    _inactiveAlpha);

            CacheReferences();
        }
#endif
    }
}
