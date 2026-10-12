using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
using Epic.OnlineServices.Platform;

using PlayEveryWare.EpicOnlineServices;

using PranaTactics.Pranam.Networking.EOS;
using PranaTactics.Unit.Networking.EOS;

namespace PranaTactics.Networking.EOS
{
    /// <summary>
    /// Epic Online ServicesのP2P Interfaceを使用して、
    /// ネットワークメッセージの送受信を管理します。
    /// </summary>
    /// <remarks>
    /// <see cref="OnlinePlayer"/>に保存されたProduct User IDを使用して、
    /// ゲーム内のプレイヤーIDとEOSのProduct User IDを対応付けます。
    /// </remarks>
    public sealed class EosP2PService
    {
        /// <summary>
        /// 汎用ネットワークメッセージを表すパケット種別です。
        /// </summary>
        private const string k_GenericPacketKind = "net";

        /// <summary>
        /// ユニット同期メッセージを表すパケット種別です。
        /// </summary>
        private const string k_UnitPacketKind = "unit";

        /// <summary>
        /// プラーナム同期メッセージを表すパケット種別です。
        /// </summary>
        private const string k_TriggerPacketKind = "trigger";

        /// <summary>
        /// <see cref="OnlinePlayer.Properties"/>へ
        /// Product User IDを保存するときに使用するキーです。
        /// </summary>
        private const string k_ProductUserIdPropertyKey =
            "productUserId";

        /// <summary>
        /// EOS設定が存在しない場合に使用する既定のSocket名です。
        /// </summary>
        private const string k_DefaultSocketName =
            "prana_tactics";

        /// <summary>
        /// EOS設定が存在しない場合に使用する既定のチャンネル番号です。
        /// </summary>
        private const byte k_DefaultChannel = 0;

        /// <summary>
        /// EOS設定が存在しない場合に使用する
        /// 既定の最大パケットサイズです。
        /// </summary>
        private const uint k_DefaultMaximumPacketSize = 1170;

        /// <summary>
        /// 1フレームで処理する受信パケットの最大数です。
        /// </summary>
        private const int k_MaximumPacketsPerPoll = 128;

        /// <summary>
        /// プレイヤーIDを解決できなかったことを示す値です。
        /// </summary>
        private const int k_InvalidPlayerId = -1;

        /// <summary>
        /// EOS P2P通信の設定です。
        /// </summary>
        private EosConfig _eosConfig;

        /// <summary>
        /// EOS認証サービスです。
        /// </summary>
        private EosAuthService _authService;

        /// <summary>
        /// EOSのP2P Interfaceです。
        /// </summary>
        private P2PInterface _p2pInterface;

        /// <summary>
        /// ゲーム内プレイヤーIDからEOS Product User IDへの
        /// 対応関係を保持します。
        /// </summary>
        private readonly Dictionary<int, ProductUserId>
            _playerIdToProductUserId = new();

        /// <summary>
        /// EOS Product User IDからゲーム内プレイヤーIDへの
        /// 対応関係を保持します。
        /// </summary>
        private readonly Dictionary<string, int>
            _productUserIdToPlayerId =
                new(StringComparer.Ordinal);

        /// <summary>
        /// P2P接続を承認済みのProduct User IDを保持します。
        /// </summary>
        private readonly HashSet<string>
            _acceptedPeerProductUserIds =
                new(StringComparer.Ordinal);

        public bool IsInitialized
        {
            get
            {
                return _p2pInterface != null &&
                    _authService != null &&
                    _authService.IsLoggedIn &&
                    _authService.LocalProductUserId != null;
            }
        }

        public event Action<NetMessage> MessageReceived;

        public event Action<EosUnitP2PMessage>
    UnitMessageReceived;

        public event Action<EosPranamP2PMessage>
    PranamMessageReceived;

        /// <summary>
        /// EOS P2P Interfaceを取得し、
        /// P2Pサービスを初期化します。
        /// </summary>
        /// <param name="eosConfig">
        /// EOS P2P通信に使用する設定です。
        /// </param>
        /// <param name="authService">
        /// ログイン済みのEOS認証サービスです。
        /// </param>
        public void Initialize(
            EosConfig eosConfig,
            EosAuthService authService)
        {
            if (eosConfig == null)
            {
                Debug.LogError(
                    $"{nameof(EosP2PService)}: " +
                    $"{nameof(EosConfig)}が指定されていません。");

                return;
            }

            if (authService == null)
            {
                Debug.LogError(
                    $"{nameof(EosP2PService)}: " +
                    $"{nameof(EosAuthService)}が指定されていません。");

                return;
            }

            if (!authService.IsLoggedIn ||
                authService.LocalProductUserId == null)
            {
                Debug.LogError(
                    $"{nameof(EosP2PService)}: " +
                    "EOSへのログインが完了していないため、" +
                    "P2Pサービスを初期化できません。");

                return;
            }

            if (IsInitialized &&
                ReferenceEquals(_eosConfig, eosConfig) &&
                ReferenceEquals(_authService, authService))
            {
                return;
            }

            ClearPeerMappings();

            _eosConfig = eosConfig;
            _authService = authService;
            _p2pInterface = null;

            EOSManager.EOSSingleton eosManager =
                EOSManager.Instance;

            if (eosManager == null)
            {
                Debug.LogError(
                    $"{nameof(EosP2PService)}: " +
                    $"{nameof(EOSManager)}が存在しません。");

                return;
            }

            PlatformInterface platformInterface =
                eosManager.GetEOSPlatformInterface();

            if (platformInterface == null)
            {
                Debug.LogError(
                    $"{nameof(EosP2PService)}: " +
                    "EOS Platform Interfaceを取得できません。");

                return;
            }

            _p2pInterface =
                platformInterface.GetP2PInterface();

            if (_p2pInterface == null)
            {
                Debug.LogError(
                    $"{nameof(EosP2PService)}: " +
                    "EOS P2P Interfaceを取得できません。");

                return;
            }

            Debug.Log(
                $"{nameof(EosP2PService)}: " +
                "EOS P2Pサービスを初期化しました。" +
                $"Socket={GetSocketName()}, " +
                $"Channel={GetChannel()}, " +
                $"MaximumPacketSize={GetMaximumPacketSize()}");
        }

        /// <summary>
        /// Lobbyのプレイヤー一覧から、
        /// ゲーム内プレイヤーIDとProduct User IDの対応表を更新します。
        /// </summary>
        /// <param name="players">
        /// 現在のLobbyへ参加しているプレイヤー一覧です。
        /// </param>
        public void SyncPeers(
            IReadOnlyList<OnlinePlayer> players)
        {
            if (!IsInitialized ||
                players == null)
            {
                return;
            }

            Dictionary<int, ProductUserId>
                nextPlayerIdToProductUserId = new();

            Dictionary<string, int>
                nextProductUserIdToPlayerId =
                    new(StringComparer.Ordinal);

            HashSet<string>
                activeRemoteProductUserIds =
                    new(StringComparer.Ordinal);

            foreach (OnlinePlayer player in players)
            {
                if (player == null ||
                    player.PlayerId < 0)
                {
                    continue;
                }

                if (!player.Properties.TryGetValue(
                        k_ProductUserIdPropertyKey,
                        out object rawProductUserId))
                {
                    continue;
                }

                string productUserIdString =
                    rawProductUserId?.ToString();

                if (string.IsNullOrWhiteSpace(
                        productUserIdString))
                {
                    continue;
                }

                ProductUserId productUserId =
                    ProductUserId.FromString(
                        productUserIdString);

                if (productUserId == null)
                {
                    Debug.LogWarning(
                        $"{nameof(EosP2PService)}: " +
                        "無効なProduct User IDが指定されています。" +
                        $"PlayerId={player.PlayerId}, " +
                        $"ProductUserId={productUserIdString}");

                    continue;
                }

                if (nextPlayerIdToProductUserId.TryGetValue(
                        player.PlayerId,
                        out ProductUserId existingProductUserId) &&
                    existingProductUserId != null &&
                    !string.Equals(
                        existingProductUserId.ToString(),
                        productUserIdString,
                        StringComparison.Ordinal))
                {
                    Debug.LogWarning(
                        $"{nameof(EosP2PService)}: " +
                        "同じプレイヤーIDへ複数のProduct User IDが" +
                        "割り当てられています。" +
                        $"PlayerId={player.PlayerId}");

                    continue;
                }

                if (nextProductUserIdToPlayerId.TryGetValue(
                        productUserIdString,
                        out int existingPlayerId) &&
                    existingPlayerId != player.PlayerId)
                {
                    Debug.LogWarning(
                        $"{nameof(EosP2PService)}: " +
                        "同じProduct User IDへ複数のプレイヤーIDが" +
                        "割り当てられています。" +
                        $"ProductUserId={productUserIdString}, " +
                        $"CurrentPlayerId={existingPlayerId}, " +
                        $"RequestedPlayerId={player.PlayerId}");

                    continue;
                }

                nextPlayerIdToProductUserId[player.PlayerId] =
                    productUserId;

                nextProductUserIdToPlayerId[productUserIdString] =
                    player.PlayerId;

                bool hasChanged =
                    !_playerIdToProductUserId.TryGetValue(
                        player.PlayerId,
                        out ProductUserId previousProductUserId) ||
                    previousProductUserId == null ||
                    !string.Equals(
                        previousProductUserId.ToString(),
                        productUserIdString,
                        StringComparison.Ordinal);

                if (hasChanged)
                {
                    Debug.Log(
                        $"{nameof(EosP2PService)}: " +
                        "P2Pピアを登録しました。" +
                        $"PlayerId={player.PlayerId}, " +
                        $"ProductUserId={productUserIdString}");
                }

                if (IsLocalProductUserId(
                        productUserIdString))
                {
                    continue;
                }

                activeRemoteProductUserIds.Add(
                    productUserIdString);

                TryAcceptPeer(productUserId);
            }

            LogRemovedPeers(
                nextPlayerIdToProductUserId);

            _playerIdToProductUserId.Clear();

            foreach (
                KeyValuePair<int, ProductUserId> pair
                in nextPlayerIdToProductUserId)
            {
                _playerIdToProductUserId[pair.Key] =
                    pair.Value;
            }

            _productUserIdToPlayerId.Clear();

            foreach (
                KeyValuePair<string, int> pair
                in nextProductUserIdToPlayerId)
            {
                _productUserIdToPlayerId[pair.Key] =
                    pair.Value;
            }

            _acceptedPeerProductUserIds.IntersectWith(
                activeRemoteProductUserIds);
        }

        /// <summary>
        /// 指定されたプレイヤーへ汎用ネットワークメッセージを送信します。
        /// </summary>
        /// <param name="targetPlayerId">
        /// 送信先となるプレイヤーIDです。
        /// </param>
        /// <param name="message">
        /// 送信するネットワークメッセージです。
        /// </param>
        /// <param name="sendMode">
        /// パケットの送信方式です。
        /// </param>
        public void Send(
            int targetPlayerId,
            NetMessage message,
            NetworkSendMode sendMode)
        {
            if (message == null)
            {
                return;
            }

            SendEnvelope(
                targetPlayerId,
                k_GenericPacketKind,
                JsonUtility.ToJson(message),
                sendMode);
        }

        /// <summary>
        /// 指定されたプレイヤーへ
        /// ユニット用P2Pメッセージを送信します。
        /// </summary>
        /// <param name="targetPlayerId">
        /// 送信先となるプレイヤーIDです。
        /// </param>
        /// <param name="message">
        /// 送信するユニット用メッセージです。
        /// </param>
        /// <param name="sendMode">
        /// パケットの送信方式です。
        /// </param>
        public void SendUnitMessage(
            int targetPlayerId,
            EosUnitP2PMessage message,
            NetworkSendMode sendMode =
                NetworkSendMode.Reliable)
        {
            if (message == null)
            {
                return;
            }

            SendEnvelope(
                targetPlayerId,
                k_UnitPacketKind,
                JsonUtility.ToJson(message),
                sendMode);
        }

        /// <summary>
        /// 指定されたプレイヤーへ
        /// プラーナム用P2Pメッセージを送信します。
        /// </summary>
        /// <param name="targetPlayerId">
        /// 送信先となるプレイヤーIDです。
        /// </param>
        /// <param name="message">
        /// 送信するプラーナム用メッセージです。
        /// </param>
        /// <param name="sendMode">
        /// パケットの送信方式です。
        /// </param>
        public void SendTriggerMessage(
            int targetPlayerId,
            EosPranamP2PMessage message,
            NetworkSendMode sendMode =
                NetworkSendMode.Reliable)
        {
            if (message == null)
            {
                return;
            }

            SendEnvelope(
                targetPlayerId,
                k_TriggerPacketKind,
                JsonUtility.ToJson(message),
                sendMode);
        }

        /// <summary>
        /// 受信待ちのEOS P2Pパケットを取得し、
        /// 最大件数まで順番に処理します。
        /// </summary>
        public void Poll()
        {
            if (!IsInitialized)
            {
                return;
            }

            int processedPacketCount = 0;

            while (processedPacketCount <
                k_MaximumPacketsPerPoll)
            {
                if (!TryGetNextPacketSize(
                        out uint packetSizeBytes))
                {
                    break;
                }

                processedPacketCount++;

                if (!TryReceivePacket(
                        packetSizeBytes,
                        out ProductUserId remoteUserId,
                        out byte[] packetData))
                {
                    break;
                }

                if (packetData.Length == 0)
                {
                    continue;
                }

                HandlePacket(
                    remoteUserId,
                    packetData);
            }

            if (processedPacketCount >=
                k_MaximumPacketsPerPoll)
            {
                Debug.LogWarning(
                    $"{nameof(EosP2PService)}: " +
                    "1フレームのP2P受信上限へ到達しました。" +
                    $"Limit={k_MaximumPacketsPerPoll}");
            }
        }

        /// <summary>
        /// EOS P2Pサービスを停止し、
        /// プレイヤー対応表を破棄します。
        /// </summary>
        public void Shutdown()
        {
            ClearPeerMappings();

            _p2pInterface = null;
            _authService = null;
            _eosConfig = null;
        }

        /// <summary>
        /// 共通エンベロープへ格納したメッセージを
        /// 指定されたプレイヤーへ送信します。
        /// </summary>
        /// <param name="targetPlayerId">
        /// 送信先となるプレイヤーIDです。
        /// </param>
        /// <param name="packetKind">
        /// 送信するパケットの種別です。
        /// </param>
        /// <param name="payload">
        /// 送信するメッセージのJSON文字列です。
        /// </param>
        /// <param name="sendMode">
        /// パケットの送信方式です。
        /// </param>
        private void SendEnvelope(
            int targetPlayerId,
            string packetKind,
            string payload,
            NetworkSendMode sendMode)
        {
            if (!IsInitialized)
            {
                Debug.LogWarning(
                    $"{nameof(EosP2PService)}: " +
                    "P2Pサービスが初期化されていないため、" +
                    "メッセージを送信できません。");

                return;
            }

            if (string.IsNullOrWhiteSpace(packetKind))
            {
                Debug.LogWarning(
                    $"{nameof(EosP2PService)}: " +
                    "パケット種別が指定されていません。");

                return;
            }

            if (!_playerIdToProductUserId.TryGetValue(
                    targetPlayerId,
                    out ProductUserId targetUserId) ||
                targetUserId == null)
            {
                Debug.LogWarning(
                    $"{nameof(EosP2PService)}: " +
                    "送信先プレイヤーがP2Pピアとして登録されていません。" +
                    $"PlayerId={targetPlayerId}, " +
                    $"PacketKind={packetKind}");

                return;
            }

            string targetProductUserId =
                targetUserId.ToString();

            if (IsLocalProductUserId(
                    targetProductUserId))
            {
                return;
            }

            EosP2PPacketEnvelope envelope =
                new EosP2PPacketEnvelope
                {
                    Kind = packetKind,
                    Payload = payload ?? string.Empty
                };

            string envelopeJson =
                JsonUtility.ToJson(envelope);

            byte[] packetData =
                Encoding.UTF8.GetBytes(envelopeJson);

            uint maximumPacketSize =
                GetMaximumPacketSize();

            if ((uint)packetData.Length >
                maximumPacketSize)
            {
                Debug.LogWarning(
                    $"{nameof(EosP2PService)}: " +
                    "送信するP2Pパケットが最大サイズを超えています。" +
                    $"Bytes={packetData.Length}, " +
                    $"Maximum={maximumPacketSize}, " +
                    $"PacketKind={packetKind}");

                return;
            }

            TryAcceptPeer(targetUserId);

            SendPacketOptions options =
                new SendPacketOptions
                {
                    LocalUserId =
                        _authService.LocalProductUserId,
                    RemoteUserId =
                        targetUserId,
                    SocketId =
                        BuildSocketId(),
                    Channel =
                        GetChannel(),
                    Data =
                        new ArraySegment<byte>(packetData),
                    AllowDelayedDelivery =
                        true,
                    Reliability =
                        ConvertPacketReliability(sendMode)
                };

            Result result =
                _p2pInterface.SendPacket(ref options);

            if (result != Result.Success)
            {
                Debug.LogWarning(
                    $"{nameof(EosP2PService)}: " +
                    "P2Pパケットの送信に失敗しました。" +
                    $"Result={result}, " +
                    $"TargetPlayerId={targetPlayerId}, " +
                    $"PacketKind={packetKind}");
            }
        }

        /// <summary>
        /// 次に受信するP2Pパケットのサイズを取得します。
        /// </summary>
        /// <param name="packetSizeBytes">
        /// 次に受信するパケットのバイト数です。
        /// </param>
        /// <returns>
        /// 受信可能なパケットが存在する場合は<c>true</c>です。
        /// </returns>
        private bool TryGetNextPacketSize(
            out uint packetSizeBytes)
        {
            packetSizeBytes = 0;

            GetNextReceivedPacketSizeOptions options =
                new GetNextReceivedPacketSizeOptions
                {
                    LocalUserId =
                        _authService.LocalProductUserId,
                    RequestedChannel =
                        GetChannel()
                };

            Result result =
                _p2pInterface.GetNextReceivedPacketSize(
                    ref options,
                    out packetSizeBytes);

            if (result == Result.NotFound ||
                packetSizeBytes == 0)
            {
                return false;
            }

            if (result != Result.Success)
            {
                Debug.LogWarning(
                    $"{nameof(EosP2PService)}: " +
                    "次のP2Pパケットサイズを取得できませんでした。" +
                    $"Result={result}");

                return false;
            }

            return true;
        }

        /// <summary>
        /// 指定されたサイズのP2Pパケットを受信します。
        /// </summary>
        /// <param name="packetSizeBytes">
        /// 受信するパケットのバイト数です。
        /// </param>
        /// <param name="remoteUserId">
        /// パケットを送信したProduct User IDです。
        /// </param>
        /// <param name="packetData">
        /// 受信したパケットデータです。
        /// </param>
        /// <returns>
        /// パケットを正常に受信できた場合は<c>true</c>です。
        /// </returns>
        private bool TryReceivePacket(
            uint packetSizeBytes,
            out ProductUserId remoteUserId,
            out byte[] packetData)
        {
            remoteUserId = null;
            packetData = Array.Empty<byte>();

            int bufferLength;

            try
            {
                bufferLength =
                    checked((int)packetSizeBytes);
            }
            catch (OverflowException)
            {
                Debug.LogWarning(
                    $"{nameof(EosP2PService)}: " +
                    "受信パケットサイズを管理配列へ変換できません。" +
                    $"PacketSize={packetSizeBytes}");

                return false;
            }

            if (bufferLength <= 0)
            {
                return false;
            }

            byte[] receiveBuffer =
                new byte[bufferLength];

            ArraySegment<byte> receiveSegment =
                new ArraySegment<byte>(receiveBuffer);

            ReceivePacketOptions options =
                new ReceivePacketOptions
                {
                    LocalUserId =
                        _authService.LocalProductUserId,
                    MaxDataSizeBytes =
                        packetSizeBytes,
                    RequestedChannel =
                        GetChannel()
                };

            SocketId socketId = default;

            Result result =
                _p2pInterface.ReceivePacket(
                    ref options,
                    ref remoteUserId,
                    ref socketId,
                    out byte receivedChannel,
                    receiveSegment,
                    out uint bytesWritten);

            if (result != Result.Success)
            {
                Debug.LogWarning(
                    $"{nameof(EosP2PService)}: " +
                    "P2Pパケットの受信に失敗しました。" +
                    $"Result={result}");

                return false;
            }

            if (receivedChannel != GetChannel())
            {
                Debug.LogWarning(
                    $"{nameof(EosP2PService)}: " +
                    "想定していないチャンネルのパケットを受信しました。" +
                    $"Expected={GetChannel()}, " +
                    $"Actual={receivedChannel}");
            }

            if (bytesWritten == 0)
            {
                return true;
            }

            if (bytesWritten >
                (uint)receiveBuffer.Length)
            {
                Debug.LogWarning(
                    $"{nameof(EosP2PService)}: " +
                    "受信バイト数がバッファサイズを超えています。" +
                    $"BytesWritten={bytesWritten}, " +
                    $"BufferLength={receiveBuffer.Length}");

                return false;
            }

            int writtenLength =
                checked((int)bytesWritten);

            if (writtenLength ==
                receiveBuffer.Length)
            {
                packetData = receiveBuffer;

                return true;
            }

            packetData =
                new byte[writtenLength];

            Buffer.BlockCopy(
                receiveBuffer,
                0,
                packetData,
                0,
                writtenLength);

            return true;
        }

        /// <summary>
        /// 受信したパケットをデシリアライズし、
        /// パケット種別に対応するイベントへ通知します。
        /// </summary>
        /// <param name="remoteUserId">
        /// パケットを送信したProduct User IDです。
        /// </param>
        /// <param name="packetData">
        /// 受信したUTF-8形式のJSONデータです。
        /// </param>
        private void HandlePacket(
            ProductUserId remoteUserId,
            byte[] packetData)
        {
            if (packetData == null ||
                packetData.Length == 0)
            {
                return;
            }

            string envelopeJson;

            try
            {
                envelopeJson =
                    Encoding.UTF8.GetString(packetData);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"{nameof(EosP2PService)}: " +
                    "受信データをUTF-8文字列へ変換できませんでした。" +
                    $"Error={exception.Message}");

                return;
            }

            if (!TryDeserializeJson(
                    envelopeJson,
                    k_GenericPacketKind,
                    out EosP2PPacketEnvelope envelope))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(
                    envelope.Kind))
            {
                Debug.LogWarning(
                    $"{nameof(EosP2PService)}: " +
                    "パケット種別が設定されていないため破棄します。");

                return;
            }

            int actualSenderPlayerId =
                ResolvePlayerId(remoteUserId);

            switch (envelope.Kind)
            {
                case k_UnitPacketKind:
                    HandleUnitPacket(
                        remoteUserId,
                        actualSenderPlayerId,
                        envelope.Payload);
                    break;

                case k_TriggerPacketKind:
                    HandleTriggerPacket(
                        remoteUserId,
                        actualSenderPlayerId,
                        envelope.Payload);
                    break;

                case k_GenericPacketKind:
                    HandleGenericPacket(
                        actualSenderPlayerId,
                        envelope.Payload);
                    break;

                default:
                    Debug.LogWarning(
                        $"{nameof(EosP2PService)}: " +
                        "未対応のパケット種別を受信しました。" +
                        $"PacketKind={envelope.Kind}");
                    break;
            }
        }

        /// <summary>
        /// ユニット用P2Pパケットを処理します。
        /// </summary>
        /// <param name="remoteUserId">
        /// EOSから取得した送信元Product User IDです。
        /// </param>
        /// <param name="actualSenderPlayerId">
        /// Product User IDから解決したプレイヤーIDです。
        /// </param>
        /// <param name="payload">
        /// ユニットメッセージのJSON文字列です。
        /// </param>
        private void HandleUnitPacket(
            ProductUserId remoteUserId,
            int actualSenderPlayerId,
            string payload)
        {
            if (!TryDeserializeJson(
                    payload,
                    k_UnitPacketKind,
                    out EosUnitP2PMessage message))
            {
                return;
            }

            int payloadSenderPlayerId =
                message.SenderPlayerId;

            if (actualSenderPlayerId !=
                k_InvalidPlayerId)
            {
                message.SenderPlayerId =
                    actualSenderPlayerId;
            }
            else
            {
                Debug.LogWarning(
                    $"{nameof(EosP2PService)}: " +
                    "ユニットメッセージの送信元プレイヤーIDを" +
                    "Product User IDから解決できませんでした。" +
                    $"RemoteProductUserId={remoteUserId}, " +
                    $"PayloadSenderPlayerId={payloadSenderPlayerId}");
            }

            if (message.MessageType != EosUnitP2PMessageTypes.MicroMovement)
            {
                Debug.Log(
                    $"{nameof(EosP2PService)}: " +
                    "ユニット用P2Pメッセージを受信しました。" +
                    $"RemoteProductUserId={remoteUserId}, " +
                    $"ResolvedSender={actualSenderPlayerId}, " +
                    $"MessageSender={message.SenderPlayerId}, " +
                    $"Owner={message.OwnerId}, " +
                    $"Type={message.MessageType}, " +
                    $"Sequence={message.Sequence}");
            }

            UnitMessageReceived?.Invoke(message);
        }

        /// <summary>
        /// プラーナム用P2Pパケットを処理します。
        /// </summary>
        /// <param name="remoteUserId">
        /// EOSから取得した送信元Product User IDです。
        /// </param>
        /// <param name="actualSenderPlayerId">
        /// Product User IDから解決したプレイヤーIDです。
        /// </param>
        /// <param name="payload">
        /// プラーナムメッセージのJSON文字列です。
        /// </param>
        private void HandleTriggerPacket(
            ProductUserId remoteUserId,
            int actualSenderPlayerId,
            string payload)
        {
            if (!TryDeserializeJson(
                    payload,
                    k_TriggerPacketKind,
                    out EosPranamP2PMessage message))
            {
                return;
            }

            int payloadSenderPlayerId =
                message.SenderPlayerId;

            if (actualSenderPlayerId !=
                k_InvalidPlayerId)
            {
                message.SenderPlayerId =
                    actualSenderPlayerId;
            }
            else
            {
                Debug.LogWarning(
                    $"{nameof(EosP2PService)}: " +
                    "プラーナムメッセージの送信元プレイヤーIDを" +
                    "Product User IDから解決できませんでした。" +
                    $"RemoteProductUserId={remoteUserId}, " +
                    $"PayloadSenderPlayerId={payloadSenderPlayerId}");
            }

            PranamMessageReceived?.Invoke(message);
        }

        /// <summary>
        /// 汎用ネットワークパケットを処理します。
        /// </summary>
        /// <param name="actualSenderPlayerId">
        /// 接続先のユーザーIDから検証した送信元プレイヤーIDです。
        /// </param>
        /// <param name="payload">
        /// 汎用ネットワークメッセージのJSON文字列です。
        /// </param>
        private void HandleGenericPacket(int actualSenderPlayerId, string payload)
        {
            if (actualSenderPlayerId < 0 || !TryDeserializeJson(
                    payload,
                    k_GenericPacketKind,
                    out NetMessage message))
            {
                return;
            }

            message.SenderPlayerId = actualSenderPlayerId;
            MessageReceived?.Invoke(message);
        }

        /// <summary>
        /// JSON文字列を指定されたメッセージ型へ変換します。
        /// </summary>
        /// <typeparam name="TMessage">
        /// 変換するメッセージの型です。
        /// </typeparam>
        /// <param name="json">
        /// 変換するJSON文字列です。
        /// </param>
        /// <param name="packetKind">
        /// エラーログへ出力するパケット種別です。
        /// </param>
        /// <param name="message">
        /// 変換されたメッセージです。
        /// </param>
        /// <returns>
        /// 正常に変換できた場合は<c>true</c>です。
        /// </returns>
        private static bool TryDeserializeJson<TMessage>(
            string json,
            string packetKind,
            out TMessage message)
            where TMessage : class
        {
            message = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning(
                    $"{nameof(EosP2PService)}: " +
                    "受信したJSONが空です。" +
                    $"PacketKind={packetKind}");

                return false;
            }

            try
            {
                message =
                    JsonUtility.FromJson<TMessage>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"{nameof(EosP2PService)}: " +
                    "P2PメッセージのJSON解析に失敗しました。" +
                    $"PacketKind={packetKind}, " +
                    $"Error={exception.Message}");

                return false;
            }

            if (message == null)
            {
                Debug.LogWarning(
                    $"{nameof(EosP2PService)}: " +
                    "P2Pメッセージをデシリアライズできませんでした。" +
                    $"PacketKind={packetKind}");

                return false;
            }

            return true;
        }

        /// <summary>
        /// Product User IDに対応するゲーム内プレイヤーIDを取得します。
        /// </summary>
        /// <param name="productUserId">
        /// 解決するEOS Product User IDです。
        /// </param>
        /// <returns>
        /// 対応するプレイヤーIDです。
        /// 未登録の場合は<c>-1</c>です。
        /// </returns>
        private int ResolvePlayerId(
            ProductUserId productUserId)
        {
            if (productUserId == null)
            {
                return k_InvalidPlayerId;
            }

            string productUserIdString =
                productUserId.ToString();

            if (_productUserIdToPlayerId.TryGetValue(
                    productUserIdString,
                    out int playerId))
            {
                return playerId;
            }

            return k_InvalidPlayerId;
        }

        /// <summary>
        /// 指定されたProduct User IDとのP2P接続を承認します。
        /// </summary>
        /// <param name="remoteUserId">
        /// 接続を承認するリモートユーザーです。
        /// </param>
        private void TryAcceptPeer(
            ProductUserId remoteUserId)
        {
            if (!IsInitialized ||
                remoteUserId == null)
            {
                return;
            }

            string remoteProductUserId =
                remoteUserId.ToString();

            if (IsLocalProductUserId(
                    remoteProductUserId) ||
                _acceptedPeerProductUserIds.Contains(
                    remoteProductUserId))
            {
                return;
            }

            AcceptConnectionOptions options =
                new AcceptConnectionOptions
                {
                    LocalUserId =
                        _authService.LocalProductUserId,
                    RemoteUserId =
                        remoteUserId,
                    SocketId =
                        BuildSocketId()
                };

            Result result =
                _p2pInterface.AcceptConnection(
                    ref options);

            if (result == Result.Success)
            {
                _acceptedPeerProductUserIds.Add(
                    remoteProductUserId);

                Debug.Log(
                    $"{nameof(EosP2PService)}: " +
                    "P2P接続を承認しました。" +
                    $"RemoteProductUserId={remoteProductUserId}");

                return;
            }

            Debug.LogWarning(
                $"{nameof(EosP2PService)}: " +
                "P2P接続の承認に失敗しました。" +
                $"Result={result}, " +
                $"RemoteProductUserId={remoteProductUserId}");
        }

        /// <summary>
        /// 現在のP2P Socket IDを生成します。
        /// </summary>
        /// <returns>
        /// EOS P2P通信に使用するSocket IDです。
        /// </returns>
        private SocketId BuildSocketId()
        {
            return new SocketId
            {
                SocketName = GetSocketName()
            };
        }

        /// <summary>
        /// 設定されているP2P Socket名を取得します。
        /// </summary>
        /// <returns>
        /// P2P通信に使用するSocket名です。
        /// </returns>
        private string GetSocketName()
        {
            if (_eosConfig != null &&
                !string.IsNullOrWhiteSpace(
                    _eosConfig.SocketName))
            {
                return _eosConfig.SocketName;
            }

            return k_DefaultSocketName;
        }

        /// <summary>
        /// 設定されているP2Pチャンネル番号を取得します。
        /// </summary>
        /// <returns>
        /// P2P通信に使用するチャンネル番号です。
        /// </returns>
        private byte GetChannel()
        {
            if (_eosConfig == null)
            {
                return k_DefaultChannel;
            }

            return _eosConfig.P2PChannel;
        }

        /// <summary>
        /// 設定されている最大パケットサイズを取得します。
        /// </summary>
        /// <returns>
        /// 送信可能な最大パケットサイズです。
        /// </returns>
        private uint GetMaximumPacketSize()
        {
            if (_eosConfig == null ||
                _eosConfig.MaximumPacketSize == 0)
            {
                return k_DefaultMaximumPacketSize;
            }

            return _eosConfig.MaximumPacketSize;
        }

        /// <summary>
        /// 送信モードをEOSのパケット信頼性へ変換します。
        /// </summary>
        /// <param name="sendMode">
        /// 共通ネットワーク送信モードです。
        /// </param>
        /// <returns>
        /// EOS P2Pで使用するパケット信頼性です。
        /// </returns>
        private static PacketReliability ConvertPacketReliability(
            NetworkSendMode sendMode)
        {
            switch (sendMode)
            {
                case NetworkSendMode.Reliable:
                    return PacketReliability.ReliableOrdered;

                case NetworkSendMode.Unreliable:
                    return PacketReliability.UnreliableUnordered;

                default:
                    Debug.LogWarning(
                        $"{nameof(EosP2PService)}: " +
                        "未定義の送信モードが指定されたため、" +
                        "ReliableOrderedを使用します。" +
                        $"SendMode={sendMode}");

                    return PacketReliability.ReliableOrdered;
            }
        }

        /// <summary>
        /// 指定されたProduct User IDが
        /// ローカルプレイヤーのIDかどうかを確認します。
        /// </summary>
        /// <param name="productUserId">
        /// 確認するProduct User ID文字列です。
        /// </param>
        /// <returns>
        /// ローカルプレイヤーのIDの場合は<c>true</c>です。
        /// </returns>
        private bool IsLocalProductUserId(
            string productUserId)
        {
            return _authService != null &&
                string.Equals(
                    productUserId,
                    _authService.LocalProductUserIdString,
                    StringComparison.Ordinal);
        }

        /// <summary>
        /// 前回の同期から削除されたP2Pピアをログへ出力します。
        /// </summary>
        /// <param name="nextPeerMappings">
        /// 次の同期で有効になるピア対応表です。
        /// </param>
        private void LogRemovedPeers(
            IReadOnlyDictionary<int, ProductUserId>
                nextPeerMappings)
        {
            foreach (
                KeyValuePair<int, ProductUserId> pair
                in _playerIdToProductUserId)
            {
                if (nextPeerMappings.ContainsKey(pair.Key))
                {
                    continue;
                }

                Debug.Log(
                    $"{nameof(EosP2PService)}: " +
                    "P2Pピアを登録解除しました。" +
                    $"PlayerId={pair.Key}, " +
                    $"ProductUserId={pair.Value}");
            }
        }

        /// <summary>
        /// P2Pピアの対応表と承認済み状態を初期化します。
        /// </summary>
        private void ClearPeerMappings()
        {
            _playerIdToProductUserId.Clear();
            _productUserIdToPlayerId.Clear();
            _acceptedPeerProductUserIds.Clear();
        }
    }
}
