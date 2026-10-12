using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

using PranaTactics.Networking;
using PranaTactics.UI.Interaction;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// 入力されたルームIDとパスワードを使用して、
    /// パーティルームへの参加を要求します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JoinRoom :
        RepeatClickBehaviorBase,
        IClickableCondition
    {
        /// <summary>
        /// ルーム情報が未入力の場合に表示するメッセージです。
        /// </summary>
        private const string k_MissingInputMessage =
            "部屋番号とパスワードを入力してください。";

        /// <summary>
        /// PartyManagerを取得できなかった場合に表示するメッセージです。
        /// </summary>
        private const string k_ManagerUnavailableMessage =
            "パーティ管理を作成できませんでした。";

        /// <summary>
        /// ルーム参加に失敗した際の既定メッセージです。
        /// </summary>
        private const string k_DefaultJoinFailureMessage =
            "パーティルームへの参加に失敗しました。";

        /// <summary>
        /// 参加するルームIDの入力欄です。
        /// </summary>
        [Header("Input")]
        [SerializeField]
        private TMP_InputField _roomIdInputField;

        /// <summary>
        /// 参加するルームのパスワード入力欄です。
        /// </summary>
        [SerializeField]
        private TMP_InputField _passwordInputField;

        /// <summary>
        /// 入力エラーやルーム参加エラーを表示するテキストです。
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
        /// パーティ参加成功後に閉じるUIです。
        /// </summary>
        [Header("Navigation")]
        [SerializeField]
        private CloseUI _closeUI;

        /// <summary>
        /// イベントを購読しているPartyManagerです。
        /// </summary>
        private PartyManager _subscribedPartyManager;

        /// <summary>
        /// 現在、パーティルームへの参加を要求中かどうかを示します。
        /// </summary>
        private bool _isJoiningRoom;

        /// <summary>
        /// 現在参加を要求しているルームIDです。
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

            _isJoiningRoom =
                false;

            _pendingRoomId =
                string.Empty;

            base.OnDisable();
        }

        /// <summary>
        /// パーティルームへの参加処理中でなければ、
        /// クリックを許可します。
        /// </summary>
        /// <returns>
        /// 参加要求を開始できる場合は<c>true</c>です。
        /// </returns>
        public bool CanClick()
        {
            return !_isJoiningRoom;
        }

        /// <summary>
        /// 入力内容を検証し、
        /// パーティルームへの参加を要求します。
        /// </summary>
        protected override void OnClickEnter()
        {
            if (_isJoiningRoom)
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

            _isJoiningRoom =
                true;

            partyManager.JoinParty(
                roomId,
                password);
        }

        /// <summary>
        /// パーティルームへの参加成功通知を処理します。
        /// </summary>
        private void HandlePartyJoinSucceeded()
        {
            _isJoiningRoom =
                false;

            ClearError();

            _closeUI?.Close();

            LobbyUIManager lobbyUIManager =
                LobbyUIManager.Instance;

            if (lobbyUIManager != null)
            {
                lobbyUIManager.OnJoinRoom(
                    _pendingRoomId);
            }

            _pendingRoomId =
                string.Empty;
        }

        /// <summary>
        /// パーティルームへの参加失敗通知を処理します。
        /// </summary>
        /// <param name="message">
        /// 参加に失敗した理由です。
        /// </param>
        private void HandlePartyJoinFailed(
            string message)
        {
            _isJoiningRoom =
                false;

            _pendingRoomId =
                string.Empty;

            ShowError(
                string.IsNullOrWhiteSpace(message)
                    ? k_DefaultJoinFailureMessage
                    : message);
        }

        /// <summary>
        /// 指定された入力欄から、
        /// 前後の空白を除いた文字列を取得します。
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
            string validMessage =
                message ??
                string.Empty;

            if (_errorText == null)
            {
                Debug.LogWarning(
                    $"{nameof(JoinRoom)}: {validMessage}",
                    this);

                return;
            }

            _errorText.text =
                validMessage;

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
        /// ルーム参加イベントを購読します。
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

            partyManager.PartyJoinSucceeded +=
                HandlePartyJoinSucceeded;

            partyManager.PartyJoinFailed +=
                HandlePartyJoinFailed;

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

            _subscribedPartyManager.PartyJoinSucceeded -=
                HandlePartyJoinSucceeded;

            _subscribedPartyManager.PartyJoinFailed -=
                HandlePartyJoinFailed;

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
                    $"{nameof(JoinRoom)}: " +
                    "ルームID入力欄が設定されていません。",
                    this);
            }

            if (_passwordInputField == null)
            {
                Debug.LogWarning(
                    $"{nameof(JoinRoom)}: " +
                    "パスワード入力欄が設定されていません。",
                    this);
            }
        }
#endif
    }
}
