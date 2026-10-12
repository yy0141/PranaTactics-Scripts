// Copyright © 2018 Chris Nolet. All rights reserved.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace PranaTactics.Rendering.Outlines
{
    /// <summary>
    /// 輪郭の表示対象と色・画面ピクセル幅を保持し、描画機能へ登録します。
    /// </summary>
    /// <remarks>
    /// QuickOutlineの公開APIとシリアライズ参照を引き継ぎます。
    /// 登録は有効化から無効化まで保持し、ドメインリロードを省略した再生でも継続します。
    /// </remarks>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class Outline : MonoBehaviour
    {
        public enum Mode
        {
            OutlineAll,
            OutlineVisible,
            OutlineHidden,
            OutlineAndSilhouette,
            SilhouetteOnly
        }

        private static readonly List<Outline> s_active = new();
        [FormerlySerializedAs("outlineMode")]
        [SerializeField]
        private Mode _outlineMode;
        [FormerlySerializedAs("outlineColor")]
        [SerializeField]
        private Color _outlineColor = Color.white;
        /// <summary>
        /// 輪郭全体の幅を描画先のピクセル数で指定します。
        /// </summary>
        [FormerlySerializedAs("outlineWidth")]
        [Range(0.0f, 40.0f)]
        [SerializeField]
        private float _outlineWidth = 2.0f;
        /// <summary>
        /// 通常のハイライトより優先して表示するターゲット指定の状態です。
        /// </summary>
        private bool _hasTargetHighlight;

        /// <summary>
        /// ターゲット指定の枠線色です。
        /// </summary>
        private Color _targetColor;

        /// <summary>
        /// ターゲット指定の枠線幅です。
        /// </summary>
        private float _targetWidth;

        private Renderer[] _renderers;
        private Material _maskMaterial;

        internal static IReadOnlyList<Outline> Active => s_active;
        internal Renderer[] Renderers => _renderers;
        internal bool IsRenderingEnabled => gameObject.activeInHierarchy && (enabled || _hasTargetHighlight);
        public Mode OutlineMode
        {
            get => _hasTargetHighlight ? Mode.OutlineVisible : _outlineMode;
            set => _outlineMode = value;
        }
        public Color OutlineColor
        {
            get => _hasTargetHighlight ? _targetColor : _outlineColor;
            set => _outlineColor = value;
        }
        public float OutlineWidth
        {
            get => _hasTargetHighlight ? _targetWidth : _outlineWidth;
            set => _outlineWidth = float.IsNaN(value) || float.IsInfinity(value) ? 0.0f : Mathf.Clamp(value, 0.0f, 40.0f);
        }

        /// <summary>
        /// 通常の色・幅・有効状態を維持しながら、ターゲット指定の枠線を重ねます。
        /// </summary>
        /// <remarks>
        /// 指定中も通常の攻撃ハイライトの有効状態や設定は更新できます。
        /// 指定を解除すると、その時点の通常設定で描画します。
        /// </remarks>
        public void SetTargetHighlight(bool isHighlighted, Color color, float width)
        {
            bool needsRenderers = isHighlighted && !_hasTargetHighlight;
            _hasTargetHighlight = isHighlighted;
            _targetColor = color;
            _targetWidth = float.IsNaN(width) || float.IsInfinity(width) ? 0.0f : Mathf.Clamp(width, 0.0f, 40.0f);
            if (isHighlighted)
            {
                if (needsRenderers)
                {
                    _renderers = GetComponentsInChildren<Renderer>(true);
                }
                if (!s_active.Contains(this))
                {
                    s_active.Add(this);
                }
            }
            else if (!isActiveAndEnabled)
            {
                s_active.Remove(this);
            }
        }

        private void OnValidate()
        {
            OutlineWidth = _outlineWidth;
        }

        private void OnEnable()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            if (!s_active.Contains(this))
            {
                s_active.Add(this);
            }
        }

        private void OnDisable()
        {
            if (!_hasTargetHighlight)
            {
                s_active.Remove(this);
            }
        }

        private void OnDestroy()
        {
            s_active.Remove(this);
            CoreUtils.Destroy(_maskMaterial);
        }

        /// <summary>
        /// 元のマテリアルや共有メッシュを変更せず、輪郭マスク専用の材質を用意します。
        /// </summary>
        internal Material PrepareMaterial(Shader shader, int identifier)
        {
            if (_maskMaterial == null)
            {
                _maskMaterial = CoreUtils.CreateEngineMaterial(shader);
            }
            _maskMaterial.SetInt("_OutlineId", identifier);
            _maskMaterial.SetInt("_OutlineMode", (int)OutlineMode);
            _maskMaterial.SetFloat("_OutlineWidth", OutlineMode == Mode.SilhouetteOnly ? 0.0f : OutlineWidth);
            _maskMaterial.SetColor("_OutlineColor", OutlineColor);
            return _maskMaterial;
        }
    }
}
