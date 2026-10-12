using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

using PranaTactics.Prana;
using PranaTactics.Pranam;
using PranaTactics.Unit;

namespace PranaTactics.Editor.Validation
{
    /// <summary>
    /// 実際の物理Colliderを使用し、イラプトの減衰、直撃とシールド吸収を検証します。
    /// </summary>
    public static class ShooterExplosionValidation
    {
        /// <summary>
        /// 通常のシーンから離れた検証領域の中心です。
        /// </summary>
        private static readonly Vector3 s_origin = new Vector3(10000.0f, 10000.0f, 10000.0f);

        /// <summary>
        /// 今回の検証で生成した一時オブジェクトです。
        /// </summary>
        private static readonly List<GameObject> s_objects = new();

        /// <summary>
        /// アセット、通信往復、球状の判定と各シールドの防御を検証します。
        /// </summary>
        [MenuItem("Prana Tactics/Validation/Validate Erupt Explosion")]
        public static void Run()
        {
            ShooterPranamData validationData = null;
            try
            {
                ShooterPranamData data = AssetDatabase.LoadAssetAtPath<ShooterPranamData>(
                    "Assets/Datas/TriggerAssets/Shooter/Erupt.asset");
                Check(data != null && data.Explosion.Enabled, "イラプトの設定がありません。");
                Check(data.PrefabResourcePath == "Trion/Shooter/Erupt", "イラプト専用の弾Prefabを使用していません。");
                Check(data.PranamPrefab != null &&
                    data.PranamPrefab == Resources.Load<GameObject>(data.PrefabResourcePath) &&
                    data.PranamPrefab.GetComponent<Erupt>() != null &&
                    data.PranamPrefab.GetComponent<Gerade>() == null,
                    "弾Prefabの参照または射出コンポーネントが不正です。");
                ValidateProjectileInitialization(data);
                validationData = UnityEngine.Object.Instantiate(data);
                var validationSettings = new SerializedObject(validationData);
                validationSettings.FindProperty("_explosion._radius").floatValue = 5.0f;
                validationSettings.FindProperty("_explosion._falloffRatio").floatValue = 1.0f;
                validationSettings.ApplyModifiedPropertiesWithoutUndo();
                ValidateSettings(validationData);
                ValidateSphere(validationData);
                ValidateShield(validationData, true, 150.0f, false, false);
                ValidateShield(validationData, true, 25.0f, false, false);
                ValidateShield(validationData, false, 150.0f, false, false);
                ValidateShield(validationData, false, 25.0f, false, false);
                ValidateShield(validationData, true, 150.0f, true, false);
                ValidateShield(validationData, true, 150.0f, true, true);
                ValidateShield(validationData, false, 150.0f, true, false);
                ValidateShield(validationData, false, 25.0f, true, false);
                ValidateVfx(data);
                Debug.Log("ERUPT VALIDATION PASSED: settings, JSON, sphere, direct hit, deduplication, allies, Scutum, Parma, VFX.");
            }
            finally
            {
                ClearObjects();
                if (validationData != null)
                {
                    UnityEngine.Object.DestroyImmediate(validationData);
                }
            }
        }

        /// <summary>
        /// 減衰設定、発射時の複製とJSON通信往復を検証します。
        /// </summary>
        private static void ValidateSettings(ShooterPranamData data)
        {
            ShooterExplosionSettings settings = data.Explosion;
            Near(settings.CalculateDamage(100.0f, 0.0f), 100.0f, "爆心威力");
            Near(settings.CalculateDamage(100.0f, 2.5f), 50.0f, "半径中央の減衰");
            Near(settings.CalculateDamage(100.0f, 5.1f), 0.0f, "範囲外");
            var context = new PranamContext { Attack = 100.0f, Explosion = settings.CreateSnapshot() };
            PranamContext restored = JsonConvert.DeserializeObject<PranamContext>(JsonConvert.SerializeObject(context));
            Check(restored.Explosion.Enabled, "通信で爆発設定が失われました。");
            Near(restored.Explosion.Radius, settings.Radius, "通信後の半径");
            Near(restored.Explosion.VfxColor.r, settings.VfxColor.r, "通信後のHDR色");
            Near(restored.Explosion.FlashColor.g, settings.FlashColor.g, "通信後の閃光色");
            Near(restored.Explosion.SmokeColor.a, settings.SmokeColor.a, "通信後の煙の透明度");
            Near(restored.Explosion.SmokeDuration, settings.SmokeDuration, "通信後の煙の寿命");
            Near(restored.Explosion.SmokeStartDelay, settings.SmokeStartDelay, "通信後の煙の開始時間");
            Near(restored.Explosion.SmokeTransitionDuration, settings.SmokeTransitionDuration, "通信後の煙の移行時間");
            Near(restored.Explosion.SmokeFadeInDuration, settings.SmokeFadeInDuration, "通信後の煙の濃くなる時間");
            Near(restored.Explosion.SmokeFadeOutDuration, settings.SmokeFadeOutDuration, "通信後の煙の薄くなる時間");
            Near(restored.Explosion.SmokeIrregularity, settings.SmokeIrregularity, "通信後の煙の不規則さ");
            Check(!ReferenceEquals(context.Explosion, settings), "発射時の設定が共有されています。");
            var edited = new SerializedObject(data);
            float original = settings.FalloffRatio;
            try
            {
                edited.FindProperty("_explosion._falloffRatio").floatValue = 0.5f;
                edited.ApplyModifiedPropertiesWithoutUndo();
                Near(data.Explosion.CalculateDamage(100.0f, 5.0f), 50.0f, "減衰率の編集");
                Near(context.Explosion.FalloffRatio, original, "発射後の設定の固定");
            }
            finally
            {
                edited.Update();
                edited.FindProperty("_explosion._falloffRatio").floatValue = original;
                edited.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// 専用Prefabが爆発設定なしの生成情報でも初期化でき、通常の地面へ着弾することを検証します。
        /// </summary>
        private static void ValidateProjectileInitialization(ShooterPranamData data)
        {
            ClearObjects();
            GameObject instance = UnityEngine.Object.Instantiate(data.PranamPrefab, s_origin, Quaternion.identity);
            s_objects.Add(instance);
            Erupt projectile = instance.GetComponent<Erupt>();
            PranamBehavior behavior = CreateObject("Behavior", Vector3.zero).AddComponent<PranamBehavior>();
            var context = new PranamContext { PlayerId = 0, Attack = 100.0f, GeneratePosition = s_origin };
            projectile.Initialize(behavior, context);
            PranamContext runtimeState = (PranamContext)typeof(PranaBase)
                .GetProperty("Context", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(projectile);
            Check(context.Explosion == null, "Eruptの初期化で生成情報を書き換えています。");
            Check(runtimeState.Explosion != null && runtimeState.Explosion.Enabled,
                "Eruptの初期化で爆発設定が確定していません。");
            Check(!ReferenceEquals(runtimeState.Explosion, data.Explosion), "Prefabの設定が複製されていません。");
            Near(runtimeState.Explosion.Radius, data.Explosion.Radius, "専用Prefabの爆発半径");

            Collider surface = CreateObject("Ground", Vector3.forward).AddComponent<BoxCollider>();
            MethodInfo canExplode = typeof(Erupt).GetMethod("CanExplodeOnCollider",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Check((bool)canExplode.Invoke(projectile, new object[] { surface }),
                "通常の地形Colliderへ着弾しても爆発しません。");
            surface.isTrigger = true;
            Check(!(bool)canExplode.Invoke(projectile, new object[] { surface }),
                "検出用のTriggerへ爆発しています。");

            ShooterExplosionSettings received = context.Explosion.CreateSnapshot();
            context.Explosion = received;
            projectile.Initialize(behavior, context);
            Check(ReferenceEquals(context.Explosion, received), "受信済みの爆発設定を上書きしています。");
        }

        /// <summary>
        /// 上下を含む球状範囲、直撃の満額と複数Colliderの集約を検証します。
        /// </summary>
        private static void ValidateSphere(ShooterPranamData data)
        {
            ClearObjects();
            Collider direct = CreateTarget(1, Vector3.zero);
            direct.gameObject.AddComponent<SphereCollider>().radius = 0.1f;
            CreateTarget(2, Vector3.up * 2.5f);
            CreateTarget(3, Vector3.down * 2.5f);
            CreateTarget(4, Vector3.right * 5.5f);
            CreateTarget(0, Vector3.forward);
            Dictionary<int, float> hits = Explode(data, direct, null);
            Near(hits[1], 100.0f, "直撃");
            Near(hits[2], 52.0f, "上方への爆風");
            Near(hits[3], 52.0f, "下方への爆風");
            Check(hits.Count == 3 && !hits.ContainsKey(0) && !hits.ContainsKey(4), "味方または範囲外へ命中しました。");
        }

        /// <summary>
        /// スクトゥムの全方向防御、パルマの向きと破壊時の残余ダメージを検証します。
        /// </summary>
        private static void ValidateShield(ShooterPranamData data, bool directHit,
            float health, bool useParma, bool reverse)
        {
            ClearObjects();
            CreateTarget(1, Vector3.forward * 2.0f);
            GameObject shieldObject = CreateObject("Shield", Vector3.forward);
            ShieldPranaBase shield;
            if (useParma)
            {
                BoxCollider box = shieldObject.AddComponent<BoxCollider>();
                box.size = new Vector3(2.0f, 2.0f, 0.2f);
                shieldObject.transform.forward = reverse ? Vector3.forward : Vector3.back;
                shield = shieldObject.AddComponent<Parma>();
            }
            else
            {
                shieldObject.AddComponent<SphereCollider>().radius = 1.2f;
                shield = shieldObject.AddComponent<Scutum>();
            }

            PranamBehavior behavior = CreateObject("Behavior", Vector3.zero).AddComponent<PranamBehavior>();
            shield.Initialize(behavior, new PranamContext { PlayerId = 1, Defense = health, IsMain = true });
            Dictionary<int, float> hits = Explode(data, null, directHit && !reverse ? shield : null);
            float blastDamage = 62.0f;
            float expected = useParma && reverse ? blastDamage : Mathf.Max(0.0f, blastDamage - health);
            Near(hits.TryGetValue(1, out float actual) ? actual : 0.0f, expected,
                $"{(useParma ? "Parma" : "Scutum")}, direct={directHit}, health={health}, reverse={reverse}");
            float shieldDamage = reverse ? 0.0f : directHit ? 100.0f : useParma ? 82.0f : 100.0f;
            Near(shield.GetPranaHealth(), Mathf.Max(0.0f, health - shieldDamage), "シールドへの一回のダメージ");
        }

        /// <summary>
        /// 実装のResolverに物理シーンと記録用コールバックを渡します。
        /// </summary>
        private static Dictionary<int, float> Explode(ShooterPranamData data,
            Collider direct, ShieldPranaBase shield)
        {
            Physics.SyncTransforms();
            var hits = new Dictionary<int, float>();
            Action<int, float> applyHit = (id, damage) =>
            {
                Check(!hits.ContainsKey(id), "同じユニットへの二重ダメージです。");
                hits.Add(id, damage);
            };
            Type type = typeof(ShooterPranaBase).Assembly.GetType("PranaTactics.Prana.ShooterExplosionResolver", true);
            object resolver = Activator.CreateInstance(type,
                new PranamContext { PlayerId = 0, Attack = 100.0f, Explosion = data.Explosion.CreateSnapshot() },
                applyHit, new Func<int, bool>(id => id == 0), 0.0f);
            type.GetMethod("Explode").Invoke(resolver, new object[] { s_origin, Vector3.forward, direct, shield });
            return hits;
        }

        /// <summary>
        /// 新規マテリアルのシェーダーと球状粒子の生成を検証します。
        /// </summary>
        private static void ValidateVfx(ShooterPranamData data)
        {
            Material material = Resources.Load<Material>("VFX/EruptExplosion");
            Check(material != null && material.shader != null && !ShaderUtil.ShaderHasError(material.shader),
                "爆発シェーダーにエラーがあります。");
            Type type = typeof(ShooterPranaBase).Assembly.GetType("PranaTactics.Prana.ShooterExplosionVfx", true);
            type.GetMethod("Play").Invoke(null, new object[] { s_origin, data.Explosion });
            GameObject root = GameObject.Find("Erupt Explosion");
            Check(root != null, "VFXが生成されませんでした。");
            s_objects.Add(root);
            int effectLayer = LayerMask.NameToLayer("Trigger");
            Check(effectLayer >= 0 && root.layer == effectLayer,
                "爆発VFXが戦闘カメラに表示されるレイヤーではありません。");
            ValidateVfxCameras(effectLayer);
            ParticleSystem[] particles = root.GetComponentsInChildren<ParticleSystem>();
            Check(particles.Length == 2, "炎と煙を兼ねる粒子、または火花が生成されませんでした。");
            foreach (ParticleSystem particle in particles)
            {
                Check(particle.gameObject.layer == effectLayer,
                    "爆発VFXの子粒子が戦闘カメラの描画レイヤーではありません。");
                Check(particle.shape.shapeType == ParticleSystemShapeType.Sphere, "球体を基準としたVFXではありません。");
                particle.Simulate(particle.main.startDelay.constantMax + 0.1f, true, true);
                Check(particle.particleCount > 0, "爆発粒子がありません。");
                if (particle.name == "Fire")
                {
                    Near(particle.main.startLifetime.constantMax,
                        data.Explosion.VfxLifetime, "炎から煙までの寿命");
                    var properties = new MaterialPropertyBlock();
                    particle.GetComponent<ParticleSystemRenderer>().GetPropertyBlock(properties);
                    Near(properties.GetFloat("_UseSmokeFront"), 1.0f, "外周からの煙の進行");
                    Near(properties.GetFloat("_SmokeIrregularity"), data.Explosion.SmokeIrregularity, "煙の輪郭の不規則さ");
                    Check(particle.noise.enabled == (data.Explosion.SmokeIrregularity > 0.0f),
                        "煙の流れのノイズが設定と一致しません。");
                    Vector4 smokeTiming = properties.GetVector("_SmokeTiming");
                    Near(smokeTiming.x, data.Explosion.SmokeStartDelay, "描画へ渡す煙の開始時間");
                    Near(smokeTiming.y, data.Explosion.SmokeTransitionDuration, "描画へ渡す煙の移行時間");
                    Near(properties.GetColor("_FlashColor").g, data.Explosion.FlashColor.g, "黄色い閃光");
                    Near(properties.GetColor("_SmokeColor").r, data.Explosion.SmokeColor.r, "煙の色");
                    Vector4 smokeAlphaTiming = properties.GetVector("_SmokeAlphaTiming");
                    Near(smokeAlphaTiming.x, data.Explosion.SmokeFadeInDuration, "煙のAlpha増加時間");
                    Near(smokeAlphaTiming.y, data.Explosion.SmokeFadeOutDuration, "煙のAlpha減少時間");
                    Near(smokeAlphaTiming.z, data.Explosion.VfxLifetime, "煙のAlphaがゼロになる時間");
                }
            }
        }

        /// <summary>
        /// 実際のUnitカメラPrefabが爆発VFXのレイヤーを描画することを検証します。
        /// </summary>
        private static void ValidateVfxCameras(int effectLayer)
        {
            int cameraCount = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources" }))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (MicroUnitBehavior micro in prefab.GetComponentsInChildren<MicroUnitBehavior>(true))
                {
                    Camera camera = micro.UnitCamera;
                    Check(camera != null && (camera.cullingMask & (1 << effectLayer)) != 0,
                        $"Unitカメラが爆発VFXを描画しません。Prefab={prefab.name}");
                    cameraCount++;
                }
            }

            Check(cameraCount > 0, "爆発VFXの描画を検証するUnitカメラが見つかりません。");
        }

        /// <summary>
        /// 指定IDを持つ球状のダメージ対象を作成します。
        /// </summary>
        private static Collider CreateTarget(int id, Vector3 offset)
        {
            GameObject target = CreateObject("Target", offset);
            target.AddComponent<ShooterExplosionValidationTarget>().PlayerId = id;
            SphereCollider collider = target.AddComponent<SphereCollider>();
            collider.radius = 0.1f;
            return collider;
        }

        /// <summary>
        /// 検証領域へ一時オブジェクトを作成します。
        /// </summary>
        private static GameObject CreateObject(string name, Vector3 offset)
        {
            var instance = new GameObject(name);
            instance.transform.position = s_origin + offset;
            s_objects.Add(instance);
            return instance;
        }

        /// <summary>
        /// 例外発生時も検証用オブジェクトを全て破棄します。
        /// </summary>
        private static void ClearObjects()
        {
            foreach (GameObject instance in s_objects)
            {
                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            s_objects.Clear();
        }

        /// <summary>
        /// 浮動小数点の誤差を許容して期待値と比較します。
        /// </summary>
        private static void Near(float actual, float expected, string label)
        {
            Check(Mathf.Abs(actual - expected) < 0.05f, $"{label}: expected={expected}, actual={actual}");
        }

        /// <summary>
        /// 検証に失敗した場合はバッチ実行を失敗させます。
        /// </summary>
        private static void Check(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
