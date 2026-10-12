using System;
using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.Unit
{
    /// <summary>
    /// クリック単位の経路と行動ポイントを保持し、分岐後のやり直し履歴を破棄します。
    /// </summary>
    public sealed class MacroPathHistory
    {
        private readonly List<Vector2Int> _coordinates = new();

        private readonly List<int> _balances = new();

        private readonly List<int> _commandEnds = new();

        private int _position;

        public bool CanUndo => _position > 0;

        public bool CanRedo => _position < _commandEnds.Count - 1;

        public Vector2Int Coordinate => _coordinates[_commandEnds[_position]];

        public int Proactiveness => _balances[_position];

        public void Clear()
        {
            if (_coordinates.Count > 0)
            {
                Reset(_coordinates[0], _balances[0]);
            }
        }

        public void Reset(Vector2Int origin, int proactiveness)
        {
            _coordinates.Clear();
            _balances.Clear();
            _commandEnds.Clear();
            _coordinates.Add(origin);
            _balances.Add(proactiveness);
            _commandEnds.Add(0);
            _position = 0;
        }

        public void Append(Vector2Int coordinate, int proactiveness)
        {
            AppendPath(new[] { coordinate }, proactiveness);
        }

        /// <summary>
        /// 始点を含まない追加経路を一つの操作として記録します。
        /// </summary>
        public void AppendPath(IReadOnlyList<Vector2Int> path, int proactiveness)
        {
            if (path == null || path.Count == 0)
            {
                return;
            }

            int coordinateCount = _commandEnds[_position] + 1;

            _coordinates.RemoveRange(coordinateCount, _coordinates.Count - coordinateCount);
            int removedCount = _commandEnds.Count - _position - 1;

            _balances.RemoveRange(_position + 1, removedCount);
            _commandEnds.RemoveRange(_position + 1, removedCount);

            for (int index = 0; index < path.Count; index++)
            {
                _coordinates.Add(path[index]);
            }

            _balances.Add(proactiveness);
            _commandEnds.Add(_coordinates.Count - 1);
            _position++;
        }

        public bool TryStep(bool isRedo)
        {
            if (isRedo ? !CanRedo : !CanUndo)
            {
                return false;
            }

            _position += isRedo ? 1 : -1;

            return true;
        }

        public Vector2Int[] GetPath()
        {
            return _coordinates.GetRange(1, _commandEnds[_position]).ToArray();
        }

        /// <summary>
        /// 編集前の経路から現在の経路へ移る表示用の順路を返します。
        /// </summary>
        public Vector2Int[] GetTransitionFrom(IReadOnlyList<Vector2Int> previousPath)
        {
            Vector2Int[] nextPath = GetPath();
            int commonCount = 0;

            while (commonCount < previousPath.Count && commonCount < nextPath.Length &&
                previousPath[commonCount].Equals(nextPath[commonCount]))
            {
                commonCount++;
            }

            var transition = new List<Vector2Int>();

            for (int index = previousPath.Count - 2; index >= Math.Max(0, commonCount - 1); index--)
            {
                transition.Add(previousPath[index]);
            }

            if (commonCount == 0 && previousPath.Count > 0)
            {
                transition.Add(_coordinates[0]);
            }

            for (int index = commonCount; index < nextPath.Length; index++)
            {
                transition.Add(nextPath[index]);
            }

            return transition.ToArray();
        }
    }
}
