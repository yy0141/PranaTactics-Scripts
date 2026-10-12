using System;
using Newtonsoft.Json;
using UnityEngine;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// 着弾爆発の範囲、距離減衰と演出設定を保持します。
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ShooterExplosionSettings
    {
        /// <summary>
        /// 着弾時に爆発するかどうかです。
        /// </summary>
        [SerializeField]
        [JsonProperty]
        private bool _enabled;

        /// <summary>
        /// 爆風が届くワールド座標での半径です。
        /// </summary>
        [Min(0.01f)]
        [SerializeField]
        [JsonProperty]
        private float _radius = 5.0f;

        /// <summary>
        /// 半径に相当する距離で失われる威力の割合です。
        /// </summary>
        [Range(0.0f, 1.0f)]
        [SerializeField]
        [JsonProperty]
        private float _falloffRatio = 1.0f;

        /// <summary>
        /// 発光する爆発粒子の色です。
        /// </summary>
        [ColorUsage(true, true)]
        [SerializeField]
        private Color _vfxColor = new Color(2.0f, 0.65f, 0.12f, 1.0f);

        /// <summary>
        /// 爆発粒子が消えるまでのゲーム内秒数です。
        /// </summary>
        [Min(0.05f)]
        [SerializeField]
        [JsonProperty]
        private float _vfxDuration = 0.45f;

        /// <summary>
        /// 爆発直後の黄色い閃光の色です。
        /// </summary>
        [ColorUsage(true, true)]
        [SerializeField]
        private Color _flashColor = new Color(2.0f, 1.65f, 0.3f, 1.0f);

        /// <summary>
        /// 火球が冷めた後に残る煙の色です。
        /// </summary>
        [SerializeField]
        private Color _smokeColor = new Color(0.18f, 0.16f, 0.14f, 0.8f);

        /// <summary>
        /// 火球の後に煙が残るゲーム内秒数です。
        /// </summary>
        [Min(0.05f)]
        [SerializeField]
        [JsonProperty]
        private float _smokeDuration = 1.2f;

        /// <summary>
        /// 爆発してから外周の煙への変化を開始するまでのゲーム内秒数です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        [JsonProperty]
        private float _smokeStartDelay = 0.2925f;

        /// <summary>
        /// 外周から中心まで煙への変化が進むゲーム内秒数です。
        /// </summary>
        [Min(0.05f)]
        [SerializeField]
        [JsonProperty]
        private float _smokeTransitionDuration = 0.5175f;

        /// <summary>
        /// 煙の移行開始から最大Alphaまで濃くなるゲーム内秒数です。
        /// </summary>
        [Min(0.05f)]
        [SerializeField]
        [JsonProperty]
        private float _smokeFadeInDuration = 0.6f;

        /// <summary>
        /// 演出の終了前に煙のAlphaをゼロまで下げるゲーム内秒数です。
        /// </summary>
        [Min(0.05f)]
        [SerializeField]
        [JsonProperty]
        private float _smokeFadeOutDuration = 1.0f;

        /// <summary>
        /// 煙の粒子の流れと輪郭に与える不規則さの強さです。
        /// </summary>
        [Range(0.0f, 1.0f)]
        [SerializeField]
        [JsonProperty]
        private float _smokeIrregularity = 0.65f;

        [JsonProperty]
        private float ColorR { get => _vfxColor.r; set => _vfxColor.r = value; }

        [JsonProperty]
        private float ColorG { get => _vfxColor.g; set => _vfxColor.g = value; }

        [JsonProperty]
        private float ColorB { get => _vfxColor.b; set => _vfxColor.b = value; }

        [JsonProperty]
        private float ColorA { get => _vfxColor.a; set => _vfxColor.a = value; }

        [JsonProperty]
        private float FlashR { get => _flashColor.r; set => _flashColor.r = value; }

        [JsonProperty]
        private float FlashG { get => _flashColor.g; set => _flashColor.g = value; }

        [JsonProperty]
        private float FlashB { get => _flashColor.b; set => _flashColor.b = value; }

        [JsonProperty]
        private float FlashA { get => _flashColor.a; set => _flashColor.a = value; }

        [JsonProperty]
        private float SmokeR { get => _smokeColor.r; set => _smokeColor.r = value; }

        [JsonProperty]
        private float SmokeG { get => _smokeColor.g; set => _smokeColor.g = value; }

        [JsonProperty]
        private float SmokeB { get => _smokeColor.b; set => _smokeColor.b = value; }

        [JsonProperty]
        private float SmokeA { get => _smokeColor.a; set => _smokeColor.a = value; }

        public bool Enabled => _enabled;
        public float Radius => Mathf.Max(0.01f, _radius);
        public float FalloffRatio => Mathf.Clamp01(_falloffRatio);
        public Color VfxColor => _vfxColor;
        public float VfxDuration => Mathf.Max(0.05f, _vfxDuration);
        public Color FlashColor => _flashColor;
        public Color SmokeColor => _smokeColor;
        public float SmokeDuration => Mathf.Max(0.05f, _smokeDuration);
        public float SmokeStartDelay => Mathf.Max(0.0f, _smokeStartDelay);
        public float SmokeTransitionDuration => Mathf.Max(0.05f, _smokeTransitionDuration);
        public float SmokeFadeInDuration => Mathf.Max(0.05f, _smokeFadeInDuration);
        public float SmokeFadeOutDuration => Mathf.Max(0.05f, _smokeFadeOutDuration);
        public float SmokeIrregularity => Mathf.Clamp01(_smokeIrregularity);
        public float VfxLifetime => Mathf.Max(VfxDuration, SmokeStartDelay + SmokeTransitionDuration) + SmokeDuration;

        /// <summary>
        /// 発射後のアセット編集の影響を受けない設定の複製を返します。
        /// </summary>
        public ShooterExplosionSettings CreateSnapshot()
        {
            return (ShooterExplosionSettings)MemberwiseClone();
        }

        /// <summary>
        /// 爆心からの距離に応じた爆風ダメージを計算します。
        /// </summary>
        /// <remarks>
        /// 半径内は線形に減衰し、半径を超えた対象へはダメージを与えません。
        /// 直撃はこの計算を使用せず、弾の攻撃力をそのまま使用します。
        /// </remarks>
        public float CalculateDamage(float attack, float distance)
        {
            if (!Enabled || float.IsNaN(attack) || float.IsInfinity(attack) ||
                float.IsNaN(distance) || float.IsInfinity(distance) || distance > Radius)
            {
                return 0.0f;
            }

            return Mathf.Max(0.0f, attack) *
                (1.0f - Mathf.Clamp01(distance / Radius) * FalloffRatio);
        }
    }
}
