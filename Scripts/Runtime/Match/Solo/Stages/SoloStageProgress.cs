using System;
using System.Collections.Generic;

namespace PranaTactics.Match.Solo.Stages
{
    public interface ISoloStageProgressStore
    {
        bool IsCleared(string stageId);
        void SaveClear(string stageId);
    }

    /// <summary>
    /// 保存方式に依存せず、順次解放とクリア記録を管理します。
    /// </summary>
    public sealed class SoloStageProgress
    {
        private readonly ISoloStageProgressStore _store;

        public SoloStageProgress(ISoloStageProgressStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>
        /// ステージのクリア済み状態を取得します。
        /// </summary>
        public bool IsCleared(string stageId)
        {
            return _store.IsCleared(stageId);
        }

        /// <summary>
        /// 先行ステージがすべてクリア済みの場合だけ解放します。
        /// </summary>
        public bool IsUnlocked(IReadOnlyList<string> stageIds, int index)
        {
            if (stageIds == null || index < 0 || index >= stageIds.Count)
            {
                return false;
            }

            for (int previous = 0; previous < index; previous++)
            {
                if (!_store.IsCleared(stageIds[previous]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 解放済みステージの勝利だけを一度記録します。
        /// </summary>
        public bool RecordResult(IReadOnlyList<string> stageIds, int index, bool hasWon)
        {
            if (!hasWon || !IsUnlocked(stageIds, index) || IsCleared(stageIds[index]))
            {
                return false;
            }

            _store.SaveClear(stageIds[index]);
            return true;
        }
    }
}
