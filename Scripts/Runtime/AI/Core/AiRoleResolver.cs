using PranaTactics.Core;
using PranaTactics.Unit;

namespace PranaTactics.AI.Solo
{
    /// <summary>
    /// CharacterDataとAiDefinitionCatalogから安定したRoleIdを解決します。
    /// 新しいロールを追加しても、このクラスへ分岐を追加する必要はありません。
    /// </summary>
    public static class AiRoleResolver
    {
        public static AiRoleDefinition ResolveDefinition(UnitBehavior unit)
        {
            return AiDefinitionRegistry.GetRole(ResolveRoleId(unit));
        }

        public static string ResolveRoleId(UnitBehavior unit)
        {
            if (unit == null || unit.GetUnitStatus() == null)
            {
                return AiRoleIds.GenericSafe;
            }

            string characterId = unit.GetUnitStatus().UnitName;
            object characterData = null;

            if (GameDataRepository.Instance != null)
            {
                characterData = GameDataRepository.Instance.GetCharacterData(characterId);
            }

            IAiCharacterDefinitionSource definitionSource = characterData as IAiCharacterDefinitionSource;

            if (definitionSource != null && !string.IsNullOrWhiteSpace(definitionSource.AiRoleId))
            {
                return definitionSource.AiRoleId;
            }

            string explicitRoleId = AiReflection.ReadString(
                characterData,
                string.Empty,
                "CharacterAiDefinitionId",
                "AiRoleId",
                "RoleId",
                "_characterAiDefinitionId",
                "_aiRoleId");

            if (!string.IsNullOrWhiteSpace(explicitRoleId))
            {
                return AiDefinitionRegistry.GetRole(explicitRoleId).Id;
            }

            object legacyRole = AiReflection.ReadObject(
                characterData,
                "CharacterType",
                "UnitRole",
                "Role",
                "_characterType",
                "_unitRole",
                "_role");

            return AiDefinitionRegistry.ResolveCharacterRoleId(
                characterId,
                legacyRole != null ? legacyRole.ToString() : string.Empty);
        }

        /// <summary>
        /// 既存コード向けの代表enumを返します。
        /// 新規ロールはGenericSafeとなりますが、AI内部のRoleIdは保持されます。
        /// </summary>
        public static AiUnitRole Resolve(UnitBehavior unit)
        {
            return ResolveDefinition(unit).LegacyRole;
        }

        public static string ParseRoleId(string value)
        {
            return AiDefinitionRegistry.ResolveRoleIdFromAlias(value);
        }

        public static AiUnitRole Parse(string value)
        {
            return AiDefinitionRegistry.GetRole(ParseRoleId(value)).LegacyRole;
        }

        public static string ToRoleId(AiUnitRole role)
        {
            switch (role)
            {
                case AiUnitRole.Attacker:
                    return AiRoleIds.Attacker;
                case AiUnitRole.Shooter:
                    return AiRoleIds.Shooter;
                case AiUnitRole.Gunner:
                    return AiRoleIds.Gunner;
                case AiUnitRole.Sniper:
                    return AiRoleIds.Sniper;
                default:
                    return AiRoleIds.GenericSafe;
            }
        }
    }
}
