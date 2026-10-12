using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.Serialization;
using UnityEngine.UI;

using PranaTactics.Networking;
using PranaTactics.UI.Interaction;
using PranaTactics.UI.Screens;

namespace PranaTactics.UI
{
    /// <summary>
    /// パーティルームの作成・参加ポップアップと、
    /// パーティルーム退出ボタンの表示を管理します。
    /// </summary>
    /// <remarks>
    /// パーティルームへ参加していない場合は、
    /// クリックするたびにポップアップの表示状態を切り替えます。
    ///
    /// パーティルームへ参加している場合は、
    /// クリック時に現在のルームから退出します。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PopupUI :
        RepeatClickBehaviorBase,
        IClickableCondition
    {
        /// <summary>
        /// 作成・参加ボタンのローカライズキーです。
        /// </summary>
        private const string k_CreateAndJoinRoomButtonKey =
            "CreateAndJoinRoomButton";

        /// <summary>
        /// ローカライズ文字列を取得できなかった場合に使用する
        /// 作成・参加ボタンの既定表示です。
        /// </summary>
        private const string k_DefaultButtonText =
            "ルームを作成・参加";

        /// <summary>
        /// UIテキストを保持するローカライズテーブルです。
        /// </summary>
        [Header("Localization")]
        [FormerlySerializedAs("_UITable")]
        [SerializeField]
        private LocalizedStringTable _uiTable;

        /// <summary>
        /// 読み込み済みのUI文字列テーブルです。
        /// </summary>
        private StringTable _uiTextTable;

        /// <summary>
        /// 表示状態を切り替えるルーム作成・参加ポップアップです。
        /// </summary>
        [Header("Popup")]
        [FormerlySerializedAs("_popUpUI")]
        [SerializeField]
        private GameObject _popupUI;

        /// <summary>
        /// ルームIDやパスワードを入力するフィールドです。
        /// </summary>
        [FormerlySerializedAs("_inputField")]
        [SerializeField]
        private TMP_InputField _roomInputField;

        /// <summary>
        /// ポップアップに表示するタイトルです。
        /// </summary>
        [FormerlySerializedAs("_title")]
        [SerializeField]
        private TextMeshProUGUI _titleText;

        /// <summary>
        /// 作成・参加ボタンの背景画像です。
        /// </summary>
        [Header("Button Appearance")]
        [FormerlySerializedAs("_buttonImage")]
        [SerializeField]
        private Image _buttonImage;

        /// <summary>
        /// 作成・参加ボタンに表示するテキストです。
        /// </summary>
        [FormerlySerializedAs("_text")]
        [SerializeField]
        private TextMeshProUGUI _buttonText;

        /// <summary>
        /// パーティルームへ参加していないときのボタン色です。
        /// </summary>
        [FormerlySerializedAs("_baseColor")]
        [SerializeField]
        private Color _defaultButtonColor =
            Color.white;

        /// <summary>
        /// パーティルームへ参加しているときのボタン色です。
        /// </summary>
        [FormerlySerializedAs("_changeColor")]
        [SerializeField]
        private Color _partyButtonColor =
            Color.white;

        /// <summary>
        /// 通常状態のボタン文字サイズです。
        /// </summary>
        [FormerlySerializedAs("_baseFontSize")]
        [Min(1)]
        [SerializeField]
        private int _defaultFontSize =
            40;

        /// <summary>
        /// パーティルーム参加中のボタン文字サイズです。
        /// </summary>
        [FormerlySerializedAs("_changeFontSize")]
        [Min(1)]
        [SerializeField]
        private int _partyFontSize =
            25;

        /// <summary>
        /// イベントを購読しているPartyManagerです。
        /// </summary>
        private PartyManager _subscribedPartyManager;

        /// <summary>
        /// ローカライズテーブルを取得します。
        /// </summary>
        private void Awake()
        {
            LoadLocalizationTable();
        }

        /// <summary>
        /// パーティルーム関連イベントを購読し、
        /// 現在のルーム状態をUIへ反映します。
        /// </summary>
        private void OnEnable()
        {
            SubscribePartyEvents();
            RefreshButtonAppearance();
        }

        /// <summary>
        /// 他のマネージャーの初期化完了後に、
        /// イベント購読と表示状態を再確認します。
        /// </summary>
        private void Start()
        {
            SubscribePartyEvents();
            RefreshButtonAppearance();
        }

        /// <summary>
        /// パーティルーム関連イベントの購読を解除し、
        /// クリック状態とポップアップ表示を終了します。
        /// </summary>
        protected override void OnDisable()
        {
            UnsubscribePartyEvents();

            base.OnDisable();

            ClosePopup();
        }

        /// <summary>
        /// ボタンがクリックされたとき、
        /// パーティルームへの参加状態に応じた処理を実行します。
        /// </summary>
        protected override void OnClickEnter()
        {
            if (IsMatchmaking())
            {
                return;
            }

            if (IsInPartyRoom())
            {
                LeaveCurrentRoom();

                return;
            }

            TogglePopup();
        }

        /// <summary>
        /// クリック状態が終了したとき、
        /// 作成・参加ポップアップを閉じます。
        /// </summary>
        protected override void OnClickExit()
        {
            ClosePopup();
        }

        /// <summary>
        /// マッチング中でなければクリックを許可します。
        /// </summary>
        /// <returns>
        /// クリック可能な場合は<c>true</c>です。
        /// </returns>
        public bool CanClick()
        {
            return !IsMatchmaking();
        }

        /// <summary>
        /// ポップアップのタイトルを設定します。
        /// </summary>
        /// <param name="title">
        /// 表示するタイトルです。
        /// </param>
        public void SetTitle(
            string title)
        {
            if (_titleText == null)
            {
                return;
            }

            _titleText.text =
                title ??
                string.Empty;
        }

        /// <summary>
        /// ルーム作成完了通知を受け取り、
        /// パーティルーム情報を表示します。
        /// </summary>
        /// <param name="roomName">
        /// 作成されたルーム名です。
        /// UnityEventとの互換性維持のため受け取ります。
        /// </param>
        public void OnCreateRoom(
            string roomName)
        {
            _ = roomName;

            HandlePartyRoomEntered();
        }

        /// <summary>
        /// ルーム参加完了通知を受け取り、
        /// パーティルーム情報を表示します。
        /// </summary>
        /// <param name="roomName">
        /// 参加したルーム名です。
        /// UnityEventとの互換性維持のため受け取ります。
        /// </param>
        public void OnJoinRoom(
            string roomName)
        {
            _ = roomName;

            HandlePartyRoomEntered();
        }

        /// <summary>
        /// ルーム退出後の通常表示へ戻します。
        /// </summary>
        public void OnLeaveRoom()
        {
            ClosePopup();
            ShowDefaultButtonAppearance();
        }

        /// <summary>
        /// パーティルームの作成成功イベントを処理します。
        /// </summary>
        private void HandlePartyCreationSucceeded()
        {
            HandlePartyRoomEntered();
        }

        /// <summary>
        /// パーティルームへの参加成功イベントを処理します。
        /// </summary>
        private void HandlePartyJoinSucceeded()
        {
            HandlePartyRoomEntered();
        }

        /// <summary>
        /// パーティルーム参加後の表示更新を行います。
        /// </summary>
        private void HandlePartyRoomEntered()
        {
            if (IsMatchmaking() ||
                !IsInPartyRoom())
            {
                return;
            }

            ClosePopup();
            ShowPartyRoomInformation();
        }

        /// <summary>
        /// ポップアップの表示状態を切り替えます。
        /// </summary>
        private void TogglePopup()
        {
            if (_popupUI == null)
            {
                Debug.LogWarning(
                    $"{nameof(PopupUI)}: " +
                    "表示するポップアップが設定されていません。",
                    this);

                return;
            }

            if (_popupUI.activeSelf)
            {
                ClosePopup();

                return;
            }

            OpenPopup();
        }

        /// <summary>
        /// ルーム作成・参加ポップアップを表示します。
        /// </summary>
        private void OpenPopup()
        {
            if (_popupUI == null)
            {
                return;
            }

            _popupUI.SetActive(
                true);

            if (_roomInputField != null)
            {
                _roomInputField.SetTextWithoutNotify(
                    string.Empty);
            }
        }

        /// <summary>
        /// ルーム作成・参加ポップアップを非表示にします。
        /// </summary>
        private void ClosePopup()
        {
            if (_popupUI == null ||
                !_popupUI.activeSelf)
            {
                return;
            }

            _popupUI.SetActive(
                false);
        }

        /// <summary>
        /// 現在参加しているオンラインルームから退出します。
        /// </summary>
        private void LeaveCurrentRoom()
        {
            var onlineService =
                NetworkServiceLocator.Online;

            if (onlineService != null &&
                onlineService.IsInRoom)
            {
                onlineService.LeaveRoom();
            }

            OnLeaveRoom();
        }

        /// <summary>
        /// 現在パーティルームへ参加しているかどうかを判定します。
        /// </summary>
        /// <returns>
        /// パーティルームへ参加中の場合は<c>true</c>です。
        /// </returns>
        private static bool IsInPartyRoom()
        {
            var onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                !onlineService.IsInRoom ||
                onlineService.CurrentRoom == null)
            {
                return false;
            }

            if (!onlineService.CurrentRoom.TryGetProperty(
                    NetworkPropertyKeys.Type,
                    out string roomType))
            {
                return false;
            }

            return roomType ==
                NetworkPropertyKeys.TypeParty;
        }

        /// <summary>
        /// 現在マッチング中かどうかを判定します。
        /// </summary>
        /// <returns>
        /// マッチング中の場合は<c>true</c>です。
        /// </returns>
        private static bool IsMatchmaking()
        {
            LobbyUIManager lobbyUIManager =
                LobbyUIManager.Instance;

            return lobbyUIManager != null &&
                lobbyUIManager.GetIsMatchiMaking();
        }

        /// <summary>
        /// 現在のルーム参加状態をボタン表示へ反映します。
        /// </summary>
        private void RefreshButtonAppearance()
        {
            if (IsInPartyRoom())
            {
                ShowPartyRoomInformation();

                return;
            }

            ShowDefaultButtonAppearance();
        }

        /// <summary>
        /// パーティルームのIDとパスワードをボタンへ表示します。
        /// </summary>
        private void ShowPartyRoomInformation()
        {
            PartyManager partyManager =
                PartyManager.Instance;

            if (partyManager == null)
            {
                return;
            }

            string roomId =
                string.IsNullOrEmpty(
                    partyManager.RoomId)
                        ? "-"
                        : partyManager.RoomId;

            string password =
                string.IsNullOrEmpty(
                    partyManager.Password)
                        ? "-"
                        : partyManager.Password;

            if (_buttonText != null)
            {
                _buttonText.fontSize =
                    _partyFontSize;

                _buttonText.text =
                    $"ルームID: {roomId}\n" +
                    $"パスワード: {password} から退出";
            }

            if (_buttonImage != null)
            {
                _buttonImage.color =
                    _partyButtonColor;
            }
        }

        /// <summary>
        /// ボタン表示をルーム作成・参加前の状態へ戻します。
        /// </summary>
        private void ShowDefaultButtonAppearance()
        {
            if (_buttonText != null)
            {
                _buttonText.fontSize =
                    _defaultFontSize;

                _buttonText.text =
                    GetLocalizedText(
                        k_CreateAndJoinRoomButtonKey,
                        k_DefaultButtonText);
            }

            if (_buttonImage != null)
            {
                _buttonImage.color =
                    _defaultButtonColor;
            }
        }

        /// <summary>
        /// UI文字列テーブルを同期的に取得します。
        /// </summary>
        private void LoadLocalizationTable()
        {
            if (_uiTable == null)
            {
                _uiTextTable = null;

                return;
            }

            _uiTextTable =
                _uiTable.GetTable();
        }

        /// <summary>
        /// 指定されたキーに対応するローカライズ文字列を取得します。
        /// </summary>
        /// <param name="key">
        /// ローカライズテーブルのキーです。
        /// </param>
        /// <param name="fallback">
        /// 文字列を取得できなかった場合の既定値です。
        /// </param>
        /// <returns>
        /// ローカライズ済みの文字列です。
        /// </returns>
        private string GetLocalizedText(
            string key,
            string fallback)
        {
            if (_uiTextTable == null)
            {
                LoadLocalizationTable();
            }

            if (_uiTextTable == null)
            {
                return fallback;
            }

            StringTableEntry entry =
                _uiTextTable[key];

            if (entry == null ||
                string.IsNullOrEmpty(
                    entry.Value))
            {
                return fallback;
            }

            return entry.Value;
        }

        /// <summary>
        /// PartyManagerのルーム作成・参加成功イベントを購読します。
        /// </summary>
        private void SubscribePartyEvents()
        {
            PartyManager partyManager =
                PartyManager.Instance;

            if (partyManager == null ||
                ReferenceEquals(
                    _subscribedPartyManager,
                    partyManager))
            {
                return;
            }

            UnsubscribePartyEvents();

            partyManager.PartyCreationSucceeded +=
                HandlePartyCreationSucceeded;

            partyManager.PartyJoinSucceeded +=
                HandlePartyJoinSucceeded;

            _subscribedPartyManager =
                partyManager;
        }

        /// <summary>
        /// PartyManagerのイベント購読を解除します。
        /// </summary>
        private void UnsubscribePartyEvents()
        {
            if (_subscribedPartyManager == null)
            {
                return;
            }

            _subscribedPartyManager.PartyCreationSucceeded -=
                HandlePartyCreationSucceeded;

            _subscribedPartyManager.PartyJoinSucceeded -=
                HandlePartyJoinSucceeded;

            _subscribedPartyManager =
                null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorから設定された値を有効範囲へ補正します。
        /// </summary>
        private void OnValidate()
        {
            _defaultFontSize =
                Mathf.Max(
                    1,
                    _defaultFontSize);

            _partyFontSize =
                Mathf.Max(
                    1,
                    _partyFontSize);
        }
#endif
    }
}
