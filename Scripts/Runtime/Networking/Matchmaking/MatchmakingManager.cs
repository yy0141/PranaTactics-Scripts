using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Serialization;

using PranaTactics.GameFlow;
using PranaTactics.GameFlow.States;
using PranaTactics.Match.Modes;

namespace PranaTactics.Networking
{
    /// <summary>
    /// パーティルームからのマッチング開始、マッチングルームへの参加、
    /// プレイヤーの準備完了確認、チーム編成およびキャンセル処理を管理します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchmakingManager : MonoBehaviour
    {
        /// <summary>
        /// 開始通知と同時に公開するチーム編成です。
        /// </summary>
        private readonly Dictionary<string, object> _assignedTeamProperties = new();

        /// <summary>
        /// 参加、再検索、再試行を通して固定する今回のゲームルールIDです。
        /// </summary>
        private string _matchingGameModeId = GameModeRegistry.k_DefaultModeId;

        /// <summary>
        /// ソロプレイヤーのパーティIDに使用する接頭辞です。
        /// </summary>
        private const string k_SoloPartyIdPrefix = "solo_";

        /// <summary>
        /// EOS Product User IDを取得するプロパティキーです。
        /// </summary>
        private const string k_ProductUserIdPropertyKey = "productUserId";

        /// <summary>
        /// チームAの識別子です。
        /// </summary>
        private const int k_TeamAId = 0;

        /// <summary>
        /// チームBの識別子です。
        /// </summary>
        private const int k_TeamBId = 1;

        /// <summary>
        /// チームが未割り当てであることを示す値です。
        /// </summary>
        private const int k_UnassignedTeamId = -1;

        /// <summary>
        /// ルーム退出を待機する最大秒数です。
        /// </summary>
        private const float k_RoomLeaveTimeoutSeconds = 10.0f;

        /// <summary>
        /// マッチングLobbyの再検索要求を共有する
        /// ルームプロパティキーです。
        /// </summary>
        private const string k_MatchingRequeueTokenPropertyKey =
            "matchingRequeueToken";

        /// <summary>
        /// 4桁のルームIDを検証する正規表現です。
        /// </summary>
        private static readonly Regex s_RoomIdRegex =
            new(
                @"^\d{4}$",
                RegexOptions.CultureInvariant);

        /// <summary>
        /// 4桁のパスワードを検証する正規表現です。
        /// </summary>
        private static readonly Regex s_PasswordRegex =
            new(
                @"^\d{4}$",
                RegexOptions.CultureInvariant);

        /// <summary>
        /// マッチへ参加する人間パーティの数です。
        /// </summary>
        [Header("Match Settings")]
        [Range(1, 2)]
        [SerializeField]
        private int _partyCount = 2;

        /// <summary>
        /// 1つのパーティに参加するプレイヤー数です。
        /// </summary>
        [Range(1, 3)]
        [SerializeField]
        private int _partySize = 1;

        /// <summary>
        /// パーティルームから退出する前に待機する秒数です。
        /// </summary>
        [Header("Transition Delays")]
        [Min(0.0f)]
        [SerializeField]
        private float _partyLobbyToMatchingDelay = 1.0f;

        /// <summary>
        /// パーティのクライアントがマッチングルームへ
        /// 参加する前に待機する秒数です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _partyClientMatchingJoinDelay = 2.0f;

        /// <summary>
        /// 敵パーティがいないマッチングLobbyで待機する秒数です。
        /// </summary>
        [Header("Matching Requeue")]
        [Min(1.0f)]
        [SerializeField]
        private float _matchingRoomTimeoutSeconds = 6.0f;

        /// <summary>
        /// 同時開始したホスト同士の再検索をずらす、ルームごとの追加待機秒数です。
        /// </summary>
        private float _matchingRoomTimeoutJitterSeconds;

        /// <summary>
        /// ホストが再検索要求を通知してから
        /// Lobbyを退出するまでの秒数です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _matchingRequeueNoticeDelaySeconds = 0.75f;

        /// <summary>
        /// Lobby退出後に再検索するまでの最小待機秒数です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _matchingRequeueMinimumDelaySeconds = 0.15f;

        /// <summary>
        /// Lobby退出後に再検索するまでの最大待機秒数です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _matchingRequeueMaximumDelaySeconds = 0.80f;

        /// <summary>
        /// ホストの再検索通知を受信できなかった非ホストが
        /// 自律的に再検索するまでの追加待機秒数です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _matchingRequeueClientFallbackSeconds = 2.0f;

        /// <summary>
        /// ホストがゲーム開始条件を再確認する間隔です。
        /// </summary>
        [Min(0.1f)]
        [SerializeField]
        private float _matchingStartCheckIntervalSeconds = 0.25f;

        /// <summary>
        /// 現在マッチング処理中かどうかを示します。
        /// </summary>
        private bool _isMatching;

        /// <summary>
        /// マッチングLobbyから退出して
        /// 再検索しているかどうかを示します。
        /// </summary>
        private bool _isRequeueingMatching;

        /// <summary>
        /// 現在のマッチングLobbyへ参加した時刻です。
        /// </summary>
        private float _matchingRoomJoinedTime;

        /// <summary>
        /// 次にゲーム開始条件を確認できる時刻です。
        /// </summary>
        private float _nextMatchingStartCheckTime;

        /// <summary>
        /// マッチングLobbyの再検索回数です。
        /// </summary>
        private int _matchingRequeueAttempt;

        /// <summary>
        /// 最後に処理したLobby再検索要求トークンです。
        /// </summary>
        private string _lastHandledRequeueToken;

        /// <summary>
        /// ゲーム開始処理を実行中かどうかを示します。
        /// </summary>
        private bool _isGameStarting;

        /// <summary>
        /// マッチングのキャンセル処理中かどうかを示します。
        /// </summary>
        private bool _isCancelingMatching;

        /// <summary>
        /// キャンセル後に元のパーティルームへ
        /// 復帰する必要があるかどうかを示します。
        /// </summary>
        private bool _shouldReturnToPartyRoom;

        /// <summary>
        /// パーティルームから開始したマッチングかどうかを示します。
        /// </summary>
        private bool _isPartyMatching;

        /// <summary>
        /// オンラインサービスのイベントを購読済みかどうかを示します。
        /// </summary>
        private bool _areOnlineEventsRegistered;

        /// <summary>
        /// キャンセル後に復帰するパーティルームの識別子です。
        /// </summary>
        private string _returnRoomId;

        /// <summary>
        /// キャンセル後に復帰するパーティルームのパスワードです。
        /// </summary>
        private string _returnPassword;

        /// <summary>
        /// パーティIDごとに参加プレイヤーを分類します。
        /// </summary>
        private readonly Dictionary<string, List<OnlinePlayer>>
            _partyMap = new();

        /// <summary>
        /// イベントを購読したオンラインサービスです。
        /// </summary>
        private IOnlineService _registeredOnlineService;

        /// <summary>
        /// オンラインサービスの生成を待機するコルーチンです。
        /// </summary>
        private Coroutine _registerEventsCoroutine;

        /// <summary>
        /// 次フレームのゲーム開始判定を待機するコルーチンです。
        /// </summary>
        private Coroutine _tryStartGameCoroutine;

        /// <summary>
        /// パーティルームからマッチングルームへ
        /// 移動するコルーチンです。
        /// </summary>
        private Coroutine _joinMatchingCoroutine;

        /// <summary>
        /// マッチングLobbyを退出して再検索するコルーチンです。
        /// </summary>
        private Coroutine _requeueMatchingCoroutine;

        /// <summary>
        /// キャンセル後のルーム復帰処理を行うコルーチンです。
        /// </summary>
        private Coroutine _returnAfterCancelCoroutine;

        /// <summary>
        /// キャンセル通知の反映を待って退出するコルーチンです。
        /// </summary>
        private Coroutine _leaveAfterCancelCoroutine;

        public static MatchmakingManager Instance
        {
            get; private set;
        }

        /// <summary>
        /// 検索ルームのホストとは別に、開始元パーティでの操作権限を保持します。
        /// </summary>
        private bool _wasPartyLeader;

        /// <summary>
        /// ルーム移動と再検索を通して保持するパーティ識別子です。
        /// </summary>
        private string _matchingPartyId;

        /// <summary>
        /// 参加初期化が完了したルームの識別子です。
        /// </summary>
        private string _initializedMatchingRoom;


        /// <summary>
        /// 同じチームに確保するパーティ人数です。
        /// </summary>
        private int _matchingPartyMemberCount = 1;

        public bool IsMatching => _isMatching && !_isCancelingMatching;

        public bool CanControlMatching => !_isPartyMatching || _wasPartyLeader;

        public event Action MatchingCanceled;

        /// <summary>
        /// パーティーホストが通知した今回のチーム数です。
        /// </summary>
        private int _sessionPartyCount;

        /// <summary>
        /// パーティーホストが通知した今回のチーム人数です。
        /// </summary>
        private int _sessionPartySize;

        private int ConfiguredPartyCount
        {
            get
            {
                return Mathf.Clamp(_partyCount, 1, 2);
            }
        }

        private int ConfiguredPartySize
        {
            get
            {
                return Mathf.Clamp(_partySize, 1, 3);
            }
        }

        private bool IsSinglePartyMatch
        {
            get
            {
                return MatchPartyCount == 1;
            }
        }

        private int MatchPartyCount
        {
            get
            {
                return _sessionPartyCount > 0 ? _sessionPartyCount : ConfiguredPartyCount;
            }
        }

        private int MatchPartySize
        {
            get
            {
                return _sessionPartySize > 0 ? _sessionPartySize : ConfiguredPartySize;
            }
        }

        private int MatchPlayerCount
        {
            get
            {
                return MatchPartyCount * MatchPartySize;
            }
        }

        private int HumanTeamCapacity
        {
            get
            {
                if (IsSinglePartyMatch)
                {
                    return MatchPartySize;
                }

                return Mathf.CeilToInt(
                    MatchPlayerCount / 2.0f);
            }
        }

        private bool ShouldSplitPlayersIntoSoloParties
        {
            get
            {
                return !IsSinglePartyMatch &&
                    !_isPartyMatching;
            }
        }

        /// <summary>
        /// シングルトンを初期化します。
        /// </summary>
        private void Awake()
        {
            if (Instance != null &&
                Instance != this)
            {
                Destroy(gameObject);

                return;
            }

            Instance = this;
        }

        /// <summary>
        /// オンラインサービスのイベント登録を開始します。
        /// </summary>
        private void OnEnable()
        {
            if (_registerEventsCoroutine == null)
            {
                _registerEventsCoroutine =
                    StartCoroutine(
                        RegisterOnlineEventsWhenReady());
            }
        }

        /// <summary>
        /// オンラインサービスのイベント購読と
        /// 実行中のコルーチンを停止します。
        /// </summary>
        private void OnDisable()
        {
            StopManagedCoroutines();
            UnregisterOnlineEvents();
        }

        /// <summary>
        /// シングルトン参照を解除します。
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 非ホスト側がルームのplaying状態を検出できなかった場合に備え、
        /// 現在のルーム状態を定期的に確認します。
        /// </summary>
        private void Update()
        {
            PollMatchingStartCondition();
            PollMatchingRoomTimeout();

            FollowPlayingStateIfNeeded(
                "Update");
        }

        /// <summary>
        /// マッチングのキャンセル処理中かどうかを取得します。
        /// </summary>
        /// <returns>
        /// キャンセル処理中の場合は<c>true</c>です。
        /// </returns>
        public bool IsCancelingMatching()
        {
            return _isCancelingMatching;
        }

        /// <summary>
        /// キャンセル後にパーティルームへ
        /// 復帰する状態かどうかを取得します。
        /// </summary>
        /// <returns>
        /// パーティルームへ復帰する場合は<c>true</c>です。
        /// </returns>
        public bool IsReturningPartyRoom()
        {
            return _isCancelingMatching &&
                _shouldReturnToPartyRoom;
        }

        /// <summary>
        /// オンラインサービスが登録されるまで待機し、
        /// イベントを購読します。
        /// </summary>
        /// <returns>
        /// 待機処理を表す列挙子です。
        /// </returns>
        private IEnumerator RegisterOnlineEventsWhenReady()
        {
            while (isActiveAndEnabled &&
                NetworkServiceLocator.Online == null)
            {
                yield return null;
            }

            _registerEventsCoroutine = null;

            if (!isActiveAndEnabled)
            {
                yield break;
            }

            RegisterOnlineEvents();
        }

        /// <summary>
        /// オンラインサービスのイベントを購読します。
        /// </summary>
        private void RegisterOnlineEvents()
        {
            if (_areOnlineEventsRegistered)
            {
                return;
            }

            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                return;
            }

            onlineService.JoinedRoom +=
                HandleJoinedRoom;

            onlineService.PartyRoomCreationFailed += HandlePartyReturnFailed;
            onlineService.PartyRoomJoinFailed += HandlePartyReturnFailed;

            onlineService.LeftRoom +=
                HandleLeftRoom;

            onlineService.PlayerJoined +=
                HandlePlayerJoined;

            onlineService.PlayerLeft +=
                HandlePlayerLeft;

            onlineService.PlayerPropertiesChanged +=
                HandlePlayerPropertiesChanged;

            onlineService.RoomPropertiesChanged +=
                HandleRoomPropertiesChanged;

            onlineService.HostChanged +=
                HandleHostChanged;

            _registeredOnlineService =
                onlineService;

            _areOnlineEventsRegistered = true;
        }

        /// <summary>
        /// オンラインサービスのイベント購読を解除します。
        /// </summary>
        private void UnregisterOnlineEvents()
        {
            if (!_areOnlineEventsRegistered ||
                _registeredOnlineService == null)
            {
                _registeredOnlineService = null;
                _areOnlineEventsRegistered = false;

                return;
            }

            _registeredOnlineService.JoinedRoom -=
                HandleJoinedRoom;

            _registeredOnlineService.PartyRoomCreationFailed -= HandlePartyReturnFailed;
            _registeredOnlineService.PartyRoomJoinFailed -= HandlePartyReturnFailed;

            _registeredOnlineService.LeftRoom -=
                HandleLeftRoom;

            _registeredOnlineService.PlayerJoined -=
                HandlePlayerJoined;

            _registeredOnlineService.PlayerLeft -=
                HandlePlayerLeft;

            _registeredOnlineService.PlayerPropertiesChanged -=
                HandlePlayerPropertiesChanged;

            _registeredOnlineService.RoomPropertiesChanged -=
                HandleRoomPropertiesChanged;

            _registeredOnlineService.HostChanged -=
                HandleHostChanged;

            _registeredOnlineService = null;
            _areOnlineEventsRegistered = false;
        }

        /// <summary>
        /// 管理中のコルーチンを停止します。
        /// </summary>
        private void StopManagedCoroutines()
        {
            if (_leaveAfterCancelCoroutine != null)
            {
                StopCoroutine(_leaveAfterCancelCoroutine);
                _leaveAfterCancelCoroutine = null;
            }

            if (_registerEventsCoroutine != null)
            {
                StopCoroutine(_registerEventsCoroutine);
                _registerEventsCoroutine = null;
            }

            if (_tryStartGameCoroutine != null)
            {
                StopCoroutine(_tryStartGameCoroutine);
                _tryStartGameCoroutine = null;
            }

            if (_joinMatchingCoroutine != null)
            {
                StopCoroutine(_joinMatchingCoroutine);
                _joinMatchingCoroutine = null;
            }

            if (_requeueMatchingCoroutine != null)
            {
                StopCoroutine(_requeueMatchingCoroutine);
                _requeueMatchingCoroutine = null;
            }

            if (_returnAfterCancelCoroutine != null)
            {
                StopCoroutine(_returnAfterCancelCoroutine);
                _returnAfterCancelCoroutine = null;
            }
        }

        /// <summary>
        /// パーティー復帰に失敗した場合もキャンセル状態を解除して再操作を許可します。
        /// </summary>
        private void HandlePartyReturnFailed(string message)
        {
            if (!IsReturningPartyRoom())
            {
                return;
            }

            Debug.LogWarning($"パーティールームへの復帰に失敗しました: {message}", this);
            ResetMatchingState();
            MatchingCanceled?.Invoke();
        }

        /// <summary>
        /// マッチングルームへの参加完了を処理します。
        /// </summary>
        private void HandleJoinedRoom()
        {
            IOnlineService onlineService = NetworkServiceLocator.Online;
            if (onlineService == null || !onlineService.IsInRoom || onlineService.CurrentRoom == null)
            {
                return;
            }

            if (IsCurrentRoomType(NetworkPropertyKeys.TypeParty) && IsReturningPartyRoom())
            {
                OnReturnedPartyRoom();
                return;
            }

            if (IsCurrentRoomType(NetworkPropertyKeys.TypeMatching))
            {
                OnJoinedMatchingRoom();
            }
        }

        /// <summary>
        /// マッチング処理中にLobbyから退出した場合、
        /// キャンセルでなければマッチング検索を継続します。
        /// </summary>
        private void HandleLeftRoom()
        {
            if (!_isMatching ||
                _isCancelingMatching ||
                _isGameStarting ||
                _joinMatchingCoroutine != null ||
                _requeueMatchingCoroutine != null)
            {
                return;
            }

            StartMatchingRequeue(
                "LeftRoom",
                false);
        }

        /// <summary>
        /// マッチングルームへプレイヤーが参加したとき、
        /// ゲーム開始条件を再確認します。
        /// </summary>
        /// <param name="player">
        /// 参加したプレイヤーです。
        /// </param>
        private void HandlePlayerJoined(
            OnlinePlayer player)
        {
            _matchingRoomJoinedTime = Time.unscaledTime;
            if (HasOpponentParty())
            {
                CancelMatchingRequeueBecauseOpponentJoined(true);
            }

            ScheduleTryStartGame();
        }

        /// <summary>
        /// マッチング中にプレイヤーが退出したとき、
        /// 参加中のマッチングをキャンセルします。
        /// </summary>
        /// <param name="player">
        /// 退出したプレイヤーです。
        /// </param>
        private void HandlePlayerLeft(
            OnlinePlayer player)
        {
            if (!_isMatching ||
                _isGameStarting ||
                _isCancelingMatching ||
                _isRequeueingMatching ||
                !IsCurrentRoomType(
                    NetworkPropertyKeys.TypeMatching))
            {
                return;
            }

            if (_isPartyMatching &&
                IsLocalPartyPlayer(
                    player))
            {
                IOnlineService onlineService =
                    NetworkServiceLocator.Online;

                if (onlineService != null &&
                    onlineService.IsHost)
                {
                    onlineService.SetRoomProperty(
                        NetworkPropertyKeys.MatchCanceled,
                        true);
                }

                HandleMatchingCanceledByOtherPlayer();

                return;
            }

            _matchingRoomJoinedTime =
                Time.unscaledTime;

            ScheduleTryStartGame();
        }

        /// <summary>
        /// プレイヤープロパティの変更を処理します。
        /// </summary>
        /// <param name="targetPlayer">
        /// プロパティが変更されたプレイヤーです。
        /// </param>
        /// <param name="changedProperties">
        /// 変更されたプロパティです。
        /// </param>
        private void HandlePlayerPropertiesChanged(
            OnlinePlayer targetPlayer,
            Dictionary<string, object> changedProperties)
        {
            bool changedMatchingReady =
                ContainsPropertyKey(
                    changedProperties,
                    NetworkPropertyKeys.MatchingReady);

            bool changedPartyId =
                ContainsPropertyKey(
                    changedProperties,
                    NetworkPropertyKeys.PartyId);

            if (!changedMatchingReady &&
                !changedPartyId)
            {
                return;
            }

            if (HasCompatibleOpponentParty())
            {
                CancelMatchingRequeueBecauseOpponentJoined();
            }

            ScheduleTryStartGame();
        }

        /// <summary>
        /// ホスト変更後にゲーム開始条件を再確認します。
        /// </summary>
        /// <param name="newHost">
        /// 新しくホストになったプレイヤーです。
        /// </param>
        private void HandleHostChanged(
            OnlinePlayer newHost)
        {
            ScheduleTryStartGame();
        }

        /// <summary>
        /// ルームプロパティの変更を処理します。
        /// </summary>
        /// <param name="changedProperties">
        /// 変更されたルームプロパティです。
        /// </param>
        private void HandleRoomPropertiesChanged(
            Dictionary<string, object> changedProperties)
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                !onlineService.IsInRoom ||
                onlineService.CurrentRoom == null ||
                !IsCurrentRoomType(
                    NetworkPropertyKeys.TypeMatching))
            {
                return;
            }

            if (TryGetChangedString(
                    changedProperties,
                    k_MatchingRequeueTokenPropertyKey,
                    out string requeueToken) &&
                !string.IsNullOrWhiteSpace(
                    requeueToken) &&
                !string.Equals(
                    requeueToken,
                    _lastHandledRequeueToken,
                    StringComparison.Ordinal))
            {
                _lastHandledRequeueToken =
                    requeueToken;

                if (_initializedMatchingRoom == onlineService.CurrentRoom.Name &&
                    !HasCompatibleOpponentParty())
                {
                    StartMatchingRequeue($"RoomProperty:{requeueToken}", true);
                }

                return;
            }

            if (TryGetChangedBoolean(
                    changedProperties,
                    NetworkPropertyKeys.MatchCanceled,
                    out bool isCanceled) &&
                isCanceled)
            {
                HandleMatchingCanceledByOtherPlayer();

                return;
            }

            if (TryGetChangedString(
                    changedProperties,
                    NetworkPropertyKeys.State,
                    out string changedState) &&
                string.Equals(
                    changedState,
                    NetworkPropertyKeys.StatePlaying,
                    StringComparison.OrdinalIgnoreCase))
            {
                StartGameFromRoomState(
                    "RoomPropertiesChanged");

                return;
            }

            if (IsCurrentRoomState(
                    NetworkPropertyKeys.StatePlaying))
            {
                StartGameFromRoomState(
                    "CurrentRoomState");
            }
        }

        /// <summary>
        /// 次のフレームでゲーム開始条件を確認します。
        /// </summary>
        private void ScheduleTryStartGame()
        {
            if (!isActiveAndEnabled ||
                _tryStartGameCoroutine != null)
            {
                return;
            }

            _tryStartGameCoroutine =
                StartCoroutine(
                    TryStartGameNextFrame());
        }

        /// <summary>
        /// 1フレーム待機した後にゲーム開始条件を確認します。
        /// </summary>
        /// <returns>
        /// 待機処理を表す列挙子です。
        /// </returns>
        private IEnumerator TryStartGameNextFrame()
        {
            yield return null;

            _tryStartGameCoroutine = null;

            TryStartGame();
        }

        /// <summary>
        /// 非ホスト側がルームのplaying状態に追従する必要があるか確認します。
        /// </summary>
        /// <param name="reason">
        /// 状態確認を実行した理由です。
        /// </param>
        private void FollowPlayingStateIfNeeded(
            string reason)
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            GameFlowManager gameFlowManager =
                GameFlowManager.Instance;

            if (onlineService == null ||
                gameFlowManager == null ||
                onlineService.IsHost ||
                !_isMatching ||
                _isCancelingMatching ||
                _isGameStarting ||
                gameFlowManager.IsGameplay ||
                !onlineService.IsInRoom ||
                onlineService.CurrentRoom == null ||
                !IsCurrentRoomType(
                    NetworkPropertyKeys.TypeMatching) ||
                !IsCurrentRoomState(
                    NetworkPropertyKeys.StatePlaying))
            {
                return;
            }

            StartGameFromRoomState(reason);
        }

        /// <summary>
        /// マッチングルームへの参加後に、
        /// パーティIDと準備完了状態を設定します。
        /// </summary>
        public void OnJoinedMatchingRoom()
        {
            if (_isCancelingMatching)
            {
                return;
            }

            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                !onlineService.IsInRoom)
            {
                return;
            }

            _initializedMatchingRoom = onlineService.CurrentRoom?.Name;
            _matchingRoomTimeoutJitterSeconds =
                (uint)Guid.NewGuid().GetHashCode() / (float)uint.MaxValue *
                Mathf.Max(1.0f, _matchingRoomTimeoutSeconds) * 2.0f;
            TryGetCurrentRoomString(k_MatchingRequeueTokenPropertyKey, out _lastHandledRequeueToken);
            _isMatching = true;
            _isRequeueingMatching = false;
            _matchingRoomJoinedTime =
                Time.unscaledTime;
            _nextMatchingStartCheckTime =
                0.0f;

            Debug.Log(
                $"{nameof(MatchmakingManager)}: " +
                "マッチングルームへ参加しました。",
                this);

            OnlinePlayer localPlayer =
                onlineService.Players.FirstOrDefault(
                    player =>
                        player != null &&
                        player.PlayerId ==
                        onlineService.LocalPlayerId);

            string partyId =
                ResolveLocalPartyId(localPlayer);

            onlineService.SetLocalPlayerProperties(
                new Dictionary<string, object>
                {
                    {
                        NetworkPropertyKeys.PartyId,
                        partyId
                    },
                    {
                        NetworkPropertyKeys.MatchingReady,
                        true
                    },
                    {
                        NetworkPropertyKeys.MatchingPartyMemberCount,
                        _matchingPartyMemberCount
                    }
                });

            TryStartGame();

            FollowPlayingStateIfNeeded(
                nameof(OnJoinedMatchingRoom));
        }

        /// <summary>
        /// パーティルームを作成します。
        /// </summary>
        /// <param name="roomId">
        /// 4桁のルームIDです。
        /// </param>
        /// <param name="password">
        /// 4桁のパスワードです。
        /// </param>
        /// <returns>
        /// 作成要求を開始できた場合は<c>null</c>、
        /// 入力が不正な場合はエラーメッセージです。
        /// </returns>
        public string CreateRoom(
            string roomId,
            string password)
        {
            string validationError =
                ValidatePartyCredentials(
                    roomId,
                    password);

            if (validationError != null)
            {
                return validationError;
            }

            if (PartyManager.Instance == null)
            {
                return "パーティ管理機能を利用できません。";
            }

            PartyManager.Instance.CreateParty(
                roomId,
                password);

            return null;
        }

        /// <summary>
        /// パーティルームへ参加します。
        /// </summary>
        /// <param name="roomId">
        /// 4桁のルームIDです。
        /// </param>
        /// <param name="password">
        /// 4桁のパスワードです。
        /// </param>
        /// <returns>
        /// 参加要求を開始できた場合は<c>null</c>、
        /// 入力が不正な場合はエラーメッセージです。
        /// </returns>
        public string JoinRoom(
            string roomId,
            string password)
        {
            string validationError =
                ValidatePartyCredentials(
                    roomId,
                    password);

            if (validationError != null)
            {
                return validationError;
            }

            if (PartyManager.Instance == null)
            {
                return "パーティ管理機能を利用できません。";
            }

            PartyManager.Instance.JoinParty(
                roomId,
                password);

            return null;
        }

        /// <summary>
        /// 現在の状態からオンラインマッチングを開始します。
        /// </summary>
        /// <returns>
        /// マッチング開始処理を開始できた場合は<c>true</c>です。
        /// </returns>
        public bool StartMatching()
        {
            GameFlowManager gameFlowManager =
                GameFlowManager.Instance;

            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (_isMatching ||
                _isCancelingMatching ||
                _isGameStarting ||
                gameFlowManager == null ||
                gameFlowManager.IsGameplay ||
                onlineService == null)
            {
                return false;
            }

            if (IsCurrentRoomType(NetworkPropertyKeys.TypeParty) && !onlineService.IsHost && !TryApplyRoomGameMode())
            {
                return false;
            }

            _matchingGameModeId = gameFlowManager.GameModeId;
            if (!GameModeRegistry.Catalog.TryGet(_matchingGameModeId, PlayCategory.Online, out _) ||
                (_matchingGameModeId != GameModeRegistry.k_DefaultModeId && onlineService is not IGameModeMatchmakingService))
            {
                Debug.LogError("選択したゲームルールのマッチングを利用できません。", this);
                return false;
            }

            Debug.Log(
                $"{nameof(MatchmakingManager)}: " +
                "マッチングを開始します。",
                this);

            _isMatching = true;
            _isCancelingMatching = false;
            _shouldReturnToPartyRoom = false;
            _returnRoomId = null;
            _returnPassword = null;
            _isRequeueingMatching = false;
            _matchingRoomJoinedTime = 0.0f;
            _nextMatchingStartCheckTime = 0.0f;
            _matchingRequeueAttempt = 0;
            _lastHandledRequeueToken = null;

            bool startedFromPartyRoom =
                IsCurrentRoomType(
                    NetworkPropertyKeys.TypeParty);

            _sessionPartyCount = startedFromPartyRoom
                ? Mathf.Clamp(onlineService.CurrentRoom.GetPropertyOrDefault(NetworkPropertyKeys.MatchingPartyCount, ConfiguredPartyCount), 1, 2)
                : ConfiguredPartyCount;
            _sessionPartySize = startedFromPartyRoom
                ? Mathf.Clamp(onlineService.CurrentRoom.GetPropertyOrDefault(NetworkPropertyKeys.MatchingPartySize, ConfiguredPartySize), 1, 3)
                : ConfiguredPartySize;

            _wasPartyLeader = startedFromPartyRoom && onlineService.IsHost;
            _matchingPartyMemberCount = startedFromPartyRoom ? onlineService.Players.Count : 1;
            _matchingPartyId = startedFromPartyRoom
                ? (TryGetCurrentRoomString(NetworkPropertyKeys.PartyId, out string partyIdentifier) ? partyIdentifier : null)
                : $"{k_SoloPartyIdPrefix}{Guid.NewGuid():N}";
            if (_matchingPartyMemberCount > HumanTeamCapacity || string.IsNullOrWhiteSpace(_matchingPartyId))
            {
                ResetMatchingState();
                return false;
            }

            if (startedFromPartyRoom)
            {
                _isPartyMatching = true;
                _shouldReturnToPartyRoom = true;

                gameFlowManager.SetPartyPlayState(true);

                TryGetCurrentRoomString(
                    NetworkPropertyKeys.RoomId,
                    out _returnRoomId);

                TryGetCurrentRoomString(
                    NetworkPropertyKeys.Password,
                    out _returnPassword);

                if (onlineService.IsHost &&
                    !IsCurrentRoomState(
                        NetworkPropertyKeys.StateMatching))
                {
                    onlineService.SetRoomProperties(new Dictionary<string, object>
                    {
                        { NetworkPropertyKeys.GameModeId, gameFlowManager.GameModeId },
                        { NetworkPropertyKeys.State, NetworkPropertyKeys.StateMatching }
                    });
                }
            }
            else
            {
                _isPartyMatching = false;

                gameFlowManager.SetPartyPlayState(false);
            }

            bool wasPartyHost =
                startedFromPartyRoom &&
                onlineService.IsHost;

            if (_joinMatchingCoroutine != null)
            {
                StopCoroutine(_joinMatchingCoroutine);
            }

            _joinMatchingCoroutine =
                StartCoroutine(
                    LeaveAndJoinMatchingRoom(
                        startedFromPartyRoom,
                        wasPartyHost));

            return true;
        }

        /// <summary>
        /// 現在のルームから退出し、マッチングルームへ参加します。
        /// </summary>
        /// <param name="startedFromPartyRoom">
        /// パーティルームから開始した場合は<c>true</c>です。
        /// </param>
        /// <param name="wasPartyHost">
        /// 元のパーティルームでホストだった場合は<c>true</c>です。
        /// </param>
        /// <returns>
        /// ルーム移動処理を表す列挙子です。
        /// </returns>
        private IEnumerator LeaveAndJoinMatchingRoom(
            bool startedFromPartyRoom,
            bool wasPartyHost)
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                _joinMatchingCoroutine = null;
                yield break;
            }

            if (startedFromPartyRoom)
            {
                float deadline = Time.unscaledTime + 30.0f;
                bool departurePublished = false;
                while (onlineService.IsInRoom &&
                    !onlineService.CurrentRoom.GetPropertyOrDefault(NetworkPropertyKeys.PartyDeparting, false))
                {
                    if (!_isMatching || _isCancelingMatching)
                    {
                        _joinMatchingCoroutine = null;
                        yield break;
                    }

                    if (Time.unscaledTime >= deadline)
                    {
                        Debug.LogWarning("パーティー全員の開始受信を確認できなかったためキャンセルします。", this);
                        CancelMatching();
                        yield break;
                    }

                    if (wasPartyHost && !departurePublished &&
                        onlineService.Players.Count == _matchingPartyMemberCount &&
                        onlineService.Players.All(player => player != null &&
                            player.GetPropertyOrDefault(NetworkPropertyKeys.Ready, false)))
                    {
                        departurePublished = true;
                        onlineService.SetRoomProperty(NetworkPropertyKeys.PartyDeparting, true);
                    }

                    yield return null;
                }
            }

            if (onlineService.IsInRoom)
            {
                if (startedFromPartyRoom &&
                    _partyLobbyToMatchingDelay > 0.0f)
                {
                    yield return new WaitForSecondsRealtime(
                        _partyLobbyToMatchingDelay);
                }

                onlineService.LeaveRoom();

                float timeoutTime =
                    Time.unscaledTime +
                    k_RoomLeaveTimeoutSeconds;

                while (onlineService.IsInRoom &&
                    Time.unscaledTime < timeoutTime)
                {
                    yield return null;
                }

                if (onlineService.IsInRoom)
                {
                    Debug.LogError(
                        $"{nameof(MatchmakingManager)}: " +
                        "ルームからの退出がタイムアウトしました。",
                        this);

                    ResetMatchingState();
                    _joinMatchingCoroutine = null;
                    yield break;
                }
            }

            if (startedFromPartyRoom &&
                !wasPartyHost &&
                _partyClientMatchingJoinDelay > 0.0f)
            {
                yield return new WaitForSecondsRealtime(
                    _partyClientMatchingJoinDelay);
            }

            Debug.Log(
                $"{nameof(MatchmakingManager)}: " +
                "マッチングルームへ参加します。" +
                $"PartyCount={MatchPartyCount}, " +
                $"PartySize={MatchPartySize}, " +
                $"ExpectedPlayers={MatchPlayerCount}, " +
                $"SingleTeamDebug={IsSinglePartyMatch}",
                this);

            PrepareMatchingMemberProperties(onlineService);
            JoinSelectedModeRoom(onlineService);

            _joinMatchingCoroutine = null;
        }

        /// <summary>
        /// マッチング中の場合にマッチングルームへの参加を再試行します。
        /// </summary>
        public void RetryJoinMatching()
        {
            GameFlowManager gameFlowManager =
                GameFlowManager.Instance;

            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (!_isMatching ||
                _isCancelingMatching ||
                gameFlowManager == null ||
                gameFlowManager.IsGameplay ||
                onlineService == null ||
                onlineService.IsInRoom)
            {
                return;
            }

            PrepareMatchingMemberProperties(onlineService);
            JoinSelectedModeRoom(onlineService);
        }

        /// <summary>
        /// 現在実行中のマッチングをキャンセルします。
        /// </summary>
        public void CancelMatching()
        {
            if (_isCancelingMatching)
            {
                return;
            }

            GameFlowManager gameFlowManager =
                GameFlowManager.Instance;

            if (!_isMatching)
            {
                MatchingCanceled?.Invoke();

                return;
            }

            if (_isGameStarting ||
                gameFlowManager == null ||
                gameFlowManager.IsGameplay)
            {
                return;
            }

            Debug.Log(
                $"{nameof(MatchmakingManager)}: " +
                "マッチングをキャンセルします。",
                this);

            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            _isMatching = false;
            _isGameStarting = false;
            _isCancelingMatching = true;
            _isRequeueingMatching = false;
            if (_joinMatchingCoroutine != null)
            {
                StopCoroutine(_joinMatchingCoroutine);
                _joinMatchingCoroutine = null;
            }


            if (_requeueMatchingCoroutine != null)
            {
                StopCoroutine(
                    _requeueMatchingCoroutine);

                _requeueMatchingCoroutine =
                    null;
            }

            if (onlineService == null)
            {
                ResetMatchingState();
                MatchingCanceled?.Invoke();

                return;
            }

            if (onlineService.IsInRoom)
            {
                onlineService.SetLocalPlayerProperty(
                    NetworkPropertyKeys.MatchingReady,
                    false);

                if (IsCurrentRoomType(
                        NetworkPropertyKeys.TypeMatching))
                {
                    onlineService.SetRoomProperty(
                        NetworkPropertyKeys.MatchCanceled,
                        true);
                }

            }

            _leaveAfterCancelCoroutine = StartCoroutine(LeaveAfterCancelNotification());

            MatchingCanceled?.Invoke();
        }

        /// <summary>
        /// キャンセル通知の反映を待ち、ルームを退出して復帰を開始します。
        /// </summary>
        private IEnumerator LeaveAfterCancelNotification()
        {
            yield return null;
            IOnlineService online = NetworkServiceLocator.Online;
            if (online != null && online.IsInRoom && online.IsHost)
            {
                if (IsCurrentRoomType(NetworkPropertyKeys.TypeParty))
                {
                    online.SetRoomProperties(new Dictionary<string, object>
                    {
                        { NetworkPropertyKeys.State, NetworkPropertyKeys.StateWaiting },
                        { NetworkPropertyKeys.PartyMatching, false },
                        { NetworkPropertyKeys.PartyDeparting, false }
                    });
                }

                float deadline = Time.unscaledTime + 3.0f;
                while (online.IsInRoom && Time.unscaledTime < deadline)
                {
                    bool notified = IsCurrentRoomType(NetworkPropertyKeys.TypeParty)
                        ? online.CurrentRoom.GetPropertyOrDefault(NetworkPropertyKeys.State, string.Empty) == NetworkPropertyKeys.StateWaiting
                        : online.CurrentRoom.GetPropertyOrDefault(NetworkPropertyKeys.MatchCanceled, false);
                    if (notified)
                    {
                        break;
                    }

                    yield return null;
                }

                yield return new WaitForSecondsRealtime(0.5f);
            }

            online?.LeaveRoom();
            _leaveAfterCancelCoroutine = null;
            StartReturnAfterCancel();
        }

        /// <summary>
        /// 他端末のキャンセル通知を受けてパーティーへ復帰します。
        /// </summary>
        private void HandleMatchingCanceledByOtherPlayer()
        {
            GameFlowManager gameFlowManager =
                GameFlowManager.Instance;

            if (_isGameStarting ||
                _isCancelingMatching ||
                gameFlowManager == null ||
                gameFlowManager.IsGameplay)
            {
                return;
            }

            Debug.Log(
                $"{nameof(MatchmakingManager)}: " +
                "他プレイヤーの退出またはキャンセルにより、" +
                "マッチングを停止します。",
                this);

            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (_joinMatchingCoroutine != null)
            {
                StopCoroutine(_joinMatchingCoroutine);
                _joinMatchingCoroutine = null;
            }

            _isMatching = false;
            _isCancelingMatching = true;

            if (onlineService != null)
            {
                onlineService.SetLocalPlayerProperty(
                    NetworkPropertyKeys.MatchingReady,
                    false);

                if (onlineService.IsInRoom)
                {
                    onlineService.LeaveRoom();
                }
            }

            MatchingCanceled?.Invoke();

            StartReturnAfterCancel();
        }

        /// <summary>
        /// キャンセル後の復帰コルーチンを開始します。
        /// </summary>
        private void StartReturnAfterCancel()
        {
            if (!isActiveAndEnabled ||
                _returnAfterCancelCoroutine != null)
            {
                return;
            }

            _returnAfterCancelCoroutine =
                StartCoroutine(
                    ReturnAfterCancel());
        }

        /// <summary>
        /// マッチングルームからの退出を待ち、
        /// 必要に応じて元のパーティルームへ復帰します。
        /// </summary>
        /// <returns>
        /// 復帰処理を表す列挙子です。
        /// </returns>
        /// <remarks>
        /// 最初のフレームを待ち、StartCoroutineの戻り値が保存される前に参照を解除しないようにします。
        /// </remarks>
        private IEnumerator ReturnAfterCancel()
        {
            yield return null;
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                ResetMatchingState();
                _returnAfterCancelCoroutine = null;
                yield break;
            }

            float timeoutTime =
                Time.unscaledTime +
                k_RoomLeaveTimeoutSeconds;

            while (onlineService.IsInRoom &&
                Time.unscaledTime < timeoutTime)
            {
                yield return null;
            }

            if (onlineService.IsInRoom)
            {
                Debug.LogError(
                    $"{nameof(MatchmakingManager)}: " +
                    "キャンセル後のルーム退出がタイムアウトしました。",
                    this);

                ResetMatchingState();
                _returnAfterCancelCoroutine = null;
                yield break;
            }

            if (_shouldReturnToPartyRoom &&
                !string.IsNullOrWhiteSpace(
                    _returnRoomId) &&
                !string.IsNullOrWhiteSpace(
                    _returnPassword))
            {
                Debug.Log(
                    $"{nameof(MatchmakingManager)}: " +
                    "元のパーティルームへ復帰します。",
                    this);

                string partyId =
                    $"{_returnRoomId}_{_returnPassword}";

                string roomName =
                    $"party_{_returnRoomId}_{_returnPassword}";

                if (!_wasPartyLeader)
                {
                    yield return new WaitForSecondsRealtime(1.0f);
                }

                onlineService.JoinOrCreatePartyRoom(
                    roomName,
                    _returnRoomId,
                    _returnPassword,
                    partyId,
                    MatchPartySize);
            }
            else
            {
                Debug.Log(
                    $"{nameof(MatchmakingManager)}: " +
                    "個人マッチングを終了しました。",
                    this);

                ResetMatchingState();
            }

            _returnAfterCancelCoroutine = null;
        }

        /// <summary>
        /// キャンセル後に元のパーティルームへ
        /// 復帰したことを通知します。
        /// </summary>
        public void OnReturnedPartyRoom()
        {
            Debug.Log(
                $"{nameof(MatchmakingManager)}: " +
                "パーティルームへ復帰しました。",
                this);

            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            ResetMatchingState();
            MatchingCanceled?.Invoke();

            if (onlineService == null)
            {
                return;
            }

            onlineService.SetLocalPlayerProperties(
                new Dictionary<string, object>
                {
                    {
                        NetworkPropertyKeys.Ready,
                        false
                    },
                    {
                        NetworkPropertyKeys.MatchingReady,
                        false
                    }
                });

            if (onlineService.IsHost)
            {
                onlineService.SetRoomProperties(
                    new Dictionary<string, object>
                    {
                        {
                            NetworkPropertyKeys.State,
                            NetworkPropertyKeys.StateWaiting
                        },
                        {
                            NetworkPropertyKeys.PartyMatching,
                            false
                        }
                    });
            }
        }

        /// <summary>
        /// ホスト側でゲーム開始条件を定期的に確認します。
        /// </summary>
        /// <remarks>
        /// EOSのPlayerJoinedまたはMatchingReady通知を
        /// 取り逃しても、ポーリングによって開始できます。
        /// </remarks>
        private void PollMatchingStartCondition()
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (!_isMatching ||
                _isCancelingMatching ||
                _isRequeueingMatching ||
                _isGameStarting ||
                onlineService == null ||
                !onlineService.IsInRoom ||
                !onlineService.IsHost ||
                onlineService.CurrentRoom == null ||
                !IsCurrentRoomType(
                    NetworkPropertyKeys.TypeMatching) ||
                Time.unscaledTime <
                _nextMatchingStartCheckTime)
            {
                return;
            }

            _nextMatchingStartCheckTime =
                Time.unscaledTime +
                Mathf.Max(
                    0.1f,
                    _matchingStartCheckIntervalSeconds);

            TryStartGame();
        }

        /// <summary>
        /// 敵パーティがいないマッチングLobbyの待機時間を監視します。
        /// </summary>
        private void PollMatchingRoomTimeout()
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (!_isMatching ||
                _isCancelingMatching ||
                _isRequeueingMatching ||
                _isGameStarting ||
                IsSinglePartyMatch ||
                onlineService == null ||
                !onlineService.IsInRoom ||
                onlineService.CurrentRoom == null ||
                !IsCurrentRoomType(
                    NetworkPropertyKeys.TypeMatching))
            {
                return;
            }

            if (HasCompatibleOpponentParty())
            {
                _matchingRoomJoinedTime =
                    Time.unscaledTime;

                return;
            }

            if (_matchingRoomJoinedTime <= 0.0f)
            {
                _matchingRoomJoinedTime =
                    Time.unscaledTime;

                return;
            }

            float timeoutSeconds =
                Mathf.Max(
                    1.0f,
                    _matchingRoomTimeoutSeconds);

            float elapsedTime =
                Time.unscaledTime -
                _matchingRoomJoinedTime;

            if (onlineService.IsHost)
            {
                if (elapsedTime <
                    timeoutSeconds + _matchingRoomTimeoutJitterSeconds)
                {
                    return;
                }

                RequestMatchingRequeueFromHost();

                return;
            }

            float fallbackSeconds =
                Mathf.Max(
                    0.0f,
                    _matchingRequeueClientFallbackSeconds);

            if (elapsedTime <
                timeoutSeconds * 3.0f +
                fallbackSeconds)
            {
                return;
            }

            StartMatchingRequeue(
                "ClientFallbackTimeout",
                false);
        }

        /// <summary>
        /// ホストがLobbyメンバーへ再検索要求を通知します。
        /// </summary>
        private void RequestMatchingRequeueFromHost()
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                !onlineService.IsInRoom ||
                !onlineService.IsHost ||
                HasCompatibleOpponentParty())
            {
                return;
            }

            onlineService.SetRoomOpen(false);
            onlineService.SetRoomVisible(false);

            string token =
                $"{onlineService.LocalPlayerId}_" +
                $"{DateTime.UtcNow.Ticks}_" +
                $"{_matchingRequeueAttempt + 1}";

            _lastHandledRequeueToken =
                token;

            onlineService.SetRoomProperty(
                k_MatchingRequeueTokenPropertyKey,
                token);

            StartMatchingRequeue(
                $"HostTimeout:{token}",
                true);
        }

        /// <summary>
        /// マッチングLobbyから退出して再検索する処理を開始します。
        /// </summary>
        private void StartMatchingRequeue(
            string reason,
            bool shouldWaitForNotice)
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (!isActiveAndEnabled ||
                !_isMatching ||
                _isCancelingMatching ||
                _isGameStarting ||
                _requeueMatchingCoroutine != null ||
                (onlineService != null &&
                 onlineService.IsInRoom &&
                 HasCompatibleOpponentParty()))
            {
                return;
            }

            _isRequeueingMatching =
                true;

            _matchingRequeueAttempt++;

            Debug.Log(
                $"{nameof(MatchmakingManager)}: " +
                "敵パーティがいないため、" +
                "マッチングLobbyを退出して再検索します。" +
                $" Reason={reason}," +
                $" Attempt={_matchingRequeueAttempt}",
                this);

            _requeueMatchingCoroutine =
                StartCoroutine(
                    RequeueMatchingRoom(
                        shouldWaitForNotice));
        }

        /// <summary>
        /// Lobby退出完了後にマッチング検索を再開します。
        /// </summary>
        private IEnumerator RequeueMatchingRoom(
            bool shouldWaitForNotice)
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                _isRequeueingMatching =
                    false;

                _requeueMatchingCoroutine =
                    null;

                yield break;
            }

            if (shouldWaitForNotice &&
                _matchingRequeueNoticeDelaySeconds >
                0.0f)
            {
                yield return new WaitForSecondsRealtime(
                    _matchingRequeueNoticeDelaySeconds);
            }

            if (!_isMatching ||
                _isCancelingMatching ||
                _isGameStarting)
            {
                _isRequeueingMatching =
                    false;

                _requeueMatchingCoroutine =
                    null;

                yield break;
            }

            if (onlineService.IsInRoom &&
                HasCompatibleOpponentParty())
            {
                ReopenWaitingRoom();
                _isRequeueingMatching =
                    false;

                _matchingRoomJoinedTime =
                    Time.unscaledTime;

                _requeueMatchingCoroutine =
                    null;

                ScheduleTryStartGame();
                yield break;
            }

            if (onlineService.IsInRoom)
            {
                onlineService.SetLocalPlayerProperty(
                    NetworkPropertyKeys.MatchingReady,
                    false);

                onlineService.LeaveRoom();

                float timeoutTime =
                    Time.unscaledTime +
                    k_RoomLeaveTimeoutSeconds;

                while (onlineService.IsInRoom &&
                    Time.unscaledTime <
                    timeoutTime)
                {
                    yield return null;
                }

                if (onlineService.IsInRoom)
                {
                    Debug.LogError(
                        $"{nameof(MatchmakingManager)}: " +
                        "再検索前のLobby退出がタイムアウトしました。",
                        this);

                    _isRequeueingMatching =
                        false;

                    _requeueMatchingCoroutine =
                        null;

                    yield break;
                }
            }

            float minimumDelay =
                Mathf.Max(
                    0.0f,
                    _matchingRequeueMinimumDelaySeconds);

            float maximumDelay =
                Mathf.Max(
                    minimumDelay,
                    _matchingRequeueMaximumDelaySeconds);

            float retryDelay =
                UnityEngine.Random.Range(
                    minimumDelay,
                    maximumDelay);

            if (retryDelay > 0.0f)
            {
                yield return new WaitForSecondsRealtime(
                    retryDelay);
            }

            if (!_isMatching ||
                _isCancelingMatching ||
                _isGameStarting)
            {
                _isRequeueingMatching =
                    false;

                _requeueMatchingCoroutine =
                    null;

                yield break;
            }

            Debug.Log(
                $"{nameof(MatchmakingManager)}: " +
                "マッチングLobbyの再検索を開始します。" +
                $" Attempt={_matchingRequeueAttempt}," +
                $" Delay={retryDelay:0.00}",
                this);

            PrepareMatchingMemberProperties(onlineService);
            JoinSelectedModeRoom(onlineService);

            _requeueMatchingCoroutine =
                null;
        }

        /// <summary>
        /// 敵パーティが参加した場合に進行中の再検索を中止します。
        /// </summary>
        private void CancelMatchingRequeueBecauseOpponentJoined(bool allowPendingMetadata = false)
        {
            if (!(allowPendingMetadata ? HasOpponentParty() : HasCompatibleOpponentParty()))
            {
                return;
            }

            if (_isRequeueingMatching)
            {
                ReopenWaitingRoom();
            }

            if (_requeueMatchingCoroutine != null)
            {
                StopCoroutine(
                    _requeueMatchingCoroutine);

                _requeueMatchingCoroutine =
                    null;
            }

            _isRequeueingMatching =
                false;

            _matchingRoomJoinedTime =
                Time.unscaledTime;
        }

        /// <summary>
        /// 再検索の通知中に参加条件が整った場合、待機ルームの受付を復元します。
        /// </summary>
        private void ReopenWaitingRoom()
        {
            IOnlineService online = NetworkServiceLocator.Online;
            if (!_isGameStarting && online != null && online.IsInRoom && online.IsHost &&
                IsCurrentRoomType(NetworkPropertyKeys.TypeMatching) &&
                IsCurrentRoomState(NetworkPropertyKeys.StateWaiting))
            {
                online.SetRoomProperty(k_MatchingRequeueTokenPropertyKey, string.Empty);
                online.SetRoomOpen(true);
                online.SetRoomVisible(true);
            }
        }

        /// <summary>
        /// パーティ全員が到着し、同じチームに配置できる対戦候補が存在するか確認します。
        /// </summary>
        private bool HasCompatibleOpponentParty()
        {
            if (!HasOpponentParty())
            {
                return false;
            }

            IOnlineService online = NetworkServiceLocator.Online;
            if (IsSinglePartyMatch || online == null)
            {
                return true;
            }

            var expectedSizes = new Dictionary<string, int>();
            var memberCounts = new Dictionary<string, int>();
            foreach (OnlinePlayer player in online.Players)
            {
                if (player == null || !player.TryGetProperty(NetworkPropertyKeys.MatchingPartyMemberCount, out int size))
                {
                    return false;
                }

                string id = ResolvePlayerPartyId(player);
                expectedSizes[id] = expectedSizes.TryGetValue(id, out int previous) ? Math.Max(previous, size) : size;
                memberCounts[id] = memberCounts.TryGetValue(id, out int count) ? count + 1 : 1;
            }

            foreach (KeyValuePair<string, int> party in expectedSizes)
            {
                if (memberCounts[party.Key] != party.Value)
                {
                    return false;
                }
            }

            return PartyTeamPlanner.TryAssign(new List<int>(expectedSizes.Values), HumanTeamCapacity,
                MatchPlayerCount - HumanTeamCapacity, out _);
        }

        /// <summary>
        /// 現在のルームに別パーティが参加しているか確認します。
        /// </summary>
        private bool HasOpponentParty()
        {
            if (IsSinglePartyMatch)
            {
                return true;
            }

            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                onlineService.Players == null)
            {
                return false;
            }

            if (ShouldSplitPlayersIntoSoloParties)
            {
                return onlineService.Players.Any(
                    player =>
                        player != null &&
                        player.PlayerId !=
                        onlineService.LocalPlayerId);
            }

            OnlinePlayer localPlayer =
                onlineService.Players.FirstOrDefault(
                    player =>
                        player != null &&
                        player.PlayerId ==
                        onlineService.LocalPlayerId);

            if (localPlayer == null ||
                !localPlayer.TryGetProperty(
                    NetworkPropertyKeys.PartyId,
                    out string localPartyId) ||
                string.IsNullOrWhiteSpace(
                    localPartyId))
            {
                return true;
            }

            foreach (OnlinePlayer player in
                    onlineService.Players)
            {
                if (player == null ||
                    player.PlayerId ==
                    onlineService.LocalPlayerId)
                {
                    continue;
                }

                if (!player.TryGetProperty(
                        NetworkPropertyKeys.PartyId,
                        out string partyId) ||
                    string.IsNullOrWhiteSpace(
                        partyId))
                {
                    return true;
                }

                if (!string.Equals(
                        partyId,
                        localPartyId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 指定されたプレイヤーがローカルパーティの
        /// メンバーか確認します。
        /// </summary>
        private bool IsLocalPartyPlayer(
            OnlinePlayer player)
        {
            if (player == null ||
                ShouldSplitPlayersIntoSoloParties)
            {
                return false;
            }

            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            OnlinePlayer localPlayer =
                onlineService?.Players.FirstOrDefault(
                    currentPlayer =>
                        currentPlayer != null &&
                        currentPlayer.PlayerId ==
                        onlineService.LocalPlayerId);

            if (localPlayer == null ||
                !localPlayer.TryGetProperty(
                    NetworkPropertyKeys.PartyId,
                    out string localPartyId) ||
                string.IsNullOrWhiteSpace(
                    localPartyId))
            {
                return false;
            }

            return player.TryGetProperty(
                    NetworkPropertyKeys.PartyId,
                    out string partyId) &&
                string.Equals(
                    partyId,
                    localPartyId,
                    StringComparison.Ordinal);
        }

        /// <summary>
        /// ホストがマッチング参加人数と準備状態を確認し、
        /// 条件を満たした場合にゲームを開始します。
        /// </summary>
        private void TryStartGame()
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            GameFlowManager gameFlowManager =
                GameFlowManager.Instance;

            if (onlineService == null ||
                gameFlowManager == null ||
                !onlineService.IsInRoom ||
                onlineService.CurrentRoom == null ||
                !IsCurrentRoomType(
                    NetworkPropertyKeys.TypeMatching) ||
                !onlineService.IsHost ||
                !_isMatching ||
                _isCancelingMatching ||
                _isGameStarting ||
                gameFlowManager.IsGameplay)
            {
                return;
            }

            if (TryGetCurrentRoomString(
                    NetworkPropertyKeys.State,
                    out string currentState) &&
                !string.Equals(
                    currentState,
                    NetworkPropertyKeys.StateWaiting,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!AreAllPlayersMatchingReady())
            {
                return;
            }

            if (!CreateTeams())
            {
                return;
            }

            _isGameStarting = true;
            _isMatching = false;
            onlineService.SetRoomOpen(false);
            onlineService.SetRoomVisible(false);

            _assignedTeamProperties[NetworkPropertyKeys.State] = NetworkPropertyKeys.StatePlaying;
            onlineService.SetRoomProperties(new Dictionary<string, object>(_assignedTeamProperties));

            StartGame();
        }

        /// <summary>
        /// 全プレイヤーがマッチング準備完了かどうかを確認します。
        /// </summary>
        /// <returns>
        /// 人数と準備状態が条件を満たす場合は<c>true</c>です。
        /// </returns>
        private bool AreAllPlayersMatchingReady()
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                onlineService.Players.Count !=
                MatchPlayerCount)
            {
                return false;
            }

            foreach (
                OnlinePlayer player
                in onlineService.Players)
            {
                if (player == null ||
                    !player.TryGetProperty(
                        NetworkPropertyKeys.MatchingReady,
                        out bool isReady) ||
                    !isReady)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// パーティ構成を維持しながらプレイヤーを
        /// 2つのチームへ割り当てます。
        /// </summary>
        /// <returns>
        /// チーム編成に成功した場合は<c>true</c>です。
        /// </returns>
        private bool CreateTeams()
        {
            _assignedTeamProperties.Clear();
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                return false;
            }

            if (IsSinglePartyMatch)
            {
                List<OnlinePlayer> humanTeam =
                    onlineService.Players
                        .Where(player => player != null)
                        .OrderBy(player => player.PlayerId)
                        .ToList();

                Debug.Log(
                    $"{nameof(MatchmakingManager)}: " +
                    "人間プレイヤーをチームAへ割り当てます。" +
                    $"TeamA={humanTeam.Count}, " +
                    "SingleTeamDebug=True",
                    this);

                AssignTeam(
                    humanTeam,
                    k_TeamAId);

                return true;
            }

            _partyMap.Clear();

            foreach (
                OnlinePlayer player
                in onlineService.Players)
            {
                if (player == null)
                {
                    continue;
                }

                string partyId =
                    ResolvePlayerPartyId(player);

                if (!_partyMap.TryGetValue(
                        partyId,
                        out List<OnlinePlayer> party))
                {
                    party = new List<OnlinePlayer>();
                    _partyMap.Add(
                        partyId,
                        party);
                }

                party.Add(player);
            }

            List<List<OnlinePlayer>> parties =
                new(_partyMap.Values);

            parties.Sort(CompareParties);

            List<OnlinePlayer> teamA = new();
            List<OnlinePlayer> teamB = new();

            var sizes = new List<int>();
            foreach (List<OnlinePlayer> party in parties)
            {
                foreach (OnlinePlayer player in party)
                {
                    if (!player.TryGetProperty(NetworkPropertyKeys.MatchingPartyMemberCount, out int expected) ||
                        expected != party.Count)
                    {
                        return false;
                    }
                }

                sizes.Add(party.Count);
            }

            if (!PartyTeamPlanner.TryAssign(sizes, HumanTeamCapacity, MatchPlayerCount - HumanTeamCapacity, out bool[] assignment))
            {
                return false;
            }

            for (int i = 0; i < parties.Count; i++)
            {
                (assignment[i] ? teamA : teamB).AddRange(parties[i]);
            }

            Debug.Log(
                $"{nameof(MatchmakingManager)}: " +
                "チーム編成が完了しました。" +
                $"TeamA={teamA.Count}, " +
                $"TeamB={teamB.Count}",
                this);

            AssignTeam(
                teamA,
                k_TeamAId);

            AssignTeam(
                teamB,
                k_TeamBId);

            return true;
        }

        /// <summary>
        /// 人数の多いパーティを優先し、
        /// 同人数の場合は最小プレイヤーIDで並べ替えます。
        /// </summary>
        /// <param name="left">
        /// 比較する左側のパーティです。
        /// </param>
        /// <param name="right">
        /// 比較する右側のパーティです。
        /// </param>
        /// <returns>
        /// 並べ替えに使用する比較結果です。
        /// </returns>
        private static int CompareParties(
            List<OnlinePlayer> left,
            List<OnlinePlayer> right)
        {
            int countComparison =
                right.Count.CompareTo(left.Count);

            if (countComparison != 0)
            {
                return countComparison;
            }

            int leftMinimumPlayerId =
                left.Count > 0
                    ? left.Min(player => player.PlayerId)
                    : int.MaxValue;

            int rightMinimumPlayerId =
                right.Count > 0
                    ? right.Min(player => player.PlayerId)
                    : int.MaxValue;

            return leftMinimumPlayerId.CompareTo(
                rightMinimumPlayerId);
        }

        /// <summary>
        /// 指定されたプレイヤーへチームIDを設定します。
        /// </summary>
        /// <param name="team">
        /// チームへ所属するプレイヤー一覧です。
        /// </param>
        /// <param name="teamId">
        /// 設定するチームIDです。
        /// </param>
        private void AssignTeam(
            IEnumerable<OnlinePlayer> team,
            int teamId)
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                team == null)
            {
                return;
            }

            foreach (OnlinePlayer player in team)
            {
                if (player == null)
                {
                    continue;
                }

                _assignedTeamProperties[MatchTeamAssignment.GetKey(player.PlayerId)] = teamId;
                onlineService.SetPlayerProperty(
                    player.PlayerId,
                    NetworkPropertyKeys.TeamId,
                    teamId);
            }
        }

        /// <summary>
        /// ルームの状態がplayingになったことを検出し、
        /// 非ホスト側のゲーム開始処理を実行します。
        /// </summary>
        /// <param name="reason">
        /// 状態変化を検出した理由です。
        /// </param>
        private void StartGameFromRoomState(
            string reason)
        {
            GameFlowManager gameFlowManager =
                GameFlowManager.Instance;

            if (gameFlowManager == null ||
                gameFlowManager.IsGameplay ||
                _isCancelingMatching ||
                _isGameStarting)
            {
                return;
            }

            Debug.Log(
                $"{nameof(MatchmakingManager)}: " +
                "ルームのplaying状態を検出しました。" +
                $"Reason={reason}",
                this);

            _isGameStarting = true;
            _isMatching = false;

            StartGame();
        }

        /// <summary>
        /// キャラクター選択状態へ遷移します。
        /// </summary>
        private void StartGame()
        {
            GameFlowManager gameFlowManager =
                GameFlowManager.Instance;

            if (gameFlowManager == null ||
                gameFlowManager.IsGameplay || !TryApplyRoomGameMode())
            {
                return;
            }

            Debug.Log(
                $"{nameof(MatchmakingManager)}: " +
                "キャラクター選択へ遷移します。",
                this);

            gameFlowManager.SetGameplayState(true);

            gameFlowManager.ChangeState(
                new CharacterSelectState());
        }

        /// <summary>
        /// ルームの確定ルールを検証し、参加者のState遷移に先立って適用します。
        /// </summary>
        /// <remarks>
        /// モードIDを持たない既存ルームは殲滅戦として扱います。
        /// 不明なIDを別ルールへ置き換えて試合を開始しません。
        /// </remarks>
        private bool TryApplyRoomGameMode()
        {
            GameFlowManager flow = GameFlowManager.Instance;
            if (flow == null)
            {
                return false;
            }

            string modeId = TryGetCurrentRoomString(NetworkPropertyKeys.GameModeId, out string roomModeId)
                ? roomModeId : GameModeRegistry.k_DefaultModeId;
            if (!GameModeRegistry.Catalog.TryGet(modeId, PlayCategory.Online, out _))
            {
                Debug.LogError($"参加先のゲームモードを利用できません: {modeId}", this);
                return false;
            }

            flow.SetGameMode(PlayCategory.Online, modeId);
            return true;
        }

        /// <summary>
        /// ゲームルール別のマッチングをオンラインAdapterへ要求します。
        /// </summary>
        private void JoinSelectedModeRoom(IOnlineService onlineService)
        {
            string modeId = _matchingGameModeId;
            if (onlineService is IGameModeMatchmakingService modeService)
            {
                modeService.JoinModeRoom(MatchPartyCount, MatchPartySize, modeId);
                return;
            }

            if (modeId != GameModeRegistry.k_DefaultModeId)
            {
                throw new InvalidOperationException("オンラインサービスがゲームルール別のマッチングに対応していません。");
            }

            onlineService.JoinMatchingRoom(MatchPartyCount, MatchPartySize);
        }

        /// <summary>
        /// ローカルプレイヤーへ設定するパーティIDを決定します。
        /// </summary>
        /// <param name="localPlayer">
        /// ローカルプレイヤーです。
        /// </param>
        /// <returns>
        /// マッチングで使用するパーティIDです。
        /// </returns>
        private string ResolveLocalPartyId(OnlinePlayer localPlayer)
        {
            return !string.IsNullOrWhiteSpace(_matchingPartyId)
                ? _matchingPartyId
                : CreateSoloPartyId(localPlayer);
        }

        /// <summary>
        /// ルーム移動で失われるパーティ識別子と予約人数を参加前に設定します。
        /// </summary>
        private void PrepareMatchingMemberProperties(IOnlineService onlineService)
        {
            _initializedMatchingRoom = null;
            onlineService.SetLocalPlayerProperties(new Dictionary<string, object>
            {
                { NetworkPropertyKeys.PartyId, _matchingPartyId },
                { NetworkPropertyKeys.MatchingPartyMemberCount, _matchingPartyMemberCount },
                { NetworkPropertyKeys.MatchingReady, false }
            });
        }

        /// <summary>
        /// 指定されたプレイヤーのパーティIDを取得します。
        /// </summary>
        /// <param name="player">
        /// パーティIDを取得するプレイヤーです。
        /// </param>
        /// <returns>
        /// プレイヤーが所属するパーティのIDです。
        /// </returns>
        private string ResolvePlayerPartyId(
            OnlinePlayer player)
        {
            if (player.TryGetProperty(
                    NetworkPropertyKeys.PartyId,
                    out string partyId) &&
                !string.IsNullOrWhiteSpace(partyId))
            {
                return partyId;
            }

            return CreateSoloPartyId(player);
        }

        /// <summary>
        /// プレイヤー固有のソロパーティIDを生成します。
        /// </summary>
        /// <param name="player">
        /// パーティIDを生成するプレイヤーです。
        /// </param>
        /// <returns>
        /// 生成されたソロパーティIDです。
        /// </returns>
        private string CreateSoloPartyId(
            OnlinePlayer player)
        {
            if (player != null &&
                player.TryGetProperty(
                    k_ProductUserIdPropertyKey,
                    out string productUserId) &&
                !string.IsNullOrWhiteSpace(
                    productUserId))
            {
                return $"{k_SoloPartyIdPrefix}{productUserId}";
            }

            if (player != null)
            {
                return
                    $"{k_SoloPartyIdPrefix}player_{player.PlayerId}";
            }

            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService != null &&
                onlineService.LocalPlayerId >= 0)
            {
                return
                    $"{k_SoloPartyIdPrefix}player_" +
                    $"{onlineService.LocalPlayerId}";
            }

            return $"{k_SoloPartyIdPrefix}unknown";
        }

        /// <summary>
        /// 現在のルームが指定された種類かどうかを確認します。
        /// </summary>
        /// <param name="expectedType">
        /// 期待するルーム種類です。
        /// </param>
        /// <returns>
        /// ルーム種類が一致する場合は<c>true</c>です。
        /// </returns>
        private bool IsCurrentRoomType(
            string expectedType)
        {
            if (!TryGetCurrentRoomString(
                    NetworkPropertyKeys.Type,
                    out string roomType))
            {
                return false;
            }

            return string.Equals(
                roomType,
                expectedType,
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 現在のルームが指定された状態かどうかを確認します。
        /// </summary>
        /// <param name="expectedState">
        /// 期待するルーム状態です。
        /// </param>
        /// <returns>
        /// ルーム状態が一致する場合は<c>true</c>です。
        /// </returns>
        private bool IsCurrentRoomState(
            string expectedState)
        {
            if (!TryGetCurrentRoomString(
                    NetworkPropertyKeys.State,
                    out string roomState))
            {
                return false;
            }

            return string.Equals(
                roomState,
                expectedState,
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 現在のルームから文字列プロパティを取得します。
        /// </summary>
        /// <param name="key">
        /// 取得するプロパティキーです。
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
            value = string.Empty;

            OnlineRoom currentRoom =
                NetworkServiceLocator.Online?.CurrentRoom;

            if (currentRoom == null)
            {
                return false;
            }

            if (currentRoom.TryGetProperty(
                    key,
                    out string directValue))
            {
                value = directValue;

                return true;
            }

            if (currentRoom.Properties == null)
            {
                return false;
            }

            foreach (
                KeyValuePair<string, object> property
                in currentRoom.Properties)
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
        /// 変更されたプロパティ一覧から文字列値を取得します。
        /// </summary>
        /// <param name="changedProperties">
        /// 変更されたプロパティ一覧です。
        /// </param>
        /// <param name="key">
        /// 取得するプロパティキーです。
        /// </param>
        /// <param name="value">
        /// 取得された文字列です。
        /// </param>
        /// <returns>
        /// 値を取得できた場合は<c>true</c>です。
        /// </returns>
        private static bool TryGetChangedString(
            IReadOnlyDictionary<string, object>
                changedProperties,
            string key,
            out string value)
        {
            value = string.Empty;

            if (changedProperties == null)
            {
                return false;
            }

            foreach (
                KeyValuePair<string, object> property
                in changedProperties)
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
        /// 変更されたプロパティ一覧からbool値を取得します。
        /// </summary>
        /// <param name="changedProperties">
        /// 変更されたプロパティ一覧です。
        /// </param>
        /// <param name="key">
        /// 取得するプロパティキーです。
        /// </param>
        /// <param name="value">
        /// 取得されたbool値です。
        /// </param>
        /// <returns>
        /// 値を取得できた場合は<c>true</c>です。
        /// </returns>
        private static bool TryGetChangedBoolean(
            IReadOnlyDictionary<string, object>
                changedProperties,
            string key,
            out bool value)
        {
            value = false;

            if (changedProperties == null)
            {
                return false;
            }

            foreach (
                KeyValuePair<string, object> property
                in changedProperties)
            {
                if (!string.Equals(
                        property.Key,
                        key,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (property.Value is bool booleanValue)
                {
                    value = booleanValue;

                    return true;
                }

                return bool.TryParse(
                    property.Value?.ToString(),
                    out value);
            }

            return false;
        }

        /// <summary>
        /// プロパティ一覧に指定されたキーが含まれるか確認します。
        /// </summary>
        /// <param name="properties">
        /// 確認するプロパティ一覧です。
        /// </param>
        /// <param name="key">
        /// 確認するキーです。
        /// </param>
        /// <returns>
        /// 大文字と小文字を無視してキーが存在する場合は<c>true</c>です。
        /// </returns>
        private static bool ContainsPropertyKey(
            IReadOnlyDictionary<string, object> properties,
            string key)
        {
            if (properties == null)
            {
                return false;
            }

            foreach (string propertyKey in properties.Keys)
            {
                if (string.Equals(
                        propertyKey,
                        key,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// ルームIDとパスワードを検証します。
        /// </summary>
        /// <param name="roomId">
        /// 検証するルームIDです。
        /// </param>
        /// <param name="password">
        /// 検証するパスワードです。
        /// </param>
        /// <returns>
        /// 入力が有効な場合は<c>null</c>、
        /// 無効な場合はエラーメッセージです。
        /// </returns>
        private static string ValidatePartyCredentials(
            string roomId,
            string password)
        {
            if (string.IsNullOrEmpty(roomId) ||
                !s_RoomIdRegex.IsMatch(roomId))
            {
                return "部屋番号は4桁の数字で入力してください。";
            }

            if (string.IsNullOrEmpty(password) ||
                !s_PasswordRegex.IsMatch(password))
            {
                return "パスワードは4桁の数字で入力してください。";
            }

            return null;
        }

        /// <summary>
        /// マッチングに関する内部状態を初期化します。
        /// </summary>
        private void ResetMatchingState()
        {
            _sessionPartyCount = 0;
            _sessionPartySize = 0;
            _isCancelingMatching = false;
            _isMatching = false;
            _isGameStarting = false;
            _isPartyMatching = false;
            _wasPartyLeader = false;
            _matchingPartyId = null;
            _initializedMatchingRoom = null;
            _matchingRoomTimeoutJitterSeconds = 0.0f;
            _matchingPartyMemberCount = 1;
            _isRequeueingMatching = false;
            _matchingRoomJoinedTime = 0.0f;
            _nextMatchingStartCheckTime = 0.0f;
            _matchingRequeueAttempt = 0;
            _lastHandledRequeueToken = null;
            _shouldReturnToPartyRoom = false;
            _returnRoomId = null;
            _returnPassword = null;
            _partyMap.Clear();
        }

        /// <summary>
        /// ゲーム終了後にマッチング状態と
        /// ローカルプレイヤープロパティを初期化します。
        /// </summary>
        public void ResetAfterGame()
        {
            ResetMatchingState();

            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService != null &&
                onlineService.LocalPlayerId >= 0)
            {
                onlineService.SetLocalPlayerProperties(
                    new Dictionary<string, object>
                    {
                        {
                            NetworkPropertyKeys.MatchingReady,
                            false
                        },
                        {
                            NetworkPropertyKeys.Ready,
                            false
                        },
                        {
                            NetworkPropertyKeys.PartyId,
                            string.Empty
                        },
                        {
                            NetworkPropertyKeys.RoomId,
                            string.Empty
                        },
                        {
                            NetworkPropertyKeys.TeamId,
                            k_UnassignedTeamId
                        }
                    });
            }

            Debug.Log(
                $"{nameof(MatchmakingManager)}: " +
                "ゲーム終了後のマッチング状態を初期化しました。",
                this);
        }

        /// <summary>
        /// AI対戦相手を使用する単一パーティモードかどうかを取得します。
        /// </summary>
        /// <returns>
        /// 単一パーティモードの場合は<c>true</c>です。
        /// </returns>
        public bool IsSinglePartyMatchMode()
        {
            return IsSinglePartyMatch;
        }

        /// <summary>
        /// マッチングに使用するパーティ数を取得します。
        /// </summary>
        /// <returns>
        /// マッチングへ参加するパーティ数です。
        /// </returns>
        public int GetPartyCount()
        {
            return MatchPartyCount;
        }

        /// <summary>
        /// パーティルームへ参加できる最大人数を取得します。
        /// </summary>
        /// <returns>
        /// パーティルームの最大人数です。
        /// </returns>
        public int GetPartySize()
        {
            return MatchPartySize;
        }

        /// <summary>
        /// マッチングに使用する1パーティの人数を取得します。
        /// </summary>
        /// <returns>
        /// マッチング用のパーティ人数です。
        /// </returns>
        public int GetMatchPartySize()
        {
            return MatchPartySize;
        }

        /// <summary>
        /// マッチングへ参加する人間プレイヤーの合計人数を取得します。
        /// </summary>
        /// <returns>
        /// 期待する人間プレイヤー数です。
        /// </returns>
        public int GetExpectedHumanPlayerCount()
        {
            return MatchPlayerCount;
        }

        /// <summary>
        /// 1チームへ参加できる人間プレイヤーの最大人数を取得します。
        /// </summary>
        /// <returns>
        /// 人間チームの最大人数です。
        /// </returns>
        public int GetHumanTeamCapacity()
        {
            return HumanTeamCapacity;
        }

        /// <summary>
        /// 相手チームへAIを使用する設定かどうかを取得します。
        /// </summary>
        /// <returns>
        /// AI対戦相手を使用する場合は<c>true</c>です。
        /// </returns>
        public bool UsesAiOpponentTeam()
        {
            return false;
        }
    }
}
