using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Outline = PranaTactics.Rendering.Outlines.Outline;

namespace PranaTactics.Editor
{
    /// <summary>
    /// 一時オブジェクトの実描画から距離・投影方法による輪郭幅と共有境界を検証します。
    /// </summary>
    public static class OutlineRenderingValidation
    {
        [MenuItem("Tools/Prana Tactics/Validate outline rendering %#F10")]
        public static void Validate()
        {
            var report = new List<string>();
            var root = new GameObject("Outline validation") { hideFlags = HideFlags.HideAndDontSave };
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;

            try
            {
                material.SetColor("_BaseColor", Color.black);
                Camera camera = new GameObject("Camera").AddComponent<Camera>();

                camera.transform.SetParent(root.transform);
                camera.enabled = false;
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.allowMSAA = false;
                camera.allowHDR = false;
                camera.targetTexture = target;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                GameObject cube = CreateCube(root.transform, material, Vector3.zero);
                Outline outline = cube.GetComponent<Outline>();

                foreach (bool orthographic in new[] { false, true })
                {
                    camera.orthographic = orthographic;

                    foreach (float distance in new[] { 4.0f, 12.0f, 24.0f })
                    {
                        camera.transform.position = new Vector3(0, 0, -distance);
                        camera.orthographicSize = distance * 0.4f;

                        foreach (float angle in new[] { 0.0f, 35.0f, 70.0f })
                        {
                            cube.transform.rotation = Quaternion.Euler(0, angle, 0);
                            Render(camera, target, pixels);
                            List<int> widths = MeasureRuns(pixels, 256);
                            string label = $"orthographic={orthographic}, distance={distance}, angle={angle}: {string.Join(",", widths)} px";

                            Require(
                                widths.Count == 2 && widths.TrueForAll(width => width >= 3 && width <= 5),
                                label);
                            report.Add("PASS " + label);
                        }
                    }
                }

                camera.orthographic = true;
                camera.orthographicSize = 2;
                cube.transform.rotation = Quaternion.identity;
                cube.transform.position = new Vector3(-0.5f, 0, 0);
                GameObject neighbor = CreateCube(root.transform, material, new Vector3(0.5f, 0, 0));

                Render(camera, target, pixels);
                List<int> adjacentWidths = MeasureRuns(pixels, 256);

                Require(adjacentWidths.Count == 3 &&
                    adjacentWidths.TrueForAll(
                    width => width >= 3 &&
                    width <= 5),
                    "隣接オブジェクトの境界: " + string.Join(",", adjacentWidths));
                report.Add("PASS shared boundary: " + string.Join(",", adjacentWidths) + " px");
                File.WriteAllBytes("Temp/OutlineAdjacent.png", pixels.EncodeToPNG());
                neighbor.SetActive(false);
                cube.transform.position = Vector3.zero;
                GameObject occluder = CreateCube(root.transform, material, new Vector3(0, 0, -2));

                occluder.GetComponent<Outline>().enabled = false;
                occluder.transform.localScale = Vector3.one * 2;
                Render(camera, target, pixels);
                Require(MeasureRuns(pixels, 256).Count == 0, "OutlineVisibleは遮蔽物の後ろで非表示");
                outline.OutlineMode = Outline.Mode.OutlineHidden;
                Render(camera, target, pixels);
                Require(MeasureRuns(pixels, 256).Count == 2, "OutlineHiddenは遮蔽物の後ろで表示");
                report.Add("PASS visible/hidden depth modes");
                occluder.SetActive(false);
                outline.OutlineMode = Outline.Mode.OutlineVisible;
                outline.enabled = false;
                Render(camera, target, pixels);
                Require(MeasureRuns(pixels, 256).Count == 0, "無効化した輪郭を除去");
                outline.enabled = true;
                Render(camera, target, pixels);
                Require(MeasureRuns(pixels, 256).Count == 2, "再有効化した輪郭を表示");
                report.Add("PASS enable/disable lifecycle");
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Board/Space0.prefab");

                Require(prefab.GetComponent<Outline>() != null &&
                    prefab.GetComponent<Outline>().OutlineWidth == 3,
                    "Hex PrefabのGUIDと旧シリアライズ項目を復元");
                Mesh hexMesh = prefab.GetComponent<MeshFilter>().sharedMesh;

                cube.GetComponent<MeshFilter>().sharedMesh = hexMesh;
                camera.orthographic = false;
                float extent = hexMesh.bounds.size.magnitude;

                camera.transform.position = hexMesh.bounds.center + new Vector3(0, 2.4f, -2.4f) * extent;
                camera.transform.LookAt(hexMesh.bounds.center);
                Render(camera, target, pixels);
                ValidateHull(cube.transform, hexMesh, camera, pixels);
                File.WriteAllBytes("Temp/OutlineHex.png", pixels.EncodeToPNG());
                report.Add("PASS Hex Prefab reference and projected boundary edges");
                MeshRenderer renderer = cube.GetComponent<MeshRenderer>();

                UnityEngine.Object.DestroyImmediate(renderer);
                SkinnedMeshRenderer skinned = cube.AddComponent<SkinnedMeshRenderer>();

                skinned.sharedMesh = hexMesh;
                skinned.sharedMaterial = material;
                outline.enabled = false;
                outline.enabled = true;
                Render(camera, target, pixels);
                ValidateHull(cube.transform, hexMesh, camera, pixels);
                report.Add("PASS SkinnedMeshRenderer mask");
                Debug.Log("Outline rendering validation passed.");
            }
            catch (Exception exception)
            {
                report.Add("FAIL " + exception);
                File.WriteAllBytes("Temp/OutlineFailure.png", pixels.EncodeToPNG());
                Debug.LogException(exception);
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(material);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
                File.WriteAllLines("Temp/OutlineRenderingValidation.txt", report);
            }
        }

        private static GameObject CreateCube(Transform parent, Material material, Vector3 position)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

            cube.transform.SetParent(parent);
            cube.transform.position = position;
            cube.layer = 31;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            Outline outline = cube.AddComponent<Outline>();

            outline.OutlineColor = Color.red;
            outline.OutlineWidth = 4;
            outline.OutlineMode = Outline.Mode.OutlineVisible;

            return cube;
        }

        private static void Render(Camera camera, RenderTexture target, Texture2D pixels)
        {
            RenderPipeline.SubmitRenderRequest(
                camera,
                new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
        }

        private static List<int> MeasureRuns(Texture2D pixels, int row)
        {
            var widths = new List<int>();
            int run = 0;

            for (int x = 0; x < pixels.width; x++)
            {
                Color pixel = pixels.GetPixel(x, row);

                if (pixel.r > 0.5f && pixel.g < 0.1f)
                {
                    run++;
                }
                else if (run > 0)
                {
                    widths.Add(run);
                    run = 0;
                }
            }

            if (run > 0)
            {
                widths.Add(run);
            }

            return widths;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// 投影したメッシュの凸包を求め、各辺の中央に輪郭が存在することを確認します。
        /// </summary>
        private static void ValidateHull(Transform transform, Mesh mesh, Camera camera, Texture2D pixels)
        {
            var points = new List<Vector2>();

            foreach (Vector3 vertex in mesh.vertices)
            {
                Vector2 point = camera.WorldToScreenPoint(transform.TransformPoint(vertex));

                if (!points.Exists(existing => Vector2.Distance(existing, point) < 0.1f))
                {
                    points.Add(point);
                }
            }

            points.Sort(
                (left, right) => left.x == right.x ? left.y.CompareTo(right.y) : left.x.CompareTo(
                right.x));
            var hull = new List<Vector2>();

            foreach (Vector2 point in points)
            {
                AppendHull(hull, point, 0);
            }

            int lowerCount = hull.Count - 1;

            for (int i = points.Count - 2; i >= 0; i--)
            {
                AppendHull(hull, points[i], lowerCount);
            }

            Require(hull.Count >= 7, "Hexの投影境界が6辺以上");

            for (int i = 1; i < hull.Count; i++)
            {
                Vector2 center = (hull[i - 1] + hull[i]) * 0.5f;
                bool found = false;

                for (int y = -2; y <= 2; y++)
                {
                    for (int x = -2; x <= 2; x++)
                    {
                        Color pixel = pixels.GetPixel(
                            Mathf.RoundToInt(center.x) + x,
                            Mathf.RoundToInt(
                            center.y) +
                            y);

                        found |= pixel.r > 0.5f && pixel.g < 0.1f;
                    }
                }

                Require(found, "Hexの投影境界 " + i + " が描画される");
            }
        }

        private static void AppendHull(List<Vector2> hull, Vector2 point, int start)
        {
            while (hull.Count >= start + 2)
            {
                Vector2 edge = hull[hull.Count - 1] - hull[hull.Count - 2];
                Vector2 next = point - hull[hull.Count - 1];

                if (edge.x * next.y - edge.y * next.x > 0.01f)
                {
                    break;
                }

                hull.RemoveAt(hull.Count - 1);
            }

            hull.Add(point);
        }
    }
}
