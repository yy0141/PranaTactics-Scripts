using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

using PranaTactics.Match.Modes;
using PranaTactics.Pranam.Networking.EOS;
using PranaTactics.Unit.Networking.EOS;

namespace PranaTactics.Networking.EOS
{
    /// <summary>
    /// Epic Online Servicesを使用した認証、Lobby管理および
    /// P2Pメッセージ通信を提供します。
    /// </summary>
    /// <remarks>
    /// <see cref="IOnlineService"/>を実装し、
    /// Photon用オンラインサービスと交換可能な
    /// 共通オンラインサービスとして動作します。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EosOnlineService :
        MonoBehaviour,
        IOnlineService,
        IGameModeMatchmakingService
    {
        /// <summary>
        /// 有効なローカルプレイヤーIDを取得できない場合に返す値です。
        /// </summary>
        private const int k_InvalidPlayerId = -1;

        /// <summary>
        /// Lobbyが初期化されていない場合に返す
        /// 空のプレイヤー一覧です。
        /// </summary>
        private static readonly IReadOnlyList<OnlinePlayer>
            s_EmptyPlayers = Array.Empty<OnlinePlayer>();

        /// <summary>
        /// Epic Online Servicesの接続設定です。
        /// </summary>
        [SerializeField]
        private EosConfig _eosConfig;

        /// <summary>
        /// Start時にEpic Online Servicesへ
        /// 自動接続するかどうかを指定します。
        /// </summary>
        [SerializeField]
        private bool _shouldConnectOnStart = true;

        /// <summary>
        /// EOSの認証処理を管理します。
        /// </summary>
        private EosAuthService _authService;

        /// <summary>
        /// EOSのLobby処理を管理します。
        /// </summary>
        private EosLobbyService _lobbyService;

        /// <summary>
        /// EOSのP2P通信を管理します。
        /// </summary>
        private EosP2PService _p2pService;

        /// <summary>
        /// EOSへのログイン処理を実行中かどうかを示します。
        /// </summary>
        private bool _isConnecting;

        /// <summary>
        /// 認証、LobbyおよびP2Pサービスの
        /// 初期化が完了しているかどうかを示します。
        /// </summary>
        private bool _areServicesInitialized;

        /// <summary>
        /// Lobbyイベントを購読済みかどうかを示します。
        /// </summary>
        private bool _areLobbyEventsRegistered;

        /// <summary>
        /// P2Pイベントを購読済みかどうかを示します。
        /// </summary>
        private bool _areP2PEventsRegistered;

        /// <summary>
        /// アプリケーションの終了処理中かどうかを示します。
        /// </summary>
        private bool _isApplicationQuitting;

        /// <summary>
        /// このサービスの終了処理が完了しているかどうかを示します。
        /// </summary>
        private bool _isShutDown;

        /// <summary>
        /// EOSへのログイン完了後に実行する
        /// オンライン操作を保持します。
        /// </summary>
        private readonly Queue<Action>
            _pendingOnlineActions = new();

        public OnlineBackend Backend
        {
            get
            {
                return OnlineBackend.Eos;
            }
        }

        public bool IsConnected
        {
            get
            {
                return _authService != null &&
                    _authService.IsLoggedIn;
            }
        }

        public bool IsInLobby
        {
            get
            {
                return _lobbyService != null &&
                    _lobbyService.IsInLobby;
            }
        }

        public bool IsInRoom
        {
            get
            {
                return IsInLobby;
            }
        }

        /// <summary>
        /// 受信移動状態の送信元が現在のLobby所有者か確認します。
        /// </summary>
        public bool IsHostPlayer(int playerId)
        {
            return _lobbyService != null && _lobbyService.IsHostPlayer(playerId);
        }

        public bool IsHost
        {
            get
            {
                return _lobbyService != null &&
                    _lobbyService.IsHost;
            }
        }

        public int LocalPlayerId
        {
            get
            {
                if (_lobbyService == null)
                {
                    return k_InvalidPlayerId;
                }

                return _lobbyService.LocalPlayerId;
            }
        }

        public OnlineRoom CurrentRoom
        {
            get
            {
                return _lobbyService?.CurrentRoom;
            }
        }

        public IReadOnlyList<OnlinePlayer> Players
        {
            get
            {
                return _lobbyService?.Players ??
                    s_EmptyPlayers;
            }
        }

        public event Action JoinedLobby;

        public event Action JoinedRoom;

        public event Action LeftRoom;

        public event Action<OnlinePlayer> PlayerJoined;

        public event Action<OnlinePlayer> PlayerLeft;

        public event Action<
    OnlinePlayer,
    Dictionary<string, object>>
    PlayerPropertiesChanged;

        public event Action<Dictionary<string, object>>
    RoomPropertiesChanged;

        public event Action<OnlinePlayer> HostChanged;

        public event Action<string> PartyRoomCreationFailed;

        public event Action<string> PartyRoomJoinFailed;

        public event Action<NetMessage> MessageReceived;

        public event Action<EosUnitP2PMessage>
    UnitMessageReceived;

        public event Action<EosPranamP2PMessage>
    PranamMessageReceived;

        /// <summary>
        /// EOSサービスを生成し、
        /// NetworkServiceLocatorへ登録します。
        /// </summary>
        private void Awake()
        {
            if (_eosConfig == null)
            {
                Debug.LogError(
                    $"{nameof(EosOnlineService)}: " +
                    $"{nameof(EosConfig)}が設定されていません。",
                    this);

                enabled = false;

                return;
            }

            CreateServiceInstances();

            NetworkServiceLocator.RegisterOnlineService(this);
        }

        /// <summary>
        /// 設定に応じてEOSへの接続を開始します。
        /// </summary>
        private void Start()
        {
            if (_shouldConnectOnStart)
            {
                Connect();
            }
        }

        /// <summary>
        /// EOS LobbyとP2P通信のポーリング処理を実行します。
        /// </summary>
        private void Update()
        {
            if (_isShutDown)
            {
                return;
            }

            _lobbyService?.Poll();

            if (_p2pService == null)
            {
                return;
            }

            _p2pService.SyncPeers(Players);
            _p2pService.Poll();
        }

        /// <summary>
        /// アプリケーション終了時にEOS Lobbyから退出し、
        /// P2Pサービスを停止します。
        /// </summary>
        private void OnApplicationQuit()
        {
            _isApplicationQuitting = true;

            ShutDown(shouldLeaveLobby: true);
        }

        /// <summary>
        /// このコンポーネントが破棄されたとき、
        /// イベント購読およびサービス登録を解除します。
        /// </summary>
        private void OnDestroy()
        {
            ShutDown(
                shouldLeaveLobby:
                    !_isApplicationQuitting);
        }

        /// <summary>
        /// Epic Online Servicesへの接続と
        /// Device IDを使用したログインを開始します。
        /// </summary>
        public void Connect()
        {
            if (PranaTactics.Settings.GameBuildSettings.IsSoloOnly)
            {
                return;
            }

            if (_isShutDown)
            {
                return;
            }

            if (_areServicesInitialized &&
                IsConnected)
            {
                FlushPendingOnlineActions();

                return;
            }

            if (_isConnecting)
            {
                return;
            }

            if (_eosConfig == null)
            {
                Debug.LogError(
                    $"{nameof(EosOnlineService)}: " +
                    $"{nameof(EosConfig)}が設定されていないため、" +
                    "EOSへ接続できません。",
                    this);

                return;
            }

            CreateServiceInstances();

            _isConnecting = true;

            try
            {
                _authService.Initialize();

                _authService.LoginWithDeviceId(
                    HandleLoginCompleted);
            }
            catch (Exception exception)
            {
                _isConnecting = false;

                Debug.LogError(
                    $"{nameof(EosOnlineService)}: " +
                    "EOSへの接続開始中にエラーが発生しました。",
                    this);

                Debug.LogException(exception, this);

                ClearPendingOnlineActions(
                    "接続開始処理に失敗しました。");
            }
        }

        /// <summary>
        /// EOS Device IDログインの完了結果を処理します。
        /// </summary>
        /// <param name="isSuccessful">
        /// ログインに成功した場合は<c>true</c>です。
        /// </param>
        /// <param name="errorMessage">
        /// ログインに失敗した場合のエラーメッセージです。
        /// </param>
        private void HandleLoginCompleted(
            bool isSuccessful,
            string errorMessage)
        {
            _isConnecting = false;

            if (_isShutDown)
            {
                return;
            }

            if (!isSuccessful)
            {
                Debug.LogError(
                    $"{nameof(EosOnlineService)}: " +
                    $"EOSへのログインに失敗しました。" +
                    $"Error={errorMessage}",
                    this);

                ClearPendingOnlineActions(
                    "EOSへのログインに失敗しました。");

                return;
            }

            Debug.Log(
                $"{nameof(EosOnlineService)}: " +
                "EOSへのログインが完了しました。" +
                $"ProductUserId=" +
                $"{_authService.LocalProductUserIdString}",
                this);

            try
            {
                _lobbyService.Initialize(
                    _eosConfig,
                    _authService);

                _p2pService.Initialize(
                    _eosConfig,
                    _authService);

                RegisterLobbyEvents();
                RegisterP2PEvents();

                _areServicesInitialized = true;

                Debug.Log(
                    $"{nameof(EosOnlineService)}: " +
                    "EOSサービスの初期化が完了しました。",
                    this);

                FlushPendingOnlineActions();
            }
            catch (Exception exception)
            {
                _areServicesInitialized = false;

                Debug.LogError(
                    $"{nameof(EosOnlineService)}: " +
                    "EOSサービスの初期化中にエラーが発生しました。",
                    this);

                Debug.LogException(exception, this);

                ClearPendingOnlineActions(
                    "EOSサービスの初期化に失敗しました。");
            }
        }

        /// <summary>
        /// EOSサービスの各実装を必要に応じて生成します。
        /// </summary>
        private void CreateServiceInstances()
        {
            _authService ??=
                new EosAuthService();

            _lobbyService ??=
                new EosLobbyService();

            _p2pService ??=
                new EosP2PService();
        }

        /// <summary>
        /// パーティ用EOS Lobbyを作成します。
        /// </summary>
        /// <param name="roomName">
        /// 作成するLobbyの表示名です。
        /// </param>
        /// <param name="roomId">
        /// 作成するLobbyの識別子です。
        /// </param>
        /// <param name="password">
        /// Lobbyへの参加に使用するパスワードです。
        /// </param>
        /// <param name="partyId">
        /// Lobbyに関連付けるパーティIDです。
        /// </param>
        /// <param name="maximumPlayerCount">
        /// Lobbyへ参加できる最大プレイヤー数です。
        /// </param>
        public void CreatePartyRoom(
            string roomName,
            string roomId,
            string password,
            string partyId,
            int maximumPlayerCount)
        {
            ExecuteWhenReady(
                nameof(CreatePartyRoom),
                () =>
                {
                    _lobbyService.CreatePartyLobby(
                        roomName,
                        roomId,
                        password,
                        partyId,
                        maximumPlayerCount);
                });
        }

        /// <summary>
        /// 指定されたルームIDとパスワードに対応する
        /// パーティLobbyへ参加します。
        /// </summary>
        /// <param name="roomId">
        /// 参加するLobbyの識別子です。
        /// </param>
        /// <param name="password">
        /// Lobbyへの参加に使用するパスワードです。
        /// </param>
        public void JoinPartyRoom(
            string roomId,
            string password)
        {
            ExecuteWhenReady(
                nameof(JoinPartyRoom),
                () =>
                {
                    _lobbyService.JoinPartyLobby(
                        roomId,
                        password);
                });
        }

        /// <summary>
        /// 指定されたパーティLobbyへ参加し、
        /// 存在しない場合は新しく作成します。
        /// </summary>
        /// <param name="roomName">
        /// Lobbyの表示名です。
        /// </param>
        /// <param name="roomId">
        /// Lobbyの識別子です。
        /// </param>
        /// <param name="password">
        /// Lobbyへの参加に使用するパスワードです。
        /// </param>
        /// <param name="partyId">
        /// Lobbyに関連付けるパーティIDです。
        /// </param>
        /// <param name="partySize">
        /// パーティへ参加できる最大プレイヤー数です。
        /// </param>
        public void JoinOrCreatePartyRoom(
            string roomName,
            string roomId,
            string password,
            string partyId,
            int partySize)
        {
            ExecuteWhenReady(
                nameof(JoinOrCreatePartyRoom),
                () =>
                {
                    _lobbyService.JoinOrCreatePartyLobby(
                        roomName,
                        roomId,
                        password,
                        partyId,
                        partySize);
                });
        }

        /// <summary>
        /// 指定されたパーティ構成に対応する
        /// マッチングLobbyへ参加します。
        /// </summary>
        /// <param name="partyCount">
        /// 1試合に参加するパーティ数です。
        /// </param>
        /// <param name="partySize">
        /// 1パーティに参加するプレイヤー数です。
        /// </param>
        public void JoinMatchingRoom(
            int partyCount,
            int partySize)
        {
            JoinModeRoom(partyCount, partySize, GameModeRegistry.k_DefaultModeId);
        }

        /// <summary>
        /// 同じルールと人数構成を持つEOS Lobbyへ参加します。
        /// </summary>
        public void JoinModeRoom(int partyCount, int partySize, string gameModeId)
        {
            if (!GameModeRegistry.Catalog.TryGet(gameModeId, PlayCategory.Online, out _))
            {
                throw new ArgumentException("不明なオンラインモードです。", nameof(gameModeId));
            }

            ExecuteWhenReady(
                nameof(JoinMatchingRoom),
                () =>
                {
                    _lobbyService.JoinMatchingLobby(
                        partyCount,
                        partySize,
                        gameModeId);
                });
        }

        /// <summary>
        /// 現在参加しているEOS Lobbyから退出します。
        /// </summary>
        public void LeaveRoom()
        {
            if (_lobbyService == null)
            {
                return;
            }

            _lobbyService.CancelPendingMatchingSearch();
            _lobbyService.LeaveLobby();
        }

        /// <summary>
        /// 指定されたプレイヤーのプロパティを
        /// ローカルキャッシュへ設定します。
        /// </summary>
        /// <param name="playerId">
        /// プロパティを設定するプレイヤーの識別子です。
        /// </param>
        /// <param name="key">
        /// 設定するプロパティのキーです。
        /// </param>
        /// <param name="value">
        /// 設定するプロパティの値です。
        /// </param>
        /// <remarks>
        /// EOSでは他プレイヤーのMember Attributeを
        /// 直接更新できないため、現在はローカルキャッシュのみを更新します。
        /// </remarks>
        public void SetPlayerProperty(
            int playerId,
            string key,
            object value)
        {
            if (!IsValidPropertyKey(key))
            {
                return;
            }

            ExecuteWhenReady(
                nameof(SetPlayerProperty),
                () =>
                {
                    _lobbyService.SetPlayerPropertyInLocalCache(
                        playerId,
                        key,
                        value);
                });
        }

        /// <summary>
        /// ローカルプレイヤーのLobbyメンバープロパティを設定します。
        /// </summary>
        /// <param name="key">
        /// 設定するプロパティのキーです。
        /// </param>
        /// <param name="value">
        /// 設定するプロパティの値です。
        /// </param>
        public void SetLocalPlayerProperty(
            string key,
            object value)
        {
            if (!IsValidPropertyKey(key))
            {
                return;
            }

            ExecuteWhenReady(
                nameof(SetLocalPlayerProperty),
                () =>
                {
                    _lobbyService.SetLocalMemberProperty(
                        key,
                        value);
                });
        }

        /// <summary>
        /// ローカルプレイヤーへ複数の
        /// Lobbyメンバープロパティを設定します。
        /// </summary>
        /// <param name="properties">
        /// 設定するプロパティの一覧です。
        /// </param>
        public void SetLocalPlayerProperties(
            Dictionary<string, object> properties)
        {
            if (properties == null ||
                properties.Count == 0)
            {
                return;
            }

            Dictionary<string, object> copiedProperties =
                new(properties);

            ExecuteWhenReady(
                nameof(SetLocalPlayerProperties),
                () =>
                {
                    _lobbyService.SetLocalMemberProperties(
                        copiedProperties);
                });
        }

        /// <summary>
        /// 現在のEOS Lobbyへプロパティを設定します。
        /// </summary>
        /// <param name="key">
        /// 設定するプロパティのキーです。
        /// </param>
        /// <param name="value">
        /// 設定するプロパティの値です。
        /// </param>
        public void SetRoomProperty(
            string key,
            object value)
        {
            if (!IsValidPropertyKey(key))
            {
                return;
            }

            ExecuteWhenReady(
                nameof(SetRoomProperty),
                () =>
                {
                    _lobbyService.SetLobbyProperty(
                        key,
                        value);
                });
        }

        /// <summary>
        /// 現在のEOS Lobbyへ複数のプロパティを設定します。
        /// </summary>
        /// <param name="properties">
        /// 設定するプロパティの一覧です。
        /// </param>
        public void SetRoomProperties(
            Dictionary<string, object> properties)
        {
            if (properties == null ||
                properties.Count == 0)
            {
                return;
            }

            Dictionary<string, object> copiedProperties =
                new(properties);

            ExecuteWhenReady(
                nameof(SetRoomProperties),
                () =>
                {
                    _lobbyService.SetLobbyProperties(copiedProperties);
                });
        }

        /// <summary>
        /// 現在のEOS Lobbyを参加可能な状態にするか設定します。
        /// </summary>
        /// <param name="isOpen">
        /// 新しいプレイヤーの参加を許可する場合は<c>true</c>です。
        /// </param>
        public void SetRoomOpen(bool isOpen)
        {
            ExecuteWhenReady(
                nameof(SetRoomOpen),
                () =>
                {
                    _lobbyService.SetLobbyOpen(isOpen);
                });
        }

        /// <summary>
        /// 現在のEOS Lobbyを検索結果へ表示するか設定します。
        /// </summary>
        /// <param name="isVisible">
        /// Lobbyを検索結果へ表示する場合は<c>true</c>です。
        /// </param>
        public void SetRoomVisible(bool isVisible)
        {
            ExecuteWhenReady(
                nameof(SetRoomVisible),
                () =>
                {
                    _lobbyService.SetLobbyVisible(isVisible);
                });
        }

        /// <summary>
        /// 指定されたプレイヤーへ汎用P2Pメッセージを送信します。
        /// </summary>
        /// <param name="playerId">
        /// メッセージの送信先となるプレイヤーIDです。
        /// </param>
        /// <param name="message">
        /// 送信するメッセージです。
        /// </param>
        /// <param name="sendMode">
        /// メッセージの送信方式です。
        /// </param>
        public void SendToPlayer(
            int playerId,
            NetMessage message,
            NetworkSendMode sendMode)
        {
            if (message == null)
            {
                return;
            }

            ExecuteWhenReady(
                nameof(SendToPlayer),
                () =>
                {
                    _p2pService.SyncPeers(Players);

                    _p2pService.Send(
                        playerId,
                        message,
                        sendMode);
                });
        }

        /// <summary>
        /// 指定されたプレイヤーへ
        /// ユニット用P2Pメッセージを送信します。
        /// </summary>
        /// <param name="playerId">
        /// メッセージの送信先となるプレイヤーIDです。
        /// </param>
        /// <param name="message">
        /// 送信するユニット用メッセージです。
        /// </param>
        /// <param name="sendMode">
        /// メッセージの送信方式です。
        /// </param>
        public void SendUnitMessageToPlayer(
            int playerId,
            EosUnitP2PMessage message,
            NetworkSendMode sendMode =
                NetworkSendMode.Reliable)
        {
            if (message == null)
            {
                return;
            }

            ExecuteWhenReady(
                nameof(SendUnitMessageToPlayer),
                () =>
                {
                    _p2pService.SyncPeers(Players);

                    _p2pService.SendUnitMessage(
                        playerId,
                        message,
                        sendMode);
                });
        }

        /// <summary>
        /// ローカルプレイヤーを除く参加プレイヤー全員へ
        /// ユニット用P2Pメッセージを送信します。
        /// </summary>
        /// <param name="message">
        /// 送信するユニット用メッセージです。
        /// </param>
        /// <param name="sendMode">
        /// メッセージの送信方式です。
        /// </param>
        public void BroadcastUnitMessage(
            EosUnitP2PMessage message,
            NetworkSendMode sendMode =
                NetworkSendMode.Reliable)
        {
            if (message == null)
            {
                return;
            }

            ExecuteWhenReady(
                nameof(BroadcastUnitMessage),
                () =>
                {
                    BroadcastToRemotePlayers(
                        playerId =>
                        {
                            _p2pService.SendUnitMessage(
                                playerId,
                                message,
                                sendMode);
                        });
                });
        }

        /// <summary>
        /// 指定されたプレイヤーへ
        /// プラーナム用P2Pメッセージを送信します。
        /// </summary>
        /// <param name="playerId">
        /// メッセージの送信先となるプレイヤーIDです。
        /// </param>
        /// <param name="message">
        /// 送信するプラーナム用メッセージです。
        /// </param>
        /// <param name="sendMode">
        /// メッセージの送信方式です。
        /// </param>
        public void SendTriggerMessageToPlayer(
            int playerId,
            EosPranamP2PMessage message,
            NetworkSendMode sendMode =
                NetworkSendMode.Reliable)
        {
            if (message == null)
            {
                return;
            }

            ExecuteWhenReady(
                nameof(SendTriggerMessageToPlayer),
                () =>
                {
                    _p2pService.SyncPeers(Players);

                    _p2pService.SendTriggerMessage(
                        playerId,
                        message,
                        sendMode);
                });
        }

        /// <summary>
        /// ローカルプレイヤーを除く参加プレイヤー全員へ
        /// プラーナム用P2Pメッセージを送信します。
        /// </summary>
        /// <param name="message">
        /// 送信するプラーナム用メッセージです。
        /// </param>
        /// <param name="sendMode">
        /// メッセージの送信方式です。
        /// </param>
        public void BroadcastPranamMessage(
            EosPranamP2PMessage message,
            NetworkSendMode sendMode =
                NetworkSendMode.Reliable)
        {
            if (message == null)
            {
                return;
            }

            ExecuteWhenReady(
                nameof(BroadcastPranamMessage),
                () =>
                {
                    BroadcastToRemotePlayers(
                        playerId =>
                        {
                            _p2pService.SendTriggerMessage(
                                playerId,
                                message,
                                sendMode);
                        });
                });
        }

        /// <summary>
        /// EOSへのログイン完了後にオンライン操作を実行します。
        /// 未接続の場合は操作をキューへ追加し、接続を開始します。
        /// </summary>
        /// <param name="callerName">
        /// 操作を要求したメソッドの名前です。
        /// </param>
        /// <param name="action">
        /// ログイン完了後に実行する操作です。
        /// </param>
        /// <returns>
        /// 操作を即時実行した場合は<c>true</c>、
        /// キューへ追加した場合は<c>false</c>です。
        /// </returns>
        private bool ExecuteWhenReady(
            string callerName,
            Action action)
        {
            if (action == null ||
                _isShutDown)
            {
                return false;
            }

            if (IsReadyForOnlineAction)
            {
                action.Invoke();

                return true;
            }

            Debug.LogWarning(
                $"{nameof(EosOnlineService)}: " +
                "EOSへのログイン完了前にオンライン操作が要求されました。" +
                $"Caller={callerName}。ログイン完了後に実行します。",
                this);

            _pendingOnlineActions.Enqueue(action);

            Connect();

            return false;
        }

        private bool IsReadyForOnlineAction
        {
            get
            {
                return _areServicesInitialized &&
                    _authService != null &&
                    _authService.IsLoggedIn &&
                    _authService.LocalProductUserId != null &&
                    _lobbyService != null &&
                    _p2pService != null;
            }
        }

        /// <summary>
        /// ログイン完了前に要求されたオンライン操作を実行します。
        /// </summary>
        private void FlushPendingOnlineActions()
        {
            if (!IsReadyForOnlineAction ||
                _pendingOnlineActions.Count == 0)
            {
                return;
            }

            int pendingActionCount =
                _pendingOnlineActions.Count;

            Debug.Log(
                $"{nameof(EosOnlineService)}: " +
                "保留中のオンライン操作を実行します。" +
                $"Count={pendingActionCount}",
                this);

            while (_pendingOnlineActions.Count > 0)
            {
                Action action =
                    _pendingOnlineActions.Dequeue();

                try
                {
                    action?.Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"{nameof(EosOnlineService)}: " +
                        "保留中のオンライン操作でエラーが発生しました。",
                        this);

                    Debug.LogException(exception, this);
                }
            }
        }

        /// <summary>
        /// 保留中のオンライン操作をすべて削除します。
        /// </summary>
        /// <param name="reason">
        /// 操作を削除する理由です。
        /// </param>
        private void ClearPendingOnlineActions(
            string reason)
        {
            int pendingActionCount =
                _pendingOnlineActions.Count;

            _pendingOnlineActions.Clear();

            if (pendingActionCount == 0)
            {
                return;
            }

            Debug.LogWarning(
                $"{nameof(EosOnlineService)}: " +
                "保留中のオンライン操作を削除しました。" +
                $"Count={pendingActionCount}, Reason={reason}",
                this);
        }

        /// <summary>
        /// Lobbyサービスのイベントを購読します。
        /// </summary>
        private void RegisterLobbyEvents()
        {
            if (_areLobbyEventsRegistered ||
                _lobbyService == null)
            {
                return;
            }

            _lobbyService.JoinedLobby +=
                HandleJoinedLobby;

            _lobbyService.LeftLobby +=
                HandleLeftLobby;

            _lobbyService.PlayerJoined +=
                HandlePlayerJoined;

            _lobbyService.PlayerLeft +=
                HandlePlayerLeft;

            _lobbyService.PlayerPropertiesChanged +=
                HandlePlayerPropertiesChanged;

            _lobbyService.RoomPropertiesChanged +=
                HandleRoomPropertiesChanged;

            _lobbyService.HostChanged +=
                HandleHostChanged;

            _lobbyService.CreateLobbyFailed +=
                HandlePartyRoomCreationFailed;

            _lobbyService.JoinLobbyFailed +=
                HandlePartyRoomJoinFailed;

            _areLobbyEventsRegistered = true;
        }

        /// <summary>
        /// Lobbyサービスのイベント購読を解除します。
        /// </summary>
        private void UnregisterLobbyEvents()
        {
            if (!_areLobbyEventsRegistered ||
                _lobbyService == null)
            {
                return;
            }

            _lobbyService.JoinedLobby -=
                HandleJoinedLobby;

            _lobbyService.LeftLobby -=
                HandleLeftLobby;

            _lobbyService.PlayerJoined -=
                HandlePlayerJoined;

            _lobbyService.PlayerLeft -=
                HandlePlayerLeft;

            _lobbyService.PlayerPropertiesChanged -=
                HandlePlayerPropertiesChanged;

            _lobbyService.RoomPropertiesChanged -=
                HandleRoomPropertiesChanged;

            _lobbyService.HostChanged -=
                HandleHostChanged;

            _lobbyService.CreateLobbyFailed -=
                HandlePartyRoomCreationFailed;

            _lobbyService.JoinLobbyFailed -=
                HandlePartyRoomJoinFailed;

            _areLobbyEventsRegistered = false;
        }

        /// <summary>
        /// P2Pサービスの受信イベントを購読します。
        /// </summary>
        private void RegisterP2PEvents()
        {
            if (_areP2PEventsRegistered ||
                _p2pService == null)
            {
                return;
            }

            _p2pService.MessageReceived +=
                HandleMessageReceived;

            _p2pService.UnitMessageReceived +=
                HandleUnitMessageReceived;

            _p2pService.PranamMessageReceived +=
                HandlePranamMessageReceived;

            _areP2PEventsRegistered = true;
        }

        /// <summary>
        /// P2Pサービスの受信イベント購読を解除します。
        /// </summary>
        private void UnregisterP2PEvents()
        {
            if (!_areP2PEventsRegistered ||
                _p2pService == null)
            {
                return;
            }

            _p2pService.MessageReceived -=
                HandleMessageReceived;

            _p2pService.UnitMessageReceived -=
                HandleUnitMessageReceived;

            _p2pService.PranamMessageReceived -=
                HandlePranamMessageReceived;

            _areP2PEventsRegistered = false;
        }

        /// <summary>
        /// EOS Lobbyへの参加完了を通知します。
        /// </summary>
        private void HandleJoinedLobby()
        {
            JoinedLobby?.Invoke();
            JoinedRoom?.Invoke();
        }

        /// <summary>
        /// EOS Lobbyからの退出完了を通知します。
        /// </summary>
        private void HandleLeftLobby()
        {
            LeftRoom?.Invoke();
        }

        /// <summary>
        /// プレイヤーのLobby参加を通知します。
        /// </summary>
        /// <param name="player">
        /// Lobbyへ参加したプレイヤーです。
        /// </param>
        private void HandlePlayerJoined(
            OnlinePlayer player)
        {
            PlayerJoined?.Invoke(player);
        }

        /// <summary>
        /// プレイヤーのLobby退出を通知します。
        /// </summary>
        /// <param name="player">
        /// Lobbyから退出したプレイヤーです。
        /// </param>
        private void HandlePlayerLeft(
            OnlinePlayer player)
        {
            PlayerLeft?.Invoke(player);
        }

        /// <summary>
        /// プレイヤープロパティの変更を通知します。
        /// </summary>
        /// <param name="player">
        /// プロパティが変更されたプレイヤーです。
        /// </param>
        /// <param name="properties">
        /// 変更されたプロパティです。
        /// </param>
        private void HandlePlayerPropertiesChanged(
            OnlinePlayer player,
            Dictionary<string, object> properties)
        {
            PlayerPropertiesChanged?.Invoke(
                player,
                properties);
        }

        /// <summary>
        /// Lobbyプロパティの変更を通知します。
        /// </summary>
        /// <param name="properties">
        /// 変更されたプロパティです。
        /// </param>
        private void HandleRoomPropertiesChanged(
            Dictionary<string, object> properties)
        {
            RoomPropertiesChanged?.Invoke(properties);
        }

        /// <summary>
        /// Lobbyホストの変更を通知します。
        /// </summary>
        /// <param name="newHost">
        /// 新しくホストになったプレイヤーです。
        /// </param>
        private void HandleHostChanged(
            OnlinePlayer newHost)
        {
            HostChanged?.Invoke(newHost);
        }

        /// <summary>
        /// パーティLobby作成の失敗を通知します。
        /// </summary>
        /// <param name="message">
        /// エラーメッセージです。
        /// </param>
        private void HandlePartyRoomCreationFailed(
            string message)
        {
            PartyRoomCreationFailed?.Invoke(message);
        }

        /// <summary>
        /// パーティLobby参加の失敗を通知します。
        /// </summary>
        /// <param name="message">
        /// エラーメッセージです。
        /// </param>
        private void HandlePartyRoomJoinFailed(
            string message)
        {
            PartyRoomJoinFailed?.Invoke(message);
        }

        /// <summary>
        /// 汎用P2Pメッセージの受信を通知します。
        /// </summary>
        /// <param name="message">
        /// 受信したメッセージです。
        /// </param>
        private void HandleMessageReceived(
            NetMessage message)
        {
            MessageReceived?.Invoke(message);
        }

        /// <summary>
        /// ユニット用P2Pメッセージの受信を通知します。
        /// </summary>
        /// <param name="message">
        /// 受信したユニット用メッセージです。
        /// </param>
        private void HandleUnitMessageReceived(
            EosUnitP2PMessage message)
        {
            UnitMessageReceived?.Invoke(message);
        }

        /// <summary>
        /// プラーナム用P2Pメッセージの受信を通知します。
        /// </summary>
        /// <param name="message">
        /// 受信したプラーナム用メッセージです。
        /// </param>
        private void HandlePranamMessageReceived(
            EosPranamP2PMessage message)
        {
            PranamMessageReceived?.Invoke(message);
        }

        /// <summary>
        /// ローカルプレイヤー以外の参加プレイヤーへ
        /// 指定された処理を実行します。
        /// </summary>
        /// <param name="sendAction">
        /// 送信先プレイヤーIDを受け取る送信処理です。
        /// </param>
        private void BroadcastToRemotePlayers(
            Action<int> sendAction)
        {
            if (sendAction == null ||
                _p2pService == null)
            {
                return;
            }

            IReadOnlyList<OnlinePlayer> players =
                Players;

            _p2pService.SyncPeers(players);

            foreach (OnlinePlayer player in players)
            {
                if (player == null ||
                    player.PlayerId == LocalPlayerId)
                {
                    continue;
                }

                sendAction.Invoke(player.PlayerId);
            }
        }

        /// <summary>
        /// プロパティキーが有効かどうかを確認します。
        /// </summary>
        /// <param name="key">
        /// 確認するプロパティキーです。
        /// </param>
        /// <returns>
        /// 有効なキーの場合は<c>true</c>です。
        /// </returns>
        private static bool IsValidPropertyKey(
            string key)
        {
            return !string.IsNullOrWhiteSpace(key);
        }

        /// <summary>
        /// EOSサービスを停止し、
        /// イベント購読とサービス登録を解除します。
        /// </summary>
        /// <param name="shouldLeaveLobby">
        /// Lobbyへ参加中の場合に退出処理を実行するかどうかを指定します。
        /// </param>
        private void ShutDown(bool shouldLeaveLobby)
        {
            if (_isShutDown)
            {
                return;
            }

            _isShutDown = true;
            _isConnecting = false;
            _areServicesInitialized = false;

            ClearPendingOnlineActions(
                "EOSサービスを終了します。");

            if (shouldLeaveLobby &&
                _lobbyService != null &&
                _lobbyService.IsInLobby)
            {
                Debug.Log(
                    $"{nameof(EosOnlineService)}: " +
                    "EOS Lobbyから退出します。",
                    this);

                LeaveRoom();
            }

            UnregisterLobbyEvents();
            UnregisterP2PEvents();

            _p2pService?.Shutdown();

            NetworkServiceLocator
                .UnregisterOnlineService(this);
        }
    }
}
