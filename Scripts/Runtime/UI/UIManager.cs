using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Controls;
using PranaTactics.Core;
using PranaTactics.UI.Interaction;

namespace PranaTactics.UI
{
    /// <summary>
    /// UIオブジェクトのグループ、状態、クリックおよび
    /// マウスオーバー処理を一括して管理します。
    /// </summary>
    /// <remarks>
    /// このコンポーネントはシーンをまたいで保持される
    /// シングルトンとして動作します。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class UIManager : MonoBehaviour
    {
        /// <summary>
        /// UIレイキャスト結果を取得できなかった場合に使用する
        /// 空のリストです。
        /// </summary>
        private static readonly List<GameObject>
            s_emptyRaycastResults = new();

        /// <summary>
        /// UIオブジェクトと所属グループ名の対応を保持します。
        /// </summary>
        private readonly Dictionary<GameObject, string>
            _uiGroups = new();

        /// <summary>
        /// UIオブジェクトと状態機械の対応を保持します。
        /// </summary>
        private readonly Dictionary<GameObject, UIStateMachine>
            _uiStateMachines = new();

        /// <summary>
        /// 破棄済みUIオブジェクトを辞書から削除するときに使用する
        /// 一時バッファです。
        /// </summary>
        private readonly List<GameObject>
            _removalBuffer = new();

        /// <summary>
        /// UI状態機械を安全に更新するために使用する
        /// 一時バッファです。
        /// </summary>
        /// <remarks>
        /// UI状態の更新中にUIオブジェクトの登録や解除が行われても、
        /// Dictionaryの列挙が変更されないように使用します。
        /// </remarks>
        private readonly List<UIStateMachine>
            _stateMachineUpdateBuffer = new();

        /// <summary>
        /// UIのクリック処理を管理します。
        /// </summary>
        private readonly ClickManager
            _clickManager = new();

        /// <summary>
        /// UIのマウスオーバー処理を管理します。
        /// </summary>
        private readonly MouseOverManager
            _mouseOverManager = new();

        public static UIManager Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// シングルトンを初期化します。
        /// </summary>
        private void Awake()
        {
            if (Instance != null &&
                Instance != this)
            {
                Debug.LogWarning(
                    $"{nameof(UIManager)}が複数生成されたため、" +
                    "後から生成されたオブジェクトを破棄します。",
                    this);

                Destroy(
                    gameObject);

                return;
            }

            Instance = this;

            DontDestroyOnLoad(
                gameObject);
        }

        /// <summary>
        /// 毎フレームUI入力およびUI状態を処理します。
        /// </summary>
        private void Update()
        {
            RemoveDestroyedUIObjects();
            HandleUIInput();
            UpdateUIStateMachines();
        }

        /// <summary>
        /// このインスタンスが破棄されたとき、
        /// シングルトン参照を解除します。
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 現在のUIレイキャスト結果を取得し、
        /// マウスオーバー処理とクリック処理へ渡します。
        /// </summary>
        private void HandleUIInput()
        {
            List<GameObject> raycastResults =
                InputRaycastUtility.GetUIRaycastResults();

            if (raycastResults == null)
            {
                raycastResults =
                    s_emptyRaycastResults;
            }

            _mouseOverManager.HandleMouseOver(
                raycastResults);

            _clickManager.HandleClick(
                raycastResults);
        }

        /// <summary>
        /// 登録されているすべてのUI状態機械を更新します。
        /// </summary>
        /// <remarks>
        /// 状態の更新中にUIの登録または解除が行われても
        /// Dictionaryの列挙が変更されないように、
        /// 一時バッファへ状態機械をコピーしてから更新します。
        /// </remarks>
        private void UpdateUIStateMachines()
        {
            _stateMachineUpdateBuffer.Clear();

            foreach (
                KeyValuePair<GameObject, UIStateMachine> pair
                in _uiStateMachines)
            {
                if (pair.Key == null ||
                    pair.Value == null)
                {
                    continue;
                }

                _stateMachineUpdateBuffer.Add(
                    pair.Value);
            }

            foreach (
                UIStateMachine stateMachine
                in _stateMachineUpdateBuffer)
            {
                stateMachine.UpdateState();
            }

            _stateMachineUpdateBuffer.Clear();
        }

        /// <summary>
        /// 指定されたUIオブジェクトを
        /// デフォルトの選択対象としてクリック処理します。
        /// </summary>
        /// <param name="defaultClickUIs">
        /// デフォルトでクリックするUIオブジェクトのリストです。
        /// </param>
        public void ClickDefaultUI(
            List<GameObject> defaultClickUIs)
        {
            if (defaultClickUIs == null ||
                defaultClickUIs.Count == 0)
            {
                return;
            }

            _clickManager.ClickDefaultUI(
                defaultClickUIs);
        }

        /// <summary>
        /// UIオブジェクトとその状態を登録します。
        /// </summary>
        /// <param name="uiObject">
        /// 状態を登録するUIオブジェクトです。
        /// </param>
        /// <param name="state">
        /// UIオブジェクトへ適用する状態です。
        /// </param>
        public void RegisterUIObject(
            GameObject uiObject,
            IState state)
        {
            if (uiObject == null)
            {
                Debug.LogError(
                    $"{nameof(UIManager)}: " +
                    "登録するUIオブジェクトがnullです。",
                    this);

                return;
            }

            if (state == null)
            {
                Debug.LogError(
                    $"{nameof(UIManager)}: " +
                    $"UI状態がnullです。Object={uiObject.name}",
                    uiObject);

                return;
            }

            if (state is not UIState uiState)
            {
                Debug.LogError(
                    $"{nameof(UIManager)}: " +
                    $"{nameof(UIState)}以外の状態は登録できません。" +
                    $" Object={uiObject.name}," +
                    $" State={state.GetType().Name}",
                    uiObject);

                return;
            }

            if (!_uiStateMachines.TryGetValue(
                    uiObject,
                    out UIStateMachine stateMachine))
            {
                stateMachine =
                    new UIStateMachine();

                _uiStateMachines.Add(
                    uiObject,
                    stateMachine);
            }

            stateMachine.ChangeState(
                uiState);

            Debug.Log(
                $"{nameof(UIManager)}: " +
                $"{uiObject.name}へ" +
                $"{uiState.GetType().Name}を登録しました。",
                uiObject);
        }

        /// <summary>
        /// 指定された複数のUIオブジェクトから、
        /// 最初に登録済みのグループ名を取得します。
        /// </summary>
        /// <param name="uiObjects">
        /// グループ名を検索するUIオブジェクトのリストです。
        /// </param>
        /// <returns>
        /// 最初に見つかったグループ名です。
        /// 登録されていない場合は<c>null</c>です。
        /// </returns>
        public string GetGroupName(
            List<GameObject> uiObjects)
        {
            if (uiObjects == null)
            {
                return null;
            }

            foreach (
                GameObject uiObject
                in uiObjects)
            {
                string groupName =
                    GetGroupName(
                        uiObject);

                if (groupName != null)
                {
                    return groupName;
                }
            }

            return null;
        }

        /// <summary>
        /// 指定されたUIオブジェクトのグループ名を取得します。
        /// </summary>
        /// <param name="uiObject">
        /// グループ名を取得するUIオブジェクトです。
        /// </param>
        /// <returns>
        /// 登録されているグループ名です。
        /// 登録されていない場合は<c>null</c>です。
        /// </returns>
        public string GetGroupName(
            GameObject uiObject)
        {
            if (uiObject == null)
            {
                return null;
            }

            return _uiGroups.TryGetValue(
                uiObject,
                out string groupName)
                    ? groupName
                    : null;
        }

        /// <summary>
        /// 指定されたUIオブジェクトの状態機械を取得します。
        /// </summary>
        /// <param name="uiObject">
        /// 状態機械を取得するUIオブジェクトです。
        /// </param>
        /// <returns>
        /// 登録されている状態機械です。
        /// 登録されていない場合は<c>null</c>です。
        /// </returns>
        public UIStateMachine GetUIStateMachine(
            GameObject uiObject)
        {
            if (uiObject == null)
            {
                return null;
            }

            return _uiStateMachines.TryGetValue(
                uiObject,
                out UIStateMachine stateMachine)
                    ? stateMachine
                    : null;
        }

        /// <summary>
        /// 現在クリック状態になっている
        /// UIオブジェクトの一覧を取得します。
        /// </summary>
        /// <returns>
        /// グループ名とクリック中のUIオブジェクトの対応です。
        /// </returns>
        /// <remarks>
        /// 返される辞書は<see cref="ClickManager"/>が管理する
        /// 実体である可能性があります。
        /// 呼び出し元では内容を直接変更しないでください。
        /// </remarks>
        public Dictionary<string, List<GameObject>>
            GetCurrentClicks()
        {
            return _clickManager.GetCurrentClicks();
        }

        /// <summary>
        /// UIオブジェクトを指定されたグループへ登録します。
        /// </summary>
        /// <param name="uiObject">
        /// グループへ登録するUIオブジェクトです。
        /// </param>
        /// <param name="groupName">
        /// UIオブジェクトが所属するグループ名です。
        /// </param>
        public void RegisterUIGroup(
            GameObject uiObject,
            string groupName)
        {
            if (uiObject == null)
            {
                Debug.LogError(
                    $"{nameof(UIManager)}: " +
                    "グループへ登録するUIオブジェクトがnullです。",
                    this);

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    groupName))
            {
                Debug.LogError(
                    $"{nameof(UIManager)}: " +
                    $"グループ名が空です。Object={uiObject.name}",
                    uiObject);

                return;
            }

            if (_uiGroups.ContainsKey(
                    uiObject))
            {
                Debug.LogWarning(
                    $"{nameof(UIManager)}: " +
                    $"{uiObject.name}は既にグループへ登録されています。" +
                    $" CurrentGroup={_uiGroups[uiObject]}",
                    uiObject);

                return;
            }

            string normalizedGroupName =
                groupName.Trim();

            _uiGroups.Add(
                uiObject,
                normalizedGroupName);

            Debug.Log(
                $"{nameof(UIManager)}: " +
                $"{uiObject.name}を" +
                $"{normalizedGroupName}へ登録しました。",
                uiObject);
        }

        /// <summary>
        /// UIオブジェクトに登録されている状態機械を解除します。
        /// </summary>
        /// <param name="uiObject">
        /// 登録を解除するUIオブジェクトです。
        /// </param>
        /// <returns>
        /// 登録を解除できた場合は<c>true</c>です。
        /// </returns>
        public bool UnregisterUIObject(
            GameObject uiObject)
        {
            if (uiObject == null)
            {
                return false;
            }

            if (!_uiStateMachines.TryGetValue(
                    uiObject,
                    out UIStateMachine stateMachine))
            {
                return false;
            }

            return _uiStateMachines.Remove(
                uiObject);
        }

        /// <summary>
        /// UIオブジェクトのグループ登録を解除します。
        /// </summary>
        /// <param name="uiObject">
        /// グループ登録を解除するUIオブジェクトです。
        /// </param>
        /// <returns>
        /// 登録を解除できた場合は<c>true</c>です。
        /// </returns>
        public bool UnregisterUIGroup(
            GameObject uiObject)
        {
            if (uiObject == null)
            {
                return false;
            }

            return _uiGroups.Remove(
                uiObject);
        }

        /// <summary>
        /// UIオブジェクトに関するすべての登録を解除します。
        /// </summary>
        /// <param name="uiObject">
        /// 登録を解除するUIオブジェクトです。
        /// </param>
        public void UnregisterUI(
            GameObject uiObject)
        {
            if (uiObject == null)
            {
                return;
            }

            _uiGroups.Remove(
                uiObject);

            if (_uiStateMachines.TryGetValue(
                    uiObject,
                    out UIStateMachine stateMachine))
            {
                _uiStateMachines.Remove(
                    uiObject);
            }
        }

        /// <summary>
        /// シーン遷移などで破棄されたUIオブジェクトを
        /// 管理用辞書から削除します。
        /// </summary>
        private void RemoveDestroyedUIObjects()
        {
            _removalBuffer.Clear();

            foreach (
                GameObject uiObject
                in _uiGroups.Keys)
            {
                if (uiObject == null)
                {
                    _removalBuffer.Add(
                        uiObject);
                }
            }

            foreach (
                GameObject uiObject
                in _removalBuffer)
            {
                _uiGroups.Remove(
                    uiObject);
            }

            _removalBuffer.Clear();

            foreach (
                KeyValuePair<GameObject, UIStateMachine> pair
                in _uiStateMachines)
            {
                if (pair.Key != null)
                {
                    continue;
                }

                _removalBuffer.Add(
                    pair.Key);
            }

            foreach (
                GameObject uiObject
                in _removalBuffer)
            {
                _uiStateMachines.Remove(
                    uiObject);
            }

            _removalBuffer.Clear();
        }
    }
}
