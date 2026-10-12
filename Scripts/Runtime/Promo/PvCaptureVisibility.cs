#if UNITY_EDITOR || PV_CAPTURE
using System;
using System.Collections.Generic;

using PranaTactics.Prana;
using UnityEngine;
using UnityEngine.Rendering;
using Outline = PranaTactics.Rendering.Outlines.Outline;

namespace PranaTactics.Promo
{
    /// <summary>
    /// 視認判定や衝突を維持したまま、撮影対象以外の描画だけを一時的に抑制します。
    /// </summary>
    public sealed class PvCaptureVisibility : IDisposable
    {
        private readonly Dictionary<Renderer, bool> _original = new Dictionary<Renderer, bool>();

        private readonly HashSet<Renderer> _hidden = new HashSet<Renderer>();

        private readonly PvWallVisibility _walls = new PvWallVisibility();

        private readonly HashSet<Renderer> _retainedWalls = new HashSet<Renderer>();

        private readonly HashSet<Outline> _hiddenOutlines = new HashSet<Outline>();

        private readonly Dictionary<Outline, bool> _originalOutlines = new Dictionary<Outline, bool>();

        private readonly Camera _camera;

        private readonly CameraClearFlags _clearFlags;

        private readonly Color _background;

        public PvCaptureVisibility(Camera camera)
        {
            _camera = camera;
            _clearFlags = camera.clearFlags;
            _background = camera.backgroundColor;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        public void Apply(
            PvActor first,
            PvActor second,
            PvActor flanker,
            bool hideFirst,
            bool hideSecond,
            bool hideFlanker,
            bool isolate,
            bool hideSecondHighlight,
            bool retainWalls)
        {
            _hidden.Clear();
            _hiddenOutlines.Clear();

            if (hideSecondHighlight)
            {
                foreach (Outline outline in second.Unit.GetComponentsInChildren<Outline>(true))
                {
                    _hiddenOutlines.Add(outline);
                }
            }

            Transform firstModel = first.Unit.MicroUnitBehavior.GetModel().transform;
            Transform secondModel = second.Unit.MicroUnitBehavior.GetModel().transform;
            Transform flankModel = flanker.Unit.MicroUnitBehavior.GetModel().transform;

            foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                bool isFirst = renderer.transform.IsChildOf(firstModel);
                bool isSecond = renderer.transform.IsChildOf(secondModel);
                bool isFlanker = renderer.transform.IsChildOf(flankModel);
                Gerade projectile = renderer.GetComponentInParent<Gerade>();
                bool isOpeningProjectile = projectile != null &&
                    projectile.OwnerPlayerId == first.Unit.PlayerId;
                bool isFlankProjectile = projectile != null &&
                    projectile.OwnerPlayerId == flanker.Unit.PlayerId;
                bool shouldHide = hideFirst && isFirst || hideSecond && isSecond ||
                    hideFlanker &&
                    (isFlanker ||
                    isFlankProjectile) ||
                    isolate && !isFirst && !isSecond && !isOpeningProjectile;

                if (shouldHide)
                {
                    _hidden.Add(renderer);
                }
            }

            if (retainWalls)
            {
                _walls.Collect(_camera, first, second, _retainedWalls);
                _hidden.UnionWith(_retainedWalls);
            }
            else
            {
                _retainedWalls.Clear();
            }

            _camera.clearFlags = isolate ? CameraClearFlags.SolidColor : _clearFlags;
            _camera.backgroundColor = isolate ? new Color(0.015f, 0.02f, 0.03f) : _background;
        }

        /// <summary>
        /// 撮影カメラでは指定ハイライトとモデルを隠し、通常の視認・武器処理には表示状態を残します。
        /// </summary>
        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera != _camera)
            {
                return;
            }

            RestoreRenderers();

            foreach (Outline outline in _hiddenOutlines)
            {
                if (outline != null)
                {
                    _originalOutlines.Add(outline, outline.enabled);
                    outline.enabled = false;
                }
            }

            foreach (Renderer renderer in _hidden)
            {
                if (renderer != null)
                {
                    _original.Add(renderer, renderer.forceRenderingOff);
                    renderer.forceRenderingOff = true;
                }
            }
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == _camera)
            {
                RestoreRenderers();
            }
        }

        private void RestoreRenderers()
        {
            foreach (KeyValuePair<Outline, bool> entry in _originalOutlines)
            {
                if (entry.Key != null)
                {
                    entry.Key.enabled = entry.Value;
                }
            }

            _originalOutlines.Clear();

            foreach (KeyValuePair<Renderer, bool> entry in _original)
            {
                if (entry.Key != null)
                {
                    entry.Key.forceRenderingOff = entry.Value;
                }
            }

            _original.Clear();
        }

        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            RestoreRenderers();

            if (_camera != null)
            {
                _camera.clearFlags = _clearFlags;
                _camera.backgroundColor = _background;
            }

            _hidden.Clear();
            _hiddenOutlines.Clear();
            _retainedWalls.Clear();
        }
    }
}
#endif
