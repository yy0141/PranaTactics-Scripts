using System.Collections.Generic;

using PranaTactics.Board;
using UnityEngine;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// ホバー中の候補経路だけを既存のマス表示へ重ね、解除時に元へ戻します。
    /// </summary>
    public sealed class MacroRoutePreview
    {
        private readonly List<HexCell> _enabledCells = new();

        public void Show(BoardManager board, IReadOnlyList<Vector2Int> path)
        {
            Clear();

            foreach (Vector2Int coordinate in path)
            {
                HexCell cell = board.GetMacroPhaseHexCell(coordinate.x, coordinate.y);

                if (cell != null && !cell.IsOutlineEnabled())
                {
                    cell.SetOutlineEnabled(true);
                    _enabledCells.Add(cell);
                }
            }
        }

        public void Clear()
        {
            foreach (HexCell cell in _enabledCells)
            {
                if (cell != null)
                {
                    cell.SetOutlineEnabled(false);
                }
            }

            _enabledCells.Clear();
        }
    }
}
