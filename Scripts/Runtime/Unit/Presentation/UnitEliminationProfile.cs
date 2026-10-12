using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.VFX;

namespace PranaTactics.Unit.Presentation
{
    /// <summary>
    /// 撃破時の球体、縁、細線の見た目と再生時間を設定します。
    /// </summary>
    [CreateAssetMenu(fileName = "UnitElimination", menuName = "Prana Tactics/Unit/Elimination Profile")]
    public sealed class UnitEliminationProfile : ScriptableObject
    {
        /// <summary>
        /// 撃破演出を描画するVFX Graphです。
        /// </summary>
        [Header("VFX Assets")]
        [SerializeField]
        private VisualEffectAsset _effectAsset;

        /// <summary>
        /// 球体と雷を合成するGPU描画用マテリアルです。
        /// </summary>
        [SerializeField]
        private Material _proceduralMaterial;
        /// <summary>
        /// VFXテクスチャの解像度です。高いほど細線が鮮明になります。
        /// </summary>
        [SerializeField, Range(256, 2048)]
        private int _textureResolution = 1024;

        /// <summary>
        /// モデルの中心からずらすワールド座標の距離です。
        /// </summary>
        [Header("Sphere Size (World Units)")]
        [SerializeField]
        private Vector3 _centerOffset;

        /// <summary>
        /// 出現時の球体の半径です。直径はこの値の2倍です。
        /// </summary>
        [SerializeField, Min(0.0f)]
        private float _initialRadius = 15.0f;

        /// <summary>
        /// 急膨張後の球体の半径です。
        /// </summary>
        [SerializeField, Min(0.0f)]
        private float _expandedRadius = 90.0f;

        /// <summary>
        /// 急膨張にかける秒数です。
        /// </summary>
        [Header("Timing (Seconds)")]
        [SerializeField, Min(0.0f)]
        private float _expansionDuration = 0.18f;

        /// <summary>
        /// 最大の大きさを保つ秒数です。
        /// </summary>
        [SerializeField, Min(0.0f)]
        private float _holdDuration = 0.3f;

        /// <summary>
        /// 収縮して消えるまでの秒数です。
        /// </summary>
        [SerializeField, Min(0.0f)]
        private float _collapseDuration = 0.6f;

        /// <summary>
        /// 球体内部の色と透明度です。
        /// </summary>
        [Header("Sphere Colors")]
        [ColorUsage(true, true)]
        [SerializeField]
        private Color _bodyColor = new Color(0.003f, 0.002f, 0.008f, 1.0f);

        /// <summary>
        /// 球体の縁の色と透明度です。
        /// </summary>
        [ColorUsage(true, true)]
        [SerializeField]
        private Color _rimColor = new Color(0.03f, 0.014f, 0.065f, 1.0f);

        /// <summary>
        /// 球体の縁が占める幅です。値を大きくすると太くなります。
        /// </summary>
        [Tooltip("縁の相対的な幅です。大きくすると内側へ太くなります。ピクセル数ではありません。")]
        [SerializeField, Range(0.01f, 1.0f)]
        private float _rimWidth = 0.12f;

        /// <summary>
        /// 球体の縁の明るさ倍率です。
        /// </summary>
        [SerializeField, Min(0.0f)]
        private float _rimIntensity = 1.0f;

        /// <summary>
        /// 上下それぞれの細線の最大長です。球体の中心から測ります。
        /// </summary>
        [Header("Lines (World Units)")]
        [SerializeField, Min(0.0f)]
        [Tooltip("球体の中心から上下それぞれに伸びる長さです。ワールド単位で指定します。")]
        private float _lineLength = 360.0f;

        /// <summary>
        /// 細線の基準の太さです。場所ごとに不規則に変化し、先端では0へ細くなります。
        /// </summary>
        [SerializeField, Min(0.0f)]
        private float _lineWidth = 3.0f;

        /// <summary>
        /// 雷状の細線の折れを作る分割数です。
        /// </summary>
        [SerializeField, Range(8, 96)]
        private int _lineSegments = 18;

        /// <summary>
        /// 細線の根元の色と透明度です。
        /// </summary>
        [Header("Line Colors")]
        [ColorUsage(true, true)]
        [SerializeField]
        private Color _lineRootColor = new Color(0.03f, 0.014f, 0.065f, 1.0f);

        /// <summary>
        /// 細線の先端の色と透明度です。
        /// </summary>
        [ColorUsage(true, true)]
        [SerializeField]
        private Color _lineTipColor = new Color(0.018f, 0.008f, 0.04f, 0.0f);

        /// <summary>
        /// 細線全体の明るさ倍率です。
        /// </summary>
        [SerializeField, Min(0.0f)]
        private float _lineIntensity = 1.0f;

        /// <summary>
        /// 横方向へ揺れる最大距離です。
        /// </summary>
        [Header("Line Motion")]
        [FormerlySerializedAs("_waveAmplitude")]
        [SerializeField, Min(0.0f)]
        private float _lightningAmplitude = 12.0f;

        /// <summary>
        /// 雷状の細線の形状を1秒あたりに更新する回数です。0では形状を固定します。
        /// </summary>
        [FormerlySerializedAs("_waveSpeed")]
        [SerializeField, Min(0.0f)]
        private float _lightningRefreshRate = 12.0f;

        public VisualEffectAsset EffectAsset => _effectAsset;
        public Material ProceduralMaterial => _proceduralMaterial;
        public int TextureResolution => Mathf.ClosestPowerOfTwo(Mathf.Clamp(_textureResolution, 256, 2048));
        public Vector3 CenterOffset => _centerOffset;
        public float InitialRadius => NonNegative(_initialRadius);
        public float ExpandedRadius => Mathf.Max(InitialRadius, NonNegative(_expandedRadius));
        public float ExpansionDuration => NonNegative(_expansionDuration);
        public float HoldDuration => NonNegative(_holdDuration);
        public float CollapseDuration => NonNegative(_collapseDuration);
        public Color BodyColor => _bodyColor;
        public Color RimColor => _rimColor;
        public float RimWidth => Mathf.Clamp(NonNegative(_rimWidth), 0.01f, 1.0f);
        public float RimIntensity => NonNegative(_rimIntensity);
        public float LineLength => NonNegative(_lineLength);
        public float LineWidth => NonNegative(_lineWidth);
        public int LineSegments => Mathf.Clamp(_lineSegments, 8, 96);
        public Color LineRootColor => _lineRootColor;
        public Color LineTipColor => _lineTipColor;
        public float LineIntensity => NonNegative(_lineIntensity);
        public float LightningAmplitude => NonNegative(_lightningAmplitude);
        public float LightningRefreshRate => NonNegative(_lightningRefreshRate);

        /// <summary>
        /// 不正値を除外して安全な非負値を取得します。
        /// </summary>
        private static float NonNegative(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0.0f : Mathf.Max(0.0f, value);
        }
    }
}
