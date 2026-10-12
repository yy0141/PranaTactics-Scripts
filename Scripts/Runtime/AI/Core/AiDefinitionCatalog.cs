using System;
using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Pranam;
using PranaTactics.Unit;

namespace PranaTactics.AI.Solo
{
    public interface IAiCharacterDefinitionSource
    {
        string AiCharacterId
        {
            get;
        }

        string AiRoleId
        {
            get;
        }
    }

    public interface IAiWeaponDefinitionSource
    {
        string AiWeaponDefinitionId
        {
            get;
        }
    }

    /// <remarks>
    /// <para>AI定義を持つキャラクターデータが実装する任意インターフェースです。
    /// 既存データを変更できない期間はAiDefinitionCatalogのBindingを使用します。</para>
    /// <para>AI定義を持つ武器データが実装する任意インターフェースです。</para>
    /// </remarks>
    [Serializable]
    public sealed class AiRoleAffinity
    {
        [SerializeField]
        private string _weaponRoleId = AiRoleIds.GenericSafe;

        [SerializeField]
        private float _combatUtility;

        [SerializeField]
        private float _planningUtility;

        public string WeaponRoleId => AiId.Normalize(_weaponRoleId, AiRoleIds.GenericSafe);

        public float CombatUtility => _combatUtility;

        public float PlanningUtility => _planningUtility;

        public static AiRoleAffinity Create(string weaponRoleId, float combatUtility, float planningUtility)
        {
            return new AiRoleAffinity
            {
                _weaponRoleId = weaponRoleId,
                _combatUtility = combatUtility,
                _planningUtility = planningUtility
            };
        }
    }

    /// <summary>
    /// キャラクターロールの判断値です。
    /// 新しいロールはCatalogへ定義を追加するだけで登録できます。
    /// </summary>
    [Serializable]
    public sealed class AiRoleDefinition
    {
        [SerializeField]
        private string _id = AiRoleIds.GenericSafe;

        [SerializeField]
        private string _displayName = "Generic Safe";

        [SerializeField]
        private AiUnitRole _legacyRole = AiUnitRole.GenericSafe;

        [SerializeField]
        private string[] _aliases = new string[0];

        [SerializeField]
        private float _preferredMinimumCells = 2.0f;

        [SerializeField]
        private float _preferredMaximumCells = 5.0f;

        [SerializeField]
        private float _dangerWeight = 1.0f;

        [SerializeField]
        private float _highGroundWeight = 1.0f;

        [SerializeField]
        private float _movementWeight = 0.5f;

        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float _movementDutyRatio = 0.75f;

        /// <summary>
        /// 従来の移動可能距離に対して計画する経路長の倍率です。
        /// 速度や武器用ActionPointは変更しません。
        /// </summary>
        [SerializeField]
        [Min(3.0f)]
        private float _pathLengthMultiplier = 3.0f;

        /// <summary>
        /// 探索可能な最大経路長のうち，実際の計画へ使用する最低割合です。
        /// 1の場合は，可能な限り最大長の経路を選択します。
        /// </summary>
        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float _minimumPathUsageRatio = 1.0f;

        [SerializeField]
        private int _planningPriority = 100;

        [SerializeField]
        private float _minimumAllyDistance = 1.25f;

        [SerializeField]
        private float _isolationDistance = 5.5f;

        [SerializeField]
        private float _threatMinimumCells;

        [SerializeField]
        private float _threatMaximumCells = 6.0f;

        [SerializeField]
        private float _nearThreat = 8.0f;

        [SerializeField]
        private float _peakThreat = 20.0f;

        [SerializeField]
        private float _farThreat;

        [SerializeField]
        private AiRoleAffinity[] _weaponAffinities = new AiRoleAffinity[0];

        public string Id => AiId.Normalize(_id, AiRoleIds.GenericSafe);

        public string DisplayName => _displayName ?? string.Empty;

        public AiUnitRole LegacyRole => _legacyRole;

        public float PreferredMinimumCells => Mathf.Max(0.0f, _preferredMinimumCells);

        public float PreferredMaximumCells => Mathf.Max(PreferredMinimumCells + 0.05f, _preferredMaximumCells);

        public float DangerWeight => Mathf.Max(0.0f, _dangerWeight);

        public float HighGroundWeight => _highGroundWeight;

        public float MovementWeight => Mathf.Max(0.0f, _movementWeight);

        public float MovementDutyRatio => Mathf.Clamp01(_movementDutyRatio);

        public float PathLengthMultiplier => Mathf.Max(3.0f, _pathLengthMultiplier);

        public float MinimumPathUsageRatio => Mathf.Clamp01(_minimumPathUsageRatio);

        public int PlanningPriority => _planningPriority;

        public float MinimumAllyDistance => Mathf.Max(0.0f, _minimumAllyDistance);

        public float IsolationDistance => Mathf.Max(MinimumAllyDistance, _isolationDistance);

        public bool MatchesAlias(string value)
        {
            string normalized = AiId.NormalizeAlias(value);

            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            if (AiId.NormalizeAlias(Id) == normalized || AiId.NormalizeAlias(DisplayName) == normalized)
            {
                return true;
            }

            if (_aliases == null)
            {
                return false;
            }

            foreach (string alias in _aliases)
            {
                string normalizedAlias = AiId.NormalizeAlias(alias);

                if (!string.IsNullOrEmpty(normalizedAlias) &&
                    (normalized == normalizedAlias ||
                    normalized.Contains(
                    normalizedAlias)))
                {
                    return true;
                }
            }

            return false;
        }

        public float GetCombatAffinity(string weaponRoleId)
        {
            return GetAffinity(weaponRoleId, false);
        }

        public float GetPlanningAffinity(string weaponRoleId)
        {
            return GetAffinity(weaponRoleId, true);
        }

        public float EvaluateThreat(float distanceCells)
        {
            float minimum = Mathf.Max(0.0f, _threatMinimumCells);
            float maximum = Mathf.Max(minimum + 0.05f, _threatMaximumCells);

            if (distanceCells < minimum)
            {
                float ratio = minimum > 0.001f ? Mathf.Clamp01(distanceCells / minimum) : 1.0f;

                return Mathf.Lerp(_nearThreat, _peakThreat, ratio);
            }

            if (distanceCells <= maximum)
            {
                return _peakThreat;
            }

            float falloff = Mathf.Max(1.0f, maximum * 0.5f);
            float farRatio = Mathf.Clamp01((distanceCells - maximum) / falloff);

            return Mathf.Lerp(_peakThreat, _farThreat, farRatio);
        }

        public static AiRoleDefinition Create(
            string id,
            string displayName,
            AiUnitRole legacyRole,
            string[] aliases,
            float preferredMinimumCells,
            float preferredMaximumCells,
            float dangerWeight,
            float highGroundWeight,
            float movementWeight,
            float movementDutyRatio,
            int planningPriority,
            float minimumAllyDistance,
            float isolationDistance,
            float threatMinimumCells,
            float threatMaximumCells,
            float nearThreat,
            float peakThreat,
            float farThreat,
            AiRoleAffinity[] affinities)
        {
            return new AiRoleDefinition
            {
                _id = id,
                _displayName = displayName,
                _legacyRole = legacyRole,
                _aliases = aliases ?? new string[0],
                _preferredMinimumCells = preferredMinimumCells,
                _preferredMaximumCells = preferredMaximumCells,
                _dangerWeight = dangerWeight,
                _highGroundWeight = highGroundWeight,
                _movementWeight = movementWeight,
                _movementDutyRatio = movementDutyRatio,
                _planningPriority = planningPriority,
                _minimumAllyDistance = minimumAllyDistance,
                _isolationDistance = isolationDistance,
                _threatMinimumCells = threatMinimumCells,
                _threatMaximumCells = threatMaximumCells,
                _nearThreat = nearThreat,
                _peakThreat = peakThreat,
                _farThreat = farThreat,
                _weaponAffinities = affinities ?? new AiRoleAffinity[0]
            };
        }

        private float GetAffinity(string weaponRoleId, bool planning)
        {
            string safeId = AiId.Normalize(weaponRoleId, AiRoleIds.GenericSafe);

            if (_weaponAffinities != null)
            {
                foreach (AiRoleAffinity affinity in _weaponAffinities)
                {
                    if (affinity != null && AiId.Equals(affinity.WeaponRoleId, safeId))
                    {
                        return planning ? affinity.PlanningUtility : affinity.CombatUtility;
                    }
                }
            }

            return AiId.Equals(Id, safeId) ? planning ? 42.0f : 24.0f : 0.0f;
        }
    }

    /// <summary>
    /// 武器のAI用意味情報です。実際のダメージやコストはPranamDataを正とします。
    /// </summary>
    [Serializable]
    public sealed class AiWeaponDefinition
    {
        /// <summary>
        /// Editorで共通パラメータを同期する元の兵装データです。
        /// </summary>
        [SerializeField]
        private PranamData _sourceData;

        [SerializeField]
        private string _id = "weapon_ai.generic";

        [SerializeField]
        private string _roleId = AiRoleIds.GenericSafe;

        [SerializeField]
        private AiVisibilityRequirement _visibilityRequirement = AiVisibilityRequirement.DirectVisible;

        [SerializeField]
        private AiWeaponUseMode _useMode = AiWeaponUseMode.Attack;

        [SerializeField]
        private string[] _abilityTags = new string[0];

        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float _preferredMinimumFactor = 0.20f;

        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float _preferredMaximumFactor = 0.75f;

        [SerializeField]
        private float _fallbackMinimumRange;

        [SerializeField]
        private float _fallbackMaximumRange = 5.0f;

        [SerializeField]
        private float _fallbackPranaCost;

        [SerializeField]
        private float _fallbackExpectedDamage = 10.0f;

        [SerializeField]
        private bool _usesHold;

        public string Id => AiId.Normalize(_id, "weapon_ai.generic");

        public string RoleId => AiId.Normalize(_roleId, AiRoleIds.GenericSafe);

        public AiVisibilityRequirement VisibilityRequirement => _visibilityRequirement;

        public AiWeaponUseMode UseMode => _useMode;

        public float PreferredMinimumFactor => Mathf.Clamp(_preferredMinimumFactor, 0.0f, 0.95f);

        public float PreferredMaximumFactor => Mathf.Clamp(
            _preferredMaximumFactor,
            PreferredMinimumFactor +
            0.05f,
            1.0f);

        public float FallbackMinimumRange => Mathf.Max(0.0f, _fallbackMinimumRange);

        public float FallbackMaximumRange => Mathf.Max(FallbackMinimumRange + 0.1f, _fallbackMaximumRange);

        public float FallbackPranaCost => Mathf.Max(0.0f, _fallbackPranaCost);

        public float FallbackExpectedDamage => Mathf.Max(0.0f, _fallbackExpectedDamage);

        public bool UsesHold => _usesHold;

        public IReadOnlyList<string> AbilityTags => _abilityTags;

        public bool HasTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || _abilityTags == null)
            {
                return false;
            }

            foreach (string candidate in _abilityTags)
            {
                if (AiId.Equals(candidate, tag))
                {
                    return true;
                }
            }

            return false;
        }
    }

    [Serializable]
    public sealed class AiCharacterRoleBinding
    {
        /// <summary>
        /// Editorで名前とロールを同期する元のキャラクターデータです。
        /// </summary>
        [SerializeField]
        private UnitParams _sourceCharacter;

        [SerializeField]
        private string _characterId = string.Empty;

        [SerializeField]
        private string _roleId = AiRoleIds.GenericSafe;

        public string CharacterId => _characterId ?? string.Empty;

        public string RoleId => AiId.Normalize(_roleId, AiRoleIds.GenericSafe);
    }

    /// <summary>
    /// Project内に1つ作成し，SoloAiDirectorへ設定するAI定義一覧です。
    /// </summary>
    [CreateAssetMenu(fileName = "AiDefinitionCatalog", menuName = "Prana Tactics/AI/Definition Catalog")]
    public sealed class AiDefinitionCatalog : ScriptableObject
    {
        [SerializeField]
        private AiRoleDefinition[] _roles = new AiRoleDefinition[0];

        [SerializeField]
        private AiWeaponDefinition[] _weapons = new AiWeaponDefinition[0];

        [SerializeField]
        private AiCharacterRoleBinding[] _characters = new AiCharacterRoleBinding[0];

        public IReadOnlyList<AiRoleDefinition> Roles => _roles;

        public IReadOnlyList<AiWeaponDefinition> Weapons => _weapons;

        public IReadOnlyList<AiCharacterRoleBinding> Characters => _characters;
    }

    /// <remarks>
    /// <para>Inspectorの選択肢として使用する組み込みロールIDです。
    /// Catalogへ追加された拡張ロールはEditor側でこの一覧へ結合されます。</para>
    /// </remarks>
    public static class AiRoleIds
    {
        public const string GenericSafe = "role.generic_safe";

        public const string Attacker = "role.attacker";

        public const string Shooter = "role.shooter";

        public const string Gunner = "role.gunner";

        public const string Sniper = "role.sniper";

        private static readonly string[] s_all =
        {
            GenericSafe,
            Attacker,
            Shooter,
            Gunner,
            Sniper
        };

        public static IReadOnlyList<string> All => s_all;
    }

    /// <summary>
    /// 現在登録されている兵装の安定IDです。
    /// 表示名やPrefab名を変更しても、このIDは変更しません。
    /// </summary>
    public static class AiWeaponIds
    {
        public const string Generic = "weapon_ai.generic";

        public const string Zephyr = "weapon_ai.zephyr";

        public const string Umbra = "weapon_ai.umbra";

        public const string Scutum = "weapon_ai.scutum";

        public const string Palma = "weapon_ai.palma";

        public const string Arcus = "weapon_ai.arcus";

        public const string Obscura = "weapon_ai.obscura";

        public const string Gerade = "weapon_ai.gerade";

        /// <summary>
        /// 球状の着弾爆発を使用するイラプトのAI定義IDです。
        /// </summary>
        public const string Erupt = "weapon_ai.erupt";

        public const string Captare = "weapon_ai.captare";

        public const string SeriesGerade = "weapon_ai.series_gerade";

        public const string SeriesCaptare = "weapon_ai.series_captare";

        public const string SeriesArcus = "weapon_ai.series_arcus";

        public const string OrbisGerade = "weapon_ai.orbis_gerade";

        public const string Spargo = "weapon_ai.spargo";

        public const string Levis = "weapon_ai.levis";

        public const string Stilla = "weapon_ai.stilla";

        public const string Destinare = "weapon_ai.destinare";

        private static readonly string[] s_all =
        {
            Generic,
            Zephyr,
            Umbra,
            Scutum,
            Palma,
            Arcus,
            Obscura,
            Gerade,
            Erupt,
            Captare,
            SeriesGerade,
            SeriesCaptare,
            SeriesArcus,
            OrbisGerade,
            Spargo,
            Levis,
            Stilla,
            Destinare
        };

        public static IReadOnlyList<string> All => s_all;
    }

    public static class AiAbilityTags
    {
        public const string Direct = "ability.direct";

        public const string Arc = "ability.arc";

        public const string Homing = "ability.homing";

        public const string Tracking = "ability.tracking";

        public const string RapidFire = "ability.rapid_fire";

        public const string ShieldDeploy = "ability.shield_deploy";

        public const string FullBodyDefense = "ability.full_body_defense";

        public const string StealthActivate = "ability.stealth_activate";

        public const string Reveal = "ability.reveal";

        public const string HighCommitment = "ability.high_commitment";

        private static readonly string[] s_all =
        {
            Direct,
            Arc,
            Homing,
            Tracking,
            RapidFire,
            ShieldDeploy,
            FullBodyDefense,
            StealthActivate,
            Reveal,
            HighCommitment
        };

        public static IReadOnlyList<string> All => s_all;
    }

    internal static class AiId
    {
        public static string Normalize(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
        }

        public static string NormalizeAlias(string value)
        {
            return (value ?? string.Empty).Trim().Replace("_", string.Empty).Replace("-", string.Empty).Replace(
                " ",
                string.Empty).ToLowerInvariant();
        }

        public static bool Equals(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Definitionの登録窓口です。中央Selectorを変更せず定義を追加できます。
    /// </summary>
    public static class AiDefinitionRegistry
    {
        private static readonly Dictionary<string, AiRoleDefinition> s_roles = new Dictionary<string, AiRoleDefinition>(
            StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, AiWeaponDefinition> s_weapons = new Dictionary<string, AiWeaponDefinition>(
            StringComparer.OrdinalIgnoreCase);

        private static readonly List<string> s_roleOrder = new List<string>();

        private static readonly Dictionary<string, string> s_characterRoles = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        private static bool s_isConfigured;

        public static void Configure(AiDefinitionCatalog catalog)
        {
            s_roles.Clear();
            s_weapons.Clear();
            s_characterRoles.Clear();
            s_roleOrder.Clear();
            RegisterBuiltInRoles();

            if (catalog != null)
            {
                RegisterCatalog(catalog);
            }

            s_isConfigured = true;
        }

        public static AiRoleDefinition GetRole(string roleId)
        {
            EnsureConfigured();
            string safeId = AiId.Normalize(roleId, AiRoleIds.GenericSafe);

            if (s_roles.TryGetValue(safeId, out AiRoleDefinition definition))
            {
                return definition;
            }

            return s_roles[AiRoleIds.GenericSafe];
        }

        public static string ResolveRoleIdFromAlias(string value)
        {
            EnsureConfigured();

            foreach (string roleId in s_roleOrder)
            {
                AiRoleDefinition role = s_roles[roleId];

                if (role != null && role.MatchesAlias(value))
                {
                    return role.Id;
                }
            }

            return AiRoleIds.GenericSafe;
        }

        public static string ResolveCharacterRoleId(string characterId, string fallbackValue)
        {
            EnsureConfigured();

            if (!string.IsNullOrWhiteSpace(characterId) &&
                s_characterRoles.TryGetValue(
                characterId,
                out string roleId))
            {
                return roleId;
            }

            return ResolveRoleIdFromAlias(fallbackValue);
        }

        public static bool TryGetWeapon(string weaponDefinitionId, out AiWeaponDefinition definition)
        {
            EnsureConfigured();

            return s_weapons.TryGetValue(AiId.Normalize(weaponDefinitionId, string.Empty), out definition);
        }

        private static void EnsureConfigured()
        {
            if (!s_isConfigured)
            {
                Configure(null);
            }
        }

        private static void RegisterCatalog(AiDefinitionCatalog catalog)
        {
            IReadOnlyList<AiRoleDefinition> roles = catalog.Roles;

            if (roles != null)
            {
                foreach (AiRoleDefinition role in roles)
                {
                    if (role != null)
                    {
                        RegisterRole(role);
                    }
                }
            }

            IReadOnlyList<AiWeaponDefinition> weapons = catalog.Weapons;

            if (weapons != null)
            {
                foreach (AiWeaponDefinition weapon in weapons)
                {
                    if (weapon != null)
                    {
                        s_weapons[weapon.Id] = weapon;
                    }
                }
            }

            IReadOnlyList<AiCharacterRoleBinding> characters = catalog.Characters;

            if (characters == null)
            {
                return;
            }

            foreach (AiCharacterRoleBinding binding in characters)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.CharacterId))
                {
                    continue;
                }

                s_characterRoles[binding.CharacterId] = binding.RoleId;
            }
        }

        private static void RegisterBuiltInRoles()
        {
            RegisterRole(
                AiRoleDefinition.Create(
                AiRoleIds.GenericSafe,
                "Generic Safe",
                AiUnitRole.GenericSafe,
                new[] { "generic", "safe" },
                2.0f,
                5.0f,
                1.0f,
                1.0f,
                0.55f,
                0.88f,
                100,
                1.25f,
                5.0f,
                0.0f,
                6.0f,
                5.0f,
                15.0f,
                2.0f,
                new AiRoleAffinity[0]));
            RegisterRole(
                AiRoleDefinition.Create(
                AiRoleIds.Attacker,
                "Attacker",
                AiUnitRole.Attacker,
                new[] { "attacker", "attack", "近接" },
                0.8f,
                2.2f,
                0.75f,
                0.8f,
                0.35f,
                0.95f,
                0,
                1.25f,
                7.0f,
                0.0f,
                3.0f,
                45.0f,
                45.0f,
                0.0f,
                new[] { AiRoleAffinity.Create(
                AiRoleIds.Attacker,
                24.0f,
                42.0f), AiRoleAffinity.Create(
                AiRoleIds.Shooter,
                12.0f,
                18.0f), AiRoleAffinity.Create(
                AiRoleIds.Gunner,
                9.0f,
                14.0f), AiRoleAffinity.Create(
                AiRoleIds.Sniper,
                -8.0f,
                -12.0f) }));
            RegisterRole(
                AiRoleDefinition.Create(
                AiRoleIds.Shooter,
                "Shooter",
                AiUnitRole.Shooter,
                new[] { "shooter", "shoot", "射撃" },
                3.5f,
                6.5f,
                1.05f,
                1.2f,
                0.45f,
                0.92f,
                20,
                1.25f,
                5.5f,
                3.0f,
                7.0f,
                8.0f,
                28.0f,
                2.0f,
                new[] { AiRoleAffinity.Create(
                AiRoleIds.Attacker,
                5.0f,
                9.0f), AiRoleAffinity.Create(
                AiRoleIds.Shooter,
                24.0f,
                42.0f), AiRoleAffinity.Create(
                AiRoleIds.Gunner,
                9.0f,
                20.0f), AiRoleAffinity.Create(
                AiRoleIds.Sniper,
                2.0f,
                7.0f) }));
            RegisterRole(
                AiRoleDefinition.Create(
                AiRoleIds.Gunner,
                "Gunner",
                AiUnitRole.Gunner,
                new[] { "gunner", "gun", "銃手" },
                3.0f,
                6.5f,
                0.95f,
                1.0f,
                0.40f,
                0.90f,
                10,
                1.25f,
                5.5f,
                2.5f,
                7.0f,
                9.0f,
                31.0f,
                2.0f,
                new[] { AiRoleAffinity.Create(
                AiRoleIds.Attacker,
                -3.0f,
                5.0f), AiRoleAffinity.Create(
                AiRoleIds.Shooter,
                9.0f,
                20.0f), AiRoleAffinity.Create(
                AiRoleIds.Gunner,
                24.0f,
                42.0f), AiRoleAffinity.Create(
                AiRoleIds.Sniper,
                3.0f,
                8.0f) }));
            RegisterRole(
                AiRoleDefinition.Create(
                AiRoleIds.Sniper,
                "Sniper",
                AiUnitRole.Sniper,
                new[] { "sniper", "snipe", "狙撃" },
                6.5f,
                11.0f,
                1.35f,
                1.8f,
                0.55f,
                0.45f,
                30,
                2.5f,
                6.5f,
                6.0f,
                13.0f,
                8.0f,
                34.0f,
                8.0f,
                new[] { AiRoleAffinity.Create(
                AiRoleIds.Attacker,
                10.0f,
                2.0f), AiRoleAffinity.Create(
                AiRoleIds.Shooter,
                6.0f,
                14.0f), AiRoleAffinity.Create(
                AiRoleIds.Gunner,
                6.0f,
                12.0f), AiRoleAffinity.Create(
                AiRoleIds.Sniper,
                24.0f,
                42.0f) }));
        }

        private static void RegisterRole(AiRoleDefinition role)
        {
            if (role != null)
            {
                if (!s_roles.ContainsKey(role.Id))
                {
                    s_roleOrder.Add(role.Id);
                }

                s_roles[role.Id] = role;
            }
        }
    }
}
