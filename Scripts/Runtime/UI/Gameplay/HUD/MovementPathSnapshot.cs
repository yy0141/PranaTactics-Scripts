using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// マクロで確定した経路全体と、ミクロでの移動済み区間を表示用に保持します。
    /// </summary>
    public sealed class MovementPathSnapshot
    {
        /// <summary>
        /// 開始地点を含む表示用経路です。
        /// </summary>
        private readonly List<Vector2Int> _coordinates = new List<Vector2Int>();

        public int PlayerId { get; }

        public IReadOnlyList<Vector2Int> Coordinates => _coordinates;

        public int CompletedSteps { get; private set; }

        /// <summary>
        /// 開始地点と移動先をコピーし、経路の消費や編集から表示データを分離します。
        /// </summary>
        public MovementPathSnapshot(int playerId, Vector2Int start, IReadOnlyList<Vector2Int> steps)
        {
            PlayerId = playerId;
            _coordinates.Add(start);
            if (steps != null)
            {
                for (int index = 0; index < steps.Count; index++)
                {
                    _coordinates.Add(steps[index]);
                }
            }
        }

        /// <summary>
        /// 経路編集で表示の再構築が必要か判定します。
        /// </summary>
        public bool Matches(Vector2Int start, IReadOnlyList<Vector2Int> steps)
        {
            if (steps == null || _coordinates.Count != steps.Count + 1 || _coordinates[0] != start)
            {
                return false;
            }

            for (int index = 0; index < steps.Count; index++)
            {
                if (_coordinates[index + 1] != steps[index])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 完了区間を更新し、表示色の更新が必要な場合に<c>true</c>を返します。
        /// </summary>
        public bool UpdateProgress(int completedSteps)
        {
            int boundedSteps = Mathf.Clamp(completedSteps, 0, _coordinates.Count - 1);
            if (CompletedSteps == boundedSteps)
            {
                return false;
            }

            CompletedSteps = boundedSteps;
            return true;
        }
    }
}
