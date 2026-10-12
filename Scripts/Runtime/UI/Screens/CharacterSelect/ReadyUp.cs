using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

using PranaTactics.Networking;
using PranaTactics.UI.Interaction;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// キャラクター選択画面における
    /// ローカルプレイヤーのReady状態を切り替えます。
    /// </summary>
    /// <remarks>
    /// オンラインキャラクター選択では、
    /// ローカルプレイヤーのネットワークプロパティを更新します。
    ///
    /// ソロキャラクター選択では、
    /// <see cref="SoloCharacterSelectUIManager"/>の
    /// 準備完了状態を更新します。
    ///
    /// キャラクターが選択されるまでは、
    /// クリックおよびマウスオーバーを許可しません。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ReadyUp :
        RepeatClickBehaviorBase,
        IClickableCondition
    {
        /// <summary>
        /// キャラクター未選択時に出力するメッセージです。
        /// </summary>
        private const string k_CharacterNotSelectedMessage =
            "キャラクターが選択されていないため、" +
            "Ready状態へ変更できません。";

        /// <summary>
        /// Ready状態を切り替えるボタン画像です。
        /// </summary>
        [Header("Appearance")]
        [FormerlySerializedAs("_readyUpButton")]
        [SerializeField]
        private Image _readyButtonImage;

        /// <summary>
        /// Readyではない状態のボタン色です。
        /// </summary>
        [FormerlySerializedAs("_baseColor")]
        [SerializeField]
        private Color _notReadyColor =
            Color.white;

        /// <summary>
        /// Ready状態のボタン色です。
        /// </summary>
        [FormerlySerializedAs("_changeColor")]
        [SerializeField]
        private Color _readyColor =
            Color.white;

        /// <summary>
        /// Ready状態の変更内容をログへ出力するかどうかです。
        /// </summary>
        [Header("Debug")]
        [SerializeField]
        private bool _enableDebugLogging;

        /// <summary>
        /// 現在UIへ反映されているReady状態です。
        /// </summary>
        private bool _displayedReadyState;

        public bool IsReady
        {
            get
            {
                return _displayedReadyState;
            }
        }

        public event Action<bool> ReadyStateChanged;

        /// <summary>
        /// 必要なUI参照を取得します。
        /// </summary>
        private void Awake()
        {
            CacheReferences();
        }

        /// <summary>
        /// コンポーネントが有効になったとき、
        /// 現在のReady状態をUIへ反映します。
        /// </summary>
        private void OnEnable()
        {
            RefreshReadyState();
        }

        /// <summary>
        /// 関連Managerの初期化後に、
        /// Ready状態を再度UIへ反映します。
        /// </summary>
        private void Start()
        {
            RefreshReadyState();
        }

        /// <summary>
        /// キャラクターが選択されている場合だけ、
        /// Readyボタンのクリックを許可します。
        /// </summary>
        /// <returns>
        /// Ready状態を変更できる場合は<c>true</c>です。
        /// </returns>
        public bool CanClick()
        {
            CharacterSelectUIManagerBase selectManager =
                CharacterSelectUIManagerBase.ActiveInstance;

            if (selectManager == null ||
                !selectManager.IsCharacterSelected)
            {
                return false;
            }

            if (selectManager is
                SoloCharacterSelectUIManager)
            {
                return true;
            }

            if (selectManager is
                CharacterSelectUIManager)
            {
                return NetworkServiceLocator.Online != null &&
                    OnlinePlayerUtility.GetLocalPlayer() != null;
            }

            return false;
        }

        /// <summary>
        /// Readyボタンがクリックされたとき、
        /// 現在のプレイモードに応じてReady状態を変更します。
        /// </summary>
        protected override void OnClickEnter()
        {
            CharacterSelectUIManagerBase selectManager =
                CharacterSelectUIManagerBase.ActiveInstance;

            if (selectManager == null)
            {
                Debug.LogWarning(
                    $"{nameof(ReadyUp)}: " +
                    $"{nameof(CharacterSelectUIManagerBase)}が" +
                    "存在しません。",
                    this);

                return;
            }

            if (!selectManager.IsCharacterSelected)
            {
                Debug.LogWarning(
                    $"{nameof(ReadyUp)}: " +
                    k_CharacterNotSelectedMessage,
                    this);

                return;
            }

            if (selectManager is
                SoloCharacterSelectUIManager soloManager)
            {
                ToggleSoloReadyState(
                    soloManager);

                return;
            }

            ToggleOnlineReadyState();
        }

        /// <summary>
        /// 現在のReady状態を取得し、
        /// ボタン表示とキャラクター選択状態へ反映します。
        /// </summary>
        public void RefreshReadyState()
        {
            CharacterSelectUIManagerBase selectManager =
                CharacterSelectUIManagerBase.ActiveInstance;

            if (selectManager is
                SoloCharacterSelectUIManager soloManager)
            {
                ApplyReadyState(
                    soloManager.IsReadySelectionLocked,
                    false);

                return;
            }

            bool isReady =
                TryGetLocalPlayer(
                    out OnlinePlayer localPlayer,
                    false) &&
                GetReadyState(
                    localPlayer);

            ApplyReadyState(
                isReady,
                false);
        }

        /// <summary>
        /// 指定されたReady状態を設定します。
        /// </summary>
        /// <param name="isReady">
        /// Ready状態にする場合は<c>true</c>です。
        /// </param>
        /// <returns>
        /// Ready状態を設定できた場合は<c>true</c>です。
        /// </returns>
        public bool SetReady(
            bool isReady)
        {
            CharacterSelectUIManagerBase selectManager =
                CharacterSelectUIManagerBase.ActiveInstance;

            if (selectManager == null)
            {
                return false;
            }

            if (isReady &&
                !selectManager.IsCharacterSelected)
            {
                Debug.LogWarning(
                    $"{nameof(ReadyUp)}: " +
                    k_CharacterNotSelectedMessage,
                    this);

                return false;
            }

            if (selectManager is
                SoloCharacterSelectUIManager soloManager)
            {
                if (isReady)
                {
                    soloManager.ConfirmSelection();
                }
                else
                {
                    soloManager.CancelConfirmation();
                }

                ApplyReadyState(
                    soloManager.IsReadySelectionLocked);

                return
                    soloManager.IsReadySelectionLocked ==
                    isReady;
            }

            if (!TryGetLocalPlayer(
                    out OnlinePlayer localPlayer))
            {
                return false;
            }

            if (!TrySetLocalReady(
                    localPlayer,
                    isReady))
            {
                return false;
            }

            ApplyReadyState(
                isReady);

            return true;
        }

        /// <summary>
        /// ソロプレイのReady状態を切り替えます。
        /// </summary>
        /// <param name="soloManager">
        /// ソロ用キャラクター選択Managerです。
        /// </param>
        private void ToggleSoloReadyState(
            SoloCharacterSelectUIManager soloManager)
        {
            bool currentReady =
                soloManager.IsReadySelectionLocked;

            bool nextReady =
                !currentReady;

            if (nextReady)
            {
                soloManager.ConfirmSelection();
            }
            else
            {
                soloManager.CancelConfirmation();
            }

            bool appliedReady =
                soloManager.IsReadySelectionLocked;

            ApplyReadyState(
                appliedReady);

            if (_enableDebugLogging)
            {
                Debug.Log(
                    $"{nameof(ReadyUp)}: " +
                    "ソロプレイのReady状態を変更しました。" +
                    $" Current={currentReady}," +
                    $" Next={appliedReady}",
                    this);
            }
        }

        /// <summary>
        /// オンラインプレイのReady状態を切り替えます。
        /// </summary>
        private void ToggleOnlineReadyState()
        {
            if (!TryGetLocalPlayer(
                    out OnlinePlayer localPlayer))
            {
                RefreshReadyState();

                return;
            }

            bool currentReady =
                GetReadyState(
                    localPlayer);

            bool nextReady =
                !currentReady;

            if (nextReady &&
                !CanBecomeReady())
            {
                Debug.LogWarning(
                    $"{nameof(ReadyUp)}: " +
                    k_CharacterNotSelectedMessage,
                    this);

                ApplyReadyVisual(
                    false);

                return;
            }

            if (!TrySetLocalReady(
                    localPlayer,
                    nextReady))
            {
                RefreshReadyState();

                return;
            }

            ApplyReadyState(
                nextReady);

            if (_enableDebugLogging)
            {
                Debug.Log(
                    $"{nameof(ReadyUp)}: " +
                    "オンラインプレイヤーのReady状態を変更しました。" +
                    $" Current={currentReady}," +
                    $" Next={nextReady}",
                    this);
            }
        }

        /// <summary>
        /// Ready状態へ変更可能かどうかを判定します。
        /// </summary>
        /// <returns>
        /// キャラクターが選択されている場合は<c>true</c>です。
        /// </returns>
        private static bool CanBecomeReady()
        {
            CharacterSelectUIManagerBase selectManager =
                CharacterSelectUIManagerBase.ActiveInstance;

            return selectManager != null &&
                selectManager.IsCharacterSelected;
        }

        /// <summary>
        /// ローカルプレイヤーを取得します。
        /// </summary>
        /// <param name="localPlayer">
        /// 取得されたローカルプレイヤーです。
        /// </param>
        /// <param name="logWarning">
        /// 取得できなかった場合に警告を出力する場合は
        /// <c>true</c>です。
        /// </param>
        /// <returns>
        /// ローカルプレイヤーを取得できた場合は
        /// <c>true</c>です。
        /// </returns>
        private bool TryGetLocalPlayer(
            out OnlinePlayer localPlayer,
            bool logWarning = true)
        {
            localPlayer =
                null;

            if (NetworkServiceLocator.Online == null)
            {
                if (logWarning)
                {
                    Debug.LogWarning(
                        $"{nameof(ReadyUp)}: " +
                        "オンラインサービスが" +
                        "初期化されていません。",
                        this);
                }

                return false;
            }

            localPlayer =
                OnlinePlayerUtility.GetLocalPlayer();

            if (localPlayer != null)
            {
                return true;
            }

            if (logWarning)
            {
                Debug.LogWarning(
                    $"{nameof(ReadyUp)}: " +
                    "ローカルプレイヤーを取得できません。",
                    this);
            }

            return false;
        }

        /// <summary>
        /// ローカルプレイヤーへReady状態を設定し、
        /// オンラインサービスへ同期を要求します。
        /// </summary>
        private static bool TrySetLocalReady(
            OnlinePlayer localPlayer,
            bool isReady)
        {
            var onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                localPlayer == null)
            {
                return false;
            }

            if (localPlayer.Properties != null)
            {
                localPlayer.Properties[
                    NetworkPropertyKeys.Ready] =
                    isReady;

                localPlayer.Properties[
                    NetworkPropertyKeys.IsReady] =
                    isReady;

                localPlayer.Properties[
                    NetworkPropertyKeys.CanStartGame] =
                    isReady;
            }

            onlineService.SetLocalPlayerProperties(
                new Dictionary<string, object>
                {
                    {
                        NetworkPropertyKeys.Ready,
                        isReady
                    },
                    {
                        NetworkPropertyKeys.IsReady,
                        isReady
                    },
                    {
                        NetworkPropertyKeys.CanStartGame,
                        isReady
                    }
                });

            return true;
        }

        /// <summary>
        /// 指定されたプレイヤーからReady状態を取得します。
        /// </summary>
        private static bool GetReadyState(
            OnlinePlayer player)
        {
            if (TryGetBoolProperty(
                    player,
                    NetworkPropertyKeys.Ready,
                    out bool ready))
            {
                return ready;
            }

            if (TryGetBoolProperty(
                    player,
                    NetworkPropertyKeys.IsReady,
                    out ready))
            {
                return ready;
            }

            return TryGetBoolProperty(
                    player,
                    NetworkPropertyKeys.CanStartGame,
                    out ready) &&
                ready;
        }

        /// <summary>
        /// Ready状態をUIと
        /// キャラクター選択Managerへ反映します。
        /// </summary>
        private void ApplyReadyState(
            bool isReady,
            bool notify = true)
        {
            bool hasChanged =
                _displayedReadyState !=
                isReady;

            _displayedReadyState =
                isReady;

            ApplyReadyVisual(
                isReady);

            CharacterSelectUIManagerBase selectManager =
                CharacterSelectUIManagerBase.ActiveInstance;

            if (selectManager != null)
            {
                selectManager.SetReadySelectionLocked(
                    isReady);
            }

            if (notify &&
                hasChanged)
            {
                ReadyStateChanged?.Invoke(
                    isReady);
            }
        }

        /// <summary>
        /// Ready状態に対応するボタン色を適用します。
        /// </summary>
        private void ApplyReadyVisual(
            bool isReady)
        {
            if (_readyButtonImage == null)
            {
                return;
            }

            _readyButtonImage.color =
                isReady
                    ? _readyColor
                    : _notReadyColor;
        }

        /// <summary>
        /// プレイヤープロパティからbool値を取得します。
        /// </summary>
        private static bool TryGetBoolProperty(
            OnlinePlayer player,
            string key,
            out bool value)
        {
            value =
                false;

            if (player == null ||
                player.Properties == null ||
                !player.Properties.TryGetValue(
                    key,
                    out object rawValue))
            {
                return false;
            }

            if (rawValue is bool boolValue)
            {
                value =
                    boolValue;

                return true;
            }

            return bool.TryParse(
                rawValue?.ToString(),
                out value);
        }

        /// <summary>
        /// Inspectorで設定されていない
        /// ボタン画像を取得します。
        /// </summary>
        private void CacheReferences()
        {
            if (_readyButtonImage == null)
            {
                _readyButtonImage =
                    GetComponent<Image>();
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspector上の参照を自動設定します。
        /// </summary>
        private void OnValidate()
        {
            CacheReferences();
        }
#endif
    }
}
