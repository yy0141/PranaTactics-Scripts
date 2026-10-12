using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.UI.Interaction
{
    /// <summary>
    /// UIオブジェクトのマウスオーバー状態を管理します。
    /// </summary>
    /// <remarks>
    /// 現在ポインターが重なっているUIと
    /// クリック中のUIを比較しながら、
    /// <see cref="MouseOverState"/>の開始および終了を制御します。
    ///
    /// UIが<see cref="ClickBehaviorBase"/>を持つ場合は、
    /// 保存されているクリック可否に応じて
    /// マウスオーバー状態を制御します。
    /// </remarks>
    public sealed class MouseOverManager
    {
        /// <summary>
        /// 現在マウスオーバー状態が適用されている
        /// UIオブジェクトです。
        /// </summary>
        private readonly HashSet<GameObject>
            _activeMouseOverObjects = new();

        /// <summary>
        /// 現在ポインターが重なっている
        /// UIオブジェクトです。
        /// </summary>
        private readonly HashSet<GameObject>
            _raycastResultObjects = new();

        /// <summary>
        /// 現在クリックされている
        /// UIオブジェクトです。
        /// </summary>
        private readonly HashSet<GameObject>
            _clickedObjects = new();

        /// <summary>
        /// マウスオーバー状態を解除するUIを
        /// 一時的に保持します。
        /// </summary>
        private readonly List<GameObject>
            _removalBuffer = new();

        /// <summary>
        /// 現在のUIレイキャスト結果に基づいて、
        /// マウスオーバー状態を更新します。
        /// </summary>
        public void HandleMouseOver(
            IReadOnlyList<GameObject> results)
        {
            UIManager uiManager =
                UIManager.Instance;

            if (uiManager == null)
            {
                return;
            }

            UpdateRaycastResultObjects(
                results);

            UpdateClickedObjects(
                uiManager);

            ExitInvalidMouseOverStates(
                uiManager);

            EnterNewMouseOverStates(
                uiManager);
        }

        /// <summary>
        /// UIレイキャスト結果を一時集合へ反映します。
        /// </summary>
        private void UpdateRaycastResultObjects(
            IReadOnlyList<GameObject> results)
        {
            _raycastResultObjects.Clear();

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

                if (resultObject == null)
                {
                    continue;
                }

                _raycastResultObjects.Add(
                    resultObject);
            }
        }

        /// <summary>
        /// 現在クリックされているUIを
        /// 一時集合へ反映します。
        /// </summary>
        private void UpdateClickedObjects(
            UIManager uiManager)
        {
            _clickedObjects.Clear();

            Dictionary<string, List<GameObject>>
                currentClicks =
                    uiManager.GetCurrentClicks();

            if (currentClicks == null)
            {
                return;
            }

            foreach (KeyValuePair<string, List<GameObject>>
                    clickGroup in currentClicks)
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
                    if (clickedObject == null)
                    {
                        continue;
                    }

                    _clickedObjects.Add(
                        clickedObject);
                }
            }
        }

        /// <summary>
        /// ポインターが外れたUI、クリックされたUI、
        /// クリック不可になったUI、破棄されたUIの
        /// マウスオーバー状態を解除します。
        /// </summary>
        private void ExitInvalidMouseOverStates(
            UIManager uiManager)
        {
            _removalBuffer.Clear();

            foreach (GameObject uiObject in
                    _activeMouseOverObjects)
            {
                if (uiObject == null)
                {
                    _removalBuffer.Add(
                        uiObject);

                    continue;
                }

                bool isPointerOver =
                    _raycastResultObjects.Contains(
                        uiObject);

                bool isClicked =
                    _clickedObjects.Contains(
                        uiObject);

                bool canMouseOver =
                    CanApplyMouseOver(
                        uiObject);

                if (isPointerOver &&
                    !isClicked &&
                    canMouseOver)
                {
                    continue;
                }

                ExitMouseOverState(
                    uiManager,
                    uiObject);

                _removalBuffer.Add(
                    uiObject);
            }

            foreach (GameObject uiObject in
                    _removalBuffer)
            {
                _activeMouseOverObjects.Remove(
                    uiObject);
            }

            _removalBuffer.Clear();
        }

        /// <summary>
        /// 新しくポインターが重なったUIへ
        /// マウスオーバー状態を適用します。
        /// </summary>
        private void EnterNewMouseOverStates(
            UIManager uiManager)
        {
            foreach (GameObject uiObject in
                    _raycastResultObjects)
            {
                if (uiObject == null ||
                    _clickedObjects.Contains(
                        uiObject) ||
                    _activeMouseOverObjects.Contains(
                        uiObject))
                {
                    continue;
                }

                if (!CanApplyMouseOver(
                        uiObject))
                {
                    continue;
                }

                if (!TryEnterMouseOverState(
                        uiManager,
                        uiObject))
                {
                    continue;
                }

                _activeMouseOverObjects.Add(
                    uiObject);
            }
        }

        /// <summary>
        /// 指定されたUIへ
        /// マウスオーバーを適用できるか判定します。
        /// </summary>
        /// <remarks>
        /// クリック処理を持たないUIは、
        /// マウスオーバー専用UIとして許可します。
        ///
        /// クリック処理を持つ場合は、
        /// クリック条件を更新してから保存済みの
        /// <see cref="ClickBehaviorBase.IsClickAllowed"/>を参照します。
        /// </remarks>
        private static bool CanApplyMouseOver(
            GameObject uiObject)
        {
            if (uiObject == null)
            {
                return false;
            }

            ClickBehaviorBase clickBehavior =
                uiObject.GetComponent<ClickBehaviorBase>();

            if (clickBehavior == null)
            {
                return true;
            }

            clickBehavior.RefreshClickAvailability();

            return clickBehavior.IsClickAllowed;
        }

        /// <summary>
        /// 指定されたUIへ
        /// マウスオーバー状態を適用します。
        /// </summary>
        private static bool TryEnterMouseOverState(
            UIManager uiManager,
            GameObject uiObject)
        {
            UIStateMachine stateMachine =
                uiManager.GetUIStateMachine(
                    uiObject);

            if (stateMachine != null &&
                stateMachine.CurrentState is
                    MouseOverState)
            {
                return true;
            }

            MouseOverBehaviorBase mouseOverBehavior =
                uiObject.GetComponent<MouseOverBehaviorBase>();

            if (mouseOverBehavior == null)
            {
                return false;
            }

            MouseOverState mouseOverState =
                new(
                    uiObject,
                    mouseOverBehavior);

            uiManager.RegisterUIObject(
                uiObject,
                mouseOverState);

            return true;
        }

        /// <summary>
        /// 指定されたUIの
        /// マウスオーバー状態を解除します。
        /// </summary>
        private static void ExitMouseOverState(
            UIManager uiManager,
            GameObject uiObject)
        {
            UIStateMachine stateMachine =
                uiManager.GetUIStateMachine(
                    uiObject);

            if (stateMachine == null ||
                stateMachine.CurrentState is not
                    MouseOverState)
            {
                return;
            }

            stateMachine.ChangeState(
                new NoneState());
        }
    }
}
