using UnityEngine;
using UnityEngine.Rendering;

using PranaTactics.Pranam;

namespace PranaTactics.Prana
{
    /// <summary>
    /// 球状の黄色い閃光が炎へ変わり、煙を残して消えるイラプトの爆発を描画します。
    /// </summary>
    internal static class ShooterExplosionVfx
    {
        private enum ExplosionStage
        {
            Fire,
            Sparks
        }

        /// <summary>
        /// ビルドでも参照を保持する爆発専用マテリアルのResourcesパスです。
        /// </summary>
        private const string k_MaterialPath = "VFX/EruptExplosion";

        /// <summary>
        /// 戦闘中のUnitカメラが描画するエフェクトのレイヤー名です。
        /// </summary>
        private const string k_EffectLayerName = "Trigger";

        /// <summary>
        /// 全ての爆発で共有する描画マテリアルです。
        /// </summary>
        private static Material s_material;

        /// <summary>
        /// 着弾地点を中心に、球体を基準とする粒子の爆発と火花を生成します。
        /// </summary>
        public static void Play(Vector3 origin, ShooterExplosionSettings settings)
        {
            int effectLayer = LayerMask.NameToLayer(k_EffectLayerName);
            if (effectLayer < 0)
            {
                Debug.LogError("イラプトの爆発VFX用のTriggerレイヤーが見つかりません。");
                return;
            }

            if (s_material == null)
            {
                s_material = Resources.Load<Material>(k_MaterialPath);
            }

            if (s_material == null)
            {
                Debug.LogError("イラプトの爆発マテリアルが見つかりません。");
                return;
            }

            var root = new GameObject("Erupt Explosion");
            root.layer = effectLayer;
            root.transform.position = origin;
            CreateBurst(root.transform, settings, ExplosionStage.Fire);
            CreateBurst(root.transform, settings, ExplosionStage.Sparks);
            if (Application.isPlaying)
            {
                Object.Destroy(root, settings.VfxLifetime + 0.1f);
            }
        }

        /// <summary>
        /// 粒子ごとの速度、サイズと寿命にばらつきを持つ球状のバーストを作成します。
        /// </summary>
        /// <remarks>
        /// 炎と煙は同じ粒子を使用し、シェーダーが爆心から遠い部分から煙へ変えます。
        /// 初期の拡散後は移動を緩め、同じ輪郭のまま煙を残します。
        /// </remarks>
        private static void CreateBurst(Transform parent, ShooterExplosionSettings settings, ExplosionStage stage)
        {
            bool sparks = stage == ExplosionStage.Sparks;
            float duration = sparks ? settings.VfxDuration : settings.VfxLifetime;
            float expansionEnd = settings.VfxDuration / duration;
            var effect = new GameObject(stage.ToString());
            effect.layer = parent.gameObject.layer;
            effect.transform.SetParent(parent, false);
            ParticleSystem particles = effect.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = duration;
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                duration * (sparks ? 0.65f : 1.0f), duration);
            float speed = settings.Radius / settings.VfxDuration;
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                speed * 0.4f, speed * 0.85f);
            main.startSize = new ParticleSystem.MinMaxCurve(
                settings.Radius * (sparks ? 0.015f : 0.18f),
                settings.Radius * (sparks ? 0.04f : 0.38f));
            main.startRotation = new ParticleSystem.MinMaxCurve(0.0f, Mathf.PI * 2.0f);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 36;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0.0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.0f, (short)main.maxParticles) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = settings.Radius * 0.05f;
            shape.radiusThickness = 1.0f;

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = CreateColorGradient(settings, stage);

            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1.0f, sparks
                ? AnimationCurve.EaseInOut(0.0f, 0.3f, 1.0f, 0.0f)
                : new AnimationCurve(new Keyframe(0.0f, 0.3f),
                    new Keyframe(expansionEnd, 1.6f), new Keyframe(1.0f, 1.9f)));

            if (!sparks)
            {
                ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = CreateSmokeDrift(settings, -1.0f, 0.5f);
                velocity.y = CreateSmokeDrift(settings, -0.1f, 1.2f);
                velocity.z = CreateSmokeDrift(settings, -0.35f, 1.0f);
                velocity.speedModifier = new ParticleSystem.MinMaxCurve(1.0f,
                    new AnimationCurve(new Keyframe(0.0f, 1.0f),
                        new Keyframe(expansionEnd, 0.12f), new Keyframe(1.0f, 0.05f)));
                ConfigureSmokeNoise(particles, settings);
            }

            ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
            rotation.enabled = !sparks;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

            ParticleSystemRenderer renderer = effect.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = s_material;
            renderer.renderMode = sparks ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.lengthScale = sparks ? 2.5f : 1.0f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var properties = new MaterialPropertyBlock();
            properties.SetFloat("_UseSmokeFront", sparks ? 0.0f : 1.0f);
            properties.SetVector("_ExplosionOrigin", parent.position);
            properties.SetVector("_ExplosionTiming", new Vector4(
                Time.timeSinceLevelLoad, settings.VfxDuration, settings.SmokeDuration, settings.Radius));
            properties.SetVector("_SmokeTiming", new Vector4(
                settings.SmokeStartDelay, settings.SmokeTransitionDuration, 0.0f, 0.0f));
            properties.SetVector("_SmokeAlphaTiming", new Vector4(
                settings.SmokeFadeInDuration, settings.SmokeFadeOutDuration, settings.VfxLifetime, 0.0f));
            properties.SetFloat("_SmokeIrregularity", settings.SmokeIrregularity);
            properties.SetColor("_FlashColor", settings.FlashColor);
            properties.SetColor("_FireColor", settings.VfxColor);
            properties.SetColor("_SmokeColor", settings.SmokeColor);
            renderer.SetPropertyBlock(properties);
            particles.Play();
        }

        /// <summary>
        /// 初期の拡散から粒子ごとに異なる流れを加え、煙が現れる前に均一な広がりを崩します。
        /// </summary>
        private static ParticleSystem.MinMaxCurve CreateSmokeDrift(
            ShooterExplosionSettings settings, float minimum, float maximum)
        {
            float expansionEnd = settings.VfxDuration / settings.VfxLifetime;
            AnimationCurve minimumCurve = CreateSmokeDriftCurve(expansionEnd, minimum);
            AnimationCurve maximumCurve = CreateSmokeDriftCurve(expansionEnd, maximum);
            float driftSpeed = settings.Radius / settings.VfxDuration * 3.0f * settings.SmokeIrregularity;
            return new ParticleSystem.MinMaxCurve(driftSpeed, minimumCurve, maximumCurve);
        }

        /// <summary>
        /// 初期の拡散中に流れを滑らかに加え、その後は加速せずに弱める曲線を作成します。
        /// </summary>
        private static AnimationCurve CreateSmokeDriftCurve(float expansionEnd, float speed)
        {
            return new AnimationCurve(
                new Keyframe(0.0f, 0.0f),
                new Keyframe(expansionEnd, speed),
                new Keyframe(1.0f, speed * 0.8f));
        }

        /// <summary>
        /// 初期の拡散から空間ノイズで位置、回転と大きさを崩し、煙への移行後の急な変形を防ぎます。
        /// </summary>
        private static void ConfigureSmokeNoise(ParticleSystem particles, ShooterExplosionSettings settings)
        {
            float expansionEnd = settings.VfxDuration / settings.VfxLifetime;
            var strength = new AnimationCurve(
                new Keyframe(0.0f, 0.0f),
                new Keyframe(expansionEnd, 0.85f),
                new Keyframe(1.0f, 1.0f));

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = settings.SmokeIrregularity > 0.0f;
            noise.strength = new ParticleSystem.MinMaxCurve(
                settings.Radius * 0.45f * settings.SmokeIrregularity, strength);
            noise.frequency = 3.5f / settings.Radius;
            noise.scrollSpeed = 0.25f;
            noise.damping = false;
            noise.octaveCount = 3;
            noise.octaveMultiplier = 0.5f;
            noise.octaveScale = 2.0f;
            noise.quality = ParticleSystemNoiseQuality.High;
            noise.positionAmount = 1.0f;
            noise.rotationAmount = 0.6f * settings.SmokeIrregularity;
            noise.sizeAmount = 0.5f * settings.SmokeIrregularity;
        }

        /// <summary>
        /// 火花の色と、炎から煙まで継続する爆発粒子の透明度を設定します。
        /// </summary>
        private static Gradient CreateColorGradient(ShooterExplosionSettings settings, ExplosionStage stage)
        {
            var gradient = new Gradient();
            if (stage == ExplosionStage.Fire)
            {
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(Color.white, 0.0f),
                        new GradientColorKey(Color.white, 1.0f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(1.0f, 0.0f),
                        new GradientAlphaKey(1.0f, 1.0f)
                    });
                return gradient;
            }

            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(settings.FlashColor, 0.0f),
                    new GradientColorKey(settings.VfxColor, 0.25f),
                    new GradientColorKey(settings.VfxColor, 0.85f)
                },
                new[]
                {
                    new GradientAlphaKey(settings.FlashColor.a, 0.0f),
                    new GradientAlphaKey(settings.VfxColor.a * 0.9f, 0.25f),
                    new GradientAlphaKey(0.0f, 1.0f)
                });
            return gradient;
        }
    }
}
