using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

using PranaTactics.Networking;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// ソロまたはパーティ単位のマッチング開始、
    /// Ready状態およびキャンセル操作を管理します。
    /// </summary>
    /// <remarks>
    /// パーティルームに参加している場合は、
    /// クリックによってローカルプレイヤーのReady状態を切り替えます。
    ///
    /// パーティルームに参加していない場合は、
    /// ソロマッチングの開始またはキャンセルを行います。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class Matchmaking : MonoBehaviour
    {
        /// <summary>
        /// 通常状態で表示するボタンテキストです。
        /// </summary>
        private const string k_IdleButtonText =
            "Play";

        /// <summary>
        /// パーティ内でReady状態になったときに表示するテキストです。
        /// </summary>
        private const string k_ReadyButtonText =
            "Play";

        /// <summary>
        /// マッチング中に表示するボタンテキストです。
        /// </summary>
        private const string k_MatchingButtonText =
            "Matching ...";

        /// <summary>
        /// マッチング操作ボタンの背景画像です。
        /// </summary>
        [Header("Appearance")]
        [FormerlySerializedAs("_matchMakingButton")]
        [SerializeField]
        private Image _matchmakingButtonImage;

        /// <summary>
        /// マッチング状態を表示するテキストです。
        /// </summary>
        [FormerlySerializedAs("_text")]
        [SerializeField]
        private TextMeshProUGUI _buttonText;

        /// <summary>
        /// マッチングしていないときのボタン色です。
        /// </summary>
        [FormerlySerializedAs("_baseColor")]
        [SerializeField]
        private Color _idleColor =
            Color.white;

        /// <summary>
        /// Ready状態またはマッチング中のボタン色です。
        /// </summary>
        [FormerlySerializedAs("_changeColor")]
        [SerializeField]
        private Color _activeColor =
            Color.white;

        /// <summary>
        /// 現在のマッチングボタン状態です。
        /// </summary>
        private MatchmakingButtonState _currentState =
            MatchmakingButtonState.Idle;

        /// <summary>
        /// イベントを購読しているPartyManagerです。
        /// </summary>
        private PartyManager _subscribedPartyManager;

        /// <summary>
        /// イベントを購読しているMatchmakingManagerです。
        /// </summary>
        private MatchmakingManager _subscribedMatchmakingManager;

        public bool IsReady
        {
            get
            {
                return _currentState ==
                    MatchmakingButtonState.Ready;
            }
        }

        public bool IsMatching
        {
            get
            {
                return _currentState ==
                    MatchmakingButtonState.Matching;
            }
        }

        public bool CanToggleMatching =>
            (MatchmakingManager.Instance == null ||
                (!MatchmakingManager.Instance.IsCancelingMatching() && MatchmakingManager.Instance.CanControlMatching)) &&
            (!IsInPartyRoom() || (NetworkServiceLocator.Online != null && NetworkServiceLocator.Online.IsHost));

        /// <summary>
        /// 必要なUI参照を取得し、
        /// 初期表示を適用します。
        /// </summary>
        private void Awake()
        {
            CacheReferences();
            ApplyCurrentState();
        }

        /// <summary>
        /// PartyManagerおよびMatchmakingManagerの
        /// イベントを購読します。
        /// </summary>
        private void OnEnable()
        {
            SubscribePartyEvents();
            SubscribeMatchmakingEvents();
            ApplyCurrentState();
        }

        /// <summary>
        /// 遅れて生成された管理クラスを購読し、共有された開始状態を表示します。
        /// </summary>
        private void Update()
        {
            SubscribePartyEvents();
            SubscribeMatchmakingEvents();
            MatchmakingManager manager = MatchmakingManager.Instance;
            if (manager != null && manager.IsMatching && !IsMatching)
            {
                ApplyMatchingState();
            }
            else if (manager != null && !manager.IsMatching &&
                !manager.IsCancelingMatching() && IsMatching)
            {
                ApplyIdleState();
            }
        }

        /// <summary>
        /// UIイベントの購読を解除します。
        /// </summary>
        private void OnDisable()
        {
            UnsubscribePartyEvents();
            UnsubscribeMatchmakingEvents();

        }

        /// <summary>
        /// 現在のルーム参加状態とボタン状態に応じて、
        /// Readyまたはマッチング状態を切り替えます。
        /// </summary>
        public void ToggleMatching()
        {
            if (!CanToggleMatching)
            {
                return;
            }

            if (IsInPartyRoom())
            {
                TogglePartyReady();

                return;
            }

            ToggleSoloMatching();
        }

        /// <summary>
        /// パーティルーム内のReady状態を切り替えます。
        /// </summary>
        private void TogglePartyReady()
        {
            PartyManager partyManager =
                PartyManager.GetOrCreateInstance();

            if (partyManager == null)
            {
                Debug.LogError(
                    $"{nameof(Matchmaking)}: " +
                    $"{nameof(PartyManager)}を取得できないため、" +
                    "Ready状態を変更できません。",
                    this);

                ApplyIdleState();

                return;
            }

            SubscribePartyEvents(
                partyManager);

            bool shouldBeReady =
                _currentState ==
                MatchmakingButtonState.Idle;

            partyManager.SetReady(
                shouldBeReady);
        }

        /// <summary>
        /// ソロまたはパーティ外のマッチング状態を切り替えます。
        /// </summary>
        private void ToggleSoloMatching()
        {
            if (_currentState ==
                MatchmakingButtonState.Matching)
            {
                CancelSoloMatching();

                return;
            }

            StartSoloMatching();
        }

        /// <summary>
        /// ソロまたはパーティ外のマッチングを開始します。
        /// </summary>
        private void StartSoloMatching()
        {
            MatchmakingManager matchmakingManager =
                MatchmakingManager.Instance;

            if (matchmakingManager == null)
            {
                Debug.LogError(
                    $"{nameof(Matchmaking)}: " +
                    $"{nameof(MatchmakingManager)}が存在しないため、" +
                    "マッチングを開始できません。",
                    this);

                ApplyIdleState();

                return;
            }

            SubscribeMatchmakingEvents(
                matchmakingManager);

            bool started =
                matchmakingManager.StartMatching();

            if (!started)
            {
                ApplyIdleState();

                return;
            }

            ApplyMatchingState();
        }

        /// <summary>
        /// ソロまたはパーティ外のマッチングをキャンセルします。
        /// </summary>
        private void CancelSoloMatching()
        {
            MatchmakingManager matchmakingManager =
                MatchmakingManager.Instance;

            if (matchmakingManager == null)
            {
                ApplyIdleState();

                return;
            }

            SubscribeMatchmakingEvents(
                matchmakingManager);

            matchmakingManager.CancelMatching();
        }

        /// <summary>
        /// パーティ単位のマッチング開始通知を処理します。
        /// </summary>
        private void HandlePartyMatchingStarted()
        {
            ApplyMatchingState();
        }

        /// <summary>
        /// パーティ単位のマッチングキャンセル通知を処理します。
        /// </summary>
        private void HandlePartyMatchingCanceled()
        {
            ApplyIdleState();
        }

        /// <summary>
        /// ローカルプレイヤーのReady変更通知を処理します。
        /// </summary>
        /// <param name="isReady">
        /// Ready状態になった場合は<c>true</c>です。
        /// </param>
        private void HandleLocalReadyChanged(
            bool isReady)
        {
            if (!isReady)
            {
                ApplyIdleState();

                return;
            }

            if (_currentState ==
                MatchmakingButtonState.Matching)
            {
                return;
            }

            ApplyReadyState();
        }

        /// <summary>
        /// ソロマッチングのキャンセル通知を処理します。
        /// </summary>
        private void HandleMatchingCanceled()
        {
            ApplyIdleState();
        }

        /// <summary>
        /// ボタンをReady状態の表示へ変更します。
        /// </summary>
        private void ApplyReadyState()
        {
            _currentState =
                MatchmakingButtonState.Ready;

            SetLobbyMatchingFlag(
                false);

            ApplyButtonAppearance(
                _activeColor,
                k_ReadyButtonText);
        }

        /// <summary>
        /// ボタンをマッチング中の表示へ変更します。
        /// </summary>
        private void ApplyMatchingState()
        {
            _currentState =
                MatchmakingButtonState.Matching;

            SetLobbyMatchingFlag(
                true);

            ApplyButtonAppearance(
                _activeColor,
                k_MatchingButtonText);
        }

        /// <summary>
        /// ボタンを通常状態の表示へ戻します。
        /// </summary>
        private void ApplyIdleState()
        {
            _currentState =
                MatchmakingButtonState.Idle;

            SetLobbyMatchingFlag(
                false);

            ApplyButtonAppearance(
                _idleColor,
                k_IdleButtonText);
        }

        /// <summary>
        /// 現在のボタン状態に対応する表示を再適用します。
        /// </summary>
        private void ApplyCurrentState()
        {
            switch (_currentState)
            {
                case MatchmakingButtonState.Ready:
                    ApplyReadyState();
                    break;

                case MatchmakingButtonState.Matching:
                    ApplyMatchingState();
                    break;

                default:
                    ApplyIdleState();
                    break;
            }
        }

        /// <summary>
        /// ボタンの色とテキストを変更します。
        /// </summary>
        /// <param name="color">
        /// ボタンへ適用する色です。
        /// </param>
        /// <param name="text">
        /// ボタンへ表示する文字列です。
        /// </param>
        private void ApplyButtonAppearance(
            Color color,
            string text)
        {
            if (_matchmakingButtonImage != null)
            {
                _matchmakingButtonImage.color =
                    color;
            }

            if (_buttonText != null)
            {
                _buttonText.text =
                    text;
            }
        }

        /// <summary>
        /// 現在パーティルームへ参加しているかどうかを判定します。
        /// </summary>
        /// <returns>
        /// パーティルームへ参加中の場合は<c>true</c>です。
        /// </returns>
        private static bool IsInPartyRoom()
        {
            if (!TryGetCurrentRoomString(
                    NetworkPropertyKeys.Type,
                    out string roomType))
            {
                return false;
            }

            return string.Equals(
                roomType,
                NetworkPropertyKeys.TypeParty,
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 現在のルームから指定されたプロパティを文字列として取得します。
        /// </summary>
        /// <param name="key">
        /// 取得するルームプロパティのキーです。
        /// </param>
        /// <param name="value">
        /// 取得された文字列です。
        /// </param>
        /// <returns>
        /// プロパティを取得できた場合は<c>true</c>です。
        /// </returns>
        private static bool TryGetCurrentRoomString(
            string key,
            out string value)
        {
            value =
                string.Empty;

            var onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                !onlineService.IsInRoom ||
                onlineService.CurrentRoom == null)
            {
                return false;
            }

            if (onlineService.CurrentRoom.TryGetProperty(
                    key,
                    out string directValue))
            {
                value =
                    directValue ??
                    string.Empty;

                return true;
            }

            if (onlineService.CurrentRoom.Properties == null)
            {
                return false;
            }

            foreach (var property in
                    onlineService.CurrentRoom.Properties)
            {
                if (!string.Equals(
                        property.Key,
                        key,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                value =
                    property.Value?.ToString() ??
                    string.Empty;

                return true;
            }

            return false;
        }

        /// <summary>
        /// LobbyUIManagerへ現在のマッチング状態を通知します。
        /// </summary>
        /// <param name="isMatching">
        /// マッチング中の場合は<c>true</c>です。
        /// </param>
        private static void SetLobbyMatchingFlag(
            bool isMatching)
        {
            LobbyUIManager lobbyUIManager =
                LobbyUIManager.Instance;

            if (lobbyUIManager == null)
            {
                return;
            }

            lobbyUIManager.SetIsMatchmaking(
                isMatching);
        }

        /// <summary>
        /// PartyManagerのイベントを購読します。
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

            partyManager.PartyMatchingStarted +=
                HandlePartyMatchingStarted;

            partyManager.PartyMatchingCanceled +=
                HandlePartyMatchingCanceled;

            partyManager.LocalReadyChanged +=
                HandleLocalReadyChanged;

            _subscribedPartyManager =
                partyManager;
        }

        /// <summary>
        /// PartyManagerのイベント購読を解除します。
        /// </summary>
        private void UnsubscribePartyEvents()
        {
            if (_subscribedPartyManager != null)
            {
                _subscribedPartyManager.PartyMatchingStarted -=
                    HandlePartyMatchingStarted;

                _subscribedPartyManager.PartyMatchingCanceled -=
                    HandlePartyMatchingCanceled;

                _subscribedPartyManager.LocalReadyChanged -=
                    HandleLocalReadyChanged;
            }

            _subscribedPartyManager =
                null;
        }

        /// <summary>
        /// MatchmakingManagerのイベントを購読します。
        /// </summary>
        private void SubscribeMatchmakingEvents()
        {
            MatchmakingManager matchmakingManager =
                MatchmakingManager.Instance;

            if (matchmakingManager == null)
            {
                return;
            }

            SubscribeMatchmakingEvents(
                matchmakingManager);
        }

        /// <summary>
        /// 指定されたMatchmakingManagerのイベントを購読します。
        /// </summary>
        /// <param name="matchmakingManager">
        /// イベントを購読するMatchmakingManagerです。
        /// </param>
        private void SubscribeMatchmakingEvents(
            MatchmakingManager matchmakingManager)
        {
            if (matchmakingManager == null ||
                ReferenceEquals(
                    _subscribedMatchmakingManager,
                    matchmakingManager))
            {
                return;
            }

            UnsubscribeMatchmakingEvents();

            matchmakingManager.MatchingCanceled +=
                HandleMatchingCanceled;

            _subscribedMatchmakingManager =
                matchmakingManager;
        }

        /// <summary>
        /// MatchmakingManagerのイベント購読を解除します。
        /// </summary>
        private void UnsubscribeMatchmakingEvents()
        {
            if (_subscribedMatchmakingManager != null)
            {
                _subscribedMatchmakingManager.MatchingCanceled -=
                    HandleMatchingCanceled;
            }

            _subscribedMatchmakingManager =
                null;
        }

        /// <summary>
        /// Inspectorで設定されていないUI参照を可能な範囲で取得します。
        /// </summary>
        private void CacheReferences()
        {
            if (_matchmakingButtonImage == null)
            {
                _matchmakingButtonImage =
                    GetComponent<Image>();
            }

            if (_buttonText == null)
            {
                _buttonText =
                    GetComponentInChildren<TextMeshProUGUI>(
                        true);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspector上のUI参照を自動設定します。
        /// </summary>
        private void OnValidate()
        {
            CacheReferences();
        }

#endif

        private enum MatchmakingButtonState
        {
            Idle = 0,

            Ready = 1,

            Matching = 2
        }
    }
}
