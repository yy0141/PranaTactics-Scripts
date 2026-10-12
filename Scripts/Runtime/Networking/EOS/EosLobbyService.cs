using System;
using System.Collections.Generic;
using UnityEngine;

using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using Epic.OnlineServices.Connect;

using PlayEveryWare.EpicOnlineServices;

using PranaTactics.Match.Modes;

namespace PranaTactics.Networking.EOS
{
    /// <summary>
    /// Epic Online ServicesのLobby作成、検索、参加、退出、
    /// メンバー同期およびLobby属性同期を管理します。
    /// </summary>
    /// <remarks>
    /// EOS LobbyのMember Attributeは通知だけでは反映が遅れる場合があるため、
    /// 通常のポーリングと、重要な更新後の短時間の高速ポーリングを併用します。
    /// </remarks>
    public sealed class EosLobbyService
    {
        /// <summary>
        /// 有効なプレイヤーIDを取得できないことを示す値です。
        /// </summary>
        private const int k_InvalidPlayerId = -1;

        /// <summary>
        /// EosConfigがない場合に使用するLobby検索結果の最大件数です。
        /// </summary>
        private const uint k_DefaultSearchMaxResults = 50;

        /// <summary>
        /// 通常時にLobbyメンバーと属性を同期する間隔です。
        /// </summary>
        private const float k_MemberSyncIntervalSeconds = 0.50f;

        /// <summary>
        /// 高速同期中にLobbyメンバーと属性を同期する間隔です。
        /// </summary>
        private const float k_MemberSyncBurstIntervalSeconds = 0.15f;

        /// <summary>
        /// 重要な属性更新後に高速同期を継続する時間です。
        /// </summary>
        private const float k_MemberSyncBurstDurationSeconds = 5.0f;

        /// <summary>
        /// マッチングLobby検索の再試行回数です。
        /// </summary>
        private const int k_MatchingSearchMaxRetryCount = 1;

        /// <summary>
        /// マッチングLobby検索を再試行するまでの基本待機時間です。
        /// </summary>
        private const float k_MatchingSearchRetryBaseDelaySeconds = 0.25f;

        /// <summary>
        /// 複数クライアントによる同時Lobby作成を避けるための
        /// 待機時間ジッターの最大値です。
        /// </summary>
        private const float k_MatchingSearchRetryJitterRangeSeconds = 0.50f;

        /// <summary>
        /// マッチングへ参加するパーティ数を保存する
        /// Lobby属性キーです。
        /// </summary>
        private const string k_MatchingPartyCountPropertyKey =
            "matchingPartyCount";

        /// <summary>
        /// 1パーティのプレイヤー数を保存する
        /// Lobby属性キーです。
        /// </summary>
        private const string k_MatchingPartySizePropertyKey =
            "matchingPartySize";

        /// <summary>
        /// EOS Lobby検索でBucket IDを指定するキーです。
        /// </summary>
        private const string k_LobbySearchBucketIdKey = "bucket";

        /// <summary>
        /// 設定がない場合に使用するパーティLobbyのBucket IDです。
        /// </summary>
        private const string k_DefaultPartyBucketId = "prana_party";

        /// <summary>
        /// 設定がない場合に使用するマッチングLobbyのBucket IDです。
        /// </summary>
        private const string k_DefaultMatchingBucketId = "prana_matching";

        /// <summary>
        /// 設定がない場合に使用するゲームバージョン属性のキーです。
        /// </summary>
        private const string k_DefaultGameVersionPropertyKey = "gameVersion";

        /// <summary>
        /// 設定がない場合に使用する製品バージョンです。
        /// </summary>
        private const string k_DefaultProductVersion = "0.1.0";

        /// <summary>
        /// OnlinePlayerへProductUserId文字列を保存するプロパティのキーです。
        /// </summary>
        private const string k_ProductUserIdPropertyKey = "productUserId";

        /// <summary>
        /// プレイヤーID変更通知に使用するプロパティのキーです。
        /// </summary>
        private const string k_PlayerIdPropertyKey = "playerId";

        /// <summary>
        /// ゲームプレイシーン準備状態のプロパティキーです。
        /// </summary>
        private const string k_InGameSceneReadyPropertyKey = "inGameSceneReady";

        /// <summary>
        /// ゲームプレイ状態のプロパティキーです。
        /// </summary>
        private const string k_InGameStatePropertyKey = "inGameState";

        /// <summary>
        /// EOSユニット生成情報のプロパティキーです。
        /// </summary>
        private const string k_BattleUnitSpawnsPropertyKey = "battleUnitSpawns";

        /// <summary>
        /// LobbyメンバーのEOS IDと文字列表現を保持します。
        /// </summary>
        private sealed class LobbyMemberInfo
        {
            public ProductUserId UserId
            {
                get; set;
            }

            public string ProductUserIdString
            {
                get; set;
            }
        }

        public bool IsInLobby
        {
            get; private set;
        }

        public bool IsHost
        {
            get; private set;
        }

        public OnlineRoom CurrentRoom
        {
            get; private set;
        }

        public IReadOnlyList<OnlinePlayer> Players
        {
            get
            {
                return _players;
            }
        }

        public int LocalPlayerId { get; private set; } = k_InvalidPlayerId;

        public event Action JoinedLobby;

        public event Action LeftLobby;

        public event Action<OnlinePlayer> PlayerJoined;

        public event Action<OnlinePlayer> PlayerLeft;

        public event Action<OnlinePlayer, Dictionary<string, object>>
    PlayerPropertiesChanged;

        public event Action<Dictionary<string, object>>
    RoomPropertiesChanged;

        public event Action<OnlinePlayer> HostChanged;

        public event Action<string> CreateLobbyFailed;

        public event Action<string> JoinLobbyFailed;

        /// <summary>
        /// 中断または新しい検索要求で古い非同期結果を無効化する世代番号です。
        /// </summary>
        private int _matchingRequestVersion;

        /// <summary>
        /// 準備状態などの書き込み順序をルーム内で維持します。
        /// </summary>
        private readonly SerialLobbyOperationQueue _memberWriteQueue = new();

        /// <summary>
        /// 開始とキャンセルを要求順に反映するルーム属性更新キューです。
        /// </summary>
        private readonly SerialLobbyOperationQueue _roomWriteQueue = new();


        /// <summary>
        /// 現在のLobbyに参加しているプレイヤーのキャッシュです。
        /// </summary>
        private readonly List<OnlinePlayer> _players = new();

        /// <summary>
        /// Lobby参加前に設定されたローカルメンバー属性です。
        /// </summary>
        private readonly Dictionary<string, object>
            _pendingLocalMemberProperties =
                new(StringComparer.Ordinal);

        /// <summary>
        /// 共通プレイヤーIDからProductUserId文字列を取得する対応表です。
        /// </summary>
        private readonly Dictionary<int, string>
            _playerIdToProductUserId = new();

        /// <summary>
        /// ProductUserId文字列から共通プレイヤーIDを取得する対応表です。
        /// </summary>
        private readonly Dictionary<string, int>
            _productUserIdToPlayerId =
                new(StringComparer.Ordinal);

        /// <summary>
        /// EOS Lobbyの設定です。
        /// </summary>
        private EosConfig _eosConfig;

        /// <summary>
        /// EOS認証サービスです。
        /// </summary>
        private EosAuthService _authService;

        /// <summary>
        /// EOS SDKのLobbyインターフェースです。
        /// </summary>
        private LobbyInterface _lobbyInterface;

        /// <summary>
        /// 現在参加しているEOS Lobbyの識別子です。
        /// </summary>
        private string _currentLobbyId;

        /// <summary>
        /// パーティLobby検索中のルームIDです。
        /// </summary>
        private string _pendingJoinRoomId;

        /// <summary>
        /// パーティLobby検索中のパスワードです。
        /// </summary>
        private string _pendingJoinPassword;

        /// <summary>
        /// 参加処理中のLobbyDetailsハンドルです。
        /// </summary>
        private LobbyDetails _pendingJoinLobbyDetails;

        /// <summary>
        /// マッチングへ参加するパーティ数です。
        /// </summary>
        private int _pendingMatchingPartyCount = 2;

        /// <summary>
        /// 1パーティに参加するプレイヤー数です。
        /// </summary>
        private int _pendingMatchingPartySize = 2;

        /// <summary>
        /// 次にLobbyメンバー同期を実行できる時刻です。
        /// </summary>
        private float _nextMemberSyncTime;

        /// <summary>
        /// 高速同期を終了する時刻です。
        /// </summary>
        private float _memberSyncBurstEndTime;

        /// <summary>
        /// 次回Pollで即時同期するかどうかを示します。
        /// </summary>
        private bool _shouldForceMemberSync;

        /// <summary>
        /// マッチングLobby検索を実行中かどうかを示します。
        /// </summary>
        private bool _isMatchingSearchInProgress;

        /// <summary>
        /// マッチングLobbyの再検索を待機しているかどうかを示します。
        /// </summary>
        private bool _isMatchingSearchRetryPending;

        /// <summary>
        /// 次回検索が空の場合にLobbyを作成するかどうかを示します。
        /// </summary>
        private bool _shouldCreateWhenNextMatchingSearchIsEmpty;

        /// <summary>
        /// マッチングLobby検索を再試行した回数です。
        /// </summary>
        private int _matchingSearchRetryCount;

        /// <summary>
        /// 次にマッチングLobby検索を実行する時刻です。
        /// </summary>
        private float _nextMatchingSearchTime;

        private int PendingMatchingPlayerCount
        {
            get
            {
                return Math.Max(1, _pendingMatchingPartyCount) *
                       Math.Max(1, _pendingMatchingPartySize);
            }
        }

        private string PartyBucketId
        {
            get
            {
                if (_eosConfig != null &&
                    !string.IsNullOrWhiteSpace(_eosConfig.PartyBucketId))
                {
                    return _eosConfig.PartyBucketId;
                }

                return k_DefaultPartyBucketId;
            }
        }

        /// <summary>
        /// 非同期検索と新規作成に共通のゲームルールIDです。
        /// </summary>
        private string _pendingGameModeId = GameModeRegistry.k_DefaultModeId;

        private string MatchingBucketId
        {
            get
            {
                if (_eosConfig != null &&
                    !string.IsNullOrWhiteSpace(_eosConfig.MatchingBucketId))
                {
                    return GameModeMatchmakingKey.Create(_eosConfig.MatchingBucketId, _pendingGameModeId);
                }

                return GameModeMatchmakingKey.Create(k_DefaultMatchingBucketId, _pendingGameModeId);
            }
        }

        private string GameVersionPropertyKey
        {
            get
            {
                if (_eosConfig != null &&
                    !string.IsNullOrWhiteSpace(_eosConfig.GameVersionKey))
                {
                    return _eosConfig.GameVersionKey;
                }

                return k_DefaultGameVersionPropertyKey;
            }
        }

        private string ProductVersion
        {
            get
            {
                if (_eosConfig != null &&
                    !string.IsNullOrWhiteSpace(_eosConfig.ProductVersion))
                {
                    return _eosConfig.ProductVersion;
                }

                return k_DefaultProductVersion;
            }
        }

        /// <summary>
        /// EOS Lobbyサービスを初期化します。
        /// </summary>
        public void Initialize(
            EosConfig eosConfig,
            EosAuthService authService)
        {
            if (eosConfig == null)
            {
                throw new ArgumentNullException(nameof(eosConfig));
            }

            if (authService == null)
            {
                throw new ArgumentNullException(nameof(authService));
            }

            _eosConfig = eosConfig;
            _authService = authService;

            if (EOSManager.Instance == null)
            {
                Debug.LogError("EosLobbyService Initialize failed: EOSManager.Instance is null.");

                return;
            }

            var platform = EOSManager.Instance.GetEOSPlatformInterface();

            if (platform == null)
            {
                Debug.LogError("EosLobbyService Initialize failed: EOS PlatformInterface is null.");

                return;
            }

            _lobbyInterface = platform.GetLobbyInterface();

            if (_lobbyInterface == null)
            {
                Debug.LogError("EosLobbyService Initialize failed: LobbyInterface is null.");

                return;
            }

            Debug.Log("EosLobbyService Initialize");
        }

        /// <summary>
        /// マッチング検索の再試行とLobby情報の定期同期を実行します。
        /// </summary>
        public void Poll()
        {
            PollMatchingSearchRetry();

            if (!IsInLobby)
            {
                return;
            }

            if (string.IsNullOrEmpty(_currentLobbyId))
            {
                return;
            }

            if (_authService == null || !_authService.IsLoggedIn || _authService.LocalProductUserId == null)
            {
                return;
            }

            if (_lobbyInterface == null)
            {
                return;
            }

            if (!_shouldForceMemberSync && Time.unscaledTime < _nextMemberSyncTime)
            {
                return;
            }

            string reason = _shouldForceMemberSync ? "forcePoll" : "poll";

            _shouldForceMemberSync = false;

            float interval = Time.unscaledTime < _memberSyncBurstEndTime
                ? k_MemberSyncBurstIntervalSeconds
                : k_MemberSyncIntervalSeconds;

            _nextMemberSyncTime = Time.unscaledTime + interval;

            SyncLobbyRoomPropertiesFromCurrentLobby(reason);
            SyncLobbyMembersFromCurrentLobby(reason);
        }

        /// <summary>
        /// Lobbyメンバー情報の高速同期を要求します。
        /// </summary>
        private void RequestAcceleratedMemberSync(string reason, bool immediate = false)
        {
            if (!IsInLobby || string.IsNullOrEmpty(_currentLobbyId))
            {
                return;
            }

            _memberSyncBurstEndTime = Mathf.Max(
                _memberSyncBurstEndTime,
                Time.unscaledTime + k_MemberSyncBurstDurationSeconds);

            if (immediate)
            {
                _shouldForceMemberSync = true;
                _nextMemberSyncTime = 0f;
            }
            else
            {
                float nextBurstSyncTime = Time.unscaledTime + k_MemberSyncBurstIntervalSeconds;

                if (_nextMemberSyncTime <= 0f)
                {
                    _nextMemberSyncTime = nextBurstSyncTime;
                }
                else
                {
                    _nextMemberSyncTime = Mathf.Min(
                        _nextMemberSyncTime,
                        nextBurstSyncTime);
                }
            }

            Debug.Log(
                $"EOS RequestAcceleratedMemberSync: reason={reason}, " +
                $"immediate={immediate}, burstUntil={_memberSyncBurstEndTime:0.00}");
        }

        /// <summary>
        /// パーティーロビーの作成要求を処理中かどうかを保持します。
        /// </summary>
        private bool _isCreatingPartyLobby;

        /// <summary>
        /// 復帰時に同じロビーが存在した場合、参加へ切り替えるかどうかを保持します。
        /// </summary>
        private bool _joinExistingPartyOnConflict;

        /// <summary>
        /// 同じ資格情報では同じEOSロビーIDを使用してパーティーを作成します。
        /// </summary>
        public void CreatePartyLobby(
                    string roomName,
                    string roomId,
                    string password,
                    string partyId,
                    int maximumPlayerCount,
                    bool joinExistingOnConflict = false)
        {
            if (_isCreatingPartyLobby || IsInLobby || !CanAccessLobby(nameof(CreatePartyLobby)))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(roomId) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(partyId))
            {
                NotifyCreateLobbyFailure(
                    "ルームID、パスワード、パーティIDを設定してください。");

                return;
            }

            if (maximumPlayerCount <= 0)
            {
                NotifyCreateLobbyFailure(
                    "Lobbyの最大参加人数には1以上の値を指定してください。");

                return;
            }

            Debug.Log(
                $"EOS CreatePartyLobby start: " +
                $"roomName={roomName}, roomId={roomId}");

            CacheRoomBeforeCreate(
                roomName,
                roomId,
                password,
                partyId,
                maximumPlayerCount,
                NetworkPropertyKeys.TypeParty);

            _isCreatingPartyLobby = true;
            _joinExistingPartyOnConflict = joinExistingOnConflict;
            var options = new CreateLobbyOptions
            {
                LobbyId = PartyLobbyIdentity.Create(roomId, password),
                LocalUserId = _authService.LocalProductUserId,

                MaxLobbyMembers = (uint)maximumPlayerCount,

                PermissionLevel = LobbyPermissionLevel.Publicadvertised,

                PresenceEnabled = false,
                AllowInvites = false,

                BucketId = PartyBucketId,

                DisableHostMigration = false,

                EnableRTCRoom = false,

                EnableJoinById = true,

                RejoinAfterKickRequiresInvite = false
            };

            _lobbyInterface.CreateLobby(
                ref options,
                null,
                HandleCreatePartyLobbyCompleted);
        }

        /// <summary>
        /// パーティLobby作成の完了結果を処理します。
        /// </summary>
        private void HandleCreatePartyLobbyCompleted(ref CreateLobbyCallbackInfo data)
        {
            _isCreatingPartyLobby = false;
            if (data.ResultCode == Result.LobbyLobbyAlreadyExists && _joinExistingPartyOnConflict)
            {
                JoinPartyLobbyByIdentity(
                    CurrentRoom.GetPropertyOrDefault(NetworkPropertyKeys.RoomId, string.Empty),
                    CurrentRoom.GetPropertyOrDefault(NetworkPropertyKeys.Password, string.Empty));
                return;
            }

            Debug.Log($"EOS CreatePartyLobby result: {data.ResultCode}, LobbyId={data.LobbyId}");

            if (data.ResultCode != Result.Success)
            {
                NotifyCreateLobbyFailure($"部屋の作成に失敗しました。EOS: {data.ResultCode}");
                ClearLocalLobbyCache();

                return;
            }

            _currentLobbyId = data.LobbyId;
            IsInLobby = true;
            IsHost = true;

            if (CurrentRoom != null)
            {
                CurrentRoom.Name = _currentLobbyId;
                CurrentRoom.PlayerCount = 1;
            }

            EnsureLocalPlayer();

            Dictionary<string, object> lobbyProperties = new()
            {
                { NetworkPropertyKeys.Type, NetworkPropertyKeys.TypeParty },
                { NetworkPropertyKeys.State, NetworkPropertyKeys.StateWaiting },
                { NetworkPropertyKeys.RoomId, CurrentRoom.GetPropertyOrDefault(
                    NetworkPropertyKeys.RoomId,
                    string.Empty) },
                { NetworkPropertyKeys.Password, CurrentRoom.GetPropertyOrDefault(
                    NetworkPropertyKeys.Password,
                    string.Empty) },
                { NetworkPropertyKeys.PartyId, CurrentRoom.GetPropertyOrDefault(
                    NetworkPropertyKeys.PartyId,
                    string.Empty) },
                { NetworkPropertyKeys.PartyMatching, false },
                { GameVersionPropertyKey, ProductVersion }
            };

            UpdateLobbyAttributes(
                lobbyProperties,
                success =>
                {
                    if (!success)
                    {
                        Debug.LogWarning("EOS CreatePartyLobby succeeded, but UpdateLobbyAttributes failed.");
                    }

                    SyncLobbyMembersFromCurrentLobby("create");
                    FlushPendingLocalMemberProperties();

                    Debug.Log($"EOS Party Lobby created. LobbyId={_currentLobbyId}");

                    JoinedLobby?.Invoke();
                });
        }

        /// <summary>
        /// Lobby作成前のルーム情報をローカルへ保存します。
        /// </summary>
        private void CacheRoomBeforeCreate(
            string roomName,
            string roomId,
            string password,
            string partyId,
            int maximumPlayerCount,
            string type)
        {
            CurrentRoom = new OnlineRoom
            {
                Name = roomName,
                PlayerCount = 0,
                MaxPlayers = maximumPlayerCount,
                IsOpen = true,
                IsVisible = true
            };

            CurrentRoom.Properties[NetworkPropertyKeys.Type] = type;
            CurrentRoom.Properties[NetworkPropertyKeys.State] = NetworkPropertyKeys.StateWaiting;
            CurrentRoom.Properties[NetworkPropertyKeys.RoomId] = roomId;
            CurrentRoom.Properties[NetworkPropertyKeys.Password] = password;
            CurrentRoom.Properties[NetworkPropertyKeys.PartyId] = partyId;
            CurrentRoom.Properties[NetworkPropertyKeys.PartyMatching] = false;
        }

        /// <summary>
        /// 複数のLobby属性をEOSへ反映します。
        /// </summary>
        public void SetLobbyProperties(Dictionary<string, object> properties)
        {
            if (properties == null || properties.Count == 0)
            {
                return;
            }

            UpdateLobbyAttributes(new Dictionary<string, object>(properties), null);
        }

        /// <summary>
        /// 複数のルーム属性を一度のEOS更新で反映します。
        /// </summary>
        private void UpdateLobbyAttributes(Dictionary<string, object> properties, Action<bool> onCompleted)
        {
            var snapshot = new Dictionary<string, object>(properties);
            _roomWriteQueue.Enqueue(completed => SendLobbyAttributes(snapshot, success =>
            {
                try
                {
                    onCompleted?.Invoke(success);
                }
                finally
                {
                    completed();
                }
            }));
        }

        /// <summary>
        /// 前のルーム更新が完了してからEOSへ送信します。
        /// </summary>
        private void SendLobbyAttributes(
            Dictionary<string, object> properties,
            Action<bool> onCompleted)
        {
            if (!CanAccessLobby(nameof(UpdateLobbyAttributes)))
            {
                onCompleted?.Invoke(false);

                return;
            }

            if (string.IsNullOrEmpty(_currentLobbyId))
            {
                Debug.LogError("UpdateLobbyAttributes failed: _currentLobbyId is empty.");
                onCompleted?.Invoke(false);

                return;
            }

            string targetLobbyId = _currentLobbyId;
            int requestVersion = _matchingRequestVersion;
            var modificationOptions = new UpdateLobbyModificationOptions
            {
                LobbyId = _currentLobbyId,
                LocalUserId = _authService.LocalProductUserId
            };

            Result result = _lobbyInterface.UpdateLobbyModification(
                ref modificationOptions,
                out LobbyModification lobbyModification);

            if (result != Result.Success || lobbyModification == null)
            {
                Debug.LogError($"UpdateLobbyModification failed: {result}");
                onCompleted?.Invoke(false);

                return;
            }

            foreach (var kv in properties)
            {
                AttributeData attributeData = new AttributeData
                {
                    Key = kv.Key,
                    Value = ToLobbyAttributeValue(kv.Value)
                };

                var addOptions = new LobbyModificationAddAttributeOptions
                {
                    Attribute = attributeData,
                    Visibility = LobbyAttributeVisibility.Public
                };

                Result addResult = lobbyModification.AddAttribute(ref addOptions);

                if (addResult != Result.Success)
                {
                    Debug.LogWarning($"Lobby AddAttribute failed: key={kv.Key}, result={addResult}");
                }
            }

            var updateOptions = new UpdateLobbyOptions
            {
                LobbyModificationHandle = lobbyModification
            };

            _lobbyInterface.UpdateLobby(
                ref updateOptions,
                null,
                (ref UpdateLobbyCallbackInfo callbackInfo) =>
                {
                    if (targetLobbyId != _currentLobbyId || requestVersion != _matchingRequestVersion)
                    {
                        onCompleted?.Invoke(false);
                        return;
                    }

                    Debug.Log($"EOS UpdateLobby result: {callbackInfo.ResultCode}");

                    bool success = callbackInfo.ResultCode == Result.Success;

                    if (success)
                    {
                        foreach (var property in properties)
                        {
                            CurrentRoom.Properties[property.Key] = property.Value;
                        }

                        ApplyAssignedTeams();
                        RoomPropertiesChanged?.Invoke(new Dictionary<string, object>(properties));
                        RequestAcceleratedMemberSync(
                            $"roomAttributes:{string.Join(",", properties.Keys)}",
                            immediate: true);
                    }

                    onCompleted?.Invoke(success);
                });
        }

        /// <summary>
        /// ローカルプレイヤーのMember Attributeを設定します。
        /// </summary>
        /// <remarks>
        /// PartyManagerはLobby作成前にready/partyIdなどを設定するため，
        /// まだLobbyに入っていない場合は保留する。
        /// </remarks>
        public void SetLocalMemberProperty(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            Debug.Log(
                $"EOS SetLocalMemberProperty called: " +
                $"key={key}, value={value}, " +
                $"localProductUserId={_authService?.LocalProductUserIdString}, " +
                $"localPlayerId={LocalPlayerId}, " +
                $"isHostClient={IsHost}, " +
                $"dataPath={Application.dataPath}"
            );

            if (!IsInLobby || string.IsNullOrEmpty(_currentLobbyId))
            {
                _pendingLocalMemberProperties[key] = value;
                Debug.Log(
                    $"EOS SetLocalMemberProperty pending: " +
                    $"key={key}, value={value}, " +
                    $"localProductUserId={_authService?.LocalProductUserIdString}, " +
                    $"localPlayerId={LocalPlayerId}, " +
                    $"isHostClient={IsHost}"
                );

                return;
            }

            SetLocalMemberProperties(new Dictionary<string, object>
            {
                { key, value }
            });
        }

        /// <summary>
        /// ローカルプレイヤーの複数のMember Attributeを設定します。
        /// </summary>
        public void SetLocalMemberProperties(Dictionary<string, object> properties)
        {
            if (properties == null || properties.Count == 0)
            {
                return;
            }

            if (!IsInLobby || string.IsNullOrEmpty(_currentLobbyId))
            {
                foreach (var kv in properties)
                {
                    _pendingLocalMemberProperties[kv.Key] = kv.Value;
                    Debug.Log(
                        $"EOS SetLocalMemberProperty pending: " +
                        $"key={kv.Key}, value={kv.Value}, " +
                        $"localProductUserId={_authService?.LocalProductUserIdString}, " +
                        $"localPlayerId={LocalPlayerId}, " +
                        $"isHostClient={IsHost}");
                }

                return;
            }

            UpdateLocalMemberAttributes(properties);
        }

        /// <summary>
        /// Lobby属性を設定します。
        /// </summary>
        public void SetLobbyProperty(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (!IsInLobby || string.IsNullOrEmpty(_currentLobbyId))
            {
                if (CurrentRoom != null)
                {
                    CurrentRoom.Properties[key] = value;
                }

                Debug.LogWarning($"EOS SetLobbyProperty cached only: {key}={value}");

                return;
            }

            UpdateLobbyAttributes(
                new Dictionary<string, object>
                {
                    { key, value }
                },
                success =>
                {
                    if (!success)
                    {
                        Debug.LogWarning(
                            $"EOS SetLobbyProperty failed: {key}={value}");
                    }
                });
        }

        /// <summary>
        /// Lobby参加前に保留されたMember Attributeを反映します。
        /// </summary>
        private void FlushPendingLocalMemberProperties()
        {
            if (_pendingLocalMemberProperties.Count == 0)
            {
                return;
            }

            Dictionary<string, object> pending = new(_pendingLocalMemberProperties);

            _pendingLocalMemberProperties.Clear();

            UpdateLocalMemberAttributes(pending);
        }

        /// <summary>
        /// 複数のローカルMember AttributeをEOSへ反映します。
        /// </summary>
        private void UpdateLocalMemberAttributes(Dictionary<string, object> properties)
        {
            if (properties == null || properties.Count == 0)
            {
                return;
            }

            var snapshot = new Dictionary<string, object>(properties);
            _memberWriteQueue.Enqueue(completed => SendLocalMemberAttributes(snapshot, completed));
        }

        /// <summary>
        /// 前の更新が完了した後にEOSへ属性を送信し、成功・失敗ともに完了を通知します。
        /// </summary>
        private void SendLocalMemberAttributes(Dictionary<string, object> properties, Action completed)
        {
            if (properties == null || properties.Count == 0)
            {
                completed();
                return;
            }

            if (!CanAccessLobby(nameof(UpdateLocalMemberAttributes)))
            {
                completed();
                return;
            }

            if (string.IsNullOrEmpty(_currentLobbyId))
            {
                Debug.LogError("UpdateLocalMemberAttributes failed: _currentLobbyId is empty.");

                completed();
                return;
            }

            int requestVersion = _matchingRequestVersion;
            string targetLobbyId = _currentLobbyId;
            var modificationOptions = new UpdateLobbyModificationOptions
            {
                LobbyId = _currentLobbyId,
                LocalUserId = _authService.LocalProductUserId
            };

            Result result = _lobbyInterface.UpdateLobbyModification(
                ref modificationOptions,
                out LobbyModification lobbyModification);

            if (result != Result.Success || lobbyModification == null)
            {
                Debug.LogError($"UpdateLocalMemberAttributes UpdateLobbyModification failed: {result}");

                completed();
                return;
            }

            foreach (var kv in properties)
            {
                AttributeData attributeData = new AttributeData
                {
                    Key = kv.Key,
                    Value = ToLobbyAttributeValue(kv.Value)
                };

                var addOptions = new LobbyModificationAddMemberAttributeOptions
                {
                    Attribute = attributeData,
                    Visibility = LobbyAttributeVisibility.Public
                };

                Result addResult = lobbyModification.AddMemberAttribute(ref addOptions);

                if (addResult != Result.Success)
                {
                    Debug.LogWarning($"Lobby AddMemberAttribute failed: key={kv.Key}, result={addResult}");
                }
            }

            var updateOptions = new UpdateLobbyOptions
            {
                LobbyModificationHandle = lobbyModification
            };

            Dictionary<string, object> updateProperties = new(properties);

            _lobbyInterface.UpdateLobby(
                ref updateOptions,
                null,
                (ref UpdateLobbyCallbackInfo callbackInfo) =>
                {
                    try
                    {
                        if (requestVersion != _matchingRequestVersion || targetLobbyId != _currentLobbyId)
                        {
                            return;
                        }

                        Debug.Log(
                            $"EOS UpdateLocalMemberAttributes result: {callbackInfo.ResultCode}, " +
                            $"keys={string.Join(",", updateProperties.Keys)}");

                        if (callbackInfo.ResultCode != Result.Success)
                        {
                            return;
                        }

                        OnlinePlayer localPlayer = FindLocalPlayer();

                        if (localPlayer == null)
                        {
                            localPlayer = EnsureLocalPlayer();
                        }

                        if (localPlayer == null)
                        {
                            Debug.LogError("EOS LocalMember cache update failed: local player not found.");

                            return;
                        }

                        foreach (var kv in updateProperties)
                        {
                            localPlayer.Properties[kv.Key] = kv.Value;

                            Debug.Log(
                                $"EOS LocalMember cache update: localPlayerId={localPlayer.PlayerId}, " +
                                $"localProductUserId={_authService.LocalProductUserIdString}, " +
                                $"key={kv.Key}, value={kv.Value}, " +
                                $"isHostClient={IsHost}, " +
                                $"dataPath={Application.dataPath}");
                        }

                        PlayerPropertiesChanged?.Invoke(
                            localPlayer,
                            new Dictionary<string, object>(updateProperties));

                        RequestAcceleratedMemberSync(
                            $"localMemberAttributes:{string.Join(",", updateProperties.Keys)}",
                            immediate: true);
                    }
                    finally
                    {
                        completed();
                    }
                });
        }

        /// <summary>
        /// ルームIDとパスワードを使用してパーティLobbyを検索し、参加します。
        /// </summary>
        /// <remarks>
        /// roomId属性で検索
        /// type = party でも絞る
        /// password属性でも絞る。検索条件に含めることで，検索後の属性読み取り失敗に依存しない。
        /// </remarks>
        public void JoinPartyLobby(string roomId, string password)
        {
            if (!CanAccessLobby(nameof(JoinPartyLobby)))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(roomId) ||
                string.IsNullOrWhiteSpace(password))
            {
                NotifyJoinLobbyFailure(
                    "ルームIDとパスワードを設定してください。");

                return;
            }

            Debug.Log(
                $"EOS JoinPartyLobby start: roomId={roomId}");

            _pendingJoinRoomId = roomId;
            _pendingJoinPassword = password;
            ReleasePendingJoinLobbyDetails();

            var createSearchOptions = new CreateLobbySearchOptions
            {
                MaxResults = _eosConfig != null
                    ? _eosConfig.SearchMaxResults
                    : k_DefaultSearchMaxResults
            };

            Result createResult = _lobbyInterface.CreateLobbySearch(
                ref createSearchOptions,
                out LobbySearch lobbySearch);

            if (createResult != Result.Success || lobbySearch == null)
            {
                NotifyJoinLobbyFailure($"部屋検索の開始に失敗しました。EOS: {createResult}");

                return;
            }

            AttributeData roomIdAttribute = new AttributeData
            {
                Key = NetworkPropertyKeys.RoomId,
                Value = ToLobbyAttributeValue(roomId)
            };

            var roomIdParameterOptions = new LobbySearchSetParameterOptions
            {
                Parameter = roomIdAttribute,
                ComparisonOp = ComparisonOp.Equal
            };

            Result roomIdSetResult = lobbySearch.SetParameter(ref roomIdParameterOptions);

            if (roomIdSetResult != Result.Success)
            {
                NotifyJoinLobbyFailure($"部屋検索の番号条件設定に失敗しました。EOS: {roomIdSetResult}");
                lobbySearch.Release();

                return;
            }

            AttributeData typeAttribute = new AttributeData
            {
                Key = NetworkPropertyKeys.Type,
                Value = ToLobbyAttributeValue(NetworkPropertyKeys.TypeParty)
            };

            var typeParameterOptions = new LobbySearchSetParameterOptions
            {
                Parameter = typeAttribute,
                ComparisonOp = ComparisonOp.Equal
            };

            Result typeSetResult = lobbySearch.SetParameter(ref typeParameterOptions);

            if (typeSetResult != Result.Success)
            {
                NotifyJoinLobbyFailure($"部屋検索の条件設定に失敗しました。EOS: {typeSetResult}");
                lobbySearch.Release();

                return;
            }

            AttributeData passwordAttribute = new AttributeData
            {
                Key = NetworkPropertyKeys.Password,
                Value = ToLobbyAttributeValue(password)
            };

            var passwordParameterOptions = new LobbySearchSetParameterOptions
            {
                Parameter = passwordAttribute,
                ComparisonOp = ComparisonOp.Equal
            };

            Result passwordSetResult = lobbySearch.SetParameter(ref passwordParameterOptions);

            if (passwordSetResult != Result.Success)
            {
                NotifyJoinLobbyFailure($"部屋検索のパスワード条件設定に失敗しました。EOS: {passwordSetResult}");
                lobbySearch.Release();

                return;
            }

            var findOptions = new LobbySearchFindOptions
            {
                LocalUserId = _authService.LocalProductUserId
            };

            lobbySearch.Find(
                ref findOptions,
                null,
                (ref LobbySearchFindCallbackInfo data) =>
                {
                    Debug.Log($"EOS LobbySearch Find result: {data.ResultCode}");

                    if (data.ResultCode != Result.Success)
                    {
                        NotifyJoinLobbyFailure($"部屋の検索に失敗しました。EOS: {data.ResultCode}");
                        lobbySearch.Release();

                        return;
                    }

                    TryJoinPartyLobbyFromSearch(lobbySearch);
                });
        }

        /// <summary>
        /// パーティLobbyの検索結果から参加対象を取得します。
        /// </summary>
        private void TryJoinPartyLobbyFromSearch(LobbySearch lobbySearch)
        {
            var countOptions = new LobbySearchGetSearchResultCountOptions();

            uint resultCount = lobbySearch.GetSearchResultCount(ref countOptions);

            Debug.Log($"EOS LobbySearch result count: {resultCount}");

            if (resultCount == 0)
            {
                NotifyJoinLobbyFailure($"部屋番号またはパスワードが間違っています。roomId={_pendingJoinRoomId}");
                lobbySearch.Release();

                return;
            }

            var copyOptions = new LobbySearchCopySearchResultByIndexOptions
            {
                LobbyIndex = 0
            };

            Result copyResult = lobbySearch.CopySearchResultByIndex(
                ref copyOptions,
                out LobbyDetails lobbyDetails);

            if (copyResult != Result.Success || lobbyDetails == null)
            {
                NotifyJoinLobbyFailure($"検索結果の取得に失敗しました。EOS: {copyResult}");
                lobbySearch.Release();

                return;
            }

            _pendingJoinLobbyDetails = lobbyDetails;

            lobbySearch.Release();

            JoinFoundPartyLobby(lobbyDetails);
        }

        /// <summary>
        /// EOS Lobby属性値から文字列を取得します。
        /// </summary>
        private string FromLobbyAttributeValue(AttributeDataValue value)
        {
            if (!string.IsNullOrEmpty(value.AsUtf8))
            {
                return value.AsUtf8;
            }

            return string.Empty;
        }

        /// <summary>
        /// 検索で見つかったパーティLobbyへの参加を開始します。
        /// </summary>
        private void JoinFoundPartyLobby(LobbyDetails lobbyDetails)
        {
            if (lobbyDetails == null)
            {
                Debug.LogError("JoinFoundPartyLobby failed: lobbyDetails is null.");

                return;
            }

            Debug.Log("EOS JoinLobby start.");

            var joinOptions = new JoinLobbyOptions
            {
                LocalUserId = _authService.LocalProductUserId,
                LobbyDetailsHandle = lobbyDetails,
                PresenceEnabled = false
            };

            _lobbyInterface.JoinLobby(
                ref joinOptions,
                null,
                HandleJoinPartyLobbyCompleted);
        }

        /// <summary>
        /// パーティLobby参加の完了結果を処理します。
        /// </summary>
        private void HandleJoinPartyLobbyCompleted(ref JoinLobbyCallbackInfo data)
        {
            CompletePartyJoin(data.ResultCode, data.LobbyId);
        }

        /// <summary>
        /// 検索反映の遅延に依存せず、同じ資格情報の既存ロビーへ復帰します。
        /// </summary>
        private void JoinPartyLobbyByIdentity(string roomId, string password)
        {
            _pendingJoinRoomId = roomId;
            _pendingJoinPassword = password;
            var options = new JoinLobbyByIdOptions
            {
                LobbyId = PartyLobbyIdentity.Create(roomId, password),
                LocalUserId = _authService.LocalProductUserId,
                PresenceEnabled = false
            };
            _lobbyInterface.JoinLobbyById(ref options, null,
                (ref JoinLobbyByIdCallbackInfo result) => CompletePartyJoin(result.ResultCode, result.LobbyId));
        }

        /// <summary>
        /// パーティー参加後の同期と通知を実行します。
        /// </summary>
        private void CompletePartyJoin(Result result, string lobbyId)
        {
            Debug.Log($"EOS JoinPartyLobby result: {result}, LobbyId={lobbyId}");

            if (result != Result.Success)
            {
                NotifyJoinLobbyFailure($"部屋への参加に失敗しました。EOS: {result}");

                ReleasePendingJoinLobbyDetails();

                return;
            }

            _currentLobbyId = lobbyId;
            IsInLobby = true;
            IsHost = false;

            CacheJoinedPartyRoom(lobbyId);

            SyncLobbyRoomPropertiesFromCurrentLobby("join");
            SyncLobbyMembersFromCurrentLobby("join");
            FlushPendingLocalMemberProperties();

            Debug.Log($"EOS Party Lobby joined. LobbyId={_currentLobbyId}");

            JoinedLobby?.Invoke();

            ReleasePendingJoinLobbyDetails();
        }

        /// <summary>
        /// 参加したパーティLobbyの情報をローカルへ保存します。
        /// </summary>
        private void CacheJoinedPartyRoom(string lobbyId)
        {
            string roomId = _pendingJoinRoomId;
            string password = _pendingJoinPassword;
            string partyId = $"{roomId}_{password}";

            CurrentRoom = new OnlineRoom
            {
                Name = lobbyId,
                PlayerCount = 1,
                MaxPlayers = 2,
                IsOpen = true,
                IsVisible = true
            };

            CurrentRoom.Properties[NetworkPropertyKeys.Type] = NetworkPropertyKeys.TypeParty;
            CurrentRoom.Properties[NetworkPropertyKeys.State] = NetworkPropertyKeys.StateWaiting;
            CurrentRoom.Properties[NetworkPropertyKeys.RoomId] = roomId;
            CurrentRoom.Properties[NetworkPropertyKeys.Password] = password;
            CurrentRoom.Properties[NetworkPropertyKeys.PartyId] = partyId;
            CurrentRoom.Properties[NetworkPropertyKeys.PartyMatching] = false;
        }

        /// <summary>
        /// 現在のLobby属性をローカルルーム情報へ同期します。
        /// </summary>
        private void SyncLobbyRoomPropertiesFromCurrentLobby(string reason)
        {
            if (string.IsNullOrEmpty(_currentLobbyId))
            {
                return;
            }

            if (!TryCopyCurrentLobbyDetails(out LobbyDetails lobbyDetails))
            {
                return;
            }

            try
            {
                Dictionary<string, object> roomAttributes = ReadLobbyAttributes(lobbyDetails);

                ApplyRoomPropertySnapshot(roomAttributes, reason);
            }
            finally
            {
                lobbyDetails.Release();
            }
        }

        /// <summary>
        /// LobbyDetailsから公開Lobby属性を読み取ります。
        /// </summary>
        private Dictionary<string, object> ReadLobbyAttributes(LobbyDetails lobbyDetails)
        {
            Dictionary<string, object> result = new();

            if (lobbyDetails == null)
            {
                return result;
            }

            var countOptions = new LobbyDetailsGetAttributeCountOptions();
            uint count = lobbyDetails.GetAttributeCount(ref countOptions);

            for (uint i = 0; i < count; i++)
            {
                var copyOptions = new LobbyDetailsCopyAttributeByIndexOptions
                {
                    AttrIndex = i
                };

                Result copyResult = lobbyDetails.CopyAttributeByIndex(
                    ref copyOptions,
                    out Epic.OnlineServices.Lobby.Attribute? attribute);

                if (copyResult != Result.Success || !attribute.HasValue)
                {
                    continue;
                }

                AttributeData? nullableData = attribute.Value.Data;

                if (!nullableData.HasValue)
                {
                    continue;
                }

                AttributeData data = nullableData.Value;
                string normalizedKey = NormalizeNetworkPropertyKey(data.Key);
                object convertedValue = FromLobbyAttributeValueToObject(data.Value);

                if (!string.Equals(data.Key, normalizedKey, StringComparison.Ordinal))
                {
                    Debug.Log(
                        $"EOS LobbyAttr key normalized: " +
                        $"rawKey={data.Key}, normalizedKey={normalizedKey}, value={convertedValue}"
                    );
                }

                result[normalizedKey] = convertedValue;
            }

            return result;
        }

        /// <summary>
        /// 取得したLobby属性の差分をローカルキャッシュへ適用します。
        /// </summary>
        private void ApplyRoomPropertySnapshot(Dictionary<string, object> snapshot, string reason)
        {
            if (CurrentRoom == null)
            {
                return;
            }

            if (snapshot == null || snapshot.Count == 0)
            {
                return;
            }

            Dictionary<string, object> changed = new();

            foreach (var kv in snapshot)
            {
                if (!CurrentRoom.Properties.TryGetValue(kv.Key, out object oldValue) ||
                    !AreNetworkPropertyValuesEqual(oldValue, kv.Value))
                {
                    CurrentRoom.Properties[kv.Key] = kv.Value;
                    changed[kv.Key] = kv.Value;
                }
            }

            if (changed.Count == 0)
            {
                return;
            }

            Debug.Log(
                $"EOS RoomProperties snapshot changed: reason={reason}, " +
                $"changedKeys={string.Join(",", changed.Keys)}"
            );

            ApplyAssignedTeams();
            RoomPropertiesChanged?.Invoke(changed);
        }

        /// <summary>
        /// ネットワークプロパティの値が同等かどうかを判定します。
        /// </summary>
        private bool AreNetworkPropertyValuesEqual(object a, object b)
        {
            if (a == null && b == null)
            {
                return true;
            }

            if (a == null || b == null)
            {
                return false;
            }

            if (a is bool aBool)
            {
                if (b is bool bBool)
                {
                    return aBool == bBool;
                }

                if (bool.TryParse(b.ToString(), out bool parsedB))
                {
                    return aBool == parsedB;
                }
            }

            if (b is bool bBoolOnly)
            {
                if (bool.TryParse(a.ToString(), out bool parsedA))
                {
                    return parsedA == bBoolOnly;
                }
            }

            return string.Equals(a.ToString(), b.ToString(), StringComparison.Ordinal);
        }

        /// <summary>
        /// 現在のLobbyメンバーとMember Attributeを同期します。
        /// </summary>
        private void SyncLobbyMembersFromCurrentLobby(string reason)
        {
            if (string.IsNullOrEmpty(_currentLobbyId))
            {
                return;
            }

            if (!TryCopyCurrentLobbyDetails(out LobbyDetails lobbyDetails))
            {
                return;
            }

            try
            {
                var countOptions = new LobbyDetailsGetMemberCountOptions();
                uint memberCount = lobbyDetails.GetMemberCount(ref countOptions);

                List<LobbyMemberInfo> members = new List<LobbyMemberInfo>();
                List<string> productUserIds = new List<string>();

                for (uint i = 0; i < memberCount; i++)
                {
                    var memberOptions = new LobbyDetailsGetMemberByIndexOptions
                    {
                        MemberIndex = i
                    };

                    ProductUserId memberUserId = lobbyDetails.GetMemberByIndex(ref memberOptions);

                    if (memberUserId == null)
                    {
                        continue;
                    }

                    string productUserId = memberUserId.ToString();

                    members.Add(new LobbyMemberInfo
                    {
                        UserId = memberUserId,
                        ProductUserIdString = productUserId
                    });

                    productUserIds.Add(productUserId);
                }

                EnsureStablePlayerIdMapping(productUserIds);

                Dictionary<string, OnlinePlayer> snapshot = new();

                foreach (LobbyMemberInfo member in members)
                {
                    string productUserId = member.ProductUserIdString;
                    int playerId = GetOrCreatePlayerId(productUserId);

                    OnlinePlayer player = new OnlinePlayer
                    {
                        PlayerId = playerId,
                        DisplayName = $"Player{playerId}"
                    };

                    player.Properties[k_ProductUserIdPropertyKey] = productUserId;

                    Dictionary<string, object> memberAttributes = ReadMemberAttributes(
                        lobbyDetails,
                        member.UserId);

                    foreach (KeyValuePair<string, object> property in memberAttributes)
                    {
                        player.Properties[property.Key] = property.Value;
                    }

                    if (MatchTeamAssignment.TryGet(CurrentRoom, playerId, out int assignedTeam))
                    {
                        player.Properties[NetworkPropertyKeys.TeamId] = assignedTeam;
                    }

                    if (!player.Properties.ContainsKey(NetworkPropertyKeys.Ready))
                    {
                        player.Properties[NetworkPropertyKeys.Ready] = false;
                    }

                    if (!player.Properties.ContainsKey(NetworkPropertyKeys.MatchingReady))
                    {
                        player.Properties[NetworkPropertyKeys.MatchingReady] = false;
                    }

                    snapshot[productUserId] = player;
                }

                ApplyMemberSnapshot(snapshot, reason);
            }
            finally
            {
                lobbyDetails.Release();
            }
        }

        /// <summary>
        /// EOSが保持するLobby所有者と送信者を照合します。
        /// </summary>
        public bool IsHostPlayer(int playerId)
        {
            if (!TryCopyCurrentLobbyDetails(out LobbyDetails details))
            {
                return false;
            }

            try
            {
                var options = new LobbyDetailsGetLobbyOwnerOptions();
                ProductUserId owner = details.GetLobbyOwner(ref options);
                return owner != null && _playerIdToProductUserId.TryGetValue(playerId, out string productId) &&
                    owner.ToString() == productId;
            }
            finally
            {
                details.Release();
            }
        }

        /// <summary>
        /// 現在のLobbyDetailsハンドルを取得します。
        /// </summary>
        private bool TryCopyCurrentLobbyDetails(out LobbyDetails lobbyDetails)
        {
            lobbyDetails = null;

            if (_lobbyInterface == null || _authService == null || _authService.LocalProductUserId == null)
            {
                return false;
            }

            var options = new CopyLobbyDetailsHandleOptions
            {
                LobbyId = _currentLobbyId,
                LocalUserId = _authService.LocalProductUserId
            };

            Result result = _lobbyInterface.CopyLobbyDetailsHandle(ref options, out lobbyDetails);

            if (result != Result.Success || lobbyDetails == null)
            {
                Debug.LogWarning($"EOS CopyLobbyDetailsHandle failed: {result}");

                return false;
            }

            return true;
        }

        /// <summary>
        /// 指定されたLobbyメンバーの属性を読み取ります。
        /// </summary>
        private Dictionary<string, object> ReadMemberAttributes(
            LobbyDetails lobbyDetails,
            ProductUserId memberUserId)
        {
            Dictionary<string, object> result = new();

            if (lobbyDetails == null || memberUserId == null)
            {
                return result;
            }

            var countOptions = new LobbyDetailsGetMemberAttributeCountOptions
            {
                TargetUserId = memberUserId
            };

            uint count = lobbyDetails.GetMemberAttributeCount(ref countOptions);

            for (uint i = 0; i < count; i++)
            {
                var copyOptions = new LobbyDetailsCopyMemberAttributeByIndexOptions
                {
                    TargetUserId = memberUserId,
                    AttrIndex = i
                };

                Result copyResult = lobbyDetails.CopyMemberAttributeByIndex(
                    ref copyOptions,
                    out Epic.OnlineServices.Lobby.Attribute? attribute);

                if (copyResult != Result.Success || !attribute.HasValue)
                {
                    continue;
                }

                AttributeData? nullableData = attribute.Value.Data;

                if (!nullableData.HasValue)
                {
                    continue;
                }

                AttributeData data = nullableData.Value;
                string normalizedKey = NormalizeNetworkPropertyKey(data.Key);
                object convertedValue = FromLobbyAttributeValueToObject(data.Value);

                if (!string.Equals(data.Key, normalizedKey, StringComparison.Ordinal))
                {
                    Debug.Log(
                        $"EOS MemberAttr key normalized: targetProductUserId={memberUserId}, " +
                        $"rawKey={data.Key}, normalizedKey={normalizedKey}, value={convertedValue}"
                    );
                }

                result[normalizedKey] = convertedValue;
            }

            return result;
        }

        /// <summary>
        /// ホストの確定編成をメンバー属性より優先して適用します。
        /// </summary>
        private void ApplyAssignedTeams()
        {
            foreach (OnlinePlayer player in _players)
            {
                if (!MatchTeamAssignment.TryGet(CurrentRoom, player.PlayerId, out int teamId) ||
                    (player.TryGetProperty(NetworkPropertyKeys.TeamId, out int previous) && previous == teamId))
                {
                    continue;
                }

                player.Properties[NetworkPropertyKeys.TeamId] = teamId;
                PlayerPropertiesChanged?.Invoke(player, new Dictionary<string, object>
                {
                    { NetworkPropertyKeys.TeamId, teamId }
                });
            }
        }

        /// <summary>
        /// 取得したメンバーの状態をローカルキャッシュへ適用します。
        /// </summary>
        private void ApplyMemberSnapshot(Dictionary<string, OnlinePlayer> snapshot, string reason)
        {
            Debug.Log(
                $"EOS ApplyMemberSnapshot: reason={reason}, " +
                $"memberCount={snapshot.Count}, " +
                $"localProductUserId={_authService?.LocalProductUserIdString}, " +
                $"localPlayerId={LocalPlayerId}, " +
                $"isHostClient={IsHost}"
            );

            HashSet<string> currentIds = new();

            foreach (var player in _players)
            {
                if (player.Properties.TryGetValue(k_ProductUserIdPropertyKey, out object rawId))
                {
                    currentIds.Add(rawId?.ToString() ?? string.Empty);
                }
            }

            foreach (var pair in snapshot)
            {
                string productUserId = pair.Key;
                OnlinePlayer newPlayer = pair.Value;

                OnlinePlayer existing = FindPlayerByProductUserId(productUserId);

                if (existing == null)
                {
                    _players.Add(newPlayer);

                    if (productUserId == _authService.LocalProductUserIdString)
                    {
                        LocalPlayerId = newPlayer.PlayerId;
                    }

                    Debug.Log(
                        $"EOS Lobby member joined/cache add: playerId={newPlayer.PlayerId}, productUserId={productUserId}, reason={reason}");
                    PlayerJoined?.Invoke(newPlayer);
                    PlayerPropertiesChanged?.Invoke(
                        newPlayer,
                        new Dictionary<string, object>(
                        newPlayer.Properties));
                    RequestAcceleratedMemberSync($"memberJoined:{productUserId}", immediate: false);
                    continue;
                }

                Dictionary<string, object> changed = new();

                if (existing.PlayerId != newPlayer.PlayerId)
                {
                    Debug.Log(
                        $"EOS PlayerId cache update: productUserId={productUserId}, " +
                        $"oldPlayerId={existing.PlayerId}, newPlayerId={newPlayer.PlayerId}");

                    existing.PlayerId = newPlayer.PlayerId;
                    changed[k_PlayerIdPropertyKey] = newPlayer.PlayerId;
                }

                foreach (var kv in newPlayer.Properties)
                {
                    if (!existing.Properties.TryGetValue(kv.Key, out object oldValue) ||
                        !object.Equals(
                        oldValue,
                        kv.Value))
                    {
                        existing.Properties[kv.Key] = kv.Value;
                        changed[kv.Key] = kv.Value;
                    }
                }

                existing.DisplayName = newPlayer.DisplayName;

                if (productUserId == _authService.LocalProductUserIdString)
                {
                    LocalPlayerId = existing.PlayerId;
                }

                if (changed.Count > 0)
                {
                    PlayerPropertiesChanged?.Invoke(existing, changed);
                    RequestAcceleratedMemberSync(
                        $"memberChanged:{productUserId}:{string.Join(",", changed.Keys)}",
                        immediate: false);
                }
            }

            for (int i = _players.Count - 1; i >= 0; i--)
            {
                OnlinePlayer player = _players[i];

                if (!player.Properties.TryGetValue(k_ProductUserIdPropertyKey, out object rawId))
                {
                    continue;
                }

                string productUserId = rawId?.ToString() ?? string.Empty;

                if (snapshot.ContainsKey(productUserId))
                {
                    continue;
                }

                _players.RemoveAt(i);
                _playerIdToProductUserId.Remove(player.PlayerId);
                _productUserIdToPlayerId.Remove(productUserId);

                Debug.Log(
                    $"EOS Lobby member left/cache remove: playerId={player.PlayerId}, productUserId={productUserId}, reason={reason}");
                PlayerLeft?.Invoke(player);
            }

            if (CurrentRoom != null)
            {
                CurrentRoom.PlayerCount = _players.Count;
            }

            foreach (var player in _players)
            {
                bool ready = false;
                bool hasMatchingReady = player.TryGetProperty(
                    NetworkPropertyKeys.MatchingReady, out bool matchingReady);

                if (player.TryGetProperty(NetworkPropertyKeys.Ready, out bool readyValue))
                {
                    ready = readyValue;
                }

                string productUserId = "";

                if (player.Properties.TryGetValue(k_ProductUserIdPropertyKey, out object rawProductUserId))
                {
                    productUserId = rawProductUserId.ToString();
                }

                bool isLocal = productUserId == (_authService != null ? _authService.LocalProductUserIdString : string.Empty);

                Debug.Log(
                    $"EOS Player State: playerId={player.PlayerId}, " +
                    $"productUserId={productUserId}, " +
                    $"partyReady={ready}, matchingReady={(hasMatchingReady ? matchingReady.ToString() : "pending")}, " +
                    $"lobbyId={_currentLobbyId}, " +
                    $"isLocal={isLocal}, " +
                    $"isHostClient={IsHost}"
                );
            }
        }

        /// <summary>
        /// ProductUserIdからオンラインプレイヤーを検索します。
        /// </summary>
        private OnlinePlayer FindPlayerByProductUserId(string productUserId)
        {
            foreach (var player in _players)
            {
                if (!player.Properties.TryGetValue(k_ProductUserIdPropertyKey, out object rawId))
                {
                    continue;
                }

                if ((rawId?.ToString() ?? string.Empty) == productUserId)
                {
                    return player;
                }
            }

            return null;
        }

        /// <summary>
        /// ProductUserIdと共通プレイヤーIDの対応を更新します。
        /// </summary>
        private void EnsureStablePlayerIdMapping(List<string> productUserIds)
        {
            if (productUserIds == null || productUserIds.Count == 0)
            {
                return;
            }

            productUserIds.Sort(StringComparer.Ordinal);

            HashSet<string> activeIds = new HashSet<string>(productUserIds);

            List<string> oldProductIds = new List<string>(_productUserIdToPlayerId.Keys);

            foreach (string oldProductId in oldProductIds)
            {
                if (activeIds.Contains(oldProductId))
                {
                    continue;
                }

                if (_productUserIdToPlayerId.TryGetValue(oldProductId, out int oldPlayerId))
                {
                    _playerIdToProductUserId.Remove(oldPlayerId);
                }

                _productUserIdToPlayerId.Remove(oldProductId);
            }

            _playerIdToProductUserId.Clear();

            for (int i = 0; i < productUserIds.Count; i++)
            {
                string productUserId = productUserIds[i];
                int stablePlayerId = i + 1;

                if (_productUserIdToPlayerId.TryGetValue(productUserId, out int oldPlayerId) &&
                    oldPlayerId != stablePlayerId)
                {
                    Debug.Log(
                        $"EOS StablePlayerId remap: productUserId={productUserId}, " +
                        $"oldPlayerId={oldPlayerId}, newPlayerId={stablePlayerId}");
                }

                _productUserIdToPlayerId[productUserId] = stablePlayerId;
                _playerIdToProductUserId[stablePlayerId] = productUserId;

                if (_authService != null && productUserId == _authService.LocalProductUserIdString)
                {
                    LocalPlayerId = stablePlayerId;
                }
            }
        }

        /// <summary>
        /// ProductUserIdに対応する共通プレイヤーIDを取得または生成します。
        /// </summary>
        private int GetOrCreatePlayerId(string productUserId)
        {
            if (_productUserIdToPlayerId.TryGetValue(productUserId, out int existingPlayerId))
            {
                return existingPlayerId;
            }

            int playerId = 1;

            while (_playerIdToProductUserId.ContainsKey(playerId))
            {
                playerId++;
            }

            _playerIdToProductUserId[playerId] = productUserId;
            _productUserIdToPlayerId[productUserId] = playerId;

            return playerId;
        }

        /// <summary>
        /// EOSから取得したプロパティキーを共通表記へ正規化します。
        /// </summary>
        /// <remarks>
        /// EOS Lobbyの属性キーは大文字化されて返ることがある。
        /// NetworkPropertyKeysにまだ定義していないゲーム内同期用キーもここで正規化する。
        /// </remarks>
        private string NormalizeNetworkPropertyKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            string trimmedKey = key.Trim();
            if (string.Equals(trimmedKey, NetworkPropertyKeys.GameModeId, StringComparison.OrdinalIgnoreCase))
            {
                return NetworkPropertyKeys.GameModeId;
            }
            if (string.Equals(trimmedKey, NetworkPropertyKeys.MatchingPartyCount, StringComparison.OrdinalIgnoreCase))
            {
                return NetworkPropertyKeys.MatchingPartyCount;
            }

            if (string.Equals(trimmedKey, NetworkPropertyKeys.MatchingPartySize, StringComparison.OrdinalIgnoreCase))
            {
                return NetworkPropertyKeys.MatchingPartySize;
            }


            if (string.Equals(trimmedKey, NetworkPropertyKeys.PartyDeparting, StringComparison.OrdinalIgnoreCase))
            {
                return NetworkPropertyKeys.PartyDeparting;
            }

            if (string.Equals(trimmedKey, NetworkPropertyKeys.MacroPlanningRound, StringComparison.OrdinalIgnoreCase))
            {
                return NetworkPropertyKeys.MacroPlanningRound;
            }

            if (string.Equals(trimmedKey, NetworkPropertyKeys.MacroPlanningReady, StringComparison.OrdinalIgnoreCase))
            {
                return NetworkPropertyKeys.MacroPlanningReady;
            }

            if (string.Equals(trimmedKey, NetworkPropertyKeys.Type, StringComparison.OrdinalIgnoreCase))
            {
                return NetworkPropertyKeys.Type;
            }

            if (string.Equals(trimmedKey, NetworkPropertyKeys.State, StringComparison.OrdinalIgnoreCase))
            {
                return NetworkPropertyKeys.State;
            }

            if (string.Equals(trimmedKey, NetworkPropertyKeys.MatchingPartyMemberCount, StringComparison.OrdinalIgnoreCase))
            {
                return NetworkPropertyKeys.MatchingPartyMemberCount;
            }

            if (string.Equals(trimmedKey, NetworkPropertyKeys.PartyId, StringComparison.OrdinalIgnoreCase))
            {
                return NetworkPropertyKeys.PartyId;
            }

            if (string.Equals(trimmedKey, NetworkPropertyKeys.RoomId, StringComparison.OrdinalIgnoreCase))
            {
                return NetworkPropertyKeys.RoomId;
            }

            if (string.Equals(trimmedKey, NetworkPropertyKeys.Password, StringComparison.OrdinalIgnoreCase))
            {
                return NetworkPropertyKeys.Password;
            }

            if (string.Equals(trimmedKey, NetworkPropertyKeys.Ready, StringComparison.OrdinalIgnoreCase))
            {
                return NetworkPropertyKeys.Ready;
            }

            if (string.Equals(
                trimmedKey,
                NetworkPropertyKeys.MatchingReady,
                StringComparison.OrdinalIgnoreCase))
            {
                return NetworkPropertyKeys.MatchingReady;
            }

            if (string.Equals(
                trimmedKey,
                NetworkPropertyKeys.PartyMatching,
                StringComparison.OrdinalIgnoreCase))
            {
                return NetworkPropertyKeys.PartyMatching;
            }

            if (string.Equals(
                trimmedKey,
                NetworkPropertyKeys.MatchCanceled,
                StringComparison.OrdinalIgnoreCase))
            {
                return NetworkPropertyKeys.MatchCanceled;
            }

            if (string.Equals(trimmedKey, NetworkPropertyKeys.TeamId, StringComparison.OrdinalIgnoreCase))
            {
                return NetworkPropertyKeys.TeamId;
            }

            if (string.Equals(
                trimmedKey,
                NetworkPropertyKeys.CharacterName,
                StringComparison.OrdinalIgnoreCase))
            {
                return NetworkPropertyKeys.CharacterName;
            }

            if (string.Equals(trimmedKey, k_InGameSceneReadyPropertyKey, StringComparison.OrdinalIgnoreCase))
            {
                return k_InGameSceneReadyPropertyKey;
            }

            if (string.Equals(trimmedKey, k_InGameStatePropertyKey, StringComparison.OrdinalIgnoreCase))
            {
                return k_InGameStatePropertyKey;
            }

            if (string.Equals(trimmedKey, k_BattleUnitSpawnsPropertyKey, StringComparison.OrdinalIgnoreCase))
            {
                return k_BattleUnitSpawnsPropertyKey;
            }

            if (string.Equals(
                trimmedKey,
                NetworkPropertyKeys.ReturnRoomReady,
                StringComparison.OrdinalIgnoreCase))
            {
                return NetworkPropertyKeys.ReturnRoomReady;
            }

            return trimmedKey;
        }

        /// <summary>
        /// EOS Lobby属性値をbool、intまたはstringへ変換します。
        /// </summary>
        private object FromLobbyAttributeValueToObject(AttributeDataValue value)
        {
            string stringValue = FromLobbyAttributeValue(value);

            if (bool.TryParse(stringValue, out bool boolValue))
            {
                return boolValue;
            }

            if (int.TryParse(stringValue, out int intValue))
            {
                return intValue;
            }

            return stringValue;
        }

        /// <summary>
        /// Lobby作成失敗をログとイベントで通知します。
        /// </summary>
        private void NotifyCreateLobbyFailure(string message)
        {
            Debug.LogError(message);
            CreateLobbyFailed?.Invoke(message);
        }

        /// <summary>
        /// Lobby参加失敗をログとイベントで通知します。
        /// </summary>
        private void NotifyJoinLobbyFailure(string message)
        {
            Debug.LogError(message);
            JoinLobbyFailed?.Invoke(message);
        }

        /// <summary>
        /// パーティLobbyを作成します。
        /// </summary>
        /// <remarks>
        /// 既存のParty再作成用途。
        /// 今はJoinOrCreateを検索付きで作るより，Createに寄せる。
        /// </remarks>
        public void JoinOrCreatePartyLobby(
                    string roomName,
                    string roomId,
                    string password,
                    string partyId,
                    int partySize)
        {

            CreatePartyLobby(roomName, roomId, password, partyId, partySize, true);
        }

        /// <summary>
        /// 条件に合うマッチングLobbyへの参加または作成を開始します。
        /// </summary>
        /// <remarks>
        /// 1人用Lobbyには参加対象となる他プレイヤーが存在しない。
        /// 検索を繰り返す必要がないため，即時作成する。
        /// </remarks>
        public void JoinMatchingLobby(int partyCount, int partySize, string gameModeId = GameModeRegistry.k_DefaultModeId)
        {
            _matchingRequestVersion++;
            if (!CanAccessLobby(nameof(JoinMatchingLobby)))
            {
                return;
            }

            if (IsInLobby)
            {
                Debug.LogWarning(
                    "EOS JoinMatchingLobby called while already in lobby. " +
                    $"currentLobbyId={_currentLobbyId}, currentType={CurrentRoom?.GetPropertyOrDefault(NetworkPropertyKeys.Type, string.Empty)}");

                return;
            }

            _pendingMatchingPartyCount = Math.Max(1, partyCount);
            _pendingMatchingPartySize = Math.Max(1, partySize);
            _pendingGameModeId = gameModeId;

            _matchingSearchRetryCount = 0;
            _isMatchingSearchRetryPending = false;
            _shouldCreateWhenNextMatchingSearchIsEmpty = false;
            _isMatchingSearchInProgress = false;

            Debug.Log(
                $"EOS JoinMatchingLobby start: " +
                $"partyCount={_pendingMatchingPartyCount}, " +
                $"partySize={_pendingMatchingPartySize}, " +
                $"maximumPlayerCount={PendingMatchingPlayerCount}, " +
                $"searchRetryLimit={k_MatchingSearchMaxRetryCount}");

            if (PendingMatchingPlayerCount <= 1)
            {
                Debug.Log("EOS MatchingLobby single-player mode: skip search and create immediately.");
                CreateMatchingLobby();

                return;
            }

            SearchMatchingLobby();
        }

        /// <summary>
        /// 参加可能なマッチングLobbyを検索します。
        /// </summary>
        /// <remarks>
        /// EOS_LobbySearch_Find は空検索を許可しないため，
        /// type/state ではなくBucketIdだけで検索する。
        /// 取得後に IsJoinableMatchingLobby() 側で type/state/満員判定を行う。
        /// </remarks>
        private void SearchMatchingLobby()
        {
            int requestVersion = _matchingRequestVersion;
            if (IsInLobby)
            {
                return;
            }

            if (_isMatchingSearchInProgress)
            {
                return;
            }

            _isMatchingSearchInProgress = true;

            var createSearchOptions = new CreateLobbySearchOptions
            {
                MaxResults = _eosConfig != null
                    ? _eosConfig.SearchMaxResults
                    : k_DefaultSearchMaxResults
            };

            Result createResult = _lobbyInterface.CreateLobbySearch(
                ref createSearchOptions,
                out LobbySearch lobbySearch);

            if (createResult != Result.Success || lobbySearch == null)
            {
                _isMatchingSearchInProgress = false;
                Debug.LogWarning(
                    $"EOS MatchingLobby search create failed: {createResult}. Retry or create matching lobby.");
                ScheduleMatchingSearchRetryOrCreate("CreateLobbySearch failed");

                return;
            }

            AttributeData bucketAttribute = new AttributeData
            {
                Key = k_LobbySearchBucketIdKey,
                Value = ToLobbyAttributeValue(MatchingBucketId)
            };

            var bucketParameterOptions = new LobbySearchSetParameterOptions
            {
                Parameter = bucketAttribute,
                ComparisonOp = ComparisonOp.Equal
            };

            Result bucketSetResult = lobbySearch.SetParameter(ref bucketParameterOptions);

            if (bucketSetResult != Result.Success)
            {
                _isMatchingSearchInProgress = false;
                Debug.LogWarning($"EOS MatchingLobby bucket search parameter failed: {bucketSetResult}");
                lobbySearch.Release();
                ScheduleMatchingSearchRetryOrCreate($"Bucket parameter failed: {bucketSetResult}");

                return;
            }

            Debug.Log(
                $"EOS MatchingLobby search uses bucket. key={k_LobbySearchBucketIdKey}, value={MatchingBucketId}");

            var findOptions = new LobbySearchFindOptions
            {
                LocalUserId = _authService.LocalProductUserId
            };

            lobbySearch.Find(
                ref findOptions,
                null,
                (ref LobbySearchFindCallbackInfo data) =>
                {
                    if (requestVersion != _matchingRequestVersion)
                    {
                        lobbySearch.Release();
                        return;
                    }

                    Debug.Log($"EOS MatchingLobby search result: {data.ResultCode}");

                    _isMatchingSearchInProgress = false;

                    if (data.ResultCode != Result.Success)
                    {
                        lobbySearch.Release();
                        ScheduleMatchingSearchRetryOrCreate($"Find failed: {data.ResultCode}");

                        return;
                    }

                    TryJoinMatchingLobbyFromSearch(lobbySearch);
                });
        }

        /// <summary>
        /// マッチングLobbyの検索結果から参加可能なLobbyを探します。
        /// </summary>
        private void TryJoinMatchingLobbyFromSearch(
            LobbySearch lobbySearch)
        {
            var countOptions =
                new LobbySearchGetSearchResultCountOptions();

            uint resultCount =
                lobbySearch.GetSearchResultCount(
                    ref countOptions);

            Debug.Log(
                $"EOS MatchingLobby search count: {resultCount}");

            LobbyDetails bestLobbyDetails =
                null;

            uint bestMemberCount =
                0;
            bool bestContainsLocalParty = false;

            for (uint index = 0;
                 index < resultCount;
                 index++)
            {
                var copyOptions =
                    new LobbySearchCopySearchResultByIndexOptions
                    {
                        LobbyIndex =
                            index
                    };

                Result copyResult =
                    lobbySearch.CopySearchResultByIndex(
                        ref copyOptions,
                        out LobbyDetails lobbyDetails);

                if (copyResult != Result.Success ||
                    lobbyDetails == null)
                {
                    continue;
                }

                if (!IsJoinableMatchingLobby(
                        lobbyDetails,
                        out string skipReason))
                {
                    Debug.Log(
                        $"EOS MatchingLobby skipped: " +
                        $"index={index}, reason={skipReason}");

                    lobbyDetails.Release();
                    continue;
                }

                var memberCountOptions =
                    new LobbyDetailsGetMemberCountOptions();

                uint memberCount =
                    lobbyDetails.GetMemberCount(
                        ref memberCountOptions);

                CanFitMatchingParties(lobbyDetails, out bool containsLocalParty);
                if (bestLobbyDetails == null ||
                    (containsLocalParty && !bestContainsLocalParty) ||
                    (containsLocalParty == bestContainsLocalParty && memberCount > bestMemberCount))
                {
                    bestLobbyDetails?.Release();

                    bestLobbyDetails =
                        lobbyDetails;

                    bestMemberCount =
                        memberCount;
                    bestContainsLocalParty = containsLocalParty;

                    continue;
                }

                lobbyDetails.Release();
            }

            lobbySearch.Release();

            if (bestLobbyDetails != null)
            {
                Debug.Log(
                    $"EOS MatchingLobby selected: " +
                    $"members={bestMemberCount}/" +
                    $"{PendingMatchingPlayerCount}");

                JoinFoundMatchingLobby(
                    bestLobbyDetails);

                return;
            }

            if (_shouldCreateWhenNextMatchingSearchIsEmpty)
            {
                _shouldCreateWhenNextMatchingSearchIsEmpty =
                    false;

                Debug.Log(
                    "EOS MatchingLobby final search was empty. " +
                    "Create matching lobby.");

                CreateMatchingLobby();

                return;
            }

            ScheduleMatchingSearchRetryOrCreate(
                "No joinable matching lobby");
        }

        /// <summary>
        /// 待機中のマッチングLobby再検索を実行します。
        /// </summary>
        private void PollMatchingSearchRetry()
        {
            if (!_isMatchingSearchRetryPending)
            {
                return;
            }

            if (IsInLobby)
            {
                _isMatchingSearchRetryPending = false;
                _shouldCreateWhenNextMatchingSearchIsEmpty = false;

                return;
            }

            if (Time.unscaledTime < _nextMatchingSearchTime)
            {
                return;
            }

            _isMatchingSearchRetryPending = false;

            Debug.Log("EOS MatchingLobby retry wait finished. Run final search.");
            SearchMatchingLobby();
        }

        /// <summary>
        /// マッチングLobbyの再検索または新規作成を予約します。
        /// </summary>
        private void ScheduleMatchingSearchRetryOrCreate(
            string reason)
        {
            if (IsInLobby)
            {
                return;
            }

            if (_matchingSearchRetryCount <
                k_MatchingSearchMaxRetryCount)
            {
                _matchingSearchRetryCount++;

                float delay =
                    GetMatchingSearchRetryDelay();

                _nextMatchingSearchTime =
                    Time.unscaledTime +
                    delay;

                _isMatchingSearchRetryPending =
                    true;

                _shouldCreateWhenNextMatchingSearchIsEmpty =
                    _matchingSearchRetryCount >=
                    k_MatchingSearchMaxRetryCount;

                Debug.Log(
                    $"EOS MatchingLobby final search scheduled: " +
                    $"attempt={_matchingSearchRetryCount}/" +
                    $"{k_MatchingSearchMaxRetryCount}, " +
                    $"delay={delay:0.00}s, " +
                    $"next=FinalSearchThenCreate, " +
                    $"reason={reason}");

                return;
            }

            Debug.Log(
                $"EOS MatchingLobby final search already used. " +
                $"Create matching lobby. reason={reason}");

            _shouldCreateWhenNextMatchingSearchIsEmpty =
                false;

            CreateMatchingLobby();
        }

        /// <summary>
        /// マッチングLobbyを再検索するまでの待機時間を取得します。
        /// </summary>
        private float GetMatchingSearchRetryDelay()
        {
            int hash =
                StableProductUserIdHash(
                    _authService != null
                        ? _authService.LocalProductUserIdString
                        : string.Empty);

            float jitter =
                (hash % 1000) /
                1000.0f *
                k_MatchingSearchRetryJitterRangeSeconds;

            return
                k_MatchingSearchRetryBaseDelaySeconds +
                jitter;
        }

        /// <summary>
        /// ProductUserIdから再現可能な非負ハッシュ値を生成します。
        /// </summary>
        private int StableProductUserIdHash(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            unchecked
            {
                int hash = 23;

                for (int index = 0; index < value.Length; index++)
                {
                    hash = hash * 31 + value[index];
                }

                return hash & int.MaxValue;
            }
        }

        /// <summary>
        /// 指定されたLobbyへマッチング参加できるか判定します。
        /// </summary>
        private bool IsJoinableMatchingLobby(
            LobbyDetails lobbyDetails,
            out string reason)
        {
            reason =
                string.Empty;

            if (lobbyDetails == null)
            {
                reason =
                    "LobbyDetails is null";

                return false;
            }

            Dictionary<string, object> attributes =
                ReadLobbyAttributes(
                    lobbyDetails);

            TryGetPropertyStringIgnoreCase(attributes, NetworkPropertyKeys.GameModeId, out string candidateModeId);
            candidateModeId ??= GameModeRegistry.k_DefaultModeId;
            if (!string.Equals(candidateModeId, _pendingGameModeId, StringComparison.Ordinal))
            {
                reason = "Game mode mismatch";
                return false;
            }

            if (!TryGetPropertyStringIgnoreCase(
                    attributes,
                    NetworkPropertyKeys.Type,
                    out string type) ||
                !string.Equals(
                    type,
                    NetworkPropertyKeys.TypeMatching,
                    StringComparison.OrdinalIgnoreCase))
            {
                reason =
                    $"type is not matching: {type}";

                return false;
            }

            if (TryGetPropertyStringIgnoreCase(
                    attributes,
                    NetworkPropertyKeys.State,
                    out string state) &&
                !string.Equals(
                    state,
                    NetworkPropertyKeys.StateWaiting,
                    StringComparison.OrdinalIgnoreCase))
            {
                reason =
                    $"state is not waiting: {state}";

                return false;
            }

            if (TryGetPropertyBoolIgnoreCase(
                    attributes,
                    NetworkPropertyKeys.MatchCanceled,
                    out bool canceled) &&
                canceled)
            {
                reason =
                    "matchCanceled is true";

                return false;
            }

            if (!TryGetPropertyStringIgnoreCase(
                    attributes,
                    GameVersionPropertyKey,
                    out string gameVersion) ||
                !string.Equals(
                    gameVersion,
                    ProductVersion,
                    StringComparison.Ordinal))
            {
                reason =
                    $"gameVersion mismatch: " +
                    $"{gameVersion}/{ProductVersion}";

                return false;
            }

            if (!TryGetPropertyIntIgnoreCase(
                    attributes,
                    k_MatchingPartyCountPropertyKey,
                    out int partyCount) ||
                partyCount !=
                _pendingMatchingPartyCount)
            {
                reason =
                    $"partyCount mismatch: " +
                    $"{partyCount}/" +
                    $"{_pendingMatchingPartyCount}";

                return false;
            }

            if (!TryGetPropertyIntIgnoreCase(
                    attributes,
                    k_MatchingPartySizePropertyKey,
                    out int partySize) ||
                partySize !=
                _pendingMatchingPartySize)
            {
                reason =
                    $"partySize mismatch: " +
                    $"{partySize}/" +
                    $"{_pendingMatchingPartySize}";

                return false;
            }

            var memberCountOptions =
                new LobbyDetailsGetMemberCountOptions();

            uint memberCount =
                lobbyDetails.GetMemberCount(
                    ref memberCountOptions);

            if (memberCount >=
                PendingMatchingPlayerCount)
            {
                reason =
                    $"lobby full: " +
                    $"{memberCount}/" +
                    $"{PendingMatchingPlayerCount}";

                return false;
            }

            if (TryGetPropertyStringIgnoreCase(attributes, "matchingRequeueToken", out string requeueToken) &&
                !string.IsNullOrWhiteSpace(requeueToken))
            {
                reason = "lobby is requeueing";
                return false;
            }

            if (!CanFitMatchingParties(lobbyDetails, out _))
            {
                reason = "party groups do not fit team capacities or metadata is pending";
                return false;
            }

            reason =
                $"joinable: " +
                $"{memberCount}/" +
                $"{PendingMatchingPlayerCount}";

            return true;
        }

        /// <summary>
        /// 取得済みのパーティ情報で参加先の定員を検証します。
        /// </summary>
        /// <remarks>
        /// 検索結果ではメンバー属性を取得できない場合があるため、不明な候補は参加後に検証します。
        /// 試合開始時の完全なパーティ情報による編成チェックは省略しません。
        /// </remarks>
        private bool CanFitMatchingParties(LobbyDetails details, out bool containsLocalParty)
        {
            containsLocalParty = false;
            if (!TryGetPropertyStringIgnoreCase(_pendingLocalMemberProperties, NetworkPropertyKeys.PartyId, out string localParty) ||
                string.IsNullOrWhiteSpace(localParty) ||
                !TryGetPropertyIntIgnoreCase(_pendingLocalMemberProperties, NetworkPropertyKeys.MatchingPartyMemberCount, out int localSize))
            {
                return true;
            }

            var sizes = new Dictionary<string, int>(StringComparer.Ordinal);
            var countOptions = new LobbyDetailsGetMemberCountOptions();
            uint count = details.GetMemberCount(ref countOptions);
            for (uint index = 0; index < count; index++)
            {
                var memberOptions = new LobbyDetailsGetMemberByIndexOptions
                {
                    MemberIndex = index
                };
                ProductUserId member = details.GetMemberByIndex(ref memberOptions);
                Dictionary<string, object> properties = ReadMemberAttributes(details, member);
                if (!TryGetPropertyStringIgnoreCase(properties, NetworkPropertyKeys.PartyId, out string party) ||
                    string.IsNullOrWhiteSpace(party) ||
                    !TryGetPropertyIntIgnoreCase(properties, NetworkPropertyKeys.MatchingPartyMemberCount, out int size) || size <= 0)
                {
                    return true;
                }

                sizes[party] = sizes.TryGetValue(party, out int previous) ? Math.Max(previous, size) : size;
                containsLocalParty |= string.Equals(party, localParty, StringComparison.Ordinal);
            }

            sizes[localParty] = sizes.TryGetValue(localParty, out int current) ? Math.Max(current, localSize) : localSize;
            int capacityA = _pendingMatchingPartyCount == 1 ? PendingMatchingPlayerCount : (PendingMatchingPlayerCount + 1) / 2;
            return PartyTeamPlanner.TryAssign(new List<int>(sizes.Values), capacityA,
                PendingMatchingPlayerCount - capacityA, out _);
        }

        /// <summary>
        /// 検索で見つかったマッチングLobbyへの参加を開始します。
        /// </summary>
        private void JoinFoundMatchingLobby(LobbyDetails lobbyDetails)
        {
            int requestVersion = _matchingRequestVersion;
            _isMatchingSearchRetryPending = false;
            _shouldCreateWhenNextMatchingSearchIsEmpty = false;
            _isMatchingSearchInProgress = false;

            if (lobbyDetails == null)
            {
                CreateMatchingLobby();

                return;
            }

            Debug.Log("EOS JoinMatchingLobby found existing lobby. Join start.");

            _pendingJoinLobbyDetails = lobbyDetails;

            var joinOptions = new JoinLobbyOptions
            {
                LocalUserId = _authService.LocalProductUserId,
                LobbyDetailsHandle = lobbyDetails,
                PresenceEnabled = false
            };

            _lobbyInterface.JoinLobby(
                ref joinOptions,
                null,
                (ref JoinLobbyCallbackInfo result) =>
                {
                    if (requestVersion != _matchingRequestVersion)
                    {
                        if (result.ResultCode == Result.Success)
                        {
                            LeaveAbandonedMatchingLobby(result.LobbyId);
                        }

                        return;
                    }

                    HandleJoinMatchingLobbyCompleted(ref result);
                });
        }

        /// <summary>
        /// マッチングLobby参加の完了結果を処理します。
        /// </summary>
        private void HandleJoinMatchingLobbyCompleted(ref JoinLobbyCallbackInfo data)
        {
            Debug.Log($"EOS JoinMatchingLobby result: {data.ResultCode}, LobbyId={data.LobbyId}");

            ReleasePendingJoinLobbyDetails();

            if (data.ResultCode != Result.Success)
            {
                Debug.LogWarning(
                    $"EOS JoinMatchingLobby failed. " +
                    $"Search again before creating a Lobby. " +
                    $"result={data.ResultCode}");

                _matchingSearchRetryCount =
                    0;

                ScheduleMatchingSearchRetryOrCreate(
                    $"JoinMatchingLobby failed: " +
                    $"{data.ResultCode}");

                return;
            }

            _currentLobbyId = data.LobbyId;
            IsInLobby = true;
            IsHost = false;

            CacheMatchingRoom(data.LobbyId, PendingMatchingPlayerCount);

            SyncLobbyRoomPropertiesFromCurrentLobby("joinMatching");
            SyncLobbyMembersFromCurrentLobby("joinMatching");
            RequestAcceleratedMemberSync("joinMatching", immediate: true);
            FlushPendingLocalMemberProperties();

            Debug.Log($"EOS Matching Lobby joined. LobbyId={_currentLobbyId}");

            JoinedLobby?.Invoke();
        }

        /// <summary>
        /// 新しいマッチングLobbyを作成します。
        /// </summary>
        private void CreateMatchingLobby()
        {
            int requestVersion = _matchingRequestVersion;
            _isMatchingSearchInProgress = false;
            _isMatchingSearchRetryPending = false;
            _shouldCreateWhenNextMatchingSearchIsEmpty = false;

            if (!CanAccessLobby(nameof(CreateMatchingLobby)))
            {
                return;
            }

            int maximumPlayerCount = PendingMatchingPlayerCount;

            Debug.Log($"EOS CreateMatchingLobby start: maximumPlayerCount={maximumPlayerCount}");

            CacheMatchingRoom("matching_pending", maximumPlayerCount);

            var options = new CreateLobbyOptions
            {
                LobbyId = "ptm_" + Guid.NewGuid().ToString("N"),
                LocalUserId = _authService.LocalProductUserId,
                MaxLobbyMembers = (uint)maximumPlayerCount,
                PermissionLevel = LobbyPermissionLevel.Publicadvertised,
                PresenceEnabled = false,
                AllowInvites = false,
                BucketId = MatchingBucketId,
                DisableHostMigration = false,
                EnableRTCRoom = false,
                EnableJoinById = false,
                RejoinAfterKickRequiresInvite = false
            };

            _lobbyInterface.CreateLobby(
                ref options,
                null,
                (ref CreateLobbyCallbackInfo result) =>
                {
                    if (requestVersion != _matchingRequestVersion)
                    {
                        if (result.ResultCode == Result.Success)
                        {
                            LeaveAbandonedMatchingLobby(result.LobbyId);
                        }

                        return;
                    }

                    HandleCreateMatchingLobbyCompleted(ref result);
                });
        }

        /// <summary>
        /// マッチングLobby作成の完了結果を処理します。
        /// </summary>
        private void HandleCreateMatchingLobbyCompleted(ref CreateLobbyCallbackInfo data)
        {
            int requestVersion = _matchingRequestVersion;
            Debug.Log($"EOS CreateMatchingLobby result: {data.ResultCode}, LobbyId={data.LobbyId}");

            if (data.ResultCode != Result.Success)
            {
                Debug.LogError($"EOS CreateMatchingLobby failed: {data.ResultCode}");
                ClearLocalLobbyCache();

                return;
            }

            _currentLobbyId = data.LobbyId;
            IsInLobby = true;
            IsHost = true;

            CacheMatchingRoom(_currentLobbyId, PendingMatchingPlayerCount);
            EnsureLocalPlayer();

            Dictionary<string, object> lobbyProperties = new()
            {
                { NetworkPropertyKeys.GameModeId, _pendingGameModeId },
                { NetworkPropertyKeys.Type, NetworkPropertyKeys.TypeMatching },
                { NetworkPropertyKeys.State, NetworkPropertyKeys.StateWaiting },
                { NetworkPropertyKeys.MatchCanceled, false },
                { GameVersionPropertyKey, ProductVersion },
                {
                    k_MatchingPartyCountPropertyKey,
                    _pendingMatchingPartyCount
                },
                {
                    k_MatchingPartySizePropertyKey,
                    _pendingMatchingPartySize
                }
            };

            UpdateLobbyAttributes(
                lobbyProperties,
                success =>
                {
                    if (requestVersion != _matchingRequestVersion)
                    {
                        return;
                    }

                    if (!success)
                    {
                        Debug.LogWarning(
                            "EOS CreateMatchingLobby succeeded, but UpdateLobbyAttributes failed.");
                    }

                    SyncLobbyRoomPropertiesFromCurrentLobby("createMatching");
                    SyncLobbyMembersFromCurrentLobby("createMatching");
                    RequestAcceleratedMemberSync("createMatching", immediate: true);
                    FlushPendingLocalMemberProperties();

                    Debug.Log($"EOS Matching Lobby created. LobbyId={_currentLobbyId}");

                    JoinedLobby?.Invoke();
                });
        }

        /// <summary>
        /// マッチングLobbyの情報をローカルへ保存します。
        /// </summary>
        private void CacheMatchingRoom(string lobbyId, int maximumPlayerCount)
        {
            CurrentRoom = new OnlineRoom
            {
                Name = lobbyId,
                PlayerCount = 0,
                MaxPlayers = maximumPlayerCount,
                IsOpen = true,
                IsVisible = true
            };

            CurrentRoom.Properties[NetworkPropertyKeys.GameModeId] = _pendingGameModeId;

            CurrentRoom.Properties[NetworkPropertyKeys.Type] =
                NetworkPropertyKeys.TypeMatching;

            CurrentRoom.Properties[NetworkPropertyKeys.State] =
                NetworkPropertyKeys.StateWaiting;

            CurrentRoom.Properties[NetworkPropertyKeys.MatchCanceled] =
                false;

            CurrentRoom.Properties[GameVersionPropertyKey] =
                ProductVersion;

            CurrentRoom.Properties[k_MatchingPartyCountPropertyKey] =
                _pendingMatchingPartyCount;

            CurrentRoom.Properties[k_MatchingPartySizePropertyKey] =
                _pendingMatchingPartySize;
        }

        /// <summary>
        /// 大文字と小文字を区別せず文字列プロパティを取得します。
        /// </summary>
        private bool TryGetPropertyStringIgnoreCase(
            Dictionary<string, object> properties,
            string key,
            out string value)
        {
            value = string.Empty;

            if (properties == null)
            {
                return false;
            }

            foreach (var kv in properties)
            {
                if (!string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                value = kv.Value?.ToString() ?? string.Empty;

                return true;
            }

            return false;
        }

        /// <summary>
        /// 大文字と小文字を区別せずboolプロパティを取得します。
        /// </summary>
        private bool TryGetPropertyBoolIgnoreCase(
            Dictionary<string, object> properties,
            string key,
            out bool value)
        {
            value = false;

            if (properties == null)
            {
                return false;
            }

            foreach (var kv in properties)
            {
                if (!string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (kv.Value is bool boolValue)
                {
                    value = boolValue;

                    return true;
                }

                if (bool.TryParse(kv.Value?.ToString(), out bool parsed))
                {
                    value = parsed;

                    return true;
                }

                return false;
            }

            return false;
        }

        /// <summary>
        /// 大文字と小文字を区別せずintプロパティを取得します。
        /// </summary>
        private bool TryGetPropertyIntIgnoreCase(
            Dictionary<string, object> properties,
            string key,
            out int value)
        {
            value =
                0;

            if (properties == null)
            {
                return false;
            }

            foreach (KeyValuePair<string, object> pair
                    in properties)
            {
                if (!string.Equals(
                        pair.Key,
                        key,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (pair.Value is int intValue)
                {
                    value =
                        intValue;

                    return true;
                }

                return int.TryParse(
                    pair.Value?.ToString(),
                    out value);
            }

            return false;
        }

        /// <summary>
        /// 外部から部屋を閉じる入口。
        /// HostならLobby自体をDestroyし，Clientなら自分だけLeaveする。
        /// </summary>
        public void CloseLobby()
        {
            if (!IsInLobby || string.IsNullOrEmpty(_currentLobbyId))
            {
                ClearLocalLobbyCache();
                LeftLobby?.Invoke();

                return;
            }

            if (IsHost)
            {
                DestroyCurrentLobby();
            }
            else
            {
                LeaveLobby();
            }
        }

        /// <summary>
        /// ホストとして現在のLobbyを破棄します。
        /// </summary>
        private void DestroyCurrentLobby()
        {
            if (!CanAccessLobby(nameof(DestroyCurrentLobby)))
            {
                return;
            }

            if (string.IsNullOrEmpty(_currentLobbyId))
            {
                ClearLocalLobbyCache();
                LeftLobby?.Invoke();

                return;
            }

            Debug.Log($"EOS DestroyLobby start. LobbyId={_currentLobbyId}");

            var options = new DestroyLobbyOptions
            {
                LocalUserId = _authService.LocalProductUserId,
                LobbyId = _currentLobbyId
            };

            _lobbyInterface.DestroyLobby(
                ref options,
                null,
                (ref DestroyLobbyCallbackInfo data) =>
                {
                    Debug.Log($"EOS DestroyLobby result: {data.ResultCode}");

                    ClearLocalLobbyCache();

                    LeftLobby?.Invoke();
                });
        }

        /// <summary>
        /// 検索中断後に届く結果を無効化し、再検索予約も解除します。
        /// </summary>
        public void CancelPendingMatchingSearch()
        {
            _memberWriteQueue.Clear();
            _roomWriteQueue.Clear();
            _matchingRequestVersion++;
            _isMatchingSearchInProgress = false;
            _isMatchingSearchRetryPending = false;
            _shouldCreateWhenNextMatchingSearchIsEmpty = false;
        }

        /// <summary>
        /// 中断済み要求で遅れて参加したルームだけを退出し、現在の要求には触れません。
        /// </summary>
        private void LeaveAbandonedMatchingLobby(string lobbyId)
        {
            if (_lobbyInterface == null || _authService?.LocalProductUserId == null || string.IsNullOrEmpty(lobbyId) ||
                (IsInLobby && string.Equals(_currentLobbyId, lobbyId, StringComparison.Ordinal)))
            {
                return;
            }

            var options = new LeaveLobbyOptions
            {
                LocalUserId = _authService.LocalProductUserId,
                LobbyId = lobbyId
            };
            _lobbyInterface.LeaveLobby(ref options, null, (ref LeaveLobbyCallbackInfo result) =>
            {
                if (result.ResultCode != Result.Success)
                {
                    Debug.LogWarning($"Canceled matching lobby cleanup failed: {result.ResultCode}");
                }
            });
        }

        /// <summary>
        /// クライアントとして現在のLobbyから退出します。
        /// </summary>
        public void LeaveLobby()
        {
            if (!CanAccessLobby(nameof(LeaveLobby)))
            {
                return;
            }

            if (!IsInLobby || string.IsNullOrEmpty(_currentLobbyId))
            {
                ClearLocalLobbyCache();
                LeftLobby?.Invoke();

                return;
            }

            var options = new LeaveLobbyOptions
            {
                LocalUserId = _authService.LocalProductUserId,
                LobbyId = _currentLobbyId
            };

            _lobbyInterface.LeaveLobby(
                ref options,
                null,
                (ref LeaveLobbyCallbackInfo data) =>
                {
                    Debug.Log($"EOS LeaveLobby result: {data.ResultCode}");

                    ClearLocalLobbyCache();

                    LeftLobby?.Invoke();
                });
        }

        /// <summary>
        /// 指定されたプレイヤーのプロパティをローカルキャッシュへ設定します。
        /// </summary>
        public void SetPlayerPropertyInLocalCache(int playerId, string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            foreach (var player in _players)
            {
                if (player.PlayerId != playerId)
                {
                    continue;
                }

                player.Properties[key] = value;

                PlayerPropertiesChanged?.Invoke(
                    player,
                    new Dictionary<string, object>
                    {
                        { key, value }
                    });

                return;
            }
        }

        /// <summary>
        /// ローカルキャッシュ上のLobby参加可否を更新します。
        /// </summary>
        /// <remarks>
        /// EOS側の開閉は次のUpdateLobby拡張で反映する
        /// </remarks>
        public void SetLobbyOpen(bool isOpen)
        {
            if (CurrentRoom != null)
            {
                CurrentRoom.IsOpen = isOpen;
            }

        }

        /// <summary>
        /// ローカルキャッシュ上のLobby表示状態を更新します。
        /// </summary>
        /// <remarks>
        /// EOS側の可視性はPermissionLevel変更で扱うため後で実装
        /// </remarks>
        public void SetLobbyVisible(bool isVisible)
        {
            if (CurrentRoom != null)
            {
                CurrentRoom.IsVisible = isVisible;
            }

        }

        /// <summary>
        /// ローカルプレイヤーのキャッシュを取得します。
        /// </summary>
        private OnlinePlayer FindLocalPlayer()
        {
            if (_authService == null || string.IsNullOrEmpty(_authService.LocalProductUserIdString))
            {
                return null;
            }

            return FindPlayerByProductUserId(_authService.LocalProductUserIdString);
        }

        /// <summary>
        /// ローカルプレイヤーのキャッシュを取得または生成します。
        /// </summary>
        private OnlinePlayer EnsureLocalPlayer()
        {
            string productUserId = _authService != null
                ? _authService.LocalProductUserIdString
                : string.Empty;

            OnlinePlayer existingLocalPlayer = FindLocalPlayer();

            if (existingLocalPlayer != null)
            {
                LocalPlayerId = existingLocalPlayer.PlayerId;

                return existingLocalPlayer;
            }

            int localPlayerId = !string.IsNullOrEmpty(productUserId)
                ? GetOrCreatePlayerId(productUserId)
                : 1;

            foreach (var player in _players)
            {
                if (player.PlayerId == localPlayerId)
                {
                    player.Properties[k_ProductUserIdPropertyKey] = productUserId;
                    LocalPlayerId = player.PlayerId;

                    return player;
                }
            }

            OnlinePlayer localPlayer = new OnlinePlayer
            {
                PlayerId = localPlayerId,
                DisplayName = $"Player{localPlayerId}"
            };

            localPlayer.Properties[k_ProductUserIdPropertyKey] = productUserId;

            _players.Add(localPlayer);

            if (!string.IsNullOrEmpty(productUserId))
            {
                _playerIdToProductUserId[localPlayerId] = productUserId;
                _productUserIdToPlayerId[productUserId] = localPlayerId;
            }

            LocalPlayerId = localPlayerId;

            Debug.Log(
                $"EOS EnsureLocalPlayer: localPlayerId={LocalPlayerId}, " +
                $"localProductUserId={productUserId}"
            );

            PlayerJoined?.Invoke(localPlayer);

            if (IsHost)
            {
                HostChanged?.Invoke(localPlayer);
            }

            return localPlayer;
        }

        /// <summary>
        /// 保持しているLobbyDetailsハンドルを解放します。
        /// </summary>
        private void ReleasePendingJoinLobbyDetails()
        {
            _pendingJoinLobbyDetails?.Release();
            _pendingJoinLobbyDetails = null;
        }

        /// <summary>
        /// Lobbyに関するローカル状態とハンドルを初期化します。
        /// </summary>
        private void ClearLocalLobbyCache()
        {
            CancelPendingMatchingSearch();
            IsInLobby = false;
            IsHost = false;
            CurrentRoom = null;
            _currentLobbyId = null;
            _pendingJoinRoomId = null;
            _pendingJoinPassword = null;
            LocalPlayerId = k_InvalidPlayerId;

            ReleasePendingJoinLobbyDetails();

            _nextMemberSyncTime = 0.0f;
            _memberSyncBurstEndTime = 0.0f;
            _shouldForceMemberSync = false;

            _isMatchingSearchInProgress = false;
            _isMatchingSearchRetryPending = false;
            _shouldCreateWhenNextMatchingSearchIsEmpty = false;
            _matchingSearchRetryCount = 0;
            _nextMatchingSearchTime = 0.0f;

            _players.Clear();
            _playerIdToProductUserId.Clear();
            _productUserIdToPlayerId.Clear();
            _pendingLocalMemberProperties.Clear();
        }

        /// <summary>
        /// EOS Lobby APIを使用できる状態か確認します。
        /// </summary>
        private bool CanAccessLobby(string caller)
        {
            if (_authService == null)
            {
                Debug.LogError($"{caller} failed: EosAuthService is null.");

                return false;
            }

            if (!_authService.IsLoggedIn || _authService.LocalProductUserId == null)
            {
                Debug.LogError($"{caller} failed: EOS is not logged in.");

                return false;
            }

            if (_lobbyInterface == null)
            {
                Debug.LogError($"{caller} failed: LobbyInterface is null.");

                return false;
            }

            return true;
        }

        /// <summary>
        /// オブジェクトをEOS Lobby属性値へ変換します。
        /// </summary>
        private AttributeDataValue ToLobbyAttributeValue(object value)
        {
            string stringValue;

            if (value == null)
            {
                stringValue = string.Empty;
            }
            else if (value is bool boolValue)
            {
                stringValue = boolValue ? "true" : "false";
            }
            else
            {
                stringValue = value.ToString();
            }

            return new AttributeDataValue
            {
                AsUtf8 = stringValue
            };
        }
    }
}
