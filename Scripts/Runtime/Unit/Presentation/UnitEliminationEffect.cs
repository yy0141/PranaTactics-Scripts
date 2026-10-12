using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;

namespace PranaTactics.Unit.Presentation
{
    /// <summary>
    /// 独立したVFX Graphへ撃破演出の時間と調整値を渡します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitEliminationEffect : MonoBehaviour
    {
        /// <summary>
        /// Unitカメラに表示するレイヤーです。
        /// </summary>
        private const string k_EffectLayerName = "Visible";
        /// <summary>
        /// 再生する設定です。
        /// </summary>
        private UnitEliminationProfile _profile;
        /// <summary>
        /// GPUで描画するVFX Graphです。
        /// </summary>
        private VisualEffect _visualEffect;
        /// <summary>
        /// 個別の描画設定を保持するマテリアルです。
        /// </summary>
        private Material _material;
        /// <summary>
        /// 球体と雷状の線をGPUで合成するテクスチャです。
        /// </summary>
        private RenderTexture _texture;
        /// <summary>
        /// 再生開始時刻です。
        /// </summary>
        private double _startedAt;
        /// <summary>
        /// 同時撃破でも雷の形を変える乱数種です。
        /// </summary>
        private float _seed;

        /// <summary>
        /// Unitと親子関係を持たないVFXを同じシーンに生成します。
        /// </summary>
        /// <param name="center">
        /// 消滅するUnitのモデル中心のワールド座標です。
        /// </param>
        /// <param name="scene">
        /// Unitが所属していたシーンです。
        /// </param>
        public static UnitEliminationEffect Spawn(UnitEliminationProfile profile, Vector3 center, Scene scene)
        {
            if (profile == null || profile.EffectAsset == null || profile.ProceduralMaterial == null)
            {
                Debug.LogWarning("撃破VFXのアセットまたは描画マテリアルが未設定です。");
                return null;
            }

            int layer = LayerMask.NameToLayer(k_EffectLayerName);
            if (layer < 0)
            {
                Debug.LogError($"撃破VFXの描画レイヤーが見つかりません。Layer={k_EffectLayerName}");
                return null;
            }

            var root = new GameObject("Unit Elimination VFX");
            root.layer = layer;
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = center + profile.CenterOffset;
            UnitEliminationEffect effect = root.AddComponent<UnitEliminationEffect>();
            try
            {
                effect.Initialize(profile);
                return effect;
            }
            catch (Exception exception)
            {
                ReleaseObject(root);
                Debug.LogException(exception);
                return null;
            }
        }

        /// <summary>
        /// VFX Graphとインスタンス専用のGPU描画資源を準備します。
        /// </summary>
        private void Initialize(UnitEliminationProfile profile)
        {
            _profile = profile;
            _startedAt = Time.timeAsDouble;
            _seed = (uint)Guid.NewGuid().GetHashCode() % 4096;
            _material = new Material(profile.ProceduralMaterial) { hideFlags = HideFlags.HideAndDontSave };
            _texture = new RenderTexture(profile.TextureResolution, profile.TextureResolution, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                name = "Unit Elimination VFX Texture",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            if (!_texture.Create())
            {
                throw new InvalidOperationException("撃破VFXの描画テクスチャを生成できません。");
            }
            _visualEffect = gameObject.AddComponent<VisualEffect>();
            _visualEffect.visualEffectAsset = profile.EffectAsset;
            if (!_visualEffect.HasTexture("EffectTexture"))
            {
                throw new InvalidOperationException("撃破VFXにEffectTextureプロパティがありません。");
            }

            _visualEffect.SetTexture("EffectTexture", _texture);
            RenderAtTime(0.0);
            _visualEffect.Reinit();
        }

        /// <summary>
        /// ゲーム時間でVFXを再生し、完了後に削除します。
        /// </summary>
        private void Update()
        {
            if (!RenderAtTime(Time.timeAsDouble - _startedAt))
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 指定時刻の球体の半径と雷の長さ・太さをGPUへ渡します。
        /// </summary>
        /// <param name="elapsed">
        /// 演出開始から経過した秒数です。
        /// </param>
        /// <returns>
        /// 再生中の場合は<c>true</c>です。
        /// </returns>
        public bool RenderAtTime(double elapsed)
        {
            if (_profile == null || _material == null || _texture == null || _visualEffect == null)
            {
                return false;
            }

            UnitEliminationFrame frame = UnitEliminationTimeline.Evaluate(elapsed,
                _profile.InitialRadius, _profile.ExpandedRadius, _profile.ExpansionDuration,
                _profile.HoldDuration, _profile.CollapseDuration);
            float canvas = Mathf.Max(_profile.ExpandedRadius,
                _profile.LineLength + _profile.LightningAmplitude) * 2.2f + 1.0f;
            transform.localScale = Vector3.one * canvas;
            _material.SetFloat("_CanvasSize", canvas);
            _material.SetVector("_Sphere", new Vector4(frame.Radius, _profile.RimWidth, _profile.RimIntensity, frame.Opacity));
            _material.SetVector("_Line", new Vector4(_profile.LineLength * frame.LineScale,
                _profile.LineWidth * frame.LineScale, _profile.LightningAmplitude * frame.LineScale, _profile.LineIntensity));
            _material.SetVector("_Motion", new Vector4((float)Math.Max(0.0, elapsed),
                _profile.LightningRefreshRate, _profile.LineSegments, _seed));
            _material.SetColor("_BodyColor", _profile.BodyColor);
            _material.SetColor("_RimColor", _profile.RimColor);
            _material.SetColor("_LineRootColor", _profile.LineRootColor);
            _material.SetColor("_LineTipColor", _profile.LineTipColor);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(null, _texture, _material);
            }
            finally
            {
                RenderTexture.active = previous;
            }

            if (!Application.isPlaying)
            {
                _visualEffect.Simulate(1.0f / 60.0f, 1);
            }

            return !frame.IsComplete;
        }

        /// <summary>
        /// シーンやEditorプレビュー終了時にGPU資源を解放します。
        /// </summary>
        private void OnDestroy()
        {
            if (_texture != null)
            {
                _texture.Release();
            }

            ReleaseObject(_texture);
            ReleaseObject(_material);
        }

        /// <summary>
        /// 自身が所有する描画資源を現在の再生モードに合わせて破棄します。
        /// </summary>
        private static void ReleaseObject(UnityEngine.Object resource)
        {
            if (resource == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(resource);
            }
            else
            {
                DestroyImmediate(resource);
            }
        }
    }
}
