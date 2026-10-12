using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.UI
{
    /// <summary>
    /// ゲーム開始時に、指定されたUIオブジェクトを
    /// デフォルトのクリック状態へ変更します。
    /// </summary>
    /// <remarks>
    /// 他のUIコンポーネントによるグループ登録や
    /// 状態登録が完了してから初期クリックを適用するため、
    /// 初期化時に1フレーム待機します。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class Default : MonoBehaviour
    {
        /// <summary>
        /// 初期状態でクリック状態へ変更する
        /// UIオブジェクトの一覧です。
        /// </summary>
        [SerializeField]
        private List<GameObject> _defaultClickUIs =
            new();

        /// <summary>
        /// 有効なデフォルトUIだけを一時的に保持します。
        /// </summary>
        private readonly List<GameObject>
            _validDefaultClickUIs = new();

        /// <summary>
        /// UIの登録処理を待ってから、
        /// デフォルトのクリック状態を適用します。
        /// </summary>
        private IEnumerator Start()
        {
            yield return null;

            UIManager uiManager =
                UIManager.Instance;

            if (uiManager == null)
            {
                Debug.LogError(
                    $"{nameof(Default)}: " +
                    $"{nameof(UIManager)}が存在しないため、" +
                    "デフォルトUIを選択できません。",
                    this);

                yield break;
            }

            CollectValidDefaultClickUIs();

            if (_validDefaultClickUIs.Count == 0)
            {
                Debug.LogWarning(
                    $"{nameof(Default)}: " +
                    "デフォルトで選択する有効なUIが設定されていません。",
                    this);

                yield break;
            }

            uiManager.ClickDefaultUI(
                _validDefaultClickUIs);
        }

        /// <summary>
        /// Inspectorに設定された一覧から、
        /// nullと重複を除いたUIオブジェクトを収集します。
        /// </summary>
        private void CollectValidDefaultClickUIs()
        {
            _validDefaultClickUIs.Clear();

            if (_defaultClickUIs == null)
            {
                return;
            }

            foreach (GameObject defaultClickUI in
                    _defaultClickUIs)
            {
                if (defaultClickUI == null ||
                    _validDefaultClickUIs.Contains(
                        defaultClickUI))
                {
                    continue;
                }

                _validDefaultClickUIs.Add(
                    defaultClickUI);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorに設定されたUI一覧を検証します。
        /// </summary>
        private void OnValidate()
        {
            if (_defaultClickUIs == null)
            {
                _defaultClickUIs =
                    new List<GameObject>();
            }
        }
#endif
    }
}
