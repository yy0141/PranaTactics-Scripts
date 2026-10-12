using System;
using System.Threading;
using System.Threading.Tasks;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

using PhotonPlayer = Photon.Realtime.Player;

using PranaTactics.GameFlow;

namespace PranaTactics.Networking.Photon
{
    /// <summary>
    /// Photon固有のルーム操作と、既存システム向けの
    /// Photonコールバック通知を管理します。
    /// </summary>
    /// <remarks>
    /// オンラインサービスの共通処理と
    /// <see cref="PartyManager"/>への通知は
    /// <see cref="PhotonOnlineService"/>が担当します。
    ///
    /// このクラスは、Photon固有APIを直接使用している
    /// 既存コードとの互換性を維持するために使用します。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PhotonManager : MonoBehaviourPunCallbacks
    {
        /// <summary>
        /// パーティルーム名に使用する接頭辞です。
        /// </summary>
        private const string k_PartyRoomNamePrefix = "party_";

        /// <summary>
        /// マッチングルーム名に使用する接頭辞です。
        /// </summary>
        private const string k_MatchingRoomNamePrefix = "matching_";

        /// <summary>
        /// PhotonルームのプレイヤーTTLです。
        /// </summary>
        private const int k_PlayerTimeToLiveMilliseconds = 0;

        /// <summary>
        /// 通常のルームが空になった後に保持する時間です。
        /// </summary>
        private const int k_DefaultEmptyRoomTimeToLiveMilliseconds = 0;

        /// <summary>
        /// パーティルームが空になった後に保持する時間です。
        /// </summary>
        private const int k_PartyEmptyRoomTimeToLiveMilliseconds =
            5 * 60 * 1000;

        /// <summary>
        /// Photonで指定できる最大プレイヤー数です。
        /// </summary>
        private const int k_MaximumPhotonPlayerCount = byte.MaxValue;

        /// <summary>
        /// プレイヤーIDが未設定であることを示す値です。
        /// </summary>
        private const int k_InvalidPlayerId = -1;

        /// <summary>
        /// チームIDが未設定であることを示す値です。
        /// </summary>
        private const int k_UnassignedTeamId = -1;

        public enum NetworkState
        {
            None = 0,

            Lobby = 1,

            PartyRoom = 2,

            MatchingRoom = 3,

            InGame = 4
        }

        public static PhotonManager Instance
        {
            get; private set;
        }

        public NetworkState State
        {
            get; private set;
        }

        public string CurrentRoomType
        {
            get; private set;
        }

        public int LocalPlayerId
        {
            get
            {
                if (PhotonNetwork.LocalPlayer == null)
                {
                    return k_InvalidPlayerId;
                }

                return PhotonNetwork.LocalPlayer.ActorNumber;
            }
        }

        public event Action JoinedLobby;

        public event Action JoinedRoom;

        public event Action LeftRoom;

        public event Action<PhotonPlayer> PlayerEntered;

        public event Action<PhotonPlayer> PlayerLeft;

        public event Action<PhotonPlayer, Hashtable>
    PlayerPropertiesUpdated;

        public event Action<Hashtable> RoomPropertiesUpdated;

        public event Action<PhotonPlayer> MasterClientSwitched;

        /// <summary>
        /// シングルトンを初期化し、
        /// Photonのシーン自動同期を有効にします。
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

            State = NetworkState.None;
            CurrentRoomType = string.Empty;

            PhotonNetwork.AutomaticallySyncScene = true;
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
        /// Photonへの接続を開始します。
        /// </summary>
        public void Connect()
        {
            if (PhotonNetwork.IsConnected)
            {
                if (!PhotonNetwork.InLobby &&
                    !PhotonNetwork.InRoom)
                {
                    PhotonNetwork.JoinLobby();
                }

                return;
            }

            PhotonNetwork.ConnectUsingSettings();
        }

        /// <summary>
        /// 指定された設定でPhotonルームを作成します。
        /// </summary>
        /// <param name="roomName">
        /// 作成するルーム名です。
        /// </param>
        /// <param name="roomOptions">
        /// 作成するルームの設定です。
        /// </param>
        public void CreateRoom(
            string roomName,
            RoomOptions roomOptions)
        {
            if (!TryValidateRoomName(roomName) ||
                roomOptions == null)
            {
                return;
            }

            PhotonNetwork.CreateRoom(
                roomName,
                roomOptions);
        }

        /// <summary>
        /// 非公開または公開のパーティルームを作成します。
        /// </summary>
        /// <param name="roomName">
        /// 作成するルーム名です。
        /// </param>
        /// <param name="password">
        /// パーティルームのパスワードです。
        /// </param>
        /// <param name="isPrivate">
        /// 非公開ルームにする場合は<c>true</c>です。
        /// </param>
        /// <param name="teamSize">
        /// ルームへ参加できる最大人数です。
        /// </param>
        /// <returns>
        /// 作成要求を開始できた場合は<c>null</c>、
        /// 開始できない場合はエラーメッセージです。
        /// </returns>
        public string CreatePrivateRoom(
            string roomName,
            string password,
            bool isPrivate,
            int teamSize)
        {
            if (PhotonNetwork.InRoom)
            {
                return "すでにルームへ参加しています。";
            }

            if (!TryValidateRoomName(roomName))
            {
                return "ルーム名が設定されていません。";
            }

            if (!TryConvertPlayerCount(
                    teamSize,
                    out byte maximumPlayerCount))
            {
                return "ルームの最大人数が不正です。";
            }

            RoomOptions roomOptions =
                new RoomOptions
                {
                    MaxPlayers = maximumPlayerCount,
                    IsVisible = !isPrivate,
                    IsOpen = true,
                    PlayerTtl =
                        k_PlayerTimeToLiveMilliseconds,
                    EmptyRoomTtl =
                        k_DefaultEmptyRoomTimeToLiveMilliseconds,
                    CustomRoomProperties =
                        new Hashtable
                        {
                            {
                                NetworkPropertyKeys.Type,
                                NetworkPropertyKeys.TypeParty
                            },
                            {
                                NetworkPropertyKeys.RoomId,
                                roomName
                            },
                            {
                                NetworkPropertyKeys.Password,
                                password
                            },
                            {
                                NetworkPropertyKeys.State,
                                NetworkPropertyKeys.StateWaiting
                            },
                            {
                                NetworkPropertyKeys.PartyMatching,
                                false
                            }
                        }
                };

            PhotonNetwork.CreateRoom(
                roomName,
                roomOptions);

            return null;
        }

        /// <summary>
        /// 指定されたPhotonルームへ参加します。
        /// </summary>
        /// <param name="roomName">
        /// 参加するルーム名です。
        /// </param>
        public void JoinRoom(string roomName)
        {
            if (!TryValidateRoomName(roomName))
            {
                return;
            }

            PhotonNetwork.JoinRoom(roomName);
        }

        /// <summary>
        /// 現在のマッチング設定に対応するルームへ
        /// 参加または作成します。
        /// </summary>
        public void JoinMatchingRoom()
        {
            MatchmakingManager matchmakingManager =
                MatchmakingManager.Instance;

            if (matchmakingManager == null)
            {
                Debug.LogError(
                    $"{nameof(PhotonManager)}: " +
                    $"{nameof(MatchmakingManager)}が存在しません。",
                    this);

                return;
            }

            int partyCount =
                matchmakingManager.GetPartyCount();

            int partySize =
                matchmakingManager.GetMatchPartySize();

            long totalPlayerCount =
                (long)partyCount * partySize;

            if (!TryConvertPlayerCount(
                    totalPlayerCount,
                    out byte maximumPlayerCount))
            {
                return;
            }

            string roomName =
                $"{k_MatchingRoomNamePrefix}" +
                $"{partyCount}x{partySize}";

            RoomOptions roomOptions =
                new RoomOptions
                {
                    MaxPlayers = maximumPlayerCount,
                    IsVisible = true,
                    IsOpen = true,
                    PlayerTtl =
                        k_PlayerTimeToLiveMilliseconds,
                    EmptyRoomTtl =
                        k_DefaultEmptyRoomTimeToLiveMilliseconds,
                    CustomRoomProperties =
                        new Hashtable
                        {
                            {
                                NetworkPropertyKeys.Type,
                                NetworkPropertyKeys.TypeMatching
                            },
                            {
                                NetworkPropertyKeys.State,
                                NetworkPropertyKeys.StateWaiting
                            },
                            {
                                NetworkPropertyKeys.MatchCanceled,
                                false
                            }
                        },
                    CustomRoomPropertiesForLobby =
                        new[]
                        {
                            NetworkPropertyKeys.Type,
                            NetworkPropertyKeys.State
                        }
                };

            PhotonNetwork.JoinOrCreateRoom(
                roomName,
                roomOptions,
                TypedLobby.Default);
        }

        /// <summary>
        /// 指定された名前のPhotonルームへ参加します。
        /// </summary>
        /// <param name="roomName">
        /// 参加するルーム名です。
        /// </param>
        /// <returns>
        /// 参加要求を開始できた場合は<c>null</c>、
        /// 開始できない場合はエラーメッセージです。
        /// </returns>
        public string JoinRoomWithPassword(string roomName)
        {
            if (!PhotonNetwork.IsConnected)
            {
                PhotonNetwork.ConnectUsingSettings();

                return "Photonへ接続しています。";
            }

            if (PhotonNetwork.InRoom)
            {
                return "すでにルームへ参加しています。";
            }

            if (!TryValidateRoomName(roomName))
            {
                return "ルーム名が設定されていません。";
            }

            PhotonNetwork.JoinRoom(roomName);

            return null;
        }

        /// <summary>
        /// ルームIDとパスワードに対応する
        /// パーティルームへ参加します。
        /// </summary>
        /// <param name="roomId">
        /// 参加するルームの識別子です。
        /// </param>
        /// <param name="password">
        /// ルームへの参加に使用するパスワードです。
        /// </param>
        public void JoinPartyRoom(
            string roomId,
            string password)
        {
            if (string.IsNullOrWhiteSpace(roomId) ||
                string.IsNullOrWhiteSpace(password))
            {
                Debug.LogError(
                    $"{nameof(PhotonManager)}: " +
                    "ルームIDまたはパスワードが設定されていません。",
                    this);

                return;
            }

            string roomName =
                $"{k_PartyRoomNamePrefix}" +
                $"{roomId}_{password}";

            PhotonNetwork.JoinRoom(roomName);
        }

        /// <summary>
        /// 指定されたパーティルームへ参加し、
        /// 存在しない場合は新しく作成します。
        /// </summary>
        public void JoinOrCreatePartyRoom(
            string roomName,
            string roomId,
            string password,
            string partyId,
            int partySize)
        {
            if (!TryValidateRoomName(roomName) ||
                !TryConvertPlayerCount(
                    partySize,
                    out byte maximumPlayerCount))
            {
                return;
            }

            RoomOptions roomOptions =
                new RoomOptions
                {
                    MaxPlayers = maximumPlayerCount,
                    IsVisible = false,
                    IsOpen = true,
                    PlayerTtl =
                        k_PlayerTimeToLiveMilliseconds,
                    EmptyRoomTtl =
                        k_PartyEmptyRoomTimeToLiveMilliseconds,
                    CustomRoomProperties =
                        new Hashtable
                        {
                            {
                                NetworkPropertyKeys.Type,
                                NetworkPropertyKeys.TypeParty
                            },
                            {
                                NetworkPropertyKeys.RoomId,
                                roomId
                            },
                            {
                                NetworkPropertyKeys.Password,
                                password
                            },
                            {
                                NetworkPropertyKeys.PartyId,
                                partyId
                            },
                            {
                                NetworkPropertyKeys.State,
                                NetworkPropertyKeys.StateWaiting
                            },
                            {
                                NetworkPropertyKeys.PartyMatching,
                                false
                            }
                        }
                };

            PhotonNetwork.JoinOrCreateRoom(
                roomName,
                roomOptions,
                TypedLobby.Default);
        }

        /// <summary>
        /// 現在参加しているPhotonルームから退出します。
        /// </summary>
        public void LeaveRoom()
        {
            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
            }
        }

        /// <summary>
        /// ローカルプレイヤーへカスタムプロパティを設定します。
        /// </summary>
        public void SetPlayerProperty(
            string key,
            object value)
        {
            if (PhotonNetwork.LocalPlayer == null ||
                string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            PhotonNetwork.LocalPlayer.SetCustomProperties(
                CreateSinglePropertyHashtable(
                    key,
                    value));
        }

        /// <summary>
        /// Photonのマスターサーバーへ接続したとき、
        /// ロビーへ参加します。
        /// </summary>
        public override void OnConnectedToMaster()
        {
            if (!PhotonNetwork.InLobby &&
                !PhotonNetwork.InRoom)
            {
                PhotonNetwork.JoinLobby();
            }
        }

        /// <summary>
        /// Photonロビーへの参加完了を処理します。
        /// </summary>
        public override void OnJoinedLobby()
        {
            State = NetworkState.Lobby;
            CurrentRoomType = string.Empty;

            JoinedLobby?.Invoke();

            GameFlowManager gameFlowManager =
                GameFlowManager.Instance;

            MatchmakingManager matchmakingManager =
                MatchmakingManager.Instance;

            if (gameFlowManager == null ||
                gameFlowManager.IsGameplay ||
                matchmakingManager == null ||
                matchmakingManager.IsCancelingMatching())
            {
                return;
            }

            matchmakingManager.RetryJoinMatching();
        }

        /// <summary>
        /// Photonルームの作成完了を処理します。
        /// </summary>
        /// <remarks>
        /// パーティ作成成功の通知は
        /// <see cref="PhotonOnlineService"/>から
        /// <see cref="IOnlineService.JoinedRoom"/>を通して行われます。
        /// </remarks>
        public override void OnCreatedRoom()
        {
            Room room =
                PhotonNetwork.CurrentRoom;

            if (!TryGetRoomType(
                    room,
                    out string roomType))
            {
                return;
            }

            Debug.Log(
                $"{nameof(PhotonManager)}: " +
                $"ルームを作成しました。Type={roomType}",
                this);
        }

        /// <summary>
        /// Photonルームの作成失敗をログへ出力します。
        /// </summary>
        /// <remarks>
        /// パーティ作成失敗の通知は
        /// <see cref="PhotonOnlineService"/>から
        /// <see cref="IOnlineService.PartyRoomCreationFailed"/>を
        /// 通して行われます。
        /// </remarks>
        public override void OnCreateRoomFailed(
            short returnCode,
            string message)
        {
            Debug.LogError(
                $"{nameof(PhotonManager)}: " +
                "Photonルームの作成に失敗しました。" +
                $"Message={message}, Code={returnCode}",
                this);
        }

        /// <summary>
        /// Photonルームへの参加完了を処理します。
        /// </summary>
        public override void OnJoinedRoom()
        {
            Room room =
                PhotonNetwork.CurrentRoom;

            if (!TryGetRoomType(
                    room,
                    out string roomType))
            {
                Debug.LogError(
                    $"{nameof(PhotonManager)}: " +
                    "Typeプロパティが設定されていないルームへ" +
                    "参加したため退出します。",
                    this);

                PhotonNetwork.LeaveRoom();

                return;
            }

            CurrentRoomType = roomType;

            Debug.Log(
                $"{nameof(PhotonManager)}: " +
                $"ルームへ参加しました。Type={roomType}",
                this);

            switch (roomType)
            {
                case NetworkPropertyKeys.TypeMatching:
                    State = NetworkState.MatchingRoom;
                    break;

                case NetworkPropertyKeys.TypeParty:
                    State = NetworkState.PartyRoom;
                    HandleJoinedPartyRoom(room);
                    break;

                default:
                    State = NetworkState.InGame;

                    Debug.LogWarning(
                        $"{nameof(PhotonManager)}: " +
                        $"未定義のルーム種別です。Type={roomType}",
                        this);
                    break;
            }

            JoinedRoom?.Invoke();
        }

        /// <summary>
        /// Photonルームからの退出完了を処理します。
        /// </summary>
        public override void OnLeftRoom()
        {
            State = PhotonNetwork.InLobby
                ? NetworkState.Lobby
                : NetworkState.None;

            CurrentRoomType = string.Empty;

            LeftRoom?.Invoke();
        }

        /// <summary>
        /// Photonルームへの参加失敗をログへ出力します。
        /// </summary>
        /// <remarks>
        /// パーティ参加失敗の通知は
        /// <see cref="PhotonOnlineService"/>から
        /// <see cref="IOnlineService.PartyRoomJoinFailed"/>を
        /// 通して行われます。
        /// </remarks>
        public override void OnJoinRoomFailed(
            short returnCode,
            string message)
        {
            Debug.LogError(
                $"{nameof(PhotonManager)}: " +
                "Photonルームへの参加に失敗しました。" +
                $"Message={message}, Code={returnCode}",
                this);
        }

        /// <summary>
        /// プレイヤーのルーム退出を通知します。
        /// </summary>
        public override void OnPlayerLeftRoom(
            PhotonPlayer otherPlayer)
        {
            PlayerLeft?.Invoke(otherPlayer);
        }

        /// <summary>
        /// プレイヤーのルーム参加を通知します。
        /// </summary>
        public override void OnPlayerEnteredRoom(
            PhotonPlayer newPlayer)
        {
            PlayerEntered?.Invoke(newPlayer);
        }

        /// <summary>
        /// プレイヤープロパティの変更を通知します。
        /// </summary>
        public override void OnPlayerPropertiesUpdate(
            PhotonPlayer targetPlayer,
            Hashtable changedProperties)
        {
            PlayerPropertiesUpdated?.Invoke(
                targetPlayer,
                changedProperties);
        }

        /// <summary>
        /// ルームプロパティの変更を通知します。
        /// </summary>
        public override void OnRoomPropertiesUpdate(
            Hashtable changedProperties)
        {
            RoomPropertiesUpdated?.Invoke(
                changedProperties);
        }

        /// <summary>
        /// マスタークライアントの変更を通知します。
        /// </summary>
        public override void OnMasterClientSwitched(
            PhotonPlayer newMasterClient)
        {
            MasterClientSwitched?.Invoke(
                newMasterClient);
        }

        /// <summary>
        /// 指定されたシーンを読み込みます。
        /// </summary>
        [Obsolete(
            "Use NetworkSceneLoader.LoadScene instead.")]
        public void LoadScene(string sceneName)
        {
            NetworkSceneLoader.LoadScene(sceneName);
        }

        /// <summary>
        /// Photonへの接続とロビー参加が完了するまで待機します。
        /// </summary>
        public async Task<bool> WaitForPhotonConnectionAsync(
            int timeoutSeconds,
            int pollingIntervalMilliseconds = 100,
            CancellationToken cancellationToken = default)
        {
            if (timeoutSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeoutSeconds),
                    timeoutSeconds,
                    "待機時間には1以上の値を指定してください。");
            }

            if (pollingIntervalMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pollingIntervalMilliseconds),
                    pollingIntervalMilliseconds,
                    "確認間隔には1以上の値を指定してください。");
            }

            float elapsedSeconds = 0.0f;

            while (!PhotonNetwork.IsConnected ||
                !PhotonNetwork.InLobby)
            {
                await Task.Delay(
                    pollingIntervalMilliseconds,
                    cancellationToken);

                elapsedSeconds +=
                    pollingIntervalMilliseconds /
                    1000.0f;

                if (elapsedSeconds <
                    timeoutSeconds)
                {
                    continue;
                }

                Debug.LogError(
                    $"{nameof(PhotonManager)}: " +
                    "Photonへの接続待機がタイムアウトしました。",
                    this);

                return false;
            }

            return true;
        }

        /// <summary>
        /// Photonへの接続とロビー参加が完了するまで待機します。
        /// </summary>
        [Obsolete(
            "Use WaitForPhotonConnectionAsync instead.")]
        public async Task WaitForPhotonConnection(
            int waitTime,
            int delayTime = 100)
        {
            await WaitForPhotonConnectionAsync(
                waitTime,
                delayTime);
        }

        /// <summary>
        /// ローカルプレイヤーの準備状態を設定します。
        /// </summary>
        [Obsolete(
            "Use IOnlineService.SetLocalPlayerProperty instead.")]
        public void SetIsReady(bool isReady)
        {
            SetPlayerProperty(
                NetworkPropertyKeys.Ready,
                isReady);
        }

        /// <summary>
        /// 全プレイヤーの準備状態を確認します。
        /// </summary>
        [Obsolete(
            "Use OnlinePlayerUtility.AllPlayersReadyForCharacterSelect.")]
        public bool AllPlayersReady()
        {
            return AreAllPlayersBooleanPropertyTrue(
                NetworkPropertyKeys.Ready);
        }

        /// <summary>
        /// ローカルプレイヤーのゲーム開始可能状態を設定します。
        /// </summary>
        [Obsolete(
            "Use IOnlineService.SetLocalPlayerProperty instead.")]
        public void SetCanStartGame(bool canStartGame)
        {
            SetPlayerProperty(
                NetworkPropertyKeys.CanStartGame,
                canStartGame);
        }

        /// <summary>
        /// 全プレイヤーのゲーム開始可能状態を確認します。
        /// </summary>
        [Obsolete(
            "Use OnlinePlayerUtility.AllPlayersReadyForCharacterSelect.")]
        public bool AllPlayersCanStartGame()
        {
            return AreAllPlayersBooleanPropertyTrue(
                NetworkPropertyKeys.CanStartGame);
        }

        /// <summary>
        /// 指定されたプレイヤーのチームIDを取得します。
        /// </summary>
        public int GetTeamId(int playerId)
        {
            if (PhotonNetwork.CurrentRoom == null ||
                !PhotonNetwork.CurrentRoom.Players.TryGetValue(
                    playerId,
                    out PhotonPlayer player))
            {
                return k_UnassignedTeamId;
            }

            if (!player.CustomProperties.TryGetValue(
                    NetworkPropertyKeys.TeamId,
                    out object rawTeamId) ||
                rawTeamId is not int teamId)
            {
                return k_UnassignedTeamId;
            }

            return teamId;
        }

        /// <summary>
        /// ローカルプレイヤーのActorNumberを取得します。
        /// </summary>
        [Obsolete(
            "Use LocalPlayerId property instead.")]
        public int PlayerId()
        {
            return LocalPlayerId;
        }

        /// <summary>
        /// パーティルーム参加後のPhoton固有処理を実行します。
        /// </summary>
        /// <param name="room">
        /// 参加したPhotonルームです。
        /// </param>
        private static void HandleJoinedPartyRoom(Room room)
        {
            string roomId =
                GetStringRoomProperty(
                    room,
                    NetworkPropertyKeys.RoomId);

            string password =
                GetStringRoomProperty(
                    room,
                    NetworkPropertyKeys.Password);

            if (!string.IsNullOrWhiteSpace(roomId) &&
                !string.IsNullOrWhiteSpace(password))
            {
                PartyManager.Instance?
                    .SetPartyInfo(
                        roomId,
                        password);
            }

            if (PhotonNetwork.IsMasterClient)
            {
                room.IsOpen = true;
                room.IsVisible = false;

                room.SetCustomProperties(
                    new Hashtable
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

            MatchmakingManager matchmakingManager =
                MatchmakingManager.Instance;

            if (matchmakingManager != null &&
                matchmakingManager.IsReturningPartyRoom())
            {
                matchmakingManager.OnReturnedPartyRoom();
            }
        }

        /// <summary>
        /// ルームからルーム種別を取得します。
        /// </summary>
        private static bool TryGetRoomType(
            Room room,
            out string roomType)
        {
            roomType = null;

            if (room?.CustomProperties == null ||
                !room.CustomProperties.TryGetValue(
                    NetworkPropertyKeys.Type,
                    out object rawRoomType))
            {
                return false;
            }

            roomType =
                rawRoomType?.ToString();

            return !string.IsNullOrWhiteSpace(
                roomType);
        }

        /// <summary>
        /// ルームから文字列プロパティを取得します。
        /// </summary>
        private static string GetStringRoomProperty(
            Room room,
            string key)
        {
            if (room?.CustomProperties == null ||
                !room.CustomProperties.TryGetValue(
                    key,
                    out object rawValue))
            {
                return null;
            }

            return rawValue?.ToString();
        }

        /// <summary>
        /// 全プレイヤーの指定されたboolプロパティが
        /// <c>true</c>かどうかを確認します。
        /// </summary>
        private static bool AreAllPlayersBooleanPropertyTrue(
            string propertyKey)
        {
            if (PhotonNetwork.CurrentRoom == null ||
                PhotonNetwork.CurrentRoom.PlayerCount == 0)
            {
                return false;
            }

            foreach (
                PhotonPlayer player
                in PhotonNetwork.CurrentRoom.Players.Values)
            {
                if (!player.CustomProperties.TryGetValue(
                        propertyKey,
                        out object rawValue) ||
                    rawValue is not bool value ||
                    !value)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 単一のプロパティを保持するHashtableを生成します。
        /// </summary>
        private static Hashtable CreateSinglePropertyHashtable(
            string key,
            object value)
        {
            return new Hashtable
            {
                {
                    key,
                    value
                }
            };
        }

        /// <summary>
        /// ルーム名が有効かどうかを確認します。
        /// </summary>
        private bool TryValidateRoomName(string roomName)
        {
            if (!string.IsNullOrWhiteSpace(roomName))
            {
                return true;
            }

            Debug.LogError(
                $"{nameof(PhotonManager)}: " +
                "ルーム名が設定されていません。",
                this);

            return false;
        }

        /// <summary>
        /// プレイヤー数をPhotonで使用できるbyte値へ変換します。
        /// </summary>
        private bool TryConvertPlayerCount(
            long playerCount,
            out byte photonPlayerCount)
        {
            photonPlayerCount = 0;

            if (playerCount >= 1 &&
                playerCount <= k_MaximumPhotonPlayerCount)
            {
                photonPlayerCount =
                    (byte)playerCount;

                return true;
            }

            Debug.LogError(
                $"{nameof(PhotonManager)}: " +
                "最大プレイヤー数がPhotonの有効範囲外です。" +
                $"PlayerCount={playerCount}, " +
                $"Range=1-{k_MaximumPhotonPlayerCount}",
                this);

            return false;
        }
    }
}
