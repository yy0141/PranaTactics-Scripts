using System;
using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.AI.Solo
{
    /// <summary>
    /// 同じ移動ステップに味方CPUが同じヘックスへ入ることを防ぎます。
    /// </summary>
    public sealed class AiReservationTable
    {
        private struct Key : IEquatable<Key>
        {
            public int Step;

            public Vector2Int Coordinate;

            public bool Equals(Key other)
            {
                return Step == other.Step && Coordinate == other.Coordinate;
            }

            public override bool Equals(object obj)
            {
                return obj is Key other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Step * 397) ^ Coordinate.GetHashCode();
                }
            }
        }

        private struct EdgeKey : IEquatable<EdgeKey>
        {
            public int Step;

            public Vector2Int From;

            public Vector2Int To;

            public bool Equals(EdgeKey other)
            {
                return Step == other.Step && From == other.From && To == other.To;
            }

            public override bool Equals(object obj)
            {
                return obj is EdgeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Step;

                    hash = (hash * 397) ^ From.GetHashCode();
                    hash = (hash * 397) ^ To.GetHashCode();

                    return hash;
                }
            }
        }

        private readonly Dictionary<Key, int> _owners = new Dictionary<Key, int>();

        private readonly Dictionary<EdgeKey, int> _edgeOwners = new Dictionary<EdgeKey, int>();

        public bool IsReserved(int step, Vector2Int coordinate, int requestingPlayerId)
        {
            Key key = new Key
            {
                Step = step,
                Coordinate = coordinate
            };

            return _owners.TryGetValue(key, out int owner) && owner != requestingPlayerId;
        }

        /// <summary>
        /// 到着ヘックスの重複と、同じステップでの正面衝突を確認します。
        /// </summary>
        public bool IsMoveReserved(int step, Vector2Int from, Vector2Int to, int requestingPlayerId)
        {
            if (IsReserved(step, to, requestingPlayerId))
            {
                return true;
            }

            EdgeKey reverse = new EdgeKey
            {
                Step = step,
                From = to,
                To = from
            };

            return _edgeOwners.TryGetValue(reverse, out int owner) && owner != requestingPlayerId;
        }

        /// <remarks>
        /// <para>先に到着した味方の停止位置へ、後続が入り込むことを防ぎます。</para>
        /// </remarks>
        public void Reserve(int playerId, IReadOnlyList<Vector2Int> path, int holdUntilStep = 12)
        {
            if (path == null)
            {
                return;
            }

            for (int step = 1; step < path.Count; step++)
            {
                Key key = new Key
                {
                    Step = step,
                    Coordinate = path[step]
                };

                _owners[key] = playerId;
                EdgeKey edge = new EdgeKey
                {
                    Step = step,
                    From = path[step - 1],
                    To = path[step]
                };

                _edgeOwners[edge] = playerId;
            }

            if (path.Count == 0)
            {
                return;
            }

            Vector2Int destination = path[path.Count - 1];
            int firstHoldStep = Mathf.Max(1, path.Count);
            int lastHoldStep = Mathf.Max(firstHoldStep, holdUntilStep);

            for (int step = firstHoldStep; step <= lastHoldStep; step++)
            {
                Key key = new Key
                {
                    Step = step,
                    Coordinate = destination
                };

                _owners[key] = playerId;
            }
        }

        public void Clear()
        {
            _owners.Clear();
            _edgeOwners.Clear();
        }
    }
}
