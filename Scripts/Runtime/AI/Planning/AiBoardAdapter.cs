using System;
using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Board;
using PranaTactics.Unit;

namespace PranaTactics.AI.Solo
{
    /// <summary>
    /// BoardManagerの盤面情報を探索中だけキャッシュし、AIへ提供します。
    /// </summary>
    public sealed class AiBoardAdapter
    {
        private readonly BoardManager _board;

        /// <summary>
        /// 盤面に存在するか確認済みの座標です。
        /// </summary>
        private readonly Dictionary<Vector2Int, bool> _containsByCoordinate = new Dictionary<Vector2Int, bool>();

        /// <summary>
        /// 隣接座標の取得結果をキャッシュします。
        /// </summary>
        private readonly Dictionary<Vector2Int, List<Vector2Int>> _neighborsByCoordinate = new Dictionary<Vector2Int, List<Vector2Int>>();

        /// <summary>
        /// 各ヘックスの高さをキャッシュします。
        /// </summary>
        private readonly Dictionary<Vector2Int, int> _floorByCoordinate = new Dictionary<Vector2Int, int>();

        /// <summary>
        /// 各ヘックスのワールド座標をキャッシュします。
        /// </summary>
        private readonly Dictionary<Vector2Int, Vector3> _worldPositionByCoordinate = new Dictionary<Vector2Int, Vector3>();

        /// <summary>
        /// 現在ユニットが占有している座標と
        /// プレイヤーIDの対応を保持します。
        /// </summary>
        private readonly Dictionary<Vector2Int, int> _occupiedPlayerIdByCoordinate = new Dictionary<Vector2Int, int>();

        private float _cachedCellSpacing = -1f;

        /// <summary>
        /// 現在の盤面を参照する、今回の計画専用のキャッシュを準備します。
        /// </summary>
        public AiBoardAdapter()
        {
            _board = BoardManager.Instance;

            if (_board == null)
            {
                throw new InvalidOperationException("BoardManager.Instanceが存在しません。");
            }
        }

        /// <summary>
        /// 盤面内外の判定を座標ごとに一度だけ取得します。
        /// </summary>
        public bool Contains(Vector2Int coordinate)
        {
            if (!_containsByCoordinate.TryGetValue(coordinate, out bool contains))
            {
                contains = _board.ContainsHexCell(coordinate.x, coordinate.y);
                _containsByCoordinate[coordinate] = contains;
            }

            return contains;
        }

        /// <summary>
        /// 隣接座標をキャッシュから取得します。
        /// </summary>
        public List<Vector2Int> GetNeighbors(Vector2Int coordinate)
        {
            if (_neighborsByCoordinate.TryGetValue(coordinate, out List<Vector2Int> cachedNeighbors))
            {
                return cachedNeighbors;
            }

            List<Vector2Int> neighbors = _board.GetAdjacentHexCellCoordinates(coordinate.x, coordinate.y);

            _neighborsByCoordinate[coordinate] = neighbors;

            return neighbors;
        }

        /// <summary>
        /// マクロ盤面の高さをキャッシュから取得します。
        /// </summary>
        public int GetFloor(Vector2Int coordinate)
        {
            if (_floorByCoordinate.TryGetValue(coordinate, out int cachedFloor))
            {
                return cachedFloor;
            }

            HexCell cell = _board.GetMacroPhaseHexCell(coordinate.x, coordinate.y);
            int floor = cell != null ? cell.GetFloor() : 0;

            _floorByCoordinate[coordinate] = floor;

            return floor;
        }

        /// <summary>
        /// マップ上のワールド座標をキャッシュから取得します。
        /// </summary>
        public Vector3 GetWorldPosition(Vector2Int coordinate)
        {
            if (_worldPositionByCoordinate.TryGetValue(coordinate, out Vector3 cachedPosition))
            {
                return cachedPosition;
            }

            Vector3 position = _board.GetMapHexCellPosition(coordinate.x, coordinate.y);

            _worldPositionByCoordinate[coordinate] = position;

            return position;
        }

        /// <remarks>
        /// <para>下降は制限しません。</para>
        /// </remarks>
        public bool CanStep(Vector2Int from, Vector2Int to, int maximumClimbHeight)
        {
            if (!Contains(to))
            {
                return false;
            }

            int heightDifference = GetFloor(to) - GetFloor(from);

            if (heightDifference <= 0)
            {
                return true;
            }

            return heightDifference <= maximumClimbHeight;
        }

        public float EstimateCellDistance(Vector2Int from, Vector2Int to)
        {
            float spacing = GetCellSpacing(from);

            if (spacing <= 0.001f)
            {
                return Vector2Int.Distance(from, to);
            }

            return Vector3.Distance(GetWorldPosition(from), GetWorldPosition(to)) / spacing;
        }

        public float GetCellSpacing(Vector2Int origin)
        {
            if (_cachedCellSpacing > 0f)
            {
                return _cachedCellSpacing;
            }

            Vector3 originPosition = GetWorldPosition(origin);

            foreach (Vector2Int neighbor in GetNeighbors(origin))
            {
                float distance = Vector3.Distance(originPosition, GetWorldPosition(neighbor));

                if (distance > 0.001f)
                {
                    _cachedCellSpacing = distance;

                    return distance;
                }
            }

            _cachedCellSpacing = 1f;

            return _cachedCellSpacing;
        }

        /// <summary>
        /// 現在存在するユニットの座標をキャッシュします。
        /// 経路探索を開始する前に一度だけ呼び出します。
        /// </summary>
        public void RefreshOccupiedCoordinates()
        {
            _occupiedPlayerIdByCoordinate.Clear();
            UnitManager unitManager = UnitManager.Instance;

            if (unitManager == null)
            {
                return;
            }

            foreach (UnitBehavior unit in unitManager.GetAllUnitBehaviors())
            {
                if (unit == null || !IsPubliclyAlive(unit))
                {
                    continue;
                }

                TacticalMapUnitBehavior tacticalMapUnit = unit.GetTacticalMapUnitBehavior();

                if (tacticalMapUnit == null)
                {
                    continue;
                }

                Vector2Int coordinate = tacticalMapUnit.GetCurrentCoordinate();

                _occupiedPlayerIdByCoordinate[coordinate] = unit.PlayerId;
            }
        }

        /// <remarks>
        /// <para>ReportAlive接続前だけ使用する互換処理です。</para>
        /// </remarks>
        private static bool IsPubliclyAlive(UnitBehavior unit)
        {
            if (unit == null)
            {
                return false;
            }

            if (AiPublicStateRegistry.TryGetAlive(unit.PlayerId, out bool registered))
            {
                return registered;
            }

            return unit.GetUnitStatus() != null && unit.GetUnitStatus().CurrentHealth > 0.0f;
        }

        /// <summary>
        /// 指定された座標を別ユニットが占有しているか確認します。
        /// </summary>
        public bool IsOccupied(Vector2Int coordinate, UnitBehavior self)
        {
            if (!_occupiedPlayerIdByCoordinate.TryGetValue(coordinate, out int occupyingPlayerId))
            {
                return false;
            }

            int selfPlayerId = self != null ? self.PlayerId : -1;

            return occupyingPlayerId != selfPlayerId;
        }
    }
}
