using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// 既存の選択モデルを専用カメラで描画し、画面内の表示領域へ収めます。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoloCharacterModelViewport : MonoBehaviour
    {
        /// <summary>
        /// ソロプレビューだけに使用する未割り当てレイヤーです。
        /// </summary>
        private const int k_PreviewLayer = 31;

        /// <summary>
        /// 背景と情報パネルの間へモデルを合成する画像です。
        /// </summary>
        [SerializeField]
        private RawImage _image;

        /// <summary>
        /// 主光源の色です。
        /// </summary>
        [Header("Key Light")]
        [SerializeField]
        private Color _keyLightColor = Color.white;

        /// <summary>
        /// 主光源の強さです。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _keyLightIntensity = 1.2f;

        /// <summary>
        /// 主光源の向きを描画ルートに対するオイラー角で指定します。
        /// </summary>
        [SerializeField]
        private Vector3 _keyLightRotation = new(35.0f, -30.0f, 0.0f);

        /// <summary>
        /// 補助光源の色です。
        /// </summary>
        [Header("Fill Light")]
        [SerializeField]
        private Color _fillLightColor = Color.white;

        /// <summary>
        /// 補助光源の強さです。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _fillLightIntensity = 0.65f;

        /// <summary>
        /// 補助光源の向きを描画ルートに対するオイラー角で指定します。
        /// </summary>
        [SerializeField]
        private Vector3 _fillLightRotation = new(15.0f, 145.0f, 0.0f);

        /// <summary>
        /// 作成済みの主光源です。
        /// </summary>
        private Light _keyLight;

        /// <summary>
        /// 作成済みの補助光源です。
        /// </summary>
        private Light _fillLight;

        /// <summary>
        /// Inspectorで変更された光源設定の反映が必要かどうかです。
        /// </summary>
        private bool _shouldApplyLighting;

        /// <summary>
        /// プレビューモデルだけを描画するカメラです。
        /// </summary>
        private Camera _camera;

        /// <summary>
        /// 画面の非表示時に解放する透過描画先です。
        /// </summary>
        private RenderTexture _texture;

        /// <summary>
        /// Canvasのスケールやレイアウトから独立した描画用ルートです。
        /// </summary>
        private GameObject _previewRoot;

        /// <summary>
        /// 描画ルートの解放時に生成元へ戻すモデルです。
        /// </summary>
        private GameObject _model;

        /// <summary>
        /// 選択画面が所有するモデルの元の親です。
        /// </summary>
        private Transform _modelParent;

        /// <summary>
        /// 透過背景の描画先と専用カメラを用意します。
        /// </summary>
        private void OnEnable()
        {
            _texture = new RenderTexture(960, 1056, 24, RenderTextureFormat.ARGB32);
            _texture.name = "Solo Character Preview";
            _texture.Create();
            _image.uvRect = new Rect(0.0f, 0.0f, 1.0f, 1.0f);
            _image.texture = _texture;
            _image.enabled = false;
            _previewRoot = new GameObject("Solo Character Preview Stage");
            _previewRoot.transform.position = new Vector3(0.0f, -1000.0f, 0.0f);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_previewRoot, gameObject.scene);
            var cameraObject = new GameObject("Preview Camera", typeof(Camera));
            cameraObject.transform.SetParent(_previewRoot.transform, false);
            _camera = cameraObject.GetComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.clear;
            _camera.cullingMask = 1 << k_PreviewLayer;
            _camera.fieldOfView = 28.0f;
            _camera.aspect = (float)_texture.width / _texture.height;
            _camera.targetTexture = _texture;
            _camera.enabled = false;
            _camera.allowHDR = false;
            UniversalAdditionalCameraData cameraData = _camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.renderShadows = false;
            cameraData.volumeLayerMask = 0;
            _keyLight = CreatePreviewLight("Key Light");
            _fillLight = CreatePreviewLight("Fill Light");
            ApplyLighting();
        }

        /// <summary>
        /// 生成済みモデルの描画範囲を測定して、全身を表示します。
        /// </summary>
        /// <remarks>
        /// 選択画面が所有するモデルだけを専用レイヤーへ移し、
        /// 背景カメラとの二重描画を防ぎます。
        /// </remarks>
        public void Show(GameObject model)
        {
            if (_camera == null)
            {
                return;
            }

            _image.enabled = false;
            _camera.enabled = false;

            if (model == null)
            {
                RestoreModelParent();
                return;
            }

            if (_model != model)
            {
                RestoreModelParent();
                _model = model;
                _modelParent = model.transform.parent;
            }

            model.transform.SetParent(_previewRoot.transform, true);
            model.transform.localPosition = Vector3.zero;
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = k_PreviewLayer;
            }

            float modelHeight = bounds.size.y;
            if (float.IsNaN(modelHeight) || float.IsInfinity(modelHeight) || modelHeight <= 0.0001f)
            {
                Debug.LogWarning("選択モデルの描画範囲を取得できません。", this);
                return;
            }

            model.transform.localScale *= 2.0f / modelHeight;
            bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            float halfHeight = Mathf.Max(bounds.extents.y, bounds.extents.x / _camera.aspect);
            float distance = halfHeight / Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * 1.12f + bounds.extents.z;
            _camera.transform.position = bounds.center - Vector3.forward * Mathf.Max(0.1f, distance);
            _camera.transform.rotation = Quaternion.identity;
            _camera.nearClipPlane = Mathf.Max(0.01f, distance * 0.001f);
            _camera.farClipPlane = Mathf.Max(10.0f, distance + bounds.size.magnitude * 2.0f);
            _image.enabled = true;
            _camera.enabled = true;
        }

        /// <summary>
        /// モデル専用の光源を描画ルートの下へ作成します。
        /// </summary>
        private Light CreatePreviewLight(string lightName)
        {
            var lightObject = new GameObject(lightName, typeof(Light));
            lightObject.transform.SetParent(_previewRoot.transform, false);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.cullingMask = 1 << k_PreviewLayer;
            light.shadows = LightShadows.None;
            return light;
        }

        /// <summary>
        /// Inspectorでの変更をUnityのメインスレッドで反映します。
        /// </summary>
        private void LateUpdate()
        {
            if (_shouldApplyLighting)
            {
                ApplyLighting();
            }
        }

        /// <summary>
        /// 作成済みの主光源と補助光源へ保存された設定を反映します。
        /// </summary>
        private void ApplyLighting()
        {
            ApplyLightSettings(_keyLight, _keyLightColor, _keyLightIntensity, _keyLightRotation);
            ApplyLightSettings(_fillLight, _fillLightColor, _fillLightIntensity, _fillLightRotation);
            _shouldApplyLighting = false;
        }

        /// <summary>
        /// 指定された光源の色、強さと向きを設定します。
        /// </summary>
        private static void ApplyLightSettings(Light light, Color color, float intensity, Vector3 rotation)
        {
            if (light == null)
            {
                return;
            }

            light.color = color;
            light.intensity = float.IsNaN(intensity) || float.IsInfinity(intensity)
                ? 0.0f
                : Mathf.Max(0.0f, intensity);
            light.transform.localRotation = Quaternion.Euler(rotation);
        }

        /// <summary>
        /// Inspectorで光源設定が変更されたことを記録します。
        /// </summary>
        private void OnValidate()
        {
            _shouldApplyLighting = true;
        }

        /// <summary>
        /// 生成元の所有権を維持したまま描画ルートからモデルを戻します。
        /// </summary>
        private void RestoreModelParent()
        {
            if (_model != null)
            {
                _model.transform.SetParent(_modelParent, true);
            }

            _model = null;
            _modelParent = null;
        }

        /// <summary>
        /// カメラと描画用テクスチャを解放します。
        /// </summary>
        private void OnDisable()
        {
            RestoreModelParent();
            _image.texture = null;
            _image.enabled = false;
            if (_camera != null)
            {
                _camera.targetTexture = null;
                _camera.enabled = false;
            }

            if (_previewRoot != null)
            {
                Destroy(_previewRoot);
                _previewRoot = null;
                _keyLight = null;
                _fillLight = null;
            }

            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
        }
    }
}
