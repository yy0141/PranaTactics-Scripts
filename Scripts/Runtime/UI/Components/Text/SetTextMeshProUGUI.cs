using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace PranaTactics.UI
{
    /// <summary>
    /// 複数のTextMeshProUGUIを管理し、
    /// 指定されたインデックスの表示内容を変更します。
    /// </summary>
    /// <remarks>
    /// キャラクター名、プレイヤー名、装備名など、
    /// 同じ形式のテキストを複数のUIスロットへ設定する用途を想定しています。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SetTextMeshProUGUI : MonoBehaviour
    {
        /// <summary>
        /// 文字列を表示するTextMeshProUGUIの一覧です。
        /// </summary>
        [FormerlySerializedAs("_characterTexts")]
        [SerializeField]
        private List<TextMeshProUGUI> _targetTexts = new();

        public int Count
        {
            get
            {
                return _targetTexts.Count;
            }
        }

        /// <summary>
        /// 指定されたインデックスのテキストへ
        /// キャラクター名などの文字列を設定します。
        /// </summary>
        /// <param name="name">
        /// 表示する名前です。
        /// <c>null</c>の場合は空文字列を設定します。
        /// </param>
        /// <param name="index">
        /// 更新するテキストスロットのインデックスです。
        /// </param>
        /// <remarks>
        /// 既存コードとの互換性を維持するために残しているメソッドです。
        /// 汎用的な文字列を設定する場合は<see cref="SetText"/>を使用します。
        /// </remarks>
        public void SetName(
            string name,
            int index)
        {
            SetText(
                name,
                index);
        }

        /// <summary>
        /// 指定されたインデックスのテキストへ
        /// 文字列を設定します。
        /// </summary>
        /// <param name="text">
        /// 表示する文字列です。
        /// <c>null</c>の場合は空文字列を設定します。
        /// </param>
        /// <param name="index">
        /// 更新するテキストスロットのインデックスです。
        /// </param>
        public void SetText(
            string text,
            int index)
        {
            if (!TryGetTargetText(
                    index,
                    out TextMeshProUGUI targetText))
            {
                return;
            }

            SetTextActive(
                targetText,
                true);

            targetText.text =
                text ??
                string.Empty;
        }

        /// <summary>
        /// 指定されたインデックスのテキストと色を設定します。
        /// </summary>
        /// <param name="text">
        /// 表示する文字列です。
        /// </param>
        /// <param name="color">
        /// 設定する文字色です。
        /// </param>
        /// <param name="index">
        /// 更新するテキストスロットのインデックスです。
        /// </param>
        public void SetTextAndColor(
            string text,
            Color color,
            int index)
        {
            if (!TryGetTargetText(
                    index,
                    out TextMeshProUGUI targetText))
            {
                return;
            }

            SetTextActive(
                targetText,
                true);

            targetText.text =
                text ??
                string.Empty;

            targetText.color =
                color;
        }

        /// <summary>
        /// 指定されたインデックスのテキストを非表示にします。
        /// </summary>
        /// <param name="index">
        /// 非表示にするテキストスロットのインデックスです。
        /// </param>
        public void HideText(
            int index)
        {
            if (!TryGetTargetText(
                    index,
                    out TextMeshProUGUI targetText))
            {
                return;
            }

            SetTextActive(
                targetText,
                false);
        }

        /// <summary>
        /// 登録されているすべてのテキストを非表示にします。
        /// </summary>
        public void HideAll()
        {
            foreach (TextMeshProUGUI targetText in
                    _targetTexts)
            {
                if (targetText == null)
                {
                    continue;
                }

                SetTextActive(
                    targetText,
                    false);
            }
        }

        /// <summary>
        /// 指定されたインデックスのTextMeshProUGUIを取得します。
        /// </summary>
        /// <param name="index">
        /// 取得するテキストスロットのインデックスです。
        /// </param>
        /// <param name="targetText">
        /// 取得されたTextMeshProUGUIです。
        /// </param>
        /// <returns>
        /// 有効なTextMeshProUGUIを取得できた場合は<c>true</c>です。
        /// </returns>
        private bool TryGetTargetText(
            int index,
            out TextMeshProUGUI targetText)
        {
            targetText = null;

            if (index < 0 ||
                index >= _targetTexts.Count)
            {
                Debug.LogWarning(
                    $"{nameof(SetTextMeshProUGUI)}: " +
                    "テキストスロットのインデックスが範囲外です。" +
                    $" Index={index}, Count={_targetTexts.Count}",
                    this);

                return false;
            }

            targetText =
                _targetTexts[index];

            if (targetText == null)
            {
                Debug.LogWarning(
                    $"{nameof(SetTextMeshProUGUI)}: " +
                    "指定されたテキストスロットが設定されていません。" +
                    $" Index={index}",
                    this);

                return false;
            }

            return true;
        }

        /// <summary>
        /// 指定されたTextMeshProUGUIを表示または非表示にします。
        /// </summary>
        /// <param name="targetText">
        /// 表示状態を変更するTextMeshProUGUIです。
        /// </param>
        /// <param name="isActive">
        /// 表示する場合は<c>true</c>です。
        /// </param>
        private static void SetTextActive(
            TextMeshProUGUI targetText,
            bool isActive)
        {
            if (targetText == null ||
                targetText.gameObject.activeSelf ==
                isActive)
            {
                return;
            }

            targetText.gameObject.SetActive(
                isActive);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorに設定されたテキスト一覧を検証します。
        /// </summary>
        private void OnValidate()
        {
            if (_targetTexts == null)
            {
                _targetTexts =
                    new List<TextMeshProUGUI>();
            }
        }
#endif
    }
}
