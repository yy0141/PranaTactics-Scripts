using System;

namespace PranaTactics.UI.Gameplay
{
    public readonly struct MovementPathDisplayScale
    {
        public float Width { get; }
        public float OutlineWidth { get; }
        public float LaneSpacing { get; }
        public float StartRadius { get; }
        public float GoalRadius { get; }
        public bool ShowLabels { get; }

        /// <summary>
        /// 画面上のサイズを維持しながら、重複レーンの幅をセル内へ収めます。
        /// </summary>
        public MovementPathDisplayScale(float pixelsPerCell, float widthMultiplier, int laneCount, MovementPathData data)
        {
            float pixels = float.IsNaN(pixelsPerCell) || float.IsInfinity(pixelsPerCell)
                ? data.MapReferenceCellPixels : Math.Max(1.0f, pixelsPerCell);
            float multiplier = float.IsNaN(widthMultiplier) || float.IsInfinity(widthMultiplier)
                ? 1.0f : Math.Min(4.0f, Math.Max(0.25f, widthMultiplier));
            float outer = Math.Max(data.OutlineWidthPixels, data.LineWidthPixels + 1.0f) * multiplier / pixels;
            LaneSpacing = Math.Min(data.MaximumLaneGroupCells / Math.Max(1, laneCount), Math.Max(0.12f, outer * 1.15f));
            OutlineWidth = Math.Min(outer, LaneSpacing * 0.9f);
            Width = Math.Min(data.LineWidthPixels * multiplier / pixels,
                OutlineWidth * data.LineWidthPixels / Math.Max(data.OutlineWidthPixels, data.LineWidthPixels + 1.0f));
            StartRadius = Math.Min(0.2f, data.StartRadiusPixels / pixels);
            GoalRadius = Math.Min(0.35f, data.GoalRadiusPixels / pixels);
            ShowLabels = data.ShowUnitLabels && pixels >= data.LabelMinimumCellPixels;
        }
    }
}
