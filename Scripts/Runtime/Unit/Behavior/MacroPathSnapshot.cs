using System;
using UnityEngine;

namespace PranaTactics.Unit
{
    /// <summary>
    /// 経路編集結果を座標と行動ポイントとともに同期します。
    /// </summary>
    [Serializable]
    public sealed class MacroPathSnapshot
    {
        public int Turn;

        public Vector2Int Coordinate;

        public int Proactiveness;

        public Vector2Int[] Path;

        public bool ShouldInterpolate;

        public Vector2Int[] PlaybackPath;
    }
}
