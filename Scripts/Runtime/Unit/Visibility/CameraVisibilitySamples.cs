using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.Unit
{
    /// <summary>
    /// 描画範囲で切り取ったモデルのBoundsから、部分視認を確認する点を生成します。
    /// </summary>
    public static class CameraVisibilitySamples
    {
        private const float k_PlaneTolerance = 0.0001f;

        /// <summary>
        /// 中心、面、辺、頂点と、画面端で切り取られた辺を検査候補にします。
        /// </summary>
        public static void Collect(Bounds bounds, Plane[] planes, List<Vector3> samples)
        {
            samples.Clear();
            AddIfInside(bounds.center, planes, samples);
            Vector3 minimum = bounds.min;
            Vector3 size = bounds.size;

            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    for (int z = 0; z < 3; z++)
                    {
                        Vector3 point = minimum +
                            Vector3.Scale(
                            size,
                            new Vector3(
                            x * 0.5f,
                            y * 0.5f,
                            z * 0.5f));

                        AddIfInside(point, planes, samples);
                    }
                }
            }

            for (int axis = 0; axis < 3; axis++)
            {
                for (int first = 0; first < 2; first++)
                {
                    for (int second = 0; second < 2; second++)
                    {
                        Vector3 start = minimum;

                        start[(axis + 1) % 3] += size[(axis + 1) % 3] * first;
                        start[(axis + 2) % 3] += size[(axis + 2) % 3] * second;
                        Vector3 end = start;

                        end[axis] += size[axis];

                        if (TryClipSegment(
                            start,
                            end,
                            planes,
                            out Vector3 clippedStart,
                            out Vector3 clippedEnd))
                        {
                            for (int step = 0; step <= 4; step++)
                            {
                                AddIfInside(
                                    Vector3.Lerp(clippedStart, clippedEnd, step * 0.25f),
                                    planes,
                                    samples);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 線分をカメラの6平面の内側へ切り取り、画面端の小さな重なりを残します。
        /// </summary>
        public static bool TryClipSegment(
            Vector3 start,
            Vector3 end,
            Plane[] planes,
            out Vector3 clippedStart,
            out Vector3 clippedEnd)
        {
            float minimum = 0.0f;
            float maximum = 1.0f;

            foreach (Plane plane in planes)
            {
                float startDistance = plane.GetDistanceToPoint(start);
                float endDistance = plane.GetDistanceToPoint(end);

                if (startDistance < 0.0f && endDistance < 0.0f)
                {
                    clippedStart = default;
                    clippedEnd = default;

                    return false;
                }

                if (startDistance < 0.0f || endDistance < 0.0f)
                {
                    float crossing = startDistance / (startDistance - endDistance);

                    if (startDistance < 0.0f)
                    {
                        minimum = Mathf.Max(minimum, crossing);
                    }
                    else
                    {
                        maximum = Mathf.Min(maximum, crossing);
                    }
                }
            }

            clippedStart = Vector3.Lerp(start, end, minimum);
            clippedEnd = Vector3.Lerp(start, end, maximum);

            return minimum <= maximum;
        }

        /// <summary>
        /// カメラの描画範囲内にある重複しない検査点だけを追加します。
        /// </summary>
        private static void AddIfInside(Vector3 point, Plane[] planes, List<Vector3> samples)
        {
            foreach (Plane plane in planes)
            {
                if (plane.GetDistanceToPoint(point) < -k_PlaneTolerance)
                {
                    return;
                }
            }

            if (!samples.Contains(point))
            {
                samples.Add(point);
            }
        }
    }
}
