using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

using PranaTactics.Networking;
using PranaTactics.UI.Interaction;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// 入力されたルームIDとパスワードを使用して、
    /// パーティルームの作成を要求します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CreateRoom :
        RepeatClickBehaviorBase,
        IClickableCondition
    {
        /// <summary>
        /// ルームIDが未入力の場合に表示するエラーメッセージです。
        /// </summary>
        private const string k_MissingInputMessage =
            "部屋番号とパスワードを入力してください。";

        /// <summary>
        /// PartyManagerを取得できなかった場合に表示する
        /// エラーメッセージです。
        /// </summary>
        private const string k_ManagerUnavailableMessage =
            "パーティ管理を作成できませんでした。";

        /// <summary>
        /// 作成するルームIDの入力欄です。
        /// </summary>
        [Header("Input")]
        [SerializeField]
        private TMP_InputField _roomIdInputField;

        /// <summary>
        /// 作成するルームのパスワード入力欄です。
        /// </summary>
        [SerializeField]
        private TMP_InputField _passwordInputField;

        /// <summary>
        /// 入力エラーやルーム作成エラーを表示するテキストです。
        /// </summary>
        [Header("Feedback")]
        [FormerlySerializedAs("_errorTextUI")]
        [SerializeField]
        private TextMeshProUGUI _errorText;

        /// <summary>
        /// エラーメッセージへ適用する文字色です。
        /// </summary>
        [SerializeField]
        private Color _errorColor =
            Color.red;

        /// <summary>
        /// パーティ作成成功後に閉じるUIです。
        /// </summary>
        [Header("Navigation")]
        [SerializeField]
        private CloseUI _closeUI;

        /// <summary>
        /// イベントを購読しているPartyManagerです。
        /// </summary>
        private PartyManager _subscribedPartyManager;

        /// <summary>
        /// 現在、パーティルームの作成を要求中かどうかを示します。
        /// </summary>
        private bool _isCreatingRoom;

        /// <summary>
        /// 現在作成を要求しているルームIDです。
        /// </summary>
        private string _pendingRoomId =
            string.Empty;

        /// <summary>
        /// コンポーネントが有効になったとき、
        /// PartyManagerのイベントを購読します。
        /// </summary>
        private void OnEnable()
        {
            SubscribePartyEvents();
            ClearError();
        }

        /// <summary>
        /// イベント購読とクリック状態を解除します。
        /// </summary>
        protected override void OnDisable()
        {
            UnsubscribePartyEvents();

            _isCreatingRoom =
                false;

            _pendingRoomId =
                string.Empty;

            base.OnDisable();
        }

        /// <summary>
        /// 作成処理中でなければクリックを許可します。
        /// </summary>
        /// <returns>
        /// ルーム作成要求を開始できる場合は<c>true</c>です。
        /// </returns>
        public bool CanClick()
        {
            return !_isCreatingRoom;
        }

        /// <summary>
        /// 入力内容を検証し、
        /// パーティルームの作成を要求します。
        /// </summary>
        protected override void OnClickEnter()
        {
            if (_isCreatingRoom)
            {
                return;
            }

            ClearError();

            string roomId =
                GetTrimmedInput(
                    _roomIdInputField);

            string password =
                GetTrimmedInput(
                    _passwordInputField);

            if (string.IsNullOrEmpty(roomId) ||
                string.IsNullOrEmpty(password))
            {
                ShowError(
                    k_MissingInputMessage);

                return;
            }

            PartyManager partyManager =
                PartyManager.GetOrCreateInstance();

            if (partyManager == null)
            {
                ShowError(
                    k_ManagerUnavailableMessage);

                return;
            }

            SubscribePartyEvents(
                partyManager);

            _pendingRoomId =
                roomId;

            _isCreatingRoom =
                true;

            partyManager.CreateParty(
                roomId,
                password);
        }

        /// <summary>
        /// パーティルームの作成成功通知を処理します。
        /// </summary>
        private void HandlePartyCreationSucceeded()
        {
            _isCreatingRoom =
                false;

            ClearError();

            _closeUI?.Close();

            LobbyUIManager lobbyUIManager =
                LobbyUIManager.Instance;

            if (lobbyUIManager != null)
            {
                lobbyUIManager.OnCreateRoom(
                    _pendingRoomId);
            }

            _pendingRoomId =
                string.Empty;
        }

        /// <summary>
        /// パーティルームの作成失敗通知を処理します。
        /// </summary>
        /// <param name="message">
        /// 作成に失敗した理由です。
        /// </param>
        private void HandlePartyCreationFailed(
            string message)
        {
            _isCreatingRoom =
                false;

            _pendingRoomId =
                string.Empty;

            ShowError(
                string.IsNullOrWhiteSpace(message)
                    ? "パーティルームの作成に失敗しました。"
                    : message);
        }

        /// <summary>
        /// 指定された入力欄から前後の空白を除いた文字列を取得します。
        /// </summary>
        /// <param name="inputField">
        /// 文字列を取得する入力欄です。
        /// </param>
        /// <returns>
        /// 入力された文字列です。
        /// 入力欄が未設定の場合は空文字列です。
        /// </returns>
        private static string GetTrimmedInput(
            TMP_InputField inputField)
        {
            if (inputField == null)
            {
                return string.Empty;
            }

            return inputField.text?.Trim() ??
                string.Empty;
        }

        /// <summary>
        /// エラーメッセージを表示します。
        /// </summary>
        /// <param name="message">
        /// 表示するエラーメッセージです。
        /// </param>
        private void ShowError(
            string message)
        {
            if (_errorText == null)
            {
                Debug.LogWarning(
                    $"{nameof(CreateRoom)}: " +
                    $"{message}",
                    this);

                return;
            }

            _errorText.text =
                message ??
                string.Empty;

            _errorText.color =
                _errorColor;
        }

        /// <summary>
        /// 表示中のエラーメッセージを消去します。
        /// </summary>
        private void ClearError()
        {
            if (_errorText == null)
            {
                return;
            }

            _errorText.text =
                string.Empty;
        }

        /// <summary>
        /// 使用するPartyManagerを取得し、
        /// ルーム作成イベントを購読します。
        /// </summary>
        private void SubscribePartyEvents()
        {
            PartyManager partyManager =
                PartyManager.GetOrCreateInstance();

            if (partyManager == null)
            {
                return;
            }

            SubscribePartyEvents(
                partyManager);
        }

        /// <summary>
        /// 指定されたPartyManagerのイベントを購読します。
        /// </summary>
        /// <param name="partyManager">
        /// イベントを購読するPartyManagerです。
        /// </param>
        private void SubscribePartyEvents(
            PartyManager partyManager)
        {
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

            partyManager.PartyCreationFailed +=
                HandlePartyCreationFailed;

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

            _subscribedPartyManager.PartyCreationFailed -=
                HandlePartyCreationFailed;

            _subscribedPartyManager =
                null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspector上の参照設定を検証します。
        /// </summary>
        private void OnValidate()
        {
            if (_roomIdInputField == null)
            {
                Debug.LogWarning(
                    $"{nameof(CreateRoom)}: " +
                    "ルームID入力欄が設定されていません。",
                    this);
            }

            if (_passwordInputField == null)
            {
                Debug.LogWarning(
                    $"{nameof(CreateRoom)}: " +
                    "パスワード入力欄が設定されていません。",
                    this);
            }
        }
#endif
    }
}
