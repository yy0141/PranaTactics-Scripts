using System;
using System.Collections.Generic;

namespace PranaTactics.Networking.EOS
{
    /// <summary>
    /// 非同期の属性書き込みを一件ずつ実行し、ルーム変更時に古い完了を無効化します。
    /// </summary>
    public sealed class SerialLobbyOperationQueue
    {
        private readonly Queue<Action<Action>> _operations = new();

        private bool _isRunning;

        private int _generation;

        /// <summary>
        /// 完了通知を受け取る操作を登録します。
        /// </summary>
        public void Enqueue(Action<Action> operation)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            _operations.Enqueue(operation);
            StartNext();
        }

        /// <summary>
        /// 保留中の操作を破棄し、実行中の古い完了通知を無効化します。
        /// </summary>
        public void Clear()
        {
            _generation++;
            _operations.Clear();
            _isRunning = false;
        }

        /// <summary>
        /// 実行中の操作がない場合だけ次の書き込みを開始します。
        /// </summary>
        private void StartNext()
        {
            if (_isRunning || _operations.Count == 0)
            {
                return;
            }

            _isRunning = true;
            int generation = _generation;
            bool completed = false;
            Action<Action> operation = _operations.Dequeue();
            Action complete = () =>
            {
                if (completed || generation != _generation)
                {
                    return;
                }

                completed = true;
                _isRunning = false;
                StartNext();
            };
            try
            {
                operation(complete);
            }
            catch
            {
                complete();
                throw;
            }
        }
    }
}
