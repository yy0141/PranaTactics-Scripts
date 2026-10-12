using System;
using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.UI.Interaction
{
    /// <summary>
    /// UIオブジェクトのクリック入力と、
    /// グループごとのクリック状態を管理します。
    /// </summary>
    /// <remarks>
    /// 通常のクリック対象は同じUIオブジェクトが
    /// すでに選択されている場合、状態を再適用しません。
    ///
    /// <see cref="RepeatClickBehaviorBase"/>を持つUIは、
    /// すでに選択されている場合でもクリック状態を再適用します。
    /// </remarks>
    public sealed class ClickManager
    {
        /// <summary>
        /// 左マウスボタンのボタン番号です。
        /// </summary>
        private const int k_LeftMouseButton = 0;

        /// <summary>
        /// 現在クリック状態にあるUIオブジェクトを
        /// グループごとに保持します。
        /// </summary>
        private readonly Dictionary<string, List<GameObject>>
            _currentClicks =
                new(StringComparer.Ordinal);

        /// <summary>
        /// 現在クリック状態にある
        /// すべてのUIオブジェクトです。
        /// </summary>
        private readonly HashSet<GameObject>
            _currentClickObjects = new();

        /// <summary>
        /// 今回の入力でクリック候補になったUIを
        /// レイキャスト順に保持します。
        /// </summary>
        private readonly List<GameObject>
            _clickCandidates = new();

        /// <summary>
        /// クリック候補とグループ名の対応です。
        /// </summary>
        private readonly Dictionary<GameObject, string>
            _candidateGroups = new();

        /// <summary>
        /// クリック候補とクリック処理の対応です。
        /// </summary>
        private readonly Dictionary<GameObject, ClickBehaviorBase>
            _candidateBehaviors = new();

        /// <summary>
        /// 今回のクリックで影響を受けるグループです。
        /// </summary>
        private readonly HashSet<string>
            _affectedGroups =
                new(StringComparer.Ordinal);

        /// <summary>
        /// 削除するグループ名を一時的に保持します。
        /// </summary>
        private readonly List<string>
            _groupRemovalBuffer = new();

        /// <summary>
        /// 現在クリック状態にあるUIオブジェクトを
        /// グループごとに取得します。
        /// </summary>
        public Dictionary<string, List<GameObject>>
            GetCurrentClicks()
        {
            CleanDestroyedCurrentClicks();

            return _currentClicks;
        }

        /// <summary>
        /// 指定されたUIを
        /// 初期状態のクリック対象として処理します。
        /// </summary>
        public void ClickDefaultUI(
            List<GameObject> defaultClickUIs)
        {
            HandleClick(
                defaultClickUIs,
                true);
        }

        /// <summary>
        /// 現在のUIレイキャスト結果に基づいて、
        /// クリック状態を更新します。
        /// </summary>
        public void HandleClick(
            List<GameObject> results,
            bool isDefaultUI = false)
        {
            UIManager uiManager =
                UIManager.Instance;

            if (uiManager == null)
            {
                Debug.LogWarning(
                    $"{nameof(ClickManager)}: " +
                    $"{nameof(UIManager)}が存在しないため、" +
                    "クリック処理を実行できません。");

                return;
            }

            CleanDestroyedCurrentClicks();

            ExitUnavailableCurrentClicks(
                uiManager);

            if (!isDefaultUI &&
                !Input.GetMouseButtonDown(
                    k_LeftMouseButton))
            {
                return;
            }

            PrepareClickCandidates(
                uiManager,
                results);

            if (_clickCandidates.Count == 0)
            {
                return;
            }

            ExitUnselectedClicks(
                uiManager);

            ApplyClickCandidates(
                uiManager);
        }

        /// <summary>
        /// 指定されたUIが
        /// 現在クリック状態かどうかを判定します。
        /// </summary>
        public bool IsClicked(
            GameObject uiObject)
        {
            if (uiObject == null)
            {
                return false;
            }

            CleanDestroyedCurrentClicks();

            return _currentClickObjects.Contains(
                uiObject);
        }

        /// <summary>
        /// 指定されたグループのクリック状態を
        /// すべて解除します。
        /// </summary>
        public void ClearGroup(
            string groupName)
        {
            if (string.IsNullOrWhiteSpace(
                    groupName))
            {
                return;
            }

            UIManager uiManager =
                UIManager.Instance;

            if (uiManager == null ||
                !_currentClicks.TryGetValue(
                    groupName,
                    out List<GameObject> clickedObjects))
            {
                return;
            }

            for (int index =
                    clickedObjects.Count - 1;
                index >= 0;
                index--)
            {
                GameObject clickedObject =
                    clickedObjects[index];

                ExitClickState(
                    uiManager,
                    clickedObject);

                _currentClickObjects.Remove(
                    clickedObject);
            }

            _currentClicks.Remove(
                groupName);
        }

        /// <summary>
        /// すべてのUIのクリック状態を解除します。
        /// </summary>
        public void ClearAll()
        {
            UIManager uiManager =
                UIManager.Instance;

            if (uiManager != null)
            {
                foreach (KeyValuePair<string, List<GameObject>>
                        clickGroup in _currentClicks)
                {
                    List<GameObject> clickedObjects =
                        clickGroup.Value;

                    if (clickedObjects == null)
                    {
                        continue;
                    }

                    foreach (GameObject clickedObject in
                            clickedObjects)
                    {
                        ExitClickState(
                            uiManager,
                            clickedObject);
                    }
                }
            }

            _currentClicks.Clear();
            _currentClickObjects.Clear();
        }

        /// <summary>
        /// レイキャスト結果から、
        /// 今回処理するクリック候補を作成します。
        /// </summary>
        private void PrepareClickCandidates(
            UIManager uiManager,
            IReadOnlyList<GameObject> results)
        {
            _clickCandidates.Clear();
            _candidateGroups.Clear();
            _candidateBehaviors.Clear();
            _affectedGroups.Clear();

            if (results == null)
            {
                return;
            }

            for (int index = 0;
                index < results.Count;
                index++)
            {
                GameObject resultObject =
                    results[index];

                if (resultObject == null ||
                    _candidateGroups.ContainsKey(
                        resultObject))
                {
                    continue;
                }

                ClickBehaviorBase clickBehavior =
                    resultObject.GetComponent<ClickBehaviorBase>();

                if (clickBehavior == null)
                {
                    continue;
                }

                if (!clickBehavior.RefreshClickAvailability())
                {
                    continue;
                }

                string groupName =
                    uiManager.GetGroupName(
                        resultObject);

                if (string.IsNullOrWhiteSpace(
                        groupName))
                {
                    continue;
                }

                _clickCandidates.Add(
                    resultObject);

                _candidateGroups.Add(
                    resultObject,
                    groupName);

                _candidateBehaviors.Add(
                    resultObject,
                    clickBehavior);

                _affectedGroups.Add(
                    groupName);
            }
        }

        /// <summary>
        /// クリック不可へ変化したUIの
        /// クリック状態を解除します。
        /// </summary>
        private void ExitUnavailableCurrentClicks(
            UIManager uiManager)
        {
            _groupRemovalBuffer.Clear();

            foreach (KeyValuePair<string, List<GameObject>>
                    clickGroup in _currentClicks)
            {
                List<GameObject> clickedObjects =
                    clickGroup.Value;

                if (clickedObjects == null)
                {
                    _groupRemovalBuffer.Add(
                        clickGroup.Key);

                    continue;
                }

                for (int index =
                        clickedObjects.Count - 1;
                    index >= 0;
                    index--)
                {
                    GameObject clickedObject =
                        clickedObjects[index];

                    ClickBehaviorBase clickBehavior =
                        clickedObject != null
                            ? clickedObject
                                .GetComponent<ClickBehaviorBase>()
                            : null;

                    bool canRemainClicked =
                        clickBehavior != null &&
                        clickBehavior
                            .RefreshClickAvailability();

                    if (canRemainClicked)
                    {
                        continue;
                    }

                    ExitClickState(
                        uiManager,
                        clickedObject);

                    clickedObjects.RemoveAt(
                        index);

                    _currentClickObjects.Remove(
                        clickedObject);
                }

                if (clickedObjects.Count == 0)
                {
                    _groupRemovalBuffer.Add(
                        clickGroup.Key);
                }
            }

            foreach (string groupName in
                    _groupRemovalBuffer)
            {
                _currentClicks.Remove(
                    groupName);
            }

            _groupRemovalBuffer.Clear();
        }

        /// <summary>
        /// 今回クリックされたグループ内で、
        /// 新しい候補に含まれないUIの状態を解除します。
        /// </summary>
        private void ExitUnselectedClicks(
            UIManager uiManager)
        {
            foreach (string groupName in
                    _affectedGroups)
            {
                if (!_currentClicks.TryGetValue(
                        groupName,
                        out List<GameObject> clickedObjects) ||
                    clickedObjects == null)
                {
                    continue;
                }

                for (int index =
                        clickedObjects.Count - 1;
                    index >= 0;
                    index--)
                {
                    GameObject clickedObject =
                        clickedObjects[index];

                    if (ShouldRemainClicked(
                            clickedObject,
                            groupName))
                    {
                        continue;
                    }

                    ExitClickState(
                        uiManager,
                        clickedObject);

                    clickedObjects.RemoveAt(
                        index);

                    _currentClickObjects.Remove(
                        clickedObject);
                }

                if (clickedObjects.Count == 0)
                {
                    _currentClicks.Remove(
                        groupName);
                }
            }
        }

        /// <summary>
        /// 既にクリックされているUIが、
        /// 今回も同じグループの候補か判定します。
        /// </summary>
        private bool ShouldRemainClicked(
            GameObject clickedObject,
            string currentGroupName)
        {
            if (clickedObject == null)
            {
                return false;
            }

            return
                _candidateGroups.TryGetValue(
                    clickedObject,
                    out string candidateGroupName) &&
                string.Equals(
                    currentGroupName,
                    candidateGroupName,
                    StringComparison.Ordinal);
        }

        /// <summary>
        /// 今回のクリック候補へ
        /// クリック状態を適用します。
        /// </summary>
        private void ApplyClickCandidates(
            UIManager uiManager)
        {
            foreach (GameObject candidateObject in
                    _clickCandidates)
            {
                if (candidateObject == null ||
                    !_candidateBehaviors.TryGetValue(
                        candidateObject,
                        out ClickBehaviorBase clickBehavior) ||
                    !_candidateGroups.TryGetValue(
                        candidateObject,
                        out string groupName))
                {
                    continue;
                }

                if (!clickBehavior.RefreshClickAvailability())
                {
                    continue;
                }

                bool isRepeatClick =
                    clickBehavior is IRepeatClick;

                bool isAlreadyClicked =
                    _currentClickObjects.Contains(
                        candidateObject);

                if (isAlreadyClicked)
                {
                    if (isRepeatClick)
                    {
                        IClick repeatClick =
                            clickBehavior;

                        repeatClick.Enter();
                    }

                    continue;
                }

                ApplyClickState(
                    uiManager,
                    candidateObject,
                    clickBehavior);

                if (candidateObject == null)
                {
                    continue;
                }

                RegisterCurrentClick(
                    candidateObject,
                    groupName);
            }
        }

        /// <summary>
        /// 指定されたUIへクリック状態を適用します。
        /// </summary>
        private static void ApplyClickState(
            UIManager uiManager,
            GameObject uiObject,
            ClickBehaviorBase clickBehavior)
        {
            ClickState clickState =
                new(
                    uiObject,
                    clickBehavior);

            uiManager.RegisterUIObject(
                uiObject,
                clickState);
        }

        /// <summary>
        /// 指定されたUIのクリック状態を解除します。
        /// </summary>
        private static void ExitClickState(
            UIManager uiManager,
            GameObject uiObject)
        {
            if (uiObject == null)
            {
                return;
            }

            UIStateMachine stateMachine =
                uiManager.GetUIStateMachine(
                    uiObject);

            if (stateMachine == null ||
                stateMachine.CurrentState is not
                    ClickState)
            {
                return;
            }

            stateMachine.ChangeState(
                new NoneState());
        }

        /// <summary>
        /// UIオブジェクトを
        /// 現在のクリック一覧へ登録します。
        /// </summary>
        private void RegisterCurrentClick(
            GameObject uiObject,
            string groupName)
        {
            if (uiObject == null ||
                string.IsNullOrWhiteSpace(
                    groupName))
            {
                return;
            }

            if (!_currentClicks.TryGetValue(
                    groupName,
                    out List<GameObject> clickedObjects))
            {
                clickedObjects =
                    new List<GameObject>();

                _currentClicks.Add(
                    groupName,
                    clickedObjects);
            }

            if (!_currentClickObjects.Add(
                    uiObject))
            {
                return;
            }

            clickedObjects.Add(
                uiObject);
        }

        /// <summary>
        /// 破棄済みUIと重複登録を
        /// 現在のクリック一覧から削除します。
        /// </summary>
        private void CleanDestroyedCurrentClicks()
        {
            _currentClickObjects.Clear();
            _groupRemovalBuffer.Clear();

            foreach (KeyValuePair<string, List<GameObject>>
                    clickGroup in _currentClicks)
            {
                List<GameObject> clickedObjects =
                    clickGroup.Value;

                if (clickedObjects == null)
                {
                    _groupRemovalBuffer.Add(
                        clickGroup.Key);

                    continue;
                }

                for (int index =
                        clickedObjects.Count - 1;
                    index >= 0;
                    index--)
                {
                    GameObject clickedObject =
                        clickedObjects[index];

                    if (clickedObject == null ||
                        !_currentClickObjects.Add(
                            clickedObject))
                    {
                        clickedObjects.RemoveAt(
                            index);
                    }
                }

                if (clickedObjects.Count == 0)
                {
                    _groupRemovalBuffer.Add(
                        clickGroup.Key);
                }
            }

            foreach (string groupName in
                    _groupRemovalBuffer)
            {
                _currentClicks.Remove(
                    groupName);
            }

            _groupRemovalBuffer.Clear();
        }
    }
}
