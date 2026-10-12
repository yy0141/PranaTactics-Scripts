using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

using PhotonPlayer = Photon.Realtime.Player;
using PhotonRoom = Photon.Realtime.Room;

using PranaTactics.Match.Modes;

namespace PranaTactics.Networking.Photon
{
    /// <summary>
    /// Photon PUNを使用したオンライン接続、ルーム管理、
    /// プレイヤープロパティ同期およびイベント通知を提供します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PhotonOnlineService :
    MonoBehaviourPunCallbacks,
    IOnlineService,
    IGameModeMatchmakingService
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
        /// 通常のルームで使用する空室保持時間です。
        /// </summary>
        private const int k_DefaultEmptyRoomTimeToLiveMilliseconds = 0;

        /// <summary>
        /// パーティルームを空室状態で保持する時間です。
        /// </summary>
        private const int k_PartyEmptyRoomTimeToLiveMilliseconds =
            5 * 60 * 1000;

        /// <summary>
        /// 有効なプレイヤーIDを取得できない場合に返す値です。
        /// </summary>
        private const int k_InvalidPlayerId = -1;

        /// <summary>
        /// Photonが設定できる最大プレイヤー数です。
        /// </summary>
        private const int k_MaximumPhotonPlayerCount = byte.MaxValue;

        private event Action _joinedLobby;

        private event Action _joinedRoom;

        private event Action _leftRoom;

        private event Action<OnlinePlayer> _playerJoined;

        private event Action<OnlinePlayer> _playerLeft;

        private event Action<
    OnlinePlayer,
    Dictionary<string, object>>
    _playerPropertiesChanged;

        private event Action<Dictionary<string, object>>
    _roomPropertiesChanged;

        private event Action<OnlinePlayer> _hostChanged;

        private event Action<string> _partyRoomCreationFailed;

        private event Action<string> _partyRoomJoinFailed;

        public OnlineBackend Backend
        {
            get
            {
                return OnlineBackend.Photon;
            }
        }

        public bool IsConnected
        {
            get
            {
                return PhotonNetwork.IsConnected;
            }
        }

        public bool IsInLobby
        {
            get
            {
                return PhotonNetwork.InLobby;
            }
        }

        public bool IsInRoom
        {
            get
            {
                return PhotonNetwork.InRoom;
            }
        }

        public bool IsHost
        {
            get
            {
                return PhotonNetwork.IsMasterClient;
            }
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

        public OnlineRoom CurrentRoom
        {
            get
            {
                return ConvertRoom(
                    PhotonNetwork.CurrentRoom);
            }
        }

        public IReadOnlyList<OnlinePlayer> Players
        {
            get
            {
                return ConvertPlayers();
            }
        }

        event Action IOnlineService.JoinedLobby
        {
            add
            {
                _joinedLobby += value;
            }
            remove
            {
                _joinedLobby -= value;
            }
        }

        event Action IOnlineService.JoinedRoom
        {
            add
            {
                _joinedRoom += value;
            }
            remove
            {
                _joinedRoom -= value;
            }
        }

        event Action IOnlineService.LeftRoom
        {
            add
            {
                _leftRoom += value;
            }
            remove
            {
                _leftRoom -= value;
            }
        }

        event Action<OnlinePlayer> IOnlineService.PlayerJoined
        {
            add
            {
                _playerJoined += value;
            }
            remove
            {
                _playerJoined -= value;
            }
        }

        event Action<OnlinePlayer> IOnlineService.PlayerLeft
        {
            add
            {
                _playerLeft += value;
            }
            remove
            {
                _playerLeft -= value;
            }
        }

        event Action<
    OnlinePlayer,
    Dictionary<string, object>>
    IOnlineService.PlayerPropertiesChanged
        {
            add
            {
                _playerPropertiesChanged += value;
            }
            remove
            {
                _playerPropertiesChanged -= value;
            }
        }

        event Action<Dictionary<string, object>>
    IOnlineService.RoomPropertiesChanged
        {
            add
            {
                _roomPropertiesChanged += value;
            }
            remove
            {
                _roomPropertiesChanged -= value;
            }
        }

        event Action<OnlinePlayer> IOnlineService.HostChanged
        {
            add
            {
                _hostChanged += value;
            }
            remove
            {
                _hostChanged -= value;
            }
        }

        event Action<string> IOnlineService.PartyRoomCreationFailed
        {
            add
            {
                _partyRoomCreationFailed += value;
            }
            remove
            {
                _partyRoomCreationFailed -= value;
            }
        }

        event Action<string> IOnlineService.PartyRoomJoinFailed
        {
            add
            {
                _partyRoomJoinFailed += value;
            }
            remove
            {
                _partyRoomJoinFailed -= value;
            }
        }

        /// <summary>
        /// オンラインサービスを登録し、
        /// Photonのシーン自動同期を有効にします。
        /// </summary>
        private void Awake()
        {
            NetworkServiceLocator.RegisterOnlineService(this);

            PhotonNetwork.AutomaticallySyncScene = true;
        }

        /// <summary>
        /// このサービスが破棄されたとき、
        /// NetworkServiceLocatorから登録を解除します。
        /// </summary>
        private void OnDestroy()
        {
            NetworkServiceLocator.UnregisterOnlineService(this);
        }

        /// <summary>
        /// Photonへの接続を開始します。
        /// </summary>
        public void Connect()
        {
            if (PranaTactics.Settings.GameBuildSettings.IsSoloOnly)
            {
                return;
            }

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
        /// 新しい非公開パーティルームを作成します。
        /// </summary>
        /// <param name="roomName">
        /// 作成するPhotonルームの名前です。
        /// </param>
        /// <param name="roomId">
        /// ルームの識別子です。
        /// </param>
        /// <param name="password">
        /// ルームへ参加するためのパスワードです。
        /// </param>
        /// <param name="partyId">
        /// ルームに関連付けるパーティの識別子です。
        /// </param>
        /// <param name="maximumPlayerCount">
        /// ルームへ参加できる最大プレイヤー数です。
        /// </param>
        public void CreatePartyRoom(
            string roomName,
            string roomId,
            string password,
            string partyId,
            int maximumPlayerCount)
        {
            if (!TryValidateRoomName(roomName) ||
                !TryConvertPlayerCount(
                    maximumPlayerCount,
                    out byte photonPlayerCount))
            {
                return;
            }

            RoomOptions roomOptions =
                CreatePartyRoomOptions(
                    roomId,
                    password,
                    partyId,
                    photonPlayerCount,
                    k_DefaultEmptyRoomTimeToLiveMilliseconds);

            PhotonNetwork.CreateRoom(
                roomName,
                roomOptions);
        }

        /// <summary>
        /// 指定されたルームIDとパスワードに対応する
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
                    $"{nameof(PhotonOnlineService)}: " +
                    "ルームIDまたはパスワードが指定されていません。",
                    this);

                return;
            }

            string roomName =
                $"{k_PartyRoomNamePrefix}{roomId}_{password}";

            PhotonNetwork.JoinRoom(roomName);
        }

        /// <summary>
        /// 指定されたパーティルームへ参加し、
        /// 存在しない場合は新しく作成します。
        /// </summary>
        /// <param name="roomName">
        /// 参加または作成するルームの名前です。
        /// </param>
        /// <param name="roomId">
        /// ルームの識別子です。
        /// </param>
        /// <param name="password">
        /// ルームへ参加するためのパスワードです。
        /// </param>
        /// <param name="partyId">
        /// ルームに関連付けるパーティの識別子です。
        /// </param>
        /// <param name="partySize">
        /// パーティルームの最大参加人数です。
        /// </param>
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
                    out byte photonPlayerCount))
            {
                return;
            }

            RoomOptions roomOptions =
                CreatePartyRoomOptions(
                    roomId,
                    password,
                    partyId,
                    photonPlayerCount,
                    k_PartyEmptyRoomTimeToLiveMilliseconds);

            PhotonNetwork.JoinOrCreateRoom(
                roomName,
                roomOptions,
                TypedLobby.Default);
        }

        /// <summary>
        /// 指定されたパーティ構成に対応する
        /// マッチングルームへ参加または作成します。
        /// </summary>
        /// <param name="partyCount">
        /// 1試合へ参加するパーティ数です。
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
        /// 同じルールと人数構成を持つPhotonルームへ参加します。
        /// </summary>
        public void JoinModeRoom(int partyCount, int partySize, string gameModeId)
        {
            if (!GameModeRegistry.Catalog.TryGet(gameModeId, PlayCategory.Online, out _))
            {
                throw new ArgumentException("不明なオンラインモードです。", nameof(gameModeId));
            }

            if (partyCount <= 0 ||
                partySize <= 0)
            {
                Debug.LogError(
                    $"{nameof(PhotonOnlineService)}: " +
                    "パーティ数とパーティ人数には1以上の値を指定してください。",
                    this);

                return;
            }

            long totalPlayerCount =
                (long)partyCount * partySize;

            if (!TryConvertPlayerCount(
                    totalPlayerCount,
                    out byte photonPlayerCount))
            {
                return;
            }

            string roomName =
                $"{k_MatchingRoomNamePrefix}" +
                $"{partyCount}x{partySize}";

            roomName = GameModeMatchmakingKey.Create(roomName, gameModeId);

            RoomOptions roomOptions =
                new RoomOptions
                {
                    MaxPlayers = photonPlayerCount,
                    IsVisible = true,
                    IsOpen = true,
                    PlayerTtl =
                        k_PlayerTimeToLiveMilliseconds,
                    EmptyRoomTtl =
                        k_DefaultEmptyRoomTimeToLiveMilliseconds,
                    CustomRoomProperties =
                        new Hashtable
                        {
                            { NetworkPropertyKeys.GameModeId, gameModeId },
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
        /// 現在参加しているPhotonルームから退出します。
        /// </summary>
        public void LeaveRoom()
        {
            if (!PhotonNetwork.InRoom)
            {
                return;
            }

            PhotonNetwork.LeaveRoom();
        }

        /// <summary>
        /// 指定されたプレイヤーへカスタムプロパティを設定します。
        /// </summary>
        /// <param name="playerId">
        /// プロパティを設定するプレイヤーのActorNumberです。
        /// </param>
        /// <param name="key">
        /// 設定するプロパティのキーです。
        /// </param>
        /// <param name="value">
        /// 設定するプロパティの値です。
        /// </param>
        public void SetPlayerProperty(
            int playerId,
            string key,
            object value)
        {
            if (!TryValidatePropertyKey(key) ||
                PhotonNetwork.CurrentRoom == null)
            {
                return;
            }

            if (!PhotonNetwork.CurrentRoom.Players.TryGetValue(
                    playerId,
                    out PhotonPlayer player))
            {
                Debug.LogWarning(
                    $"{nameof(PhotonOnlineService)}: " +
                    "プロパティを設定するプレイヤーが見つかりません。" +
                    $"PlayerId={playerId}",
                    this);

                return;
            }

            player.SetCustomProperties(
                CreateSinglePropertyHashtable(
                    key,
                    value));
        }

        /// <summary>
        /// ローカルプレイヤーへカスタムプロパティを設定します。
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
            if (!TryValidatePropertyKey(key) ||
                PhotonNetwork.LocalPlayer == null)
            {
                return;
            }

            PhotonNetwork.LocalPlayer.SetCustomProperties(
                CreateSinglePropertyHashtable(
                    key,
                    value));
        }

        /// <summary>
        /// ローカルプレイヤーへ複数の
        /// カスタムプロパティを設定します。
        /// </summary>
        /// <param name="properties">
        /// 設定するプロパティの一覧です。
        /// </param>
        public void SetLocalPlayerProperties(
            Dictionary<string, object> properties)
        {
            if (properties == null ||
                properties.Count == 0 ||
                PhotonNetwork.LocalPlayer == null)
            {
                return;
            }

            PhotonNetwork.LocalPlayer.SetCustomProperties(
                ToHashtable(properties));
        }

        /// <summary>
        /// 現在のルームへカスタムプロパティを設定します。
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
            if (!TryValidatePropertyKey(key) ||
                PhotonNetwork.CurrentRoom == null)
            {
                return;
            }

            PhotonNetwork.CurrentRoom.SetCustomProperties(
                CreateSinglePropertyHashtable(
                    key,
                    value));
        }

        /// <summary>
        /// 現在のルームへ複数の
        /// カスタムプロパティを設定します。
        /// </summary>
        /// <param name="properties">
        /// 設定するプロパティの一覧です。
        /// </param>
        public void SetRoomProperties(
            Dictionary<string, object> properties)
        {
            if (properties == null ||
                properties.Count == 0 ||
                PhotonNetwork.CurrentRoom == null)
            {
                return;
            }

            PhotonNetwork.CurrentRoom.SetCustomProperties(
                ToHashtable(properties));
        }

        /// <summary>
        /// 現在のルームを参加可能な状態にするかどうかを設定します。
        /// </summary>
        /// <param name="isOpen">
        /// 新しいプレイヤーの参加を許可する場合は<c>true</c>です。
        /// </param>
        public void SetRoomOpen(bool isOpen)
        {
            if (PhotonNetwork.CurrentRoom == null)
            {
                return;
            }

            PhotonNetwork.CurrentRoom.IsOpen = isOpen;
        }

        /// <summary>
        /// 現在のルームをロビー一覧に表示するかどうかを設定します。
        /// </summary>
        /// <param name="isVisible">
        /// ルームをロビー一覧に表示する場合は<c>true</c>です。
        /// </param>
        public void SetRoomVisible(bool isVisible)
        {
            if (PhotonNetwork.CurrentRoom == null)
            {
                return;
            }

            PhotonNetwork.CurrentRoom.IsVisible = isVisible;
        }

        /// <summary>
        /// 指定されたプレイヤーへネットワークメッセージを送信します。
        /// </summary>
        /// <param name="playerId">
        /// メッセージの送信先となるプレイヤーIDです。
        /// </param>
        /// <param name="message">
        /// 送信するネットワークメッセージです。
        /// </param>
        /// <param name="sendMode">
        /// メッセージの送信方式です。
        /// </param>
        /// <remarks>
        /// 現在、Photonでの任意メッセージ送信処理は未実装です。
        /// </remarks>
        public void SendToPlayer(
            int playerId,
            NetMessage message,
            NetworkSendMode sendMode)
        {
            if (message == null)
            {
                Debug.LogWarning(
                    $"{nameof(PhotonOnlineService)}: " +
                    "送信するメッセージがnullです。",
                    this);

                return;
            }

            Debug.LogWarning(
                $"{nameof(PhotonOnlineService)}: " +
                $"{nameof(SendToPlayer)}は現在未実装です。" +
                $"PlayerId={playerId}, " +
                $"Type={message.Type}, " +
                $"Mode={sendMode}",
                this);
        }

        /// <summary>
        /// Photonのマスターサーバーへ接続したとき、
        /// 既定のロビーへ参加します。
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
        /// Photonロビーへの参加が完了したときに呼び出されます。
        /// </summary>
        public override void OnJoinedLobby()
        {
            _joinedLobby?.Invoke();
        }

        /// <summary>
        /// Photonルームへの参加が完了したときに呼び出されます。
        /// </summary>
        public override void OnJoinedRoom()
        {
            _joinedRoom?.Invoke();
        }

        /// <summary>
        /// Photonルームからの退出が完了したときに呼び出されます。
        /// </summary>
        public override void OnLeftRoom()
        {
            _leftRoom?.Invoke();
        }

        /// <summary>
        /// Photonルームの作成に失敗したときに呼び出されます。
        /// </summary>
        /// <param name="returnCode">
        /// Photonから返されたエラーコードです。
        /// </param>
        /// <param name="message">
        /// Photonから返されたエラーメッセージです。
        /// </param>
        public override void OnCreateRoomFailed(
            short returnCode,
            string message)
        {
            _partyRoomCreationFailed?.Invoke(
                "部屋の作成に失敗しました。" +
                $"Photon: {message} ({returnCode})");
        }

        /// <summary>
        /// Photonルームへの参加に失敗したときに呼び出されます。
        /// </summary>
        /// <param name="returnCode">
        /// Photonから返されたエラーコードです。
        /// </param>
        /// <param name="message">
        /// Photonから返されたエラーメッセージです。
        /// </param>
        public override void OnJoinRoomFailed(
            short returnCode,
            string message)
        {
            _partyRoomJoinFailed?.Invoke(
                "部屋への参加に失敗しました。" +
                $"Photon: {message} ({returnCode})");
        }

        /// <summary>
        /// Photonのランダムルーム参加に失敗したときに呼び出されます。
        /// </summary>
        /// <param name="returnCode">
        /// Photonから返されたエラーコードです。
        /// </param>
        /// <param name="message">
        /// Photonから返されたエラーメッセージです。
        /// </param>
        public override void OnJoinRandomFailed(
            short returnCode,
            string message)
        {
            _partyRoomJoinFailed?.Invoke(
                "部屋への参加に失敗しました。" +
                $"Photon: {message} ({returnCode})");
        }

        /// <summary>
        /// 新しいプレイヤーがPhotonルームへ参加したときに呼び出されます。
        /// </summary>
        /// <param name="newPlayer">
        /// 参加したPhotonプレイヤーです。
        /// </param>
        public override void OnPlayerEnteredRoom(
            PhotonPlayer newPlayer)
        {
            OnlinePlayer onlinePlayer =
                ConvertPlayer(newPlayer);

            if (onlinePlayer != null)
            {
                _playerJoined?.Invoke(onlinePlayer);
            }
        }

        /// <summary>
        /// プレイヤーがPhotonルームから退出したときに呼び出されます。
        /// </summary>
        /// <param name="otherPlayer">
        /// 退出したPhotonプレイヤーです。
        /// </param>
        public override void OnPlayerLeftRoom(
            PhotonPlayer otherPlayer)
        {
            OnlinePlayer onlinePlayer =
                ConvertPlayer(otherPlayer);

            if (onlinePlayer != null)
            {
                _playerLeft?.Invoke(onlinePlayer);
            }
        }

        /// <summary>
        /// Photonプレイヤーのカスタムプロパティが
        /// 更新されたときに呼び出されます。
        /// </summary>
        /// <param name="targetPlayer">
        /// プロパティが更新されたPhotonプレイヤーです。
        /// </param>
        /// <param name="changedProperties">
        /// 変更されたカスタムプロパティです。
        /// </param>
        public override void OnPlayerPropertiesUpdate(
            PhotonPlayer targetPlayer,
            Hashtable changedProperties)
        {
            OnlinePlayer onlinePlayer =
                ConvertPlayer(targetPlayer);

            if (onlinePlayer == null)
            {
                return;
            }

            _playerPropertiesChanged?.Invoke(
                onlinePlayer,
                ToDictionary(changedProperties));
        }

        /// <summary>
        /// Photonルームのカスタムプロパティが
        /// 更新されたときに呼び出されます。
        /// </summary>
        /// <param name="changedProperties">
        /// 変更されたカスタムプロパティです。
        /// </param>
        public override void OnRoomPropertiesUpdate(
            Hashtable changedProperties)
        {
            _roomPropertiesChanged?.Invoke(
                ToDictionary(changedProperties));
        }

        /// <summary>
        /// Photonのマスタークライアントが
        /// 変更されたときに呼び出されます。
        /// </summary>
        /// <param name="newMasterClient">
        /// 新しくマスタークライアントになったプレイヤーです。
        /// </param>
        public override void OnMasterClientSwitched(
            PhotonPlayer newMasterClient)
        {
            OnlinePlayer onlinePlayer =
                ConvertPlayer(newMasterClient);

            if (onlinePlayer != null)
            {
                _hostChanged?.Invoke(onlinePlayer);
            }
        }

        /// <summary>
        /// パーティルームで使用するRoomOptionsを生成します。
        /// </summary>
        /// <param name="roomId">
        /// ルームの識別子です。
        /// </param>
        /// <param name="password">
        /// ルームへ参加するためのパスワードです。
        /// </param>
        /// <param name="partyId">
        /// パーティの識別子です。
        /// </param>
        /// <param name="maximumPlayerCount">
        /// ルームの最大参加人数です。
        /// </param>
        /// <param name="emptyRoomTimeToLiveMilliseconds">
        /// ルームが空になった後に保持する時間です。
        /// </param>
        /// <returns>
        /// 生成されたRoomOptionsです。
        /// </returns>
        private static RoomOptions CreatePartyRoomOptions(
            string roomId,
            string password,
            string partyId,
            byte maximumPlayerCount,
            int emptyRoomTimeToLiveMilliseconds)
        {
            return new RoomOptions
            {
                MaxPlayers = maximumPlayerCount,
                IsVisible = false,
                IsOpen = true,
                PlayerTtl =
                    k_PlayerTimeToLiveMilliseconds,
                EmptyRoomTtl =
                    emptyRoomTimeToLiveMilliseconds,
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
        }

        /// <summary>
        /// 現在参加しているPhotonプレイヤーを
        /// 共通形式へ変換します。
        /// </summary>
        /// <returns>
        /// 共通形式へ変換されたプレイヤー一覧です。
        /// </returns>
        private static IReadOnlyList<OnlinePlayer> ConvertPlayers()
        {
            List<OnlinePlayer> onlinePlayers = new();

            PhotonPlayer[] photonPlayers =
                PhotonNetwork.PlayerList;

            if (photonPlayers == null)
            {
                return onlinePlayers;
            }

            foreach (PhotonPlayer photonPlayer in photonPlayers)
            {
                OnlinePlayer onlinePlayer =
                    ConvertPlayer(photonPlayer);

                if (onlinePlayer != null)
                {
                    onlinePlayers.Add(onlinePlayer);
                }
            }

            return onlinePlayers;
        }

        /// <summary>
        /// Photonプレイヤーを共通プレイヤー情報へ変換します。
        /// </summary>
        /// <param name="photonPlayer">
        /// 変換するPhotonプレイヤーです。
        /// </param>
        /// <returns>
        /// 変換されたオンラインプレイヤーです。
        /// </returns>
        private static OnlinePlayer ConvertPlayer(
            PhotonPlayer photonPlayer)
        {
            if (photonPlayer == null)
            {
                return null;
            }

            OnlinePlayer onlinePlayer =
                new OnlinePlayer
                {
                    PlayerId = photonPlayer.ActorNumber,
                    DisplayName = photonPlayer.NickName
                };

            CopyHashtableProperties(
                photonPlayer.CustomProperties,
                onlinePlayer.Properties);

            return onlinePlayer;
        }

        /// <summary>
        /// Photonルームを共通ルーム情報へ変換します。
        /// </summary>
        /// <param name="photonRoom">
        /// 変換するPhotonルームです。
        /// </param>
        /// <returns>
        /// 変換されたオンラインルームです。
        /// </returns>
        private static OnlineRoom ConvertRoom(
            PhotonRoom photonRoom)
        {
            if (photonRoom == null)
            {
                return null;
            }

            OnlineRoom onlineRoom =
                new OnlineRoom
                {
                    Name = photonRoom.Name,
                    PlayerCount = photonRoom.PlayerCount,
                    MaxPlayers = photonRoom.MaxPlayers,
                    IsOpen = photonRoom.IsOpen,
                    IsVisible = photonRoom.IsVisible
                };

            CopyHashtableProperties(
                photonRoom.CustomProperties,
                onlineRoom.Properties);

            return onlineRoom;
        }

        /// <summary>
        /// DictionaryをPhotonのHashtableへ変換します。
        /// </summary>
        /// <param name="properties">
        /// 変換するプロパティ一覧です。
        /// </param>
        /// <returns>
        /// 生成されたPhoton Hashtableです。
        /// </returns>
        private static Hashtable ToHashtable(
            IReadOnlyDictionary<string, object> properties)
        {
            Hashtable hashtable = new();

            if (properties == null)
            {
                return hashtable;
            }

            foreach (
                KeyValuePair<string, object> property
                in properties)
            {
                if (string.IsNullOrEmpty(property.Key))
                {
                    continue;
                }

                hashtable[property.Key] =
                    property.Value;
            }

            return hashtable;
        }

        /// <summary>
        /// PhotonのHashtableをDictionaryへ変換します。
        /// </summary>
        /// <param name="properties">
        /// 変換するPhotonプロパティ一覧です。
        /// </param>
        /// <returns>
        /// 生成されたDictionaryです。
        /// </returns>
        private static Dictionary<string, object> ToDictionary(
            Hashtable properties)
        {
            Dictionary<string, object> dictionary = new();

            CopyHashtableProperties(
                properties,
                dictionary);

            return dictionary;
        }

        /// <summary>
        /// Photon Hashtableの文字列キーを持つ要素を
        /// 指定されたDictionaryへコピーします。
        /// </summary>
        /// <param name="source">
        /// コピー元となるPhoton Hashtableです。
        /// </param>
        /// <param name="destination">
        /// コピー先となるDictionaryです。
        /// </param>
        private static void CopyHashtableProperties(
            Hashtable source,
            IDictionary<string, object> destination)
        {
            if (source == null ||
                destination == null)
            {
                return;
            }

            foreach (object key in source.Keys)
            {
                if (key is string stringKey)
                {
                    destination[stringKey] =
                        source[key];
                }
            }
        }

        /// <summary>
        /// 単一のプロパティを保持するPhoton Hashtableを生成します。
        /// </summary>
        /// <param name="key">
        /// プロパティのキーです。
        /// </param>
        /// <param name="value">
        /// プロパティの値です。
        /// </param>
        /// <returns>
        /// 生成されたPhoton Hashtableです。
        /// </returns>
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
        /// <param name="roomName">
        /// 確認するルーム名です。
        /// </param>
        /// <returns>
        /// 有効なルーム名の場合は<c>true</c>です。
        /// </returns>
        private bool TryValidateRoomName(string roomName)
        {
            if (!string.IsNullOrWhiteSpace(roomName))
            {
                return true;
            }

            Debug.LogError(
                $"{nameof(PhotonOnlineService)}: " +
                "ルーム名が指定されていません。",
                this);

            return false;
        }

        /// <summary>
        /// プレイヤー数をPhotonで使用できるbyte値へ変換します。
        /// </summary>
        /// <param name="playerCount">
        /// 変換するプレイヤー数です。
        /// </param>
        /// <param name="photonPlayerCount">
        /// 変換されたPhoton用プレイヤー数です。
        /// </param>
        /// <returns>
        /// 有効な値へ変換できた場合は<c>true</c>です。
        /// </returns>
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
                $"{nameof(PhotonOnlineService)}: " +
                "最大プレイヤー数がPhotonの有効範囲外です。" +
                $"PlayerCount={playerCount}, " +
                $"Range=1-{k_MaximumPhotonPlayerCount}",
                this);

            return false;
        }

        /// <summary>
        /// カスタムプロパティのキーが有効かどうかを確認します。
        /// </summary>
        /// <param name="key">
        /// 確認するプロパティキーです。
        /// </param>
        /// <returns>
        /// 有効なキーの場合は<c>true</c>です。
        /// </returns>
        private bool TryValidatePropertyKey(string key)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                return true;
            }

            Debug.LogError(
                $"{nameof(PhotonOnlineService)}: " +
                "プロパティのキーが指定されていません。",
                this);

            return false;
        }
    }
}
