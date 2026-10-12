using UnityEngine;
using UnityEngine.UI;

namespace PranaTactics.UI.Screens.Title
{
    /// <summary>
    /// タイトル背景に重ねた霧を実時間でゆっくり移動させます。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RawImage))]
    public sealed class TitleFogAnimator : MonoBehaviour
    {
        /// <summary>
        /// 霧の経過時間を指定するシェーダープロパティです。
        /// </summary>
        private static readonly int s_fogTimeId = Shader.PropertyToID("_FogTime");

        /// <summary>
        /// 霧の最大不透明度を指定するシェーダープロパティです。
        /// </summary>
        private static readonly int s_opacityId = Shader.PropertyToID("_Opacity");

        /// <summary>
        /// 霧の移動速度を指定するシェーダープロパティです。
        /// </summary>
        private static readonly int s_driftSpeedId = Shader.PropertyToID("_DriftSpeed");

        /// <summary>
        /// 霧の形と濃さの変化速度を指定するシェーダープロパティです。
        /// </summary>
        private static readonly int s_evolutionSpeedId = Shader.PropertyToID("_EvolutionSpeed");

        /// <summary>
        /// 霧の最大不透明度です。
        /// </summary>
        [SerializeField]
        [Range(0.0f, 0.3f)]
        private float _opacity = 0.2f;

        /// <summary>
        /// 霧のノイズ座標が1秒あたりに移動する距離です。
        /// </summary>
        [SerializeField]
        [Range(0.0f, 0.12f)]
        private float _driftSpeed = 0.035f;

        /// <summary>
        /// 霧の形と濃淡が連続的に変わる速度です。
        /// </summary>
        [SerializeField]
        [Range(0.0f, 0.3f)]
        private float _evolutionSpeed = 0.08f;

        /// <summary>
        /// 霧を表示するUIです。
        /// </summary>
        private RawImage _fogImage;

        /// <summary>
        /// Inspectorに設定された共有マテリアルです。
        /// </summary>
        private Material _sourceMaterial;

        /// <summary>
        /// この画面だけが所有する霧のマテリアルです。
        /// </summary>
        private Material _runtimeMaterial;

        /// <summary>
        /// 画面を表示してからの実経過時間です。
        /// </summary>
        private float _elapsedTime;

        /// <summary>
        /// 共有アセットを変更せずに霧の描画を開始します。
        /// </summary>
        private void OnEnable()
        {
            _fogImage = GetComponent<RawImage>();
            _sourceMaterial = _fogImage.material;
            if (_sourceMaterial == null ||
                !_sourceMaterial.HasProperty(s_fogTimeId))
            {
                Debug.LogError("タイトルの霧マテリアルが設定されていません。", this);
                enabled = false;
                return;
            }

            _runtimeMaterial = new Material(_sourceMaterial);
            _fogImage.material = _runtimeMaterial;
            _elapsedTime = 0.0f;
            ApplyFogParameters();
        }

        /// <summary>
        /// ゲーム速度に影響されずに霧の描画時間を進めます。
        /// </summary>
        private void Update()
        {
            if (_runtimeMaterial == null)
            {
                return;
            }

            _elapsedTime += Time.unscaledDeltaTime;
            ApplyFogParameters();
        }

        /// <summary>
        /// Inspector設定と経過時間を描画へ反映します。
        /// </summary>
        private void ApplyFogParameters()
        {
            _runtimeMaterial.SetFloat(s_fogTimeId, _elapsedTime);
            _runtimeMaterial.SetFloat(s_opacityId, Mathf.Clamp(_opacity, 0.0f, 0.3f));
            _runtimeMaterial.SetFloat(s_driftSpeedId, Mathf.Clamp(_driftSpeed, 0.0f, 0.12f));
            _runtimeMaterial.SetFloat(s_evolutionSpeedId, Mathf.Clamp(_evolutionSpeed, 0.0f, 0.3f));
        }

        /// <summary>
        /// 画面を離れた際に所有マテリアルを解放します。
        /// </summary>
        private void OnDisable()
        {
            if (_runtimeMaterial == null)
            {
                return;
            }

            if (_fogImage != null)
            {
                _fogImage.material = _sourceMaterial;
            }

            Destroy(_runtimeMaterial);
            _runtimeMaterial = null;
        }
    }
}
