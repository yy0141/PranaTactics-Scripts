using System;
using System.Collections.Generic;

namespace PranaTactics.Networking
{
    public interface IOnlineService
    {
        OnlineBackend Backend
        {
            get;
        }

        bool IsConnected
        {
            get;
        }

        bool IsInLobby
        {
            get;
        }

        bool IsInRoom
        {
            get;
        }

        bool IsHost
        {
            get;
        }

        int LocalPlayerId
        {
            get;
        }

        OnlineRoom CurrentRoom
        {
            get;
        }

        IReadOnlyList<OnlinePlayer> Players
        {
            get;
        }

        /// <summary>
        /// オンラインサービスへの接続を開始します。
        /// </summary>
        void Connect();

        /// <summary>
        /// 現在参加しているオンラインルームから退出します。
        /// </summary>
        void LeaveRoom();

        /// <summary>
        /// 新しいパーティルームを作成します。
        /// </summary>
        /// <param name="roomName">
        /// 作成するルームの名前です。
        /// </param>
        /// <param name="roomId">
        /// 作成するルームの識別子です。
        /// </param>
        /// <param name="password">
        /// ルームへの参加に使用するパスワードです。
        /// </param>
        /// <param name="partyId">
        /// ルームに関連付けるパーティの識別子です。
        /// </param>
        /// <param name="maximumPlayerCount">
        /// ルームへ参加できる最大プレイヤー数です。
        /// </param>
        void CreatePartyRoom(
            string roomName,
            string roomId,
            string password,
            string partyId,
            int maximumPlayerCount);

        /// <summary>
        /// 指定された識別子とパスワードを使用して、
        /// パーティルームへ参加します。
        /// </summary>
        /// <param name="roomId">
        /// 参加するルームの識別子です。
        /// </param>
        /// <param name="password">
        /// ルームへの参加に使用するパスワードです。
        /// </param>
        void JoinPartyRoom(
            string roomId,
            string password);

        /// <summary>
        /// 指定されたパーティルームへ参加します。
        /// ルームが存在しない場合は新しく作成します。
        /// </summary>
        /// <param name="roomName">
        /// 参加または作成するルームの名前です。
        /// </param>
        /// <param name="roomId">
        /// 参加または作成するルームの識別子です。
        /// </param>
        /// <param name="password">
        /// ルームへの参加に使用するパスワードです。
        /// </param>
        /// <param name="partyId">
        /// ルームに関連付けるパーティの識別子です。
        /// </param>
        /// <param name="partySize">
        /// パーティに参加できる最大プレイヤー数です。
        /// </param>
        void JoinOrCreatePartyRoom(
            string roomName,
            string roomId,
            string password,
            string partyId,
            int partySize);

        /// <summary>
        /// 指定されたパーティ数とパーティ人数に対応する
        /// マッチングルームへ参加します。
        /// </summary>
        /// <param name="partyCount">
        /// 1試合に参加するパーティ数です。
        /// </param>
        /// <param name="partySize">
        /// 1パーティに参加するプレイヤー数です。
        /// </param>
        void JoinMatchingRoom(
            int partyCount,
            int partySize);

        /// <summary>
        /// 指定されたプレイヤーのカスタムプロパティを設定します。
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
        void SetPlayerProperty(
            int playerId,
            string key,
            object value);

        /// <summary>
        /// ローカルプレイヤーのカスタムプロパティを設定します。
        /// </summary>
        /// <param name="key">
        /// 設定するプロパティのキーです。
        /// </param>
        /// <param name="value">
        /// 設定するプロパティの値です。
        /// </param>
        void SetLocalPlayerProperty(
            string key,
            object value);

        /// <summary>
        /// ローカルプレイヤーの複数のカスタムプロパティを設定します。
        /// </summary>
        /// <param name="properties">
        /// 設定するプロパティの一覧です。
        /// </param>
        void SetLocalPlayerProperties(
            Dictionary<string, object> properties);

        /// <summary>
        /// 現在のルームへカスタムプロパティを設定します。
        /// </summary>
        /// <param name="key">
        /// 設定するプロパティのキーです。
        /// </param>
        /// <param name="value">
        /// 設定するプロパティの値です。
        /// </param>
        void SetRoomProperty(
            string key,
            object value);

        /// <summary>
        /// 現在のルームへ複数のカスタムプロパティを設定します。
        /// </summary>
        /// <param name="properties">
        /// 設定するプロパティの一覧です。
        /// </param>
        void SetRoomProperties(
            Dictionary<string, object> properties);

        /// <summary>
        /// 現在のルームへ新しいプレイヤーが
        /// 参加できるかどうかを設定します。
        /// </summary>
        /// <param name="isOpen">
        /// 新しいプレイヤーの参加を許可する場合は<c>true</c>、
        /// 許可しない場合は<c>false</c>です。
        /// </param>
        void SetRoomOpen(bool isOpen);

        /// <summary>
        /// 現在のルームを検索結果や
        /// ルーム一覧に表示するかどうかを設定します。
        /// </summary>
        /// <param name="isVisible">
        /// ルームを表示する場合は<c>true</c>、
        /// 非表示にする場合は<c>false</c>です。
        /// </param>
        void SetRoomVisible(bool isVisible);

        /// <summary>
        /// 指定されたプレイヤーへネットワークメッセージを送信します。
        /// </summary>
        /// <param name="playerId">
        /// メッセージを送信するプレイヤーの識別子です。
        /// </param>
        /// <param name="message">
        /// 送信するネットワークメッセージです。
        /// </param>
        /// <param name="sendMode">
        /// メッセージの送信方式です。
        /// </param>
        void SendToPlayer(
            int playerId,
            NetMessage message,
            NetworkSendMode sendMode);

        event Action JoinedLobby;

        event Action JoinedRoom;

        event Action LeftRoom;

        event Action<OnlinePlayer> PlayerJoined;

        event Action<OnlinePlayer> PlayerLeft;

        event Action<
    OnlinePlayer,
    Dictionary<string, object>>
    PlayerPropertiesChanged;

        event Action<Dictionary<string, object>>
    RoomPropertiesChanged;

        event Action<OnlinePlayer> HostChanged;

        event Action<string> PartyRoomCreationFailed;

        event Action<string> PartyRoomJoinFailed;
    }
}
