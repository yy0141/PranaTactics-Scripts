using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using PranaTactics.GameFlow;

namespace PranaTactics.Networking
{
    /// <summary>
    /// パーティルームの作成・参加、準備完了状態、
    /// パーティ単位のマッチング開始およびキャンセルを管理します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartyManager : MonoBehaviour
    {
        /// <summary>
        /// パーティルーム名に使用する接頭辞です。
        /// </summary>
        private const string k_PartyRoomNamePrefix = "party_";

        /// <summary>
        /// パーティ人数を取得できない場合に使用する既定値です。
        /// </summary>
        private const int k_DefaultPartyPlayerCount = 2;

        private enum PendingPartyOperation
        {
            None = 0,

            Create = 1,

            Join = 2
        }

        /// <summary>
        /// 現在有効なPartyManagerのインスタンスです。
        /// </summary>
        private static PartyManager s_Instance;

        /// <summary>
        /// パーティルームへ参加できる最大プレイヤー数です。
        /// </summary>
        private int _partyPlayerCount =
            k_DefaultPartyPlayerCount;

        /// <summary>
        /// ローカルプレイヤーのReady状態を
        /// リセット中かどうかを示します。
        /// </summary>
        /// <summary>
        /// キャンセル確定待ちの間に遅れた開始通知を再実行しないようにします。
        /// </summary>
        private bool _ignorePartyStartUntilWaiting;

        private bool _isResettingLocalReady;

        /// <summary>
        /// オンラインサービスのイベントを
        /// 購読済みかどうかを示します。
        /// </summary>
        private bool _areOnlineEventsRegistered;

        /// <summary>
        /// ホストがパーティマッチング開始処理を
        /// 実行中かどうかを示します。
        /// </summary>
        private bool _isStartingPartyMatching;

        /// <summary>
        /// パーティマッチングから通常マッチングへの
        /// 遷移処理を開始済みかどうかを示します。
        /// </summary>
        private bool _hasStartedPartyMatchingFlow;

        /// <summary>
        /// 現在完了を待機しているパーティルーム操作です。
        /// </summary>
        private PendingPartyOperation _pendingPartyOperation =
            PendingPartyOperation.None;

        /// <summary>
        /// イベントを購読したオンラインサービスです。
        /// </summary>
        private IOnlineService _registeredOnlineService;

        /// <summary>
        /// オンラインサービスの登録を待機するコルーチンです。
        /// </summary>
        private Coroutine _registerEventsCoroutine;

        public static PartyManager Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance =
                        UnityEngine.Object
                            .FindFirstObjectByType<PartyManager>();
                }

                return s_Instance;
            }
            private set
            {
                s_Instance = value;
            }
        }

        public string RoomId
        {
            get; private set;
        }

        public string Password
        {
            get; private set;
        }

        public string PartyId
        {
            get; private set;
        }

        public bool IsLeader
        {
            get
            {
                IOnlineService onlineService =
                    NetworkServiceLocator.Online;

                return onlineService != null &&
                    onlineService.IsHost;
            }
        }

        public event Action PartyCreationSucceeded;

        public event Action<string> PartyCreationFailed;

        public event Action PartyJoinSucceeded;

        public event Action<string> PartyJoinFailed;

        public event Action PartyMatchingStarted;

        public event Action PartyMatchingCanceled;

        public event Action<bool> LocalReadyChanged;

        /// <summary>
        /// 既存のPartyManagerを取得します。
        /// 存在しない場合は新しいGameObjectへ追加して生成します。
        /// </summary>
        /// <returns>
        /// 既存または新しく生成されたPartyManagerです。
        /// </returns>
        public static PartyManager GetOrCreateInstance()
        {
            PartyManager manager = Instance;

            if (manager != null)
            {
                return manager;
            }

            GameObject managerObject =
                new GameObject(nameof(PartyManager));

            manager =
                managerObject.AddComponent<PartyManager>();

            DontDestroyOnLoad(managerObject);

            Debug.Log(
                $"{nameof(PartyManager)}: " +
                "シーン上に存在しなかったため、" +
                "実行時に新しいインスタンスを生成しました。");

            return manager;
        }

        /// <summary>
        /// 既存のPartyManagerを取得します。
        /// </summary>
        /// <param name="manager">
        /// 取得されたPartyManagerです。
        /// </param>
        /// <returns>
        /// インスタンスが存在する場合は<c>true</c>です。
        /// </returns>
        public static bool TryGetExistingInstance(
            out PartyManager manager)
        {
            manager = Instance;

            return manager != null;
        }

        /// <summary>
        /// シングルトンを初期化し、
        /// シーン遷移後もインスタンスを維持します。
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

            DontDestroyOnLoad(gameObject);

            InitializePartyPlayerCount();
        }

        /// <summary>
        /// オンラインサービスのイベント登録処理を開始します。
        /// </summary>
        private void OnEnable()
        {
            if (_registerEventsCoroutine != null)
            {
                return;
            }

            _registerEventsCoroutine =
                StartCoroutine(
                    RegisterOnlineEventsWhenReady());
        }

        /// <summary>
        /// オンラインサービスのイベント購読を解除します。
        /// </summary>
        private void OnDisable()
        {
            StopEventRegistrationCoroutine();
            UnregisterOnlineEvents();
        }

        /// <summary>
        /// シングルトン参照とイベント購読を解除します。
        /// </summary>
        private void OnDestroy()
        {
            StopEventRegistrationCoroutine();
            UnregisterOnlineEvents();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// マッチング設定からパーティルームの
        /// 最大プレイヤー数を取得します。
        /// </summary>
        private void InitializePartyPlayerCount()
        {
            int partyPlayerCount =
                MatchmakingManager.Instance != null
                    ? MatchmakingManager.Instance.GetPartySize()
                    : _partyPlayerCount;

            _partyPlayerCount =
                partyPlayerCount > 0
                    ? partyPlayerCount
                    : k_DefaultPartyPlayerCount;
        }

        /// <summary>
        /// オンラインサービスが登録されるまで待機し、
        /// 必要なイベントを購読します。
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

            onlineService.PlayerPropertiesChanged +=
                HandlePlayerPropertiesChanged;

            onlineService.RoomPropertiesChanged +=
                HandleRoomPropertiesChanged;

            onlineService.JoinedRoom +=
                HandleJoinedRoom;

            onlineService.PlayerJoined +=
                HandlePlayerListChanged;

            onlineService.PlayerLeft +=
                HandlePlayerListChanged;

            onlineService.PartyRoomCreationFailed +=
                HandlePartyCreationFailed;

            onlineService.PartyRoomJoinFailed +=
                HandlePartyJoinFailed;

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

            _registeredOnlineService.PlayerPropertiesChanged -=
                HandlePlayerPropertiesChanged;

            _registeredOnlineService.RoomPropertiesChanged -=
                HandleRoomPropertiesChanged;

            _registeredOnlineService.JoinedRoom -=
                HandleJoinedRoom;

            _registeredOnlineService.PlayerJoined -=
                HandlePlayerListChanged;

            _registeredOnlineService.PlayerLeft -=
                HandlePlayerListChanged;

            _registeredOnlineService.PartyRoomCreationFailed -=
                HandlePartyCreationFailed;

            _registeredOnlineService.PartyRoomJoinFailed -=
                HandlePartyJoinFailed;

            _registeredOnlineService = null;
            _areOnlineEventsRegistered = false;
        }

        /// <summary>
        /// オンラインサービスの登録待機コルーチンを停止します。
        /// </summary>
        private void StopEventRegistrationCoroutine()
        {
            if (_registerEventsCoroutine == null)
            {
                return;
            }

            StopCoroutine(_registerEventsCoroutine);
            _registerEventsCoroutine = null;
        }

        /// <summary>
        /// パーティルームIDとパスワードから
        /// オンラインルーム名を生成します。
        /// </summary>
        /// <param name="roomId">
        /// パーティルームの識別子です。
        /// </param>
        /// <param name="password">
        /// パーティルームのパスワードです。
        /// </param>
        /// <returns>
        /// オンラインサービスで使用するルーム名です。
        /// </returns>
        private static string CreatePartyRoomName(
            string roomId,
            string password)
        {
            return
                $"{k_PartyRoomNamePrefix}" +
                $"{roomId}_{password}";
        }

        /// <summary>
        /// パーティマッチングに関する内部状態を初期化します。
        /// </summary>
        private void ResetPartyMatchingFlags()
        {
            _ignorePartyStartUntilWaiting = false;
            _isStartingPartyMatching = false;
            _hasStartedPartyMatchingFlow = false;
            _isResettingLocalReady = false;
        }

        /// <summary>
        /// 現在参加しているパーティルーム情報を設定します。
        /// </summary>
        /// <param name="roomId">
        /// パーティルームの識別子です。
        /// </param>
        /// <param name="password">
        /// パーティルームのパスワードです。
        /// </param>
        public void SetPartyInfo(
            string roomId,
            string password)
        {
            RoomId = roomId ?? string.Empty;
            Password = password ?? string.Empty;
            PartyId = $"{RoomId}_{Password}";

            ResetPartyMatchingFlags();

            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                return;
            }

            SetInitialLocalPlayerProperties(
                onlineService);
        }

        /// <summary>
        /// 新しいパーティルームの作成を開始します。
        /// </summary>
        /// <param name="roomId">
        /// 作成するパーティルームの識別子です。
        /// </param>
        /// <param name="password">
        /// パーティルームのパスワードです。
        /// </param>
        public void CreateParty(
            string roomId,
            string password)
        {
            InitializePartyPlayerCount();

            RoomId = roomId ?? string.Empty;
            Password = password ?? string.Empty;
            PartyId = $"{RoomId}_{Password}";

            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                PartyCreationFailed?.Invoke(
                    "オンラインサービスが初期化されていません。");

                return;
            }

            ResetPartyMatchingFlags();

            _pendingPartyOperation =
                PendingPartyOperation.Create;

            SetInitialLocalPlayerProperties(
                onlineService);

            onlineService.CreatePartyRoom(
                CreatePartyRoomName(
                    RoomId,
                    Password),
                RoomId,
                Password,
                PartyId,
                _partyPlayerCount);
        }

        /// <summary>
        /// 指定されたパーティルームへの参加を開始します。
        /// </summary>
        /// <param name="roomId">
        /// 参加するパーティルームの識別子です。
        /// </param>
        /// <param name="password">
        /// パーティルームのパスワードです。
        /// </param>
        public void JoinParty(
            string roomId,
            string password)
        {
            InitializePartyPlayerCount();

            RoomId = roomId ?? string.Empty;
            Password = password ?? string.Empty;
            PartyId = $"{RoomId}_{Password}";

            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                PartyJoinFailed?.Invoke(
                    "オンラインサービスが初期化されていません。");

                return;
            }

            ResetPartyMatchingFlags();

            _pendingPartyOperation =
                PendingPartyOperation.Join;

            SetInitialLocalPlayerProperties(
                onlineService);

            onlineService.JoinPartyRoom(
                RoomId,
                Password);
        }

        /// <summary>
        /// パーティ参加前にローカルプレイヤーへ
        /// 共通プロパティを設定します。
        /// </summary>
        /// <param name="onlineService">
        /// プロパティを設定するオンラインサービスです。
        /// </param>
        private void SetInitialLocalPlayerProperties(
            IOnlineService onlineService)
        {
            onlineService.SetLocalPlayerProperties(
                new Dictionary<string, object>
                {
                    {
                        NetworkPropertyKeys.PartyId,
                        PartyId
                    },
                    {
                        NetworkPropertyKeys.RoomId,
                        RoomId
                    },
                    {
                        NetworkPropertyKeys.Ready,
                        false
                    },
                    {
                        NetworkPropertyKeys.MatchingReady,
                        false
                    }
                });
        }

        /// <summary>
        /// パーティルームへの参加完了を処理します。
        /// </summary>
        private void HandleJoinedRoom()
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                !onlineService.IsInRoom ||
                onlineService.CurrentRoom == null ||
                !IsCurrentPartyRoom())
            {
                return;
            }

            _hasStartedPartyMatchingFlow = false;
            _isStartingPartyMatching = false;
            LocalReadyChanged?.Invoke(false);
            if (IsPartyMatchingActive())
            {
                StartPartyMatchingFlow("JoinedPartyState");
            }

            switch (_pendingPartyOperation)
            {
                case PendingPartyOperation.Create:
                    _pendingPartyOperation =
                        PendingPartyOperation.None;

                    PartyCreationSucceeded?.Invoke();
                    break;

                case PendingPartyOperation.Join:
                    _pendingPartyOperation =
                        PendingPartyOperation.None;

                    PartyJoinSucceeded?.Invoke();
                    break;
            }
        }

        /// <summary>
        /// パーティルームの作成失敗を処理します。
        /// </summary>
        /// <param name="message">
        /// オンラインサービスから返されたエラーメッセージです。
        /// </param>
        private void HandlePartyCreationFailed(
            string message)
        {
            if (_pendingPartyOperation !=
                PendingPartyOperation.Create)
            {
                return;
            }

            _pendingPartyOperation =
                PendingPartyOperation.None;

            PartyCreationFailed?.Invoke(
                string.IsNullOrWhiteSpace(message)
                    ? "部屋の作成に失敗しました。"
                    : message);
        }

        /// <summary>
        /// パーティルームへの参加失敗を処理します。
        /// </summary>
        /// <param name="message">
        /// オンラインサービスから返されたエラーメッセージです。
        /// </param>
        private void HandlePartyJoinFailed(
            string message)
        {
            if (_pendingPartyOperation !=
                PendingPartyOperation.Join)
            {
                return;
            }

            _pendingPartyOperation =
                PendingPartyOperation.None;

            PartyJoinFailed?.Invoke(
                string.IsNullOrWhiteSpace(message)
                    ? "部屋への参加に失敗しました。"
                    : message);
        }

        /// <summary>
        /// ローカルプレイヤーのReady状態を設定します。
        /// </summary>
        /// <param name="isReady">
        /// 準備完了状態にする場合は<c>true</c>です。
        /// </param>
        public void SetReady(bool isReady)
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                !onlineService.IsHost ||
                !onlineService.IsInRoom ||
                onlineService.CurrentRoom == null ||
                !IsCurrentPartyRoom())
            {
                return;
            }

            if (isReady)
            {
                _isResettingLocalReady = false;
            }

            onlineService.SetLocalPlayerProperty(
                NetworkPropertyKeys.Ready,
                isReady);

            LocalReadyChanged?.Invoke(isReady);

            Debug.Log(
                $"{nameof(PartyManager)}: " +
                "ローカルプレイヤーのReady状態を変更しました。" +
                $"IsReady={isReady}, " +
                $"PlayerId={onlineService.LocalPlayerId}, " +
                $"IsHost={onlineService.IsHost}",
                this);

            if (isReady)
            {
                TryStartPartyMatchingAsHost(
                    "LocalReadyEnabled");

                return;
            }

            CancelPartyMatchingAsHost("HostCanceled");
        }

        /// <summary>
        /// 必要人数のプレイヤーが参加し、
        /// 全員がReady状態かどうかを確認します。
        /// </summary>
        /// <returns>
        /// 全員がReadyの場合は<c>true</c>です。
        /// </returns>
        public bool IsAllReady()
        {
            InitializePartyPlayerCount();

            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                onlineService.Players == null)
            {
                return false;
            }

            if (onlineService.Players.Count <
                _partyPlayerCount)
            {
                Debug.Log(
                    $"{nameof(PartyManager)}: " +
                    "パーティメンバーの参加を待機しています。" +
                    $"Current={onlineService.Players.Count}, " +
                    $"Required={_partyPlayerCount}",
                    this);

                return false;
            }

            foreach (
                OnlinePlayer player
                in onlineService.Players)
            {
                if (!TryGetReady(
                        player,
                        out bool isReady))
                {
                    Debug.Log(
                        $"{nameof(PartyManager)}: " +
                        "プレイヤーのReadyプロパティを取得できません。" +
                        $"PlayerId={player?.PlayerId}",
                        this);

                    return false;
                }

                if (!isReady)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// プレイヤーのReady状態を取得します。
        /// </summary>
        /// <param name="player">
        /// Ready状態を確認するプレイヤーです。
        /// </param>
        /// <param name="isReady">
        /// 取得されたReady状態です。
        /// </param>
        /// <returns>
        /// Readyプロパティを取得できた場合は<c>true</c>です。
        /// </returns>
        private static bool TryGetReady(
            OnlinePlayer player,
            out bool isReady)
        {
            isReady = false;

            if (player == null)
            {
                return false;
            }

            if (player.TryGetProperty(
                    NetworkPropertyKeys.Ready,
                    out bool booleanReady))
            {
                isReady = booleanReady;

                return true;
            }

            if (player.TryGetProperty(
                    NetworkPropertyKeys.Ready,
                    out string stringReady))
            {
                return bool.TryParse(
                    stringReady,
                    out isReady);
            }

            if (!TryGetPlayerPropertyIgnoreCase(
                    player,
                    NetworkPropertyKeys.Ready,
                    out object rawReady))
            {
                return false;
            }

            if (rawReady is bool rawBoolean)
            {
                isReady = rawBoolean;

                return true;
            }

            return bool.TryParse(
                rawReady?.ToString(),
                out isReady);
        }

        /// <summary>
        /// プレイヤープロパティを大文字と小文字を無視して取得します。
        /// </summary>
        /// <param name="player">
        /// プロパティを保持するプレイヤーです。
        /// </param>
        /// <param name="key">
        /// 取得するプロパティキーです。
        /// </param>
        /// <param name="value">
        /// 取得されたプロパティ値です。
        /// </param>
        /// <returns>
        /// プロパティを取得できた場合は<c>true</c>です。
        /// </returns>
        private static bool TryGetPlayerPropertyIgnoreCase(
            OnlinePlayer player,
            string key,
            out object value)
        {
            value = null;

            if (player?.Properties == null)
            {
                return false;
            }

            foreach (
                KeyValuePair<string, object> property
                in player.Properties)
            {
                if (!string.Equals(
                        property.Key,
                        key,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                value = property.Value;

                return true;
            }

            return false;
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
            if (changedProperties == null ||
                !IsCurrentPartyRoom() ||
                !ContainsPropertyKey(
                    changedProperties,
                    NetworkPropertyKeys.Ready))
            {
                return;
            }

            if (_isResettingLocalReady &&
                IsLocalPlayer(targetPlayer) &&
                TryGetChangedBoolean(
                    changedProperties,
                    NetworkPropertyKeys.Ready,
                    out bool changedReady) &&
                !changedReady)
            {
                _isResettingLocalReady = false;

                return;
            }

        }

        /// <summary>
        /// パーティメンバーの参加・退出後にReady状態を確認します。
        /// </summary>
        /// <param name="player">
        /// 参加または退出したプレイヤーです。
        /// </param>
        private void HandlePlayerListChanged(
            OnlinePlayer player)
        {
            if (!IsCurrentPartyRoom())
            {
                return;
            }

            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                !onlineService.IsHost)
            {
                return;
            }

            if (IsPartyMatchingActive() && !_hasStartedPartyMatchingFlow)
            {
                CancelPartyMatchingAsHost("PlayerListChanged");
            }
        }

        /// <summary>
        /// ホストの明示的な開始要求を確認し、
        /// パーティマッチングを開始します。
        /// </summary>
        /// <param name="reason">
        /// 開始判定を実行した理由です。
        /// </param>
        private void TryStartPartyMatchingAsHost(
            string reason)
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                !onlineService.IsInRoom ||
                onlineService.CurrentRoom == null ||
                !onlineService.IsHost ||
                !IsCurrentPartyRoom() ||
                _isStartingPartyMatching ||
                _hasStartedPartyMatchingFlow ||
                IsPartyMatchingActive() ||
                onlineService.Players.Count == 0)
            {
                return;
            }

            _isStartingPartyMatching = true;

            Debug.Log(
                $"{nameof(PartyManager)}: " +
                "パーティホストから開始要求を受けたため、" +
                "マッチングを開始します。" +
                $"Reason={reason}",
                this);

            onlineService.SetRoomProperties(
                new Dictionary<string, object>
                {
                    { NetworkPropertyKeys.MatchingPartyCount, MatchmakingManager.Instance.GetPartyCount() },
                    { NetworkPropertyKeys.GameModeId, GameFlowManager.Instance.GameModeId },
                    { NetworkPropertyKeys.MatchingPartySize, MatchmakingManager.Instance.GetMatchPartySize() },
                    { NetworkPropertyKeys.PartyDeparting, false },
                    {
                        NetworkPropertyKeys.PartyMatching,
                        true
                    },
                    {
                        NetworkPropertyKeys.State,
                        NetworkPropertyKeys.StateMatching
                    }
                });


        }

        /// <summary>
        /// ホスト側でパーティマッチング状態を解除します。
        /// </summary>
        /// <param name="reason">
        /// キャンセルを実行した理由です。
        /// </param>
        private void CancelPartyMatchingAsHost(
            string reason)
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                !onlineService.IsInRoom ||
                onlineService.CurrentRoom == null ||
                !onlineService.IsHost ||
                !IsCurrentPartyRoom())
            {
                return;
            }

            Debug.Log(
                $"{nameof(PartyManager)}: " +
                "ホストがパーティマッチングをキャンセルします。" +
                $"Reason={reason}",
                this);

            _ignorePartyStartUntilWaiting = true;
            onlineService.SetRoomProperties(
                new Dictionary<string, object>
                {
                    {
                        NetworkPropertyKeys.PartyMatching,
                        false
                    },
                    {
                        NetworkPropertyKeys.State,
                        NetworkPropertyKeys.StateWaiting
                    }
                });

            CancelLocalPartyMatchingFlow(reason);
        }

        /// <summary>
        /// ルームプロパティの変更から、
        /// パーティマッチングの開始またはキャンセルを判定します。
        /// </summary>
        /// <param name="changedProperties">
        /// 変更されたルームプロパティです。
        /// </param>
        private void HandleRoomPropertiesChanged(
            Dictionary<string, object> changedProperties)
        {
            if (changedProperties == null ||
                !IsCurrentPartyRoom())
            {
                return;
            }

            if (TryGetChangedString(
                    changedProperties,
                    NetworkPropertyKeys.State,
                    out string state))
            {
                if (string.Equals(
                        state,
                        NetworkPropertyKeys.StateMatching,
                        StringComparison.OrdinalIgnoreCase))
                {
                    StartPartyMatchingFlow(
                        "RoomStateChanged");

                    return;
                }

                if (string.Equals(
                        state,
                        NetworkPropertyKeys.StateWaiting,
                        StringComparison.OrdinalIgnoreCase))
                {
                    _ignorePartyStartUntilWaiting = false;
                    CancelLocalPartyMatchingFlow(
                        "RoomStateChanged");

                    return;
                }
            }

            if (!TryGetChangedBoolean(
                    changedProperties,
                    NetworkPropertyKeys.PartyMatching,
                    out bool isPartyMatching))
            {
                return;
            }

            if (isPartyMatching)
            {
                StartPartyMatchingFlow(
                    "PartyMatchingPropertyChanged");
            }
            else
            {
                CancelLocalPartyMatchingFlow(
                    "PartyMatchingPropertyChanged");
            }
        }

        /// <summary>
        /// 通常マッチングへの遷移処理を開始します。
        /// </summary>
        /// <param name="reason">
        /// 遷移処理を開始した理由です。
        /// </param>
        private void StartPartyMatchingFlow(
            string reason)
        {
            if (_hasStartedPartyMatchingFlow || _ignorePartyStartUntilWaiting)
            {
                return;
            }

            MatchmakingManager matchmakingManager =
                MatchmakingManager.Instance;

            if (matchmakingManager == null)
            {
                Debug.LogError(
                    $"{nameof(PartyManager)}: " +
                    $"{nameof(MatchmakingManager)}が存在しません。",
                    this);

                _isStartingPartyMatching = false;

                return;
            }

            _hasStartedPartyMatchingFlow = true;
            _isStartingPartyMatching = true;

            NetworkServiceLocator.Online?.SetLocalPlayerProperty(NetworkPropertyKeys.Ready, true);
            LocalReadyChanged?.Invoke(true);

            bool hasStarted =
                matchmakingManager.StartMatching();

            if (!hasStarted)
            {
                Debug.LogWarning(
                    $"{nameof(PartyManager)}: " +
                    "通常マッチングの開始要求に失敗しました。" +
                    $"Reason={reason}",
                    this);

                _hasStartedPartyMatchingFlow = false;
                _isStartingPartyMatching = false;

                IOnlineService onlineService =
                    NetworkServiceLocator.Online;

                if (onlineService != null &&
                    onlineService.IsHost)
                {
                    CancelPartyMatchingAsHost(
                        "StartMatchingFailed");
                }
                else
                {
                    CancelLocalPartyMatchingFlow(
                        "StartMatchingFailed");
                }

                return;
            }

            Debug.Log(
                $"{nameof(PartyManager)}: " +
                "パーティマッチングから通常マッチングへの" +
                "遷移を開始しました。" +
                $"Reason={reason}",
                this);

            PartyMatchingStarted?.Invoke();
        }

        /// <summary>
        /// ローカル側のパーティマッチング処理をキャンセルします。
        /// </summary>
        /// <param name="reason">
        /// キャンセルを実行した理由です。
        /// </param>
        private void CancelLocalPartyMatchingFlow(
            string reason)
        {
            if (!_hasStartedPartyMatchingFlow &&
                !_isStartingPartyMatching)
            {
                return;
            }

            Debug.Log(
                $"{nameof(PartyManager)}: " +
                "ローカル側のパーティマッチング処理を" +
                "キャンセルします。" +
                $"Reason={reason}",
                this);

            _hasStartedPartyMatchingFlow = false;
            _isStartingPartyMatching = false;

            MatchmakingManager matchmakingManager =
                MatchmakingManager.Instance;

            if (matchmakingManager != null && !matchmakingManager.IsReturningPartyRoom())
            {
                matchmakingManager.CancelMatching();
            }

            ResetLocalReady();

            PartyMatchingCanceled?.Invoke();
        }

        /// <summary>
        /// 現在のパーティルームがマッチング状態かどうかを確認します。
        /// </summary>
        /// <returns>
        /// パーティマッチング中の場合は<c>true</c>です。
        /// </returns>
        private static bool IsPartyMatchingActive()
        {
            bool isPartyMatching = false;

            if (TryGetRoomBoolean(
                    NetworkPropertyKeys.PartyMatching,
                    out bool partyMatching))
            {
                isPartyMatching |= partyMatching;
            }

            if (TryGetRoomString(
                    NetworkPropertyKeys.State,
                    out string state))
            {
                isPartyMatching |=
                    string.Equals(
                        state,
                        NetworkPropertyKeys.StateMatching,
                        StringComparison.OrdinalIgnoreCase);
            }

            return isPartyMatching;
        }

        /// <summary>
        /// 現在参加しているルームが
        /// パーティルームかどうかを確認します。
        /// </summary>
        /// <returns>
        /// パーティルームへ参加している場合は<c>true</c>です。
        /// </returns>
        private static bool IsCurrentPartyRoom()
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                !onlineService.IsInRoom ||
                onlineService.CurrentRoom == null ||
                !TryGetRoomString(
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
        private static bool TryGetRoomString(
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

            if (!TryGetRoomPropertyIgnoreCase(
                    key,
                    out object rawValue))
            {
                return false;
            }

            value =
                rawValue?.ToString() ??
                string.Empty;

            return true;
        }

        /// <summary>
        /// 現在のルームからboolプロパティを取得します。
        /// </summary>
        /// <param name="key">
        /// 取得するプロパティキーです。
        /// </param>
        /// <param name="value">
        /// 取得されたbool値です。
        /// </param>
        /// <returns>
        /// プロパティを取得できた場合は<c>true</c>です。
        /// </returns>
        private static bool TryGetRoomBoolean(
            string key,
            out bool value)
        {
            value = false;

            OnlineRoom currentRoom =
                NetworkServiceLocator.Online?.CurrentRoom;

            if (currentRoom == null)
            {
                return false;
            }

            if (currentRoom.TryGetProperty(
                    key,
                    out bool directValue))
            {
                value = directValue;

                return true;
            }

            if (!TryGetRoomPropertyIgnoreCase(
                    key,
                    out object rawValue))
            {
                return false;
            }

            if (rawValue is bool booleanValue)
            {
                value = booleanValue;

                return true;
            }

            return bool.TryParse(
                rawValue?.ToString(),
                out value);
        }

        /// <summary>
        /// 現在のルームから大文字と小文字を無視して
        /// プロパティを取得します。
        /// </summary>
        /// <param name="key">
        /// 取得するプロパティキーです。
        /// </param>
        /// <param name="value">
        /// 取得されたプロパティ値です。
        /// </param>
        /// <returns>
        /// プロパティを取得できた場合は<c>true</c>です。
        /// </returns>
        private static bool TryGetRoomPropertyIgnoreCase(
            string key,
            out object value)
        {
            value = null;

            OnlineRoom currentRoom =
                NetworkServiceLocator.Online?.CurrentRoom;

            if (currentRoom?.Properties == null)
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

                value = property.Value;

                return true;
            }

            return false;
        }

        /// <summary>
        /// プロパティ一覧に指定されたキーが存在するか確認します。
        /// </summary>
        /// <param name="properties">
        /// 確認するプロパティ一覧です。
        /// </param>
        /// <param name="key">
        /// 確認するプロパティキーです。
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
        /// 変更されたプロパティ一覧から文字列値を取得します。
        /// </summary>
        /// <param name="properties">
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
            IReadOnlyDictionary<string, object> properties,
            string key,
            out string value)
        {
            value = string.Empty;

            if (properties == null)
            {
                return false;
            }

            foreach (
                KeyValuePair<string, object> property
                in properties)
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
        /// <param name="properties">
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
            IReadOnlyDictionary<string, object> properties,
            string key,
            out bool value)
        {
            value = false;

            if (properties == null)
            {
                return false;
            }

            foreach (
                KeyValuePair<string, object> property
                in properties)
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
        /// 指定されたプレイヤーがローカルプレイヤーかどうかを確認します。
        /// </summary>
        /// <param name="player">
        /// 確認するプレイヤーです。
        /// </param>
        /// <returns>
        /// ローカルプレイヤーの場合は<c>true</c>です。
        /// </returns>
        private static bool IsLocalPlayer(
            OnlinePlayer player)
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            return player != null &&
                onlineService != null &&
                player.PlayerId ==
                onlineService.LocalPlayerId;
        }

        /// <summary>
        /// ローカルプレイヤーのReady状態を
        /// <c>false</c>へリセットします。
        /// </summary>
        public void ResetLocalReady()
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                !onlineService.IsInRoom)
            {
                return;
            }

            OnlinePlayer localPlayer =
                OnlinePlayerUtility.GetLocalPlayer();

            if (localPlayer == null ||
                !TryGetReady(
                    localPlayer,
                    out bool isReady) ||
                !isReady)
            {
                return;
            }

            _isResettingLocalReady = true;

            onlineService.SetLocalPlayerProperty(
                NetworkPropertyKeys.Ready,
                false);

            LocalReadyChanged?.Invoke(false);
        }
    }
}
