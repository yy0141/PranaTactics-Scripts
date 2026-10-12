using System;
using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Core;
using PranaTactics.Pranam;
using PranaTactics.Unit;

namespace PranaTactics.AI.Solo
{
    /// <summary>
    /// 武器Definitionと現在の公開状態からMain/Subを独立評価します。
    /// 新しい武器ロールはCatalogへDefinitionを追加するだけで使用できます。
    /// </summary>
    public sealed class AiWeaponSelector
    {
        private static readonly HashSet<string> s_legacyWarnings = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> s_definitionWarnings = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        public List<AiWeaponProfile> BuildProfiles(UnitBehavior unit, bool isMain, float cellSpacing)
        {
            List<AiWeaponProfile> result = new List<AiWeaponProfile>();

            if (unit == null || unit.GetUnitStatus() == null || GameDataRepository.Instance == null)
            {
                return result;
            }

            List<List<PranamData>> allWeapons = GameDataRepository.Instance.GetPranams(
                unit.GetUnitStatus().UnitName);
            int sideIndex = isMain ? 0 : 1;

            if (allWeapons == null || allWeapons.Count <= sideIndex || allWeapons[sideIndex] == null)
            {
                return result;
            }

            List<PranamData> sideWeapons = allWeapons[sideIndex];
            TacticalMapUnitBehavior map = unit.GetTacticalMapUnitBehavior();
            Transform slotRoot = map != null
                ? (isMain ? map.GetMainPranamTransform() : map.GetSubPranamTransform())
                : null;

            for (int slot = 0; slot < sideWeapons.Count; slot++)
            {
                result.Add(BuildProfile(sideWeapons[slot], slot, isMain, cellSpacing, slotRoot));
            }

            return result;
        }

        /// <summary>
        /// 実際に使用できる防御兵装を選び、脅威方向に合う防御を優先します。
        /// </summary>
        public AiWeaponProfile SelectDefense(
            IReadOnlyList<AiWeaponProfile> weapons,
            float currentPrana,
            int actionPoints,
            int threatDirectionCount)
        {
            AiWeaponProfile best = null;
            float bestScore = float.NegativeInfinity;
            if (weapons == null)
            {
                return null;
            }

            foreach (AiWeaponProfile weapon in weapons)
            {
                if (weapon == null || !weapon.IsUsable || weapon.UseMode != AiWeaponUseMode.Defense ||
                    weapon.RuntimePranam == null || !weapon.RuntimePranam.IsRuntimeReady ||
                    weapon.PranaCost >= currentPrana || weapon.RequiredActionPointCost > actionPoints)
                {
                    continue;
                }

                bool isLocalized = weapon.RuntimePranam is LocalizedShieldBehavior;
                float score = (isLocalized && threatDirectionCount == 1 ? 20.0f : 10.0f) -
                    weapon.PranaCost - weapon.RequiredActionPointCost;
                if (score > bestScore)
                {
                    best = weapon;
                    bestScore = score;
                }
            }

            return best;
        }

        /// <summary>
        /// オブスクラまたは待機用の空スロットを取得します。
        /// </summary>
        public AiWeaponProfile SelectNonCombatSlot(IReadOnlyList<AiWeaponProfile> weapons, bool preferStealth)
        {
            AiWeaponProfile empty = null;
            if (weapons == null)
            {
                return null;
            }

            foreach (AiWeaponProfile weapon in weapons)
            {
                if (weapon == null)
                {
                    continue;
                }

                if (preferStealth && weapon.RuntimePranam is Obscura && weapon.RuntimePranam.IsRuntimeReady)
                {
                    return weapon;
                }

                if (!weapon.IsUsable)
                {
                    empty = weapon;
                }
            }

            return empty;
        }

        public AiRoleProfile BuildMovementProfile(
            UnitBehavior unit,
            string unitRoleId,
            float cellSpacing,
            out AiWeaponProfile plannedWeapon)
        {
            AiRoleProfile defaultProfile = AiRoleProfile.Create(unitRoleId);
            float safeCellSpacing = Mathf.Max(0.1f, cellSpacing);
            List<AiWeaponProfile> weapons = new List<AiWeaponProfile>();

            weapons.AddRange(BuildProfiles(unit, true, safeCellSpacing));
            weapons.AddRange(BuildProfiles(unit, false, safeCellSpacing));
            float currentPrana = unit != null && unit.GetUnitStatus() != null ? unit.GetUnitStatus().CurrentHealth : 0.0f;
            float maximumPrana = unit != null && unit.GetUnitStatus() != null ? unit.GetUnitStatus().MaxHealth : 0.0f;

            plannedWeapon = SelectPlanningWeapon(
                unitRoleId,
                weapons,
                defaultProfile,
                safeCellSpacing,
                currentPrana,
                maximumPrana);

            if (plannedWeapon == null)
            {
                return defaultProfile;
            }

            float rawMinimumCells = plannedWeapon.MinimumRange / safeCellSpacing;
            float rawMaximumCells = plannedWeapon.MaximumRange / safeCellSpacing;
            float weaponMinimumCells = Mathf.Lerp(
                rawMinimumCells,
                rawMaximumCells,
                plannedWeapon.PreferredMinimumFactor);
            float weaponMaximumCells = Mathf.Lerp(
                rawMinimumCells,
                rawMaximumCells,
                plannedWeapon.PreferredMaximumFactor);
            const float weaponRangeInfluence = 0.72f;
            float width = Mathf.Max(0.05f, rawMaximumCells - rawMinimumCells);
            float minimumBandWidth = Mathf.Min(0.25f, width);
            float preferredMinimum = Mathf.Lerp(
                defaultProfile.PreferredMinimumCells,
                weaponMinimumCells,
                weaponRangeInfluence);
            float preferredMaximum = Mathf.Lerp(
                defaultProfile.PreferredMaximumCells,
                weaponMaximumCells,
                weaponRangeInfluence);

            preferredMinimum = Mathf.Clamp(
                preferredMinimum,
                rawMinimumCells,
                Mathf.Max(
                rawMinimumCells,
                rawMaximumCells - minimumBandWidth));
            preferredMaximum = Mathf.Clamp(
                preferredMaximum,
                preferredMinimum + minimumBandWidth,
                Mathf.Max(
                preferredMinimum +
                minimumBandWidth,
                rawMaximumCells));

            return defaultProfile.WithPreferredRange(preferredMinimum, preferredMaximum);
        }

        public AiRoleProfile BuildMovementProfile(
            UnitBehavior unit,
            AiUnitRole unitRole,
            float cellSpacing,
            out AiWeaponProfile plannedWeapon)
        {
            return BuildMovementProfile(
                unit,
                AiRoleResolver.ToRoleId(unitRole),
                cellSpacing,
                out plannedWeapon);
        }

        public AiWeaponProfile SelectBest(
            string unitRoleId,
            IReadOnlyList<AiWeaponProfile> weapons,
            float targetDistance,
            float currentPrana,
            float maximumPrana,
            out float bestUtility)
        {
            return SelectBest(
                unitRoleId,
                weapons,
                targetDistance,
                currentPrana,
                maximumPrana,
                null,
                out bestUtility);
        }

        /// <summary>
        /// 射程、可視状態、残り行動力と継続使用時間から最適な武器を選択します。
        /// </summary>
        /// <remarks>
        /// <para>Microで今すぐ使用できる射程内兵装を最優先にします。</para>
        /// <para>射程内候補が一つもない場合だけ、移動後に使用する</para>
        /// <para>射程外兵装同士をUtilityで比較します。</para>
        /// <para>射程が同じ条件なら、視認状態と対象の移動状態に</para>
        /// <para>合った兵装カテゴリを先に選びます。これにより、</para>
        /// <para>コストや射程中央への近さだけでアルクスへ偏ることを</para>
        /// <para>防ぎます。</para>
        /// <para>直接視認中は通常弾、追尾弾、狙撃弾を同じ優先段階で比較し、</para>
        /// <para>ロール適性と継続使用時間も武器選択へ反映します。</para>
        /// </remarks>
        public AiWeaponProfile SelectBest(
            string unitRoleId,
            IReadOnlyList<AiWeaponProfile> weapons,
            float targetDistance,
            float currentPrana,
            float maximumPrana,
            AiWeaponSelectionContext selectionContext,
            out float bestUtility,
            int currentSlot = -1,
            float attackSeconds = 0.0f,
            int availableActionPoints = int.MaxValue)
        {
            AiWeaponProfile best = null;

            bestUtility = float.NegativeInfinity;
            int bestRangePriority = int.MinValue;
            int bestSituationPriority = int.MinValue;

            if (weapons == null)
            {
                return null;
            }

            foreach (AiWeaponProfile weapon in weapons)
            {
                if (weapon != null && weapon.RequiredActionPointCost > availableActionPoints)
                {
                    continue;
                }

                float utility = Evaluate(
                    unitRoleId,
                    weapon,
                    targetDistance,
                    currentPrana,
                    maximumPrana,
                    selectionContext);

                if (float.IsNegativeInfinity(utility))
                {
                    continue;
                }

                if (weapon.Slot == currentSlot)
                {
                    utility -= AiCombatDecisionPolicy.EvaluateWeaponRepetitionPenalty(attackSeconds);
                }

                int rangePriority = IsWithinRange(weapon, targetDistance) ? 1 : 0;
                int situationPriority = GetSituationPriority(weapon, selectionContext);
                bool isBetter = rangePriority > bestRangePriority ||
                    (rangePriority == bestRangePriority &&
                    situationPriority > bestSituationPriority) ||
                    (rangePriority == bestRangePriority &&
                    situationPriority == bestSituationPriority &&
                    utility > bestUtility);

                if (isBetter)
                {
                    bestRangePriority = rangePriority;
                    bestSituationPriority = situationPriority;
                    bestUtility = utility;
                    best = weapon;
                }
            }

            return best;
        }

        public AiWeaponProfile SelectBest(
            AiUnitRole unitRole,
            IReadOnlyList<AiWeaponProfile> weapons,
            float targetDistance,
            float currentPrana,
            float maximumPrana,
            out float bestUtility)
        {
            return SelectBest(
                AiRoleResolver.ToRoleId(unitRole),
                weapons,
                targetDistance,
                currentPrana,
                maximumPrana,
                out bestUtility);
        }

        public float Evaluate(
            string unitRoleId,
            AiWeaponProfile weapon,
            float targetDistance,
            float currentPrana,
            float maximumPrana)
        {
            return Evaluate(unitRoleId, weapon, targetDistance, currentPrana, maximumPrana, null);
        }

        /// <summary>
        /// 武器の基礎Utilityへ、MicroUnit視認とMapUnit観測移動の
        /// 状況適性を加えて評価します。
        /// </summary>
        public float Evaluate(
            string unitRoleId,
            AiWeaponProfile weapon,
            float targetDistance,
            float currentPrana,
            float maximumPrana,
            AiWeaponSelectionContext selectionContext)
        {
            if (weapon == null || !weapon.IsUsable || weapon.UseMode != AiWeaponUseMode.Attack)
            {
                return float.NegativeInfinity;
            }

            if (currentPrana + 0.001f < weapon.PranaCost)
            {
                return float.NegativeInfinity;
            }

            float situationUtility = EvaluateSituation(weapon, selectionContext);

            if (float.IsNegativeInfinity(situationUtility))
            {
                return float.NegativeInfinity;
            }

            float rangeUtility;

            if (targetDistance < weapon.MinimumRange)
            {
                rangeUtility = -(weapon.MinimumRange - targetDistance) * 6.0f;
            }
            else if (targetDistance > weapon.MaximumRange)
            {
                rangeUtility = -(targetDistance - weapon.MaximumRange) * 8.0f;
            }
            else
            {
                float preferred = Mathf.Lerp(
                    weapon.MinimumRange,
                    weapon.MaximumRange,
                    (weapon.PreferredMinimumFactor +
                    weapon.PreferredMaximumFactor) * 0.5f);

                rangeUtility = 35.0f - Mathf.Abs(targetDistance - preferred) * 0.25f;
            }

            AiRoleDefinition unitRole = AiDefinitionRegistry.GetRole(unitRoleId);
            float roleUtility = unitRole.GetCombatAffinity(weapon.WeaponRoleId);
            float pranaRatio = maximumPrana > 0.001f ? currentPrana / maximumPrana : 0.0f;
            float costWeight = pranaRatio >= 0.75f ? 0.6f : pranaRatio >= 0.50f ? 1.0f : pranaRatio >= 0.25f ? 1.8f : 3.5f;
            float survivalPenalty = weapon.PranaCost * costWeight;

            if (currentPrana - weapon.PranaCost <= maximumPrana * 0.10f)
            {
                survivalPenalty += 80.0f;
            }

            return rangeUtility + roleUtility + weapon.ExpectedDamage * 0.25f - survivalPenalty +
                situationUtility;
        }

        public float Evaluate(
            AiUnitRole unitRole,
            AiWeaponProfile weapon,
            float targetDistance,
            float currentPrana,
            float maximumPrana)
        {
            return Evaluate(
                AiRoleResolver.ToRoleId(unitRole),
                weapon,
                targetDistance,
                currentPrana,
                maximumPrana);
        }

        /// <remarks>
        /// <para>非視認時はMapに表示され、扇形範囲から対象を取れる</para>
        /// <para>Shooter/Gunnerのアルクス弾だけを攻撃候補にします。</para>
        /// <para>非視認時はアルクスだけが有効候補なので、</para>
        /// <para>過大な加点は不要です。</para>
        /// <para>敵を直接視認できる間はアルクスを予備候補に下げます。</para>
        /// <para>追尾弾がなければ直進弾、それもなければアルクスを</para>
        /// <para>選べるよう、使用禁止にはしません。</para>
        /// </remarks>
        private static float EvaluateSituation(AiWeaponProfile weapon, AiWeaponSelectionContext context)
        {
            if (weapon == null || context == null)
            {
                return 0.0f;
            }

            if (context.ContactState == AiContactState.Unknown ||
                context.ContactState == AiContactState.LastKnown)
            {
                return float.NegativeInfinity;
            }

            if (weapon.VisibilityRequirement == AiVisibilityRequirement.DirectVisible &&
                !context.IsDirectlyVisible)
            {
                return float.NegativeInfinity;
            }

            if (weapon.VisibilityRequirement == AiVisibilityRequirement.MapTracked &&
                context.ContactState < AiContactState.MapTracked)
            {
                return float.NegativeInfinity;
            }

            bool isShooterOrGunner = AiId.Equals(weapon.WeaponRoleId, AiRoleIds.Shooter) ||
                AiId.Equals(
                weapon.WeaponRoleId,
                AiRoleIds.Gunner);
            bool isSniper = AiId.Equals(weapon.WeaponRoleId, AiRoleIds.Sniper);
            bool isArc = weapon.HasAbilityTag(AiAbilityTags.Arc);
            bool isDirect = weapon.HasAbilityTag(AiAbilityTags.Direct);
            bool isHoming = weapon.HasAbilityTag(AiAbilityTags.Homing) ||
                weapon.HasAbilityTag(
                AiAbilityTags.Tracking);

            if (!context.IsDirectlyVisible)
            {
                if (!isArc || !isShooterOrGunner)
                {
                    return float.NegativeInfinity;
                }

                return 35.0f;
            }

            if (context.IsTargetMoving)
            {
                if (isArc)
                {
                    return -45.0f;
                }

                if (isShooterOrGunner && isHoming)
                {
                    return 55.0f;
                }

                if (isShooterOrGunner && isDirect)
                {
                    return 20.0f;
                }

                return isSniper ? 30.0f : 0.0f;
            }

            if (isArc)
            {
                return -45.0f;
            }

            if (isSniper)
            {
                return 55.0f;
            }

            if (isShooterOrGunner && isDirect && !isHoming)
            {
                return 50.0f;
            }

            return isHoming ? 15.0f : 0.0f;
        }

        /// <summary>
        /// 状況に適した兵装カテゴリの優先段階を返します。
        /// 射程・コスト・Damageなどの通常Utilityより先に比較されます。
        /// </summary>
        public static int GetSituationPriority(AiWeaponProfile weapon, AiWeaponSelectionContext context)
        {
            if (weapon == null || context == null)
            {
                return 0;
            }

            bool isShooterOrGunner = AiId.Equals(weapon.WeaponRoleId, AiRoleIds.Shooter) ||
                AiId.Equals(
                weapon.WeaponRoleId,
                AiRoleIds.Gunner);
            bool isSniper = AiId.Equals(weapon.WeaponRoleId, AiRoleIds.Sniper);
            bool isArc = weapon.HasAbilityTag(AiAbilityTags.Arc);
            bool isDirect = weapon.HasAbilityTag(AiAbilityTags.Direct);
            bool isHoming = weapon.HasAbilityTag(AiAbilityTags.Homing) ||
                weapon.HasAbilityTag(
                AiAbilityTags.Tracking);

            if (!context.IsDirectlyVisible)
            {
                return isShooterOrGunner && isArc ? 3 : 0;
            }

            if (context.IsTargetMoving)
            {
                if (isShooterOrGunner && isHoming && !isArc)
                {
                    return 2;
                }

                if (isShooterOrGunner && isDirect && !isArc)
                {
                    return 2;
                }

                return isArc ? 0 : 2;
            }

            if (isSniper || (isShooterOrGunner && isDirect && !isHoming && !isArc))
            {
                return 2;
            }

            if (isShooterOrGunner && isHoming && !isArc)
            {
                return 2;
            }

            return isArc ? 0 : 1;
        }

        private static bool IsWithinRange(AiWeaponProfile weapon, float targetDistance)
        {
            return weapon != null && targetDistance >= weapon.MinimumRange &&
                targetDistance <= weapon.MaximumRange;
        }

        /// <summary>
        /// 生成済みの扇形または武器データの実射程から、判断用の兵装情報を構築します。
        /// </summary>
        /// <remarks>
        /// カタログ射程は実射程が取得できない武器だけに使用します。
        /// </remarks>
        private static AiWeaponProfile BuildProfile(
            PranamData data,
            int slot,
            bool isMain,
            float cellSpacing,
            Transform slotRoot)
        {
            string displayName = data != null ? data.DisplayName : "NONE";
            string rangeName = data != null ? data.Range : "NONE";
            bool isNone = data == null ||
                string.Equals(
                displayName,
                "NONE",
                StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                rangeName,
                "NONE",
                StringComparison.OrdinalIgnoreCase);
            string definitionId = ResolveDefinitionId(data);
            AiWeaponDefinition definition;
            bool hasDefinition = AiDefinitionRegistry.TryGetWeapon(definitionId, out definition);
            string roleId = hasDefinition ? definition.RoleId : ResolveLegacyRoleId(data);
            AiRoleDefinition role = AiDefinitionRegistry.GetRole(roleId);
            AiVisibilityRequirement visibilityRequirement = ResolveVisibilityRequirement(
                definitionId,
                definition,
                role.Id,
                hasDefinition);
            float fallbackMinimum = hasDefinition ? definition.FallbackMinimumRange : role.PreferredMinimumCells * Mathf.Max(
                0.1f,
                cellSpacing);
            float fallbackMaximum = hasDefinition ? definition.FallbackMaximumRange : role.PreferredMaximumCells * Mathf.Max(
                0.1f,
                cellSpacing);
            float minimumRange = AiReflection.ReadFloat(
                data,
                fallbackMinimum,
                "MinimumRange",
                "MinRange",
                "MIN_RANGE",
                "_minimumRange",
                "_minRange");
            float maximumRange = AiReflection.ReadFloat(
                data,
                fallbackMaximum,
                "MaximumRange",
                "MaxRange",
                "AttackRange",
                "RangeDistance",
                "MAX_RANGE",
                "_maximumRange",
                "_maxRange");
            PranamBase runtimePranam = slotRoot != null && slot < slotRoot.childCount
                ? slotRoot.GetChild(slot).GetComponent<PranamBase>() : null;
            float runtimeRange = runtimePranam != null ? runtimePranam.GetAttackRangeRadius() : 0.0f;
            float diameter = AiReflection.ReadFloat(data, 0.0f, "Diameter", "_diameter");
            Vector3 parentScale = slotRoot != null ? slotRoot.lossyScale : Vector3.one;

            maximumRange = AiCombatDecisionPolicy.ResolveAttackRange(
                runtimeRange,
                diameter,
                Mathf.Max(Mathf.Abs(parentScale.x), Mathf.Abs(parentScale.z)),
                maximumRange);
            minimumRange = Mathf.Min(minimumRange, maximumRange);
            float cost = AiReflection.ReadFloat(
                data,
                hasDefinition ? definition.FallbackPranaCost : 0.0f,
                "PranaCost",
                "Cost",
                "Consumption",
                "UseCost",
                "_pranaCost",
                "_cost");
            float damage = AiReflection.ReadFloat(
                data,
                hasDefinition ? definition.FallbackExpectedDamage : 10.0f,
                "AttackPower",
                "Damage",
                "Attack",
                "Power",
                "_attackPower",
                "_damage",
                "_attack",
                "_power");
            bool usesHold = AiReflection.ReadBool(
                data,
                hasDefinition && definition.UsesHold,
                "UsesHold",
                "CanHold",
                "IsAutomatic",
                "IsRapidFire",
                "_usesHold",
                "_isAutomatic");
            AiWeaponProfile profile = new AiWeaponProfile
            {
                Slot = slot,
                IsMain = isMain,
                IsUsable = !isNone,
                WeaponDefinitionId = definitionId,
                DisplayName = displayName,
                WeaponRoleId = role.Id,
                WeaponRole = role.LegacyRole,
                VisibilityRequirement = visibilityRequirement,
                UseMode = hasDefinition ? definition.UseMode : AiWeaponUseMode.Attack,
                MinimumRange = Mathf.Max(0.0f, minimumRange),
                MaximumRange = Mathf.Max(minimumRange + 0.1f, maximumRange),
                PreferredMinimumFactor = hasDefinition ? definition.PreferredMinimumFactor : 0.20f,
                PreferredMaximumFactor = hasDefinition ? definition.PreferredMaximumFactor : 0.75f,
                PranaCost = Mathf.Max(0.0f, cost),
                ActionPointCost = data != null ? Mathf.Max(0, data.ActionPointCost) : 0,
                RuntimePranam = runtimePranam,
                AttackArcDegrees = AiReflection.ReadFloat(data, 360.0f, "Angle", "_angle"),
                ExpectedDamage = Mathf.Max(0.0f, damage),
                UsesHold = usesHold
            };

            if (hasDefinition && definition.AbilityTags != null)
            {
                foreach (string tag in definition.AbilityTags)
                {
                    profile.AddAbilityTag(tag);
                }
            }

            if (!hasDefinition && !isNone && s_legacyWarnings.Add(definitionId))
            {
                Debug.LogWarning(
                    "AiWeaponSelector: WeaponAiDefinitionが未登録のため" + "互換プロファイルを使用します。" +
                    " AbilityTagsに基づく状況別兵装選択は" +
                    "正しく機能しません。" +
                    " DefinitionId=" +
                    definitionId +
                    ", Weapon=" +
                    displayName);
            }

            return profile;
        }

        /// <summary>
        /// AbilityTagsとRoleIdから明確に判断できる武器について、
        /// Catalogの視認条件を検証して安全な値へ補正します。
        /// </summary>
        private static AiVisibilityRequirement ResolveVisibilityRequirement(
            string definitionId,
            AiWeaponDefinition definition,
            string roleId,
            bool hasDefinition)
        {
            if (!hasDefinition || definition == null)
            {
                return AiVisibilityRequirement.DirectVisible;
            }

            bool isArc = definition.HasTag(AiAbilityTags.Arc);
            bool isDirectCombatRole = AiId.Equals(roleId, AiRoleIds.Shooter) ||
                AiId.Equals(
                roleId,
                AiRoleIds.Gunner) ||
                AiId.Equals(
                roleId,
                AiRoleIds.Sniper);
            bool hasDirectCombatTag = definition.HasTag(AiAbilityTags.Direct) ||
                definition.HasTag(
                AiAbilityTags.Homing) ||
                definition.HasTag(
                AiAbilityTags.Tracking) ||
                definition.HasTag(
                AiAbilityTags.HighCommitment);
            AiVisibilityRequirement expected = definition.VisibilityRequirement;
            bool hasExpectedRequirement = false;

            if (isArc && isDirectCombatRole)
            {
                expected = AiVisibilityRequirement.MapTracked;
                hasExpectedRequirement = true;
            }
            else if (isDirectCombatRole && (hasDirectCombatTag || AiId.Equals(roleId, AiRoleIds.Sniper)))
            {
                expected = AiVisibilityRequirement.DirectVisible;
                hasExpectedRequirement = true;
            }

            if (hasExpectedRequirement && definition.VisibilityRequirement != expected &&
                s_definitionWarnings.Add(
                definitionId +
                ".visibility"))
            {
                Debug.LogWarning(
                    "AiWeaponSelector: AbilityTags/RoleIdと" +
                    "VisibilityRequirementが矛盾しているため補正します。" +
                    " DefinitionId=" +
                    definitionId +
                    ", Configured=" +
                    definition.VisibilityRequirement +
                    ", Resolved=" +
                    expected);
            }

            if (isDirectCombatRole && definition.UseMode == AiWeaponUseMode.Attack && !isArc &&
                !hasDirectCombatTag &&
                s_definitionWarnings.Add(
                definitionId +
                ".ability_tags"))
            {
                Debug.LogWarning(
                    "AiWeaponSelector: 攻撃武器のAbilityTagsが不足しています。" +
                    " Direct/Homing/Tracking/HighCommitmentのいずれかを" +
                    "設定してください。 DefinitionId=" +
                    definitionId);
            }

            if (isDirectCombatRole && hasDirectCombatTag && definition.UseMode != AiWeaponUseMode.Attack &&
                s_definitionWarnings.Add(
                definitionId +
                ".use_mode"))
            {
                Debug.LogWarning(
                    "AiWeaponSelector: 攻撃用AbilityTagを持つ武器の" + "UseModeがAttackではありません。" +
                    " DefinitionId=" +
                    definitionId +
                    ", UseMode=" +
                    definition.UseMode);
            }

            return expected;
        }

        private static AiWeaponProfile SelectPlanningWeapon(
            string unitRoleId,
            IReadOnlyList<AiWeaponProfile> weapons,
            AiRoleProfile roleProfile,
            float cellSpacing,
            float currentPrana,
            float maximumPrana)
        {
            AiWeaponProfile best = null;
            float bestScore = float.NegativeInfinity;
            AiRoleDefinition unitRole = AiDefinitionRegistry.GetRole(unitRoleId);
            float roleMiddle = (roleProfile.PreferredMinimumCells + roleProfile.PreferredMaximumCells) * 0.5f;

            foreach (AiWeaponProfile weapon in weapons)
            {
                if (weapon == null || !weapon.IsUsable || weapon.UseMode != AiWeaponUseMode.Attack)
                {
                    continue;
                }

                if (currentPrana - weapon.PranaCost < maximumPrana * 0.10f)
                {
                    continue;
                }

                float weaponMiddleCells = (weapon.MinimumRange + weapon.MaximumRange) * 0.5f / cellSpacing;
                float score = unitRole.GetPlanningAffinity(weapon.WeaponRoleId) - Mathf.Abs(
                    weaponMiddleCells - roleMiddle) * 1.8f +
                    Mathf.Min(
                    30.0f,
                    weapon.ExpectedDamage) * 0.08f - weapon.PranaCost * 0.15f +
                    (weapon.IsMain ? 0.5f : 0.0f);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = weapon;
                }
            }

            return best;
        }

        /// <remarks>
        /// <para>移行期間だけ使用するIDです。Catalog登録後は使用されません。</para>
        /// </remarks>
        private static string ResolveDefinitionId(PranamData data)
        {
            if (data == null)
            {
                return "weapon_ai.none";
            }

            IAiWeaponDefinitionSource source = data as IAiWeaponDefinitionSource;

            if (source != null && !string.IsNullOrWhiteSpace(source.AiWeaponDefinitionId))
            {
                return AiId.Normalize(source.AiWeaponDefinitionId, "weapon_ai.generic");
            }

            string explicitId = AiReflection.ReadString(
                data,
                string.Empty,
                "WeaponAiDefinitionId",
                "AiWeaponDefinitionId",
                "WeaponId",
                "Id",
                "_weaponAiDefinitionId",
                "_weaponId");

            if (!string.IsNullOrWhiteSpace(explicitId))
            {
                return AiId.Normalize(explicitId, "weapon_ai.generic");
            }

            return "legacy." + AiId.NormalizeAlias(data.GetType().Name) + "." +
                AiId.NormalizeAlias(
                data.DisplayName);
        }

        private static string ResolveLegacyRoleId(PranamData data)
        {
            object roleValue = AiReflection.ReadObject(
                data,
                "WeaponRole",
                "PranamRole",
                "Role",
                "CharacterType",
                "_weaponRole",
                "_pranamRole",
                "_role");

            return AiRoleResolver.ParseRoleId(
                roleValue != null ? roleValue.ToString() : data != null ? data.GetType().Name : string.Empty);
        }
    }
}
