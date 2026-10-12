using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PranaTactics.Match.Modes
{
    /// <summary>
    /// 登録されたゲームモードを検証し、区分別の選択候補とID検索を提供します。
    /// </summary>
    public sealed class GameModeCatalog
    {
        /// <summary>
        /// 安定した識別子からモードを取得する索引です。
        /// </summary>
        private readonly Dictionary<string, GameMode> _modesById = new(StringComparer.Ordinal);

        public IReadOnlyList<GameMode> Modes { get; }

        public GameModeCatalog(IEnumerable<GameMode> modes)
        {
            if (modes == null)
            {
                throw new ArgumentNullException(nameof(modes));
            }

            var orderedModes = new List<GameMode>();
            foreach (GameMode mode in modes)
            {
                if (mode == null || _modesById.ContainsKey(mode.Id))
                {
                    throw new ArgumentException("モードのnullまたはID重複があります。", nameof(modes));
                }

                _modesById.Add(mode.Id, mode);
                orderedModes.Add(mode);
            }

            Modes = new ReadOnlyCollection<GameMode>(orderedModes);
        }

        /// <summary>
        /// IDと接続区分の両方が一致するモードを取得します。
        /// </summary>
        public bool TryGet(string id, PlayCategory category, out GameMode mode)
        {
            mode = null;
            if (id == null || !_modesById.TryGetValue(id, out GameMode candidate) || !candidate.Supports(category))
            {
                return false;
            }

            mode = candidate;
            return true;
        }
    }
}
