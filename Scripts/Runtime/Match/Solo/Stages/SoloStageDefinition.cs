using System;
using UnityEngine;

namespace PranaTactics.Match.Solo.Stages
{
    /// <summary>
    /// ステージの識別子、紹介文とソロ編成JSONを保持します。
    /// </summary>
    [CreateAssetMenu(menuName = "Prana Tactics/Solo/Stage")]
    public sealed class SoloStageDefinition : ScriptableObject
    {
        [SerializeField]
        private string _stageId;

        [SerializeField]
        private string _displayName;

        [TextArea(3, 8)]
        [SerializeField]
        private string _description;

        [SerializeField]
        private TextAsset _battleConfiguration;

        public string StageId => _stageId;
        public string DisplayName => _displayName;
        public string Description => _description;

        /// <summary>
        /// ステージの設定を検証し、編成を取得します。
        /// </summary>
        public SoloBattleConfiguration LoadConfiguration()
        {
            if (string.IsNullOrWhiteSpace(_stageId) ||
                string.IsNullOrWhiteSpace(_displayName) || _battleConfiguration == null)
            {
                throw new InvalidOperationException($"ステージ設定が不足しています: {name}");
            }

            return SoloBattleConfiguration.Parse(_battleConfiguration.text);
        }
    }
}
