using System;
using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.Match.Solo.Stages
{
    /// <summary>
    /// せん滅ステージを解放する順序で保持します。
    /// </summary>
    [CreateAssetMenu(menuName = "Prana Tactics/Solo/Stage Catalog")]
    public sealed class SoloStageCatalog : ScriptableObject
    {
        [SerializeField]
        private SoloStageDefinition[] _stages = Array.Empty<SoloStageDefinition>();

        public IReadOnlyList<SoloStageDefinition> Stages => Array.AsReadOnly(_stages);

        /// <summary>
        /// Resourcesからカタログを取得し、進行に必要な設定を検証します。
        /// </summary>
        public static SoloStageCatalog Load()
        {
            SoloStageCatalog catalog = Resources.Load<SoloStageCatalog>("Solo/Stages/EliminationCatalog");
            if (catalog == null || catalog._stages == null || catalog._stages.Length == 0)
            {
                throw new InvalidOperationException("せん滅ステージカタログが未設定です。");
            }

            var identifiers = new HashSet<string>(StringComparer.Ordinal);
            foreach (SoloStageDefinition stage in catalog._stages)
            {
                if (stage == null || !identifiers.Add(stage.StageId))
                {
                    throw new InvalidOperationException("ステージ参照が空、または識別子が重複しています。");
                }

                stage.LoadConfiguration();
            }

            return catalog;
        }
    }
}
