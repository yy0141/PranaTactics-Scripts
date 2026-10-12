using System;
using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.UI.Interaction
{
    /// <summary>
    /// UIオブジェクトのクリック状態と、
    /// クリック可能状態を管理する基底クラスです。
    /// </summary>
    /// <remarks>
    /// 同じゲームオブジェクトに設定されている
    /// <see cref="IClickableCondition"/>を評価し、
    /// 最終的なクリック可否を内部へ保存します。
    ///
    /// 保存されたクリック可否は、
    /// クリック処理とマウスオーバー処理の両方から参照されます。
    /// </remarks>
    public abstract class ClickBehaviorBase :
        MonoBehaviour,
        IClick
    {
        /// <summary>
        /// 外部からクリック操作を許可するかどうかです。
        /// </summary>
        [Header("Interaction")]
        [SerializeField]
        private bool _isClickEnabled = true;

        /// <summary>
        /// 現在クリック状態であるかどうかです。
        /// </summary>
        private bool _isClicked;

        /// <summary>
        /// 条件判定を反映した現在のクリック可否です。
        /// </summary>
        private bool _isClickAllowed;

        /// <summary>
        /// クリック条件を実装しているコンポーネントです。
        /// </summary>
        private readonly List<MonoBehaviour>
            _clickableConditionBehaviors = new();

        /// <summary>
        /// クリック条件を取得済みかどうかです。
        /// </summary>
        private bool _hasCachedClickableConditions;

        protected bool IsClicked
        {
            get
            {
                return _isClicked;
            }
        }

        public bool IsClickEnabled
        {
            get
            {
                return _isClickEnabled;
            }
        }

        public bool IsClickAllowed
        {
            get
            {
                return _isClickAllowed;
            }
        }

        public event Action<bool> ClickAvailabilityChanged;

        /// <summary>
        /// クリック状態へ遷移したときに呼び出されます。
        /// </summary>
        protected virtual void OnClickEnter()
        {
        }

        /// <summary>
        /// クリック状態である間に呼び出されます。
        /// </summary>
        protected virtual void OnClickUpdate()
        {
        }

        /// <summary>
        /// クリック状態が終了したときに呼び出されます。
        /// </summary>
        protected virtual void OnClickExit()
        {
        }

        /// <summary>
        /// クリック可否が変更されたときに呼び出されます。
        /// </summary>
        /// <param name="isClickAllowed">
        /// 変更後のクリック可否です。
        /// </param>
        protected virtual void OnClickAvailabilityChanged(
            bool isClickAllowed)
        {
        }

        /// <summary>
        /// 外部からのクリック許可状態を設定します。
        /// </summary>
        /// <param name="isEnabled">
        /// クリック操作を許可する場合は<c>true</c>です。
        /// </param>
        public void SetClickEnabled(
            bool isEnabled)
        {
            if (_isClickEnabled ==
                isEnabled)
            {
                return;
            }

            _isClickEnabled =
                isEnabled;

            RefreshClickAvailability();
        }

        /// <summary>
        /// クリック条件を再評価し、
        /// 最終的なクリック可否を保存します。
        /// </summary>
        /// <returns>
        /// クリック可能な場合は<c>true</c>です。
        /// </returns>
        public bool RefreshClickAvailability()
        {
            bool nextClickAllowed =
                isActiveAndEnabled &&
                _isClickEnabled;

            if (nextClickAllowed)
            {
                CacheClickableConditions();

                foreach (MonoBehaviour conditionBehavior in
                        _clickableConditionBehaviors)
                {
                    if (conditionBehavior == null)
                    {
                        continue;
                    }

                    if (conditionBehavior is not
                        IClickableCondition condition)
                    {
                        continue;
                    }

                    if (condition.CanClick())
                    {
                        continue;
                    }

                    nextClickAllowed =
                        false;

                    break;
                }
            }

            SetClickAllowedInternal(
                nextClickAllowed);

            return _isClickAllowed;
        }

        /// <summary>
        /// クリック条件のコンポーネント一覧を
        /// 再取得します。
        /// </summary>
        /// <remarks>
        /// 実行中にクリック条件を追加または削除した場合に
        /// 呼び出してください。
        /// </remarks>
        public void RefreshClickableConditionCache()
        {
            _hasCachedClickableConditions =
                false;

            _clickableConditionBehaviors.Clear();

            CacheClickableConditions();

            RefreshClickAvailability();
        }

        /// <summary>
        /// クリック状態の開始処理を実行します。
        /// </summary>
        void IClick.Enter()
        {
            if (!RefreshClickAvailability())
            {
                return;
            }

            _isClicked =
                true;

            OnClickEnter();
        }

        /// <summary>
        /// クリック状態の更新処理を実行します。
        /// </summary>
        void IClick.Update()
        {
            if (!_isClicked)
            {
                return;
            }

            if (!RefreshClickAvailability())
            {
                ExitClickInternal();

                return;
            }

            OnClickUpdate();
        }

        /// <summary>
        /// クリック状態の終了処理を実行します。
        /// </summary>
        void IClick.Exit()
        {
            ExitClickInternal();
        }

        /// <summary>
        /// コンポーネントが無効になったとき、
        /// クリック状態とクリック可能状態を解除します。
        /// </summary>
        protected virtual void OnDisable()
        {
            ExitClickInternal();

            SetClickAllowedInternal(
                false);
        }

        /// <summary>
        /// 現在のクリック状態を解除します。
        /// </summary>
        private void ExitClickInternal()
        {
            if (!_isClicked)
            {
                return;
            }

            _isClicked =
                false;

            OnClickExit();
        }

        /// <summary>
        /// クリック可能状態を設定します。
        /// </summary>
        /// <param name="isClickAllowed">
        /// クリック可能な場合は<c>true</c>です。
        /// </param>
        private void SetClickAllowedInternal(
            bool isClickAllowed)
        {
            if (_isClickAllowed ==
                isClickAllowed)
            {
                return;
            }

            _isClickAllowed =
                isClickAllowed;

            if (!_isClickAllowed)
            {
                ExitClickInternal();
            }

            OnClickAvailabilityChanged(
                _isClickAllowed);

            ClickAvailabilityChanged?.Invoke(
                _isClickAllowed);
        }

        /// <summary>
        /// 同じゲームオブジェクトに設定された
        /// クリック条件を取得します。
        /// </summary>
        private void CacheClickableConditions()
        {
            if (_hasCachedClickableConditions)
            {
                return;
            }

            _clickableConditionBehaviors.Clear();

            MonoBehaviour[] behaviors =
                GetComponents<MonoBehaviour>();

            foreach (MonoBehaviour behavior in
                    behaviors)
            {
                if (behavior == null ||
                    behavior is not IClickableCondition)
                {
                    continue;
                }

                _clickableConditionBehaviors.Add(
                    behavior);
            }

            _hasCachedClickableConditions =
                true;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspector上の変更時に
        /// クリック条件キャッシュを解除します。
        /// </summary>
        private void OnValidate()
        {
            _hasCachedClickableConditions =
                false;
        }
#endif
    }
}
