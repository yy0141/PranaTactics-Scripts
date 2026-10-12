using Newtonsoft.Json;
using Photon.Pun;
using UnityEngine;

using PranaTactics.Networking;
using PranaTactics.Networking.EOS;
using PranaTactics.Unit;

namespace PranaTactics.Board.Networking
{
    /// <summary>
    /// ボードの罠メッセージをローカル、PhotonまたはEOSへ配送します。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoardTrapManager), typeof(PhotonView))]
    public sealed class BoardTrapNetwork : MonoBehaviourPun
    {
        /// <summary>
        /// メッセージを適用する罠管理です。
        /// </summary>
        private BoardTrapManager _traps;

        /// <summary>
        /// 現在購読しているEOSサービスです。
        /// </summary>
        private EosOnlineService _eos;

        public bool HasAuthority => NetworkServiceLocator.Online == null ||
            !NetworkServiceLocator.Online.IsInRoom || NetworkServiceLocator.Online.IsHost;

        /// <summary>
        /// ボードの罠管理を取得します。
        /// </summary>
        private void Awake()
        {
            _traps = GetComponent<BoardTrapManager>();
            photonView.RefreshRpcMonoBehaviourCache();
            BindService();
        }

        /// <summary>
        /// サービスの登録と切り替えに合わせて受信購読を更新します。
        /// </summary>
        private void Update()
        {
            BindService();
        }

        /// <summary>
        /// EOSの受信購読を解除します。
        /// </summary>
        private void OnDisable()
        {
            if (_eos != null)
            {
                _eos.MessageReceived -= ReceiveEos;
                _eos = null;
            }
        }

        /// <summary>
        /// EOSサービスを重複せず購読します。
        /// </summary>
        private void BindService()
        {
            EosOnlineService service = NetworkServiceLocator.Online as EosOnlineService;
            if (_eos == service)
            {
                return;
            }
            OnDisable();
            _eos = service;
            if (_eos != null)
            {
                _eos.MessageReceived += ReceiveEos;
            }
        }

        /// <summary>
        /// 設置要求を裁定側へ配送できた場合にtrueを返します。
        /// </summary>
        public bool RequestPlacement(BoardTrapMessage message)
        {
            BindService();
            IOnlineService online = NetworkServiceLocator.Online;
            if (HasAuthority)
            {
                _traps.HandlePlacementRequest(message, online != null ? online.LocalPlayerId : -1);
                return true;
            }
            string json = JsonConvert.SerializeObject(message);
            if (online.Backend == OnlineBackend.Photon && photonView.ViewID > 0)
            {
                photonView.RPC(nameof(ReceiveRequest), RpcTarget.MasterClient, json);
                return true;
            }
            if (_eos != null)
            {
                foreach (OnlinePlayer player in online.Players)
                {
                    if (_eos.IsHostPlayer(player.PlayerId))
                    {
                        SendEos(player.PlayerId, json);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 確定結果を先にローカルへ適用し、同じ順序で他の参加者へ配送します。
        /// </summary>
        internal void Broadcast(BoardTrapMessage message)
        {
            if (!HasAuthority)
            {
                return;
            }
            _traps.ApplyMessage(message);
            IOnlineService online = NetworkServiceLocator.Online;
            if (online == null || !online.IsInRoom)
            {
                return;
            }
            string json = JsonConvert.SerializeObject(message);
            if (online.Backend == OnlineBackend.Photon)
            {
                photonView.RPC(nameof(ReceiveResult), RpcTarget.Others, json);
            }
            else
            {
                BindService();
                foreach (OnlinePlayer player in online.Players)
                {
                    if (player.PlayerId != online.LocalPlayerId)
                    {
                        SendEos(player.PlayerId, json);
                    }
                }
            }
        }

        /// <summary>
        /// 送信者が所有者、またはAIを管理するホストか確認します。
        /// </summary>
        internal bool CanControlUnit(UnitBehavior unit, int senderId)
        {
            IOnlineService online = NetworkServiceLocator.Online;
            if (online == null || !online.IsInRoom)
            {
                return true;
            }
            bool isHost = online.Backend == OnlineBackend.Photon
                ? PhotonNetwork.MasterClient != null && PhotonNetwork.MasterClient.ActorNumber == senderId
                : _eos != null && _eos.IsHostPlayer(senderId);
            return senderId == unit.GetOwnerId() ||
                (isHost && UnitManager.Instance != null && UnitManager.Instance.IsAIControlledUnit(unit.PlayerId));
        }

        /// <summary>
        /// Photonからの設置要求を実際の送信者IDとともに裁定します。
        /// </summary>
        [PunRPC]
        private void ReceiveRequest(string json, PhotonMessageInfo info)
        {
            if (HasAuthority && info.Sender != null && TryDeserialize(json, out BoardTrapMessage message) &&
                message.Command == BoardTrapCommand.RequestPlacement)
            {
                _traps.HandlePlacementRequest(message, info.Sender.ActorNumber);
            }
        }

        /// <summary>
        /// Photonホストが送った確定結果だけを適用します。
        /// </summary>
        [PunRPC]
        private void ReceiveResult(string json, PhotonMessageInfo info)
        {
            if (info.Sender == PhotonNetwork.MasterClient && TryDeserialize(json, out BoardTrapMessage message))
            {
                _traps.ApplyMessage(message);
            }
        }

        /// <summary>
        /// EOSの認証済み送信元に基づいて要求と結果を振り分けます。
        /// </summary>
        private void ReceiveEos(NetMessage envelope)
        {
            if (envelope.Type != NetMessageType.BoardTrap || !TryDeserialize(envelope.Json, out BoardTrapMessage message))
            {
                return;
            }
            if (message.Command == BoardTrapCommand.RequestPlacement)
            {
                if (HasAuthority)
                {
                    _traps.HandlePlacementRequest(message, envelope.SenderPlayerId);
                }
            }
            else if (_eos != null && _eos.IsHostPlayer(envelope.SenderPlayerId))
            {
                _traps.ApplyMessage(message);
            }
        }

        /// <summary>
        /// 罠専用の汎用メッセージをEOSへ送信します。
        /// </summary>
        private void SendEos(int playerId, string json)
        {
            _eos?.SendToPlayer(playerId, new NetMessage
            {
                Type = NetMessageType.BoardTrap,
                SenderPlayerId = _eos.LocalPlayerId,
                TargetPlayerId = playerId,
                Json = json
            }, NetworkSendMode.Reliable);
        }

        /// <summary>
        /// 不正なJSONをゲームルールへ渡さず破棄します。
        /// </summary>
        private static bool TryDeserialize(string json, out BoardTrapMessage message)
        {
            message = null;
            try
            {
                message = JsonConvert.DeserializeObject<BoardTrapMessage>(json);
                return message != null;
            }
            catch (JsonException exception)
            {
                Debug.LogWarning($"罠メッセージを解析できません: {exception.Message}");
                return false;
            }
        }
    }
}
