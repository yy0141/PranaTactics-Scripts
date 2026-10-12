using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace PranaTactics.Match.Modes
{
    /// <summary>
    /// 接続区分から独立したモード情報と試合ごとのルール生成を保持します。
    /// </summary>
    public sealed class GameMode
    {
        /// <summary>
        /// 試合ごとに独立したStrategyを生成するFactoryです。
        /// </summary>
        private readonly Func<IMatchRule> _createRule;

        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public IReadOnlyCollection<PlayCategory> Categories { get; }

        public GameMode(string id, string displayName, string description,
            IEnumerable<PlayCategory> categories, Func<IMatchRule> createRule)
        {
            if (string.IsNullOrWhiteSpace(id) || !Regex.IsMatch(id, "^[a-z0-9][a-z0-9_-]{0,47}$"))
            {
                throw new ArgumentException("モードIDは48文字以内の英小文字、数字、ハイフン、アンダースコアで指定してください。", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("モード表示名が必要です。", nameof(displayName));
            }

            var supportedCategories = new HashSet<PlayCategory>(categories ?? throw new ArgumentNullException(nameof(categories)));
            if (supportedCategories.Count == 0)
            {
                throw new ArgumentException("一つ以上の接続区分が必要です。", nameof(categories));
            }

            foreach (PlayCategory category in supportedCategories)
            {
                if (!Enum.IsDefined(typeof(PlayCategory), category))
                {
                    throw new ArgumentException("不明な接続区分です。", nameof(categories));
                }
            }

            Id = id;
            DisplayName = displayName;
            Description = description ?? string.Empty;
            Categories = new ReadOnlyCollection<PlayCategory>(new List<PlayCategory>(supportedCategories));
            _createRule = createRule ?? throw new ArgumentNullException(nameof(createRule));
        }

        /// <summary>
        /// 指定された接続区分で選択できるか確認します。
        /// </summary>
        public bool Supports(PlayCategory category)
        {
            foreach (PlayCategory supported in Categories)
            {
                if (supported == category)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 試合に適用する新しいルールStrategyを生成します。
        /// </summary>
        public IMatchRule CreateRule()
        {
            return _createRule() ?? throw new InvalidOperationException($"ルールを生成できません: {Id}");
        }
    }
}
