using System;
using UnityEngine;

namespace PranaTactics.Match.Modes
{
    /// <summary>
    /// Resources/GameModesに配置する追加ゲームモードの定義です。
    /// </summary>
    [CreateAssetMenu(menuName = "Prana Tactics/Game Modes/Mode Definition")]
    public sealed class GameModeDefinition : ScriptableObject
    {
        /// <summary>
        /// 保存、ネットワーク同期、検索に共通で使用する安定したIDです。
        /// </summary>
        [SerializeField]
        private string _id;

        /// <summary>
        /// モード選択カードに表示する名前です。
        /// </summary>
        [SerializeField]
        private string _displayName;

        /// <summary>
        /// モードの目的を説明する文章です。
        /// </summary>
        [TextArea]
        [SerializeField]
        private string _description;

        /// <summary>
        /// このモードで利用できる接続区分です。
        /// </summary>
        [SerializeField]
        private PlayCategory[] _categories = { PlayCategory.Local, PlayCategory.Online };

        /// <summary>
        /// 試合ルールの生成と設定を担当するアセットです。
        /// </summary>
        [SerializeField]
        private GameRuleFactory _ruleFactory;

        /// <summary>
        /// Inspectorの定義を検証し、実行時のモード情報へ変換します。
        /// </summary>
        public GameMode CreateMode()
        {
            if (_ruleFactory == null)
            {
                throw new InvalidOperationException($"ルールFactoryが未設定です: {name}");
            }

            return new GameMode(_id, _displayName, _description, _categories, _ruleFactory.CreateRule);
        }
    }
}
