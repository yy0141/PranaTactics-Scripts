using UnityEngine;

namespace PranaTactics.Board.Networking.Local
{
    /// <summary>
    /// ネットワーク通信を使用せず、ローカル環境でボードを生成します。
    /// </summary>
    /// <remarks>
    /// このコンポーネントと同じGameObjectにアタッチされている
    /// <see cref="BoardManager"/>を使用してボードを生成します。
    /// </remarks>
    [RequireComponent(typeof(BoardManager))]
    public sealed class LocalBoardNetwork : MonoBehaviour, IBoardNetwork
    {
        /// <summary>
        /// ローカル環境でボードの生成を管理するBoardManagerです。
        /// </summary>
        private BoardManager _boardManager;

        /// <summary>
        /// 同じGameObjectにアタッチされているBoardManagerを取得します。
        /// </summary>
        private void Awake()
        {
            if (TryGetComponent(out BoardManager boardManager))
            {
                _boardManager = boardManager;

                return;
            }

            Debug.LogError(
                $"{nameof(LocalBoardNetwork)}と同じGameObjectに" +
                $"{nameof(BoardManager)}が存在しません。",
                this);
        }

        /// <summary>
        /// BoardManagerを使用してローカル環境にボードを生成します。
        /// </summary>
        public void CreateBoard()
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
