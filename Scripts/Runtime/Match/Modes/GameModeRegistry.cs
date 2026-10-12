using System;
using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.Match.Modes
{
    /// <summary>
    /// 既存モードと追加定義アセットから共通カタログを構築します。
    /// </summary>
    public static class GameModeRegistry
    {
        /// <summary>
        /// 既存の敵撃破モードに使用する安定した識別子です。
        /// </summary>
        public const string k_DefaultModeId = "elimination";

        /// <summary>
        /// 定義を一度だけ読み込む共通カタログです。
        /// </summary>
        private static GameModeCatalog s_catalog;

        public static GameModeCatalog Catalog => s_catalog ??= LoadCatalog();

        /// <summary>
        /// Domain Reload無効時も前回のアセット参照を破棄します。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCatalog()
        {
            s_catalog = null;
        }

        /// <summary>
        /// 既存動作を既定の定義として登録し、追加アセットを読み込みます。
        /// </summary>
        private static GameModeCatalog LoadCatalog()
        {
            var modes = new List<GameMode>
            {
                new GameMode(k_DefaultModeId, "殲滅戦", "敵を撃破し、最後まで生き残る。",
                    new[] { PlayCategory.Local, PlayCategory.Online }, () => new EliminationRule())
            };
            var ids = new HashSet<string>(StringComparer.Ordinal) { k_DefaultModeId };
            GameModeDefinition[] definitions = Resources.LoadAll<GameModeDefinition>("GameModes");
            Array.Sort(definitions, (left, right) => string.CompareOrdinal(left.name, right.name));
            foreach (GameModeDefinition definition in definitions)
            {
                try
                {
                    GameMode mode = definition.CreateMode();
                    if (!ids.Add(mode.Id))
                    {
                        throw new InvalidOperationException($"モードIDが重複しています: {mode.Id}");
                    }

                    modes.Add(mode);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"モード定義を登録できません: {definition.name}\n{exception}", definition);
                }
            }

            return new GameModeCatalog(modes);
        }
    }
}
