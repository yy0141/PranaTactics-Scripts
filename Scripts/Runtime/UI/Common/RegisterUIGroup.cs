using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace PranaTactics.UI
{
    /// <summary>
    /// 指定されたタグを持つ自身および子UIオブジェクトを、
    /// 同一グループとして<see cref="UIManager"/>へ登録します。
    /// </summary>
    /// <remarks>
    /// 非アクティブな子オブジェクトも登録対象に含まれます。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RegisterUIGroup : MonoBehaviour
    {
        /// <summary>
        /// UIオブジェクトを識別するタグ名です。
        /// </summary>
        private const string k_UiTagName = "UI";

        /// <summary>
        /// グループ名が未設定の場合に使用する既定値です。
        /// </summary>
        private const string k_DefaultGroupName = "None";

        /// <summary>
        /// UIManagerへ登録するグループ名です。
        /// </summary>
        [FormerlySerializedAs("_groupName")]
        [SerializeField]
        private string _uiGroupName = k_DefaultGroupName;

        /// <summary>
        /// UIManagerへ登録済みのオブジェクトを保持します。
        /// </summary>
        private readonly HashSet<GameObject>
            _registeredObjects = new();

        /// <summary>
        /// 自身および子UIオブジェクトを
        /// UIManagerへ登録します。
        /// </summary>
        private void Start()
        {
            RegisterTaggedUIObjects();
        }

        /// <summary>
        /// 指定されたタグを持つ自身および子オブジェクトを検索し、
        /// UIManagerへ登録します。
        /// </summary>
        private void RegisterTaggedUIObjects()
        {
            UIManager uiManager =
                UIManager.Instance;

            if (uiManager == null)
            {
                Debug.LogError(
                    $"{nameof(RegisterUIGroup)}: " +
                    $"{nameof(UIManager)}が存在しないため、" +
                    "UIグループを登録できません。",
                    this);

                return;
            }

            string groupName =
                NormalizeGroupName(
                    _uiGroupName);

            Transform[] childTransforms =
                GetComponentsInChildren<Transform>(
                    true);

            foreach (Transform childTransform in
                    childTransforms)
            {
                if (childTransform == null ||
                    !childTransform.CompareTag(
                        k_UiTagName))
                {
                    continue;
                }

                GameObject uiObject =
                    childTransform.gameObject;

                if (!_registeredObjects.Add(
                        uiObject))
                {
                    continue;
                }

                uiManager.RegisterUIGroup(
                    uiObject,
                    groupName);
            }
        }

        /// <summary>
        /// グループ名の前後の空白を除去し、
        /// 空の場合は既定値へ置き換えます。
        /// </summary>
        /// <param name="groupName">
        /// Inspectorに設定されたグループ名です。
        /// </param>
        /// <returns>
        /// UIManagerへの登録に使用するグループ名です。
        /// </returns>
        private static string NormalizeGroupName(
            string groupName)
        {
            if (string.IsNullOrWhiteSpace(
                    groupName))
            {
                return k_DefaultGroupName;
            }

            return groupName.Trim();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorに設定されたグループ名を検証します。
        /// </summary>
        private void OnValidate()
        {
            _uiGroupName =
                NormalizeGroupName(
                    _uiGroupName);
        }
#endif
    }
}
