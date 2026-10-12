using UnityEngine;
using UnityEngine.UI;

using UiToggle = UnityEngine.UI.Toggle;

using PranaTactics.GameFlow;
using PranaTactics.Match.Modes;
using PranaTactics.UI.Screens.GamePlayMode;
using PranaTactics.Settings;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// トグルの状態に応じて、
    /// ソロプレイとオンライン対戦を切り替えます。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SwitchPlayMode : MonoBehaviour
    {
        private GameObject _connectionWarning;

        /// <summary>
        /// プレイモードの切り替えに使用するトグルです。
        /// ONの場合はソロプレイ、OFFの場合はオンライン対戦です。
        /// </summary>
        [SerializeField]
        private UiToggle _toggle;

        /// <summary>
        /// ソロプレイを表す画像です。
        /// </summary>
        [SerializeField]
        private Image _soloModeImage;

        /// <summary>
        /// オンライン対戦を表す画像です。
        /// </summary>
        [SerializeField]
        private Image _multiModeImage;

        /// <summary>
        /// ソロプレイ選択時に表示する
        /// ボタン群のルートオブジェクトです。
        /// </summary>
        [SerializeField]
        private GameObject _soloModeButtonGroup;

        /// <summary>
        /// オンライン対戦選択時に表示する
        /// ボタン群のルートオブジェクトです。
        /// </summary>
        [SerializeField]
        private GameObject _multiModeButtonGroup;

        /// <summary>
        /// 選択中のプレイモードに適用する色です。
        /// </summary>
        [SerializeField]
        private Color _backgroundOnColor;

        /// <summary>
        /// 選択されていないプレイモードに適用する色です。
        /// </summary>
        [SerializeField]
        private Color _backgroundOffColor;

        /// <summary>
        /// オブジェクトが有効になったとき、
        /// トグルの変更イベントを登録します。
        /// </summary>
        private void OnEnable()
        {
            if (_toggle == null)
            {
                Debug.LogError(
                    $"{nameof(SwitchPlayMode)}: " +
                    $"{nameof(_toggle)}が設定されていません。",
                    this);

                return;
            }

            _toggle.onValueChanged.AddListener(
                OnPlayModeChanged);

            ApplyPlayMode(SettingsManager.Current.General.IsSoloPlay || !PlayModeSelection.CanPlayOnline);
        }

        /// <summary>
        /// オブジェクトが無効になったとき、
        /// トグルの変更イベントを解除します。
        /// </summary>
        private void OnDisable()
        {
            if (_connectionWarning != null)
            {
                Destroy(_connectionWarning);
            }

            if (_toggle == null)
            {
                return;
            }

            _toggle.onValueChanged.RemoveListener(
                OnPlayModeChanged);
        }

        /// <summary>
        /// トグルの状態が変更されたとき、
        /// 選択されたプレイモードを設定します。
        /// </summary>
        /// <param name="isOn">
        /// ソロプレイを選択した場合は<c>true</c>、
        /// オンライン対戦を選択した場合は<c>false</c>です。
        /// </param>
        private void OnPlayModeChanged(
            bool isOn)
        {
            PlayCategory category = isOn ? PlayCategory.Local : PlayCategory.Online;
            string modeId = GameModeRegistry.Catalog.TryGet(PlayModeSelection.GameModeId, category, out GameMode mode)
                ? mode.Id : GameModeRegistry.k_DefaultModeId;
            if (!PlayModeSelection.TrySelect(category, modeId))
            {
                ApplyPlayMode(PlayModeSelection.IsSoloPlay);
                if (!isOn)
                {
                    if (_connectionWarning == null)
                    {
                        _connectionWarning = LobbyConnectionWarning.Show(transform);
                    }

                    PlayModeSelection.TryReconnect();
                }

                return;
            }

            ApplyPlayMode(isOn);
        }

        /// <summary>
        /// 共通モード画面での変更をロビーの既存ボタン群にも反映します。
        /// </summary>
        private void Update()
        {
            if (_toggle != null && _toggle.isOn != PlayModeSelection.IsSoloPlay)
            {
                ApplyPlayMode(PlayModeSelection.IsSoloPlay);
            }
        }

        /// <summary>
        /// プレイモードを設定し、
        /// 選択状態に合わせてUIを更新します。
        /// </summary>
        /// <param name="isSoloPlay">
        /// ソロプレイの場合は<c>true</c>、
        /// オンライン対戦の場合は<c>false</c>です。
        /// </param>
        private void ApplyPlayMode(
            bool isSoloPlay)
        {
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.SetSoloPlayState(
                    isSoloPlay);
            }
            else
            {
                Debug.LogWarning(
                    $"{nameof(SwitchPlayMode)}: " +
                    $"{nameof(GameFlowManager)}が存在しないため、" +
                    "プレイモードの状態を保存できません。",
                    this);
            }

            RefreshDisplay(isSoloPlay);
        }

        /// <summary>
        /// 非表示のモード切り替えUIからも、ロビーのボタン群と選択表示を更新します。
        /// </summary>
        public void RefreshDisplay(bool isSoloPlay)
        {
            if (_toggle != null)
            {
                _toggle.SetIsOnWithoutNotify(isSoloPlay);
            }

            UpdateModeImages(isSoloPlay);
            UpdateButtonGroups(isSoloPlay);
        }

        /// <summary>
        /// 現在のプレイモードに合わせて
        /// 画像の色を更新します。
        /// </summary>
        /// <param name="isSoloPlay">
        /// ソロプレイの場合は<c>true</c>、
        /// オンライン対戦の場合は<c>false</c>です。
        /// </param>
        private void UpdateModeImages(
            bool isSoloPlay)
        {
            if (_soloModeImage != null)
            {
                _soloModeImage.color =
                    isSoloPlay
                        ? _backgroundOnColor
                        : _backgroundOffColor;
            }

            if (_multiModeImage != null)
            {
                _multiModeImage.color =
                    isSoloPlay
                        ? _backgroundOffColor
                        : _backgroundOnColor;
            }
        }

        /// <summary>
        /// 現在のプレイモードに合わせて
        /// ボタン群の表示状態を更新します。
        /// </summary>
        /// <param name="isSoloPlay">
        /// ソロプレイの場合は<c>true</c>、
        /// オンライン対戦の場合は<c>false</c>です。
        /// </param>
        private void UpdateButtonGroups(
            bool isSoloPlay)
        {
            SetActiveIfChanged(
                _soloModeButtonGroup,
                isSoloPlay);

            SetActiveIfChanged(
                _multiModeButtonGroup,
                !isSoloPlay);
        }

        /// <summary>
        /// オブジェクトの表示状態が異なる場合のみ、
        /// 表示状態を変更します。
        /// </summary>
        /// <param name="target">
        /// 表示状態を変更するオブジェクトです。
        /// </param>
        /// <param name="isActive">
        /// 表示する場合は<c>true</c>です。
        /// </param>
        private static void SetActiveIfChanged(
            GameObject target,
            bool isActive)
        {
            if (target == null ||
                target.activeSelf == isActive)
            {
                return;
            }

            target.SetActive(
                isActive);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspector上の参照設定を検証します。
        /// </summary>
        private void OnValidate()
        {
            if (_toggle == null)
            {
                Debug.LogWarning(
                    $"{nameof(SwitchPlayMode)}: " +
                    $"{nameof(_toggle)}が設定されていません。",
                    this);
            }

            if (_soloModeButtonGroup == null)
            {
                Debug.LogWarning(
                    $"{nameof(SwitchPlayMode)}: " +
                    $"{nameof(_soloModeButtonGroup)}が設定されていません。",
                    this);
            }

            if (_multiModeButtonGroup == null)
            {
                Debug.LogWarning(
                    $"{nameof(SwitchPlayMode)}: " +
                    $"{nameof(_multiModeButtonGroup)}が設定されていません。",
                    this);
            }
        }
#endif
    }
}
