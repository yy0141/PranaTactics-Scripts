using System;
using System.Collections.Generic;

namespace PranaTactics.Match.Solo.Stages
{
    /// <summary>
    /// ステージ選択、解放状態と試合のクリア記録を接続します。
    /// </summary>
    public sealed class SoloStageSession
    {
        private readonly SoloStageCatalog _catalog;
        private readonly SoloStageProgress _progress;
        private readonly IReadOnlyList<string> _stageIds;
        private int _selectedIndex = -1;
        private bool _isBattleStarted;

        public IReadOnlyList<SoloStageDefinition> Stages => _catalog.Stages;
        public SoloStageDefinition SelectedStage => _selectedIndex < 0 ? null : Stages[_selectedIndex];

        public SoloStageSession(SoloStageCatalog catalog, ISoloStageProgressStore store)
        {
            _catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            _progress = new SoloStageProgress(store);
            var identifiers = new List<string>();
            foreach (SoloStageDefinition stage in catalog.Stages)
            {
                identifiers.Add(stage.StageId);
            }

            _stageIds = identifiers.AsReadOnly();
        }

        /// <summary>
        /// 指定されたステージの解放状態を取得します。
        /// </summary>
        public bool IsUnlocked(int index)
        {
            return _progress.IsUnlocked(_stageIds, index);
        }

        /// <summary>
        /// 指定されたステージのクリア状態を取得します。
        /// </summary>
        public bool IsCleared(int index)
        {
            return _progress.IsCleared(_stageIds[index]);
        }

        /// <summary>
        /// 解放済みステージだけを選択します。
        /// </summary>
        public void Select(int index)
        {
            if (!IsUnlocked(index))
            {
                throw new InvalidOperationException("未解放のステージは開始できません。");
            }

            _selectedIndex = index;
            _isBattleStarted = false;
        }

        /// <summary>
        /// 選択を解除し、途中退出の試合を終了します。
        /// </summary>
        public void ClearSelection()
        {
            _selectedIndex = -1;
            _isBattleStarted = false;
        }

        /// <summary>
        /// 選択中の編成を読み込み、今回の試合の記録を開始します。
        /// </summary>
        public SoloBattleConfiguration BeginBattle()
        {
            if (SelectedStage == null || !IsUnlocked(_selectedIndex))
            {
                throw new InvalidOperationException("開始するステージが選択されていません。");
            }

            SoloBattleConfiguration configuration = SelectedStage.LoadConfiguration();
            _isBattleStarted = true;
            return configuration;
        }

        /// <summary>
        /// 開始済みの試合結果を一度だけ受け付けます。
        /// </summary>
        public void RecordResult(bool hasWon)
        {
            if (!_isBattleStarted)
            {
                return;
            }

            _isBattleStarted = false;
            _progress.RecordResult(_stageIds, _selectedIndex, hasWon);
        }
    }
}
