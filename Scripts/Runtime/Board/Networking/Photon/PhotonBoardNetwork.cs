using Photon.Pun;
using UnityEngine;

namespace PranaTactics.Board.Networking.Photon
{
    /// <summary>
    /// Photon PUNのRPCを使用して、参加中の全クライアントで
    /// ボード生成処理を同期します。
    /// </summary>
    /// <remarks>
    /// ボードオブジェクト自体をネットワーク生成するのではなく、
    /// 各クライアントの<see cref="BoardManager"/>が同じボードを
    /// ローカル環境に生成します。
    /// </remarks>
    [RequireComponent(
    typeof(BoardManager),
    typeof(PhotonView))]

    public sealed class PhotonBoardNetwork :
    MonoBehaviourPunCallbacks,
    IBoardNetwork
    {
        /// <summary>
        /// ローカル環境におけるボード生成処理を管理します。
        /// </summary>
        private BoardManager _boardManager;

        /// <summary>
        /// 同じGameObjectにアタッチされている
        /// <see cref="BoardManager"/>を取得します。
        /// </summary>
        private void Awake()
        {
            if (TryGetComponent(out BoardManager boardManager))
            {
                _boardManager = boardManager;

                return;
            }

            Debug.LogError(
                $"{nameof(PhotonBoardNetwork)}と同じGameObjectに" +
                $"{nameof(BoardManager)}が存在しません。",
                this);
        }

        /// <summary>
        /// Photonルームに参加しているすべてのクライアントへ
        /// ボード生成RPCを送信します。
        /// </summary>
        public void CreateBoard()
        {
            if (_boardManager == null)
            {
                Debug.LogError(
                    $"{nameof(BoardManager)}を取得できないため、" +
                    "ボード生成RPCを送信できません。",
                    this);

                return;
            }

            if (!PhotonNetwork.IsConnectedAndReady)
            {
                Debug.LogWarning(
                    "Photonへ接続されていないため、" +
                    "ボード生成RPCを送信できません。",
                    this);

                return;
            }

            if (!PhotonNetwork.InRoom)
            {
                Debug.LogWarning(
                    "Photonルームに参加していないため、" +
                    "ボード生成RPCを送信できません。",
                    this);

                return;
            }

            photonView.RPC(
                nameof(RpcCreateBoard),
                RpcTarget.All);
        }

        /// <summary>
        /// このクライアントの<see cref="BoardManager"/>を使用して
        /// ボードをローカルに生成します。
        /// </summary>
        [PunRPC]
        private void RpcCreateBoard()
        {
            if (_boardManager == null)
            {
                Debug.LogError(
                    $"{nameof(BoardManager)}を取得できないため、" +
                    "ボードを生成できません。",
                    this);

                return;
            }

            _boardManager.CreateBoard();
        }
    }
}
