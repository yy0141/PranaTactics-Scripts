using System;
using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.Board
{
    /// <summary>
    /// 罠効果や通信方式に依存せず、設置マス、敵の進入と一度だけの検知を管理します。
    /// </summary>
    public sealed class BoardTrapRegistry
    {
        /// <summary>
        /// 設置マスごとの罠です。
        /// </summary>
        private readonly Dictionary<Vector2Int, BoardTrapMessage> _traps = new();

        /// <summary>
        /// 最初の敵を検知済みの罠IDです。
        /// </summary>
        private readonly HashSet<string> _triggered = new();

        public IEnumerable<BoardTrapMessage> Traps => _traps.Values;

        /// <summary>
        /// 同じ設置マスへの重ね置きを防いで登録します。検知範囲同士の重なりは許可します。
        /// </summary>
        public bool TryPlace(BoardTrapMessage trap)
        {
            if (trap == null || string.IsNullOrWhiteSpace(trap.TrapId) || trap.OwnerId < 0 ||
                trap.OwnerTeamId < 0 || trap.Floor < 0 || trap.Range < 0 || trap.ActivationDelay < 0.0f ||
                float.IsNaN(trap.ActivationDelay) || float.IsInfinity(trap.ActivationDelay))
            {
                return false;
            }

            foreach (BoardTrapMessage existing in _traps.Values)
            {
                if (existing.TrapId == trap.TrapId)
                {
                    return false;
                }
            }

            return _traps.TryAdd(new Vector2Int(trap.X, trap.Y), trap);
        }

        /// <summary>
        /// 指定マスに設置済みの罠があるか確認します。
        /// </summary>
        public bool Contains(Vector2Int coordinate)
        {
            return _traps.ContainsKey(coordinate);
        }

        /// <summary>
        /// 敵の進入で検知した罠を返し、後続の進入では二重検知しないよう確保します。
        /// </summary>
        public List<BoardTrapMessage> CaptureEnteringEnemy(int playerId, int teamId, Vector2Int coordinate, int floor)
        {
            var captured = new List<BoardTrapMessage>();
            if (playerId < 0 || teamId < 0)
            {
                return captured;
            }

            foreach (BoardTrapMessage trap in _traps.Values)
            {
                if (trap.OwnerId == playerId || trap.OwnerTeamId == teamId || _triggered.Contains(trap.TrapId) ||
                    !IsInTriggerRange(trap, coordinate, floor))
                {
                    continue;
                }

                _triggered.Add(trap.TrapId);
                captured.Add(trap);
            }

            captured.Sort((first, second) => string.CompareOrdinal(first.TrapId, second.TrapId));
            return captured;
        }

        /// <summary>
        /// 発動済みの罠を除去し、その設置マスを再利用可能にします。
        /// </summary>
        public bool Remove(BoardTrapMessage trap)
        {
            if (trap == null)
            {
                return false;
            }
            Vector2Int coordinate = new Vector2Int(trap.X, trap.Y);
            if (!_traps.TryGetValue(coordinate, out BoardTrapMessage existing) || existing.TrapId != trap.TrapId)
            {
                return false;
            }

            _triggered.Remove(trap.TrapId);
            return _traps.Remove(coordinate);
        }

        /// <summary>
        /// 裁定側が確定した検知を受信側でも保持し、裁定引継ぎ時の再検知を防ぎます。
        /// </summary>
        public void MarkTriggered(string trapId)
        {
            _triggered.Add(trapId);
        }

        /// <summary>
        /// 盤面を破棄するときに罠の登録を全て解除します。
        /// </summary>
        public void Clear()
        {
            _traps.Clear();
            _triggered.Clear();
        }

        /// <summary>
        /// 起動判定と表示に共通の、同じ高さかつ六角形距離内のマスか確認します。
        /// </summary>
        public static bool IsInTriggerRange(BoardTrapMessage trap, Vector2Int coordinate, int floor)
        {
            return trap != null && trap.Floor == floor &&
                (coordinate.x - trap.X + coordinate.y - trap.Y) % 2 == 0 &&
                GetHexDistance(new Vector2Int(trap.X, trap.Y), coordinate) <= trap.Range;
        }

        /// <summary>
        /// プロジェクトの倍化Y座標における六角形の最短隣接距離を求めます。
        /// </summary>
        public static int GetHexDistance(Vector2Int first, Vector2Int second)
        {
            int deltaX = Math.Abs(first.x - second.x);
            int deltaY = Math.Abs(first.y - second.y);
            return deltaX + Math.Max(0, (deltaY - deltaX) / 2);
        }
    }
}
