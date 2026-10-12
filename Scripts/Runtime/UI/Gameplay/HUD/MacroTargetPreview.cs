using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

using PranaTactics.Unit;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// ターゲット候補のMacroモデルだけを一時的に透過し、元の描画設定を復元します。
    /// </summary>
    public sealed class MacroTargetPreview
    {
        /// <summary>
        /// Rendererごとの復元情報と候補専用マテリアルです。
        /// </summary>
        private sealed class RendererState
        {
            public Renderer Renderer;
            public Material[] Originals;
            public Material[] Previews;
            public MaterialPropertyBlock OriginalProperties;
        }

        private readonly List<RendererState> _states = new List<RendererState>();
        private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
        private GameObject _model;

        /// <summary>
        /// 候補を変更したときだけ専用マテリアルを生成します。
        /// </summary>
        public void SetModel(GameObject model, float opacity)
        {
            if (_model == model && (model != null || _states.Count == 0))
            {
                return;
            }

            Clear();
            _model = model;
            if (model == null)
            {
                return;
            }

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] originals = renderer.sharedMaterials;
                var previews = new Material[originals.Length];
                var originalProperties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(originalProperties);
                for (int index = 0; index < originals.Length; index++)
                {
                    if (originals[index] != null)
                    {
                        previews[index] = CreateTransparentMaterial(originals[index]);
                    }
                }

                _states.Add(new RendererState
                {
                    Renderer = renderer,
                    Originals = originals,
                    Previews = previews,
                    OriginalProperties = originalProperties
                });
                renderer.sharedMaterials = previews;
            }

            ApplyOpacity(opacity);
        }

        /// <summary>
        /// 現在のチーム色を維持して、表示濃度だけを変更します。
        /// </summary>
        public void ApplyOpacity(float opacity)
        {
            float alpha = float.IsNaN(opacity) || float.IsInfinity(opacity) ? 0.5f : Mathf.Clamp01(opacity);
            foreach (RendererState state in _states)
            {
                if (state.Renderer == null)
                {
                    continue;
                }

                state.Renderer.GetPropertyBlock(_properties);
                Material material = state.Originals.Length > 0 ? state.Originals[0] : null;
                ApplyColorAlpha("_BaseColor", material, alpha);
                ApplyColorAlpha("_Color", material, alpha);
                state.Renderer.SetPropertyBlock(_properties);
                _properties.Clear();
            }
        }

        /// <summary>
        /// プロパティブロックまたは元のマテリアルの色を使い、透明度だけを変更します。
        /// </summary>
        private void ApplyColorAlpha(string property, Material material, float alpha)
        {
            if (!_properties.HasColor(property) && (material == null || !material.HasProperty(property)))
            {
                return;
            }

            Color color = _properties.HasColor(property) ? _properties.GetColor(property) : material.GetColor(property);
            color.a = alpha;
            _properties.SetColor(property, color);
        }

        /// <summary>
        /// 共有アセットを変更せず、URPのアルファ合成で表示するコピーを作成します。
        /// </summary>
        private static Material CreateTransparentMaterial(Material original)
        {
            var material = new Material(original);
            material.SetFloat("_Surface", 1.0f);
            material.SetFloat("_Blend", 0.0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0.0f);
            material.SetFloat("_AlphaClip", 0.0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        /// <summary>
        /// キャンセル、確定、フェイズ終了時に元の表示へ戻し、一時マテリアルを破棄します。
        /// </summary>
        public void Clear()
        {
            foreach (RendererState state in _states)
            {
                if (state.Renderer != null)
                {
                    state.Renderer.sharedMaterials = state.Originals;
                    state.Renderer.SetPropertyBlock(state.OriginalProperties);
                }

                foreach (Material material in state.Previews)
                {
                    if (material != null)
                    {
                        Object.Destroy(material);
                    }
                }
            }

            if (_model != null)
            {
                _model.GetComponentInParent<UnitBehavior>()?.GetComponent<UnitTeamColorView>()?.Invalidate();
            }
            _states.Clear();
            _model = null;
        }
    }
}
