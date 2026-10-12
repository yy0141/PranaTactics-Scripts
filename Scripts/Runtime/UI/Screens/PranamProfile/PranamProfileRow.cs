using TMPro;
using UnityEngine;
using UnityEngine.UI;

using PranaTactics.Pranam;

namespace PranaTactics.UI.Screens.PranamProfile
{
    /// <summary>
    /// スクロール一覧の1行に兵装と選択状態を表示します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PranamProfileRow : MonoBehaviour
    {
        [SerializeField]
        private PranamProfileClick _click;

        [SerializeField]
        private Image _icon;

        [SerializeField]
        private TMP_Text _name;

        [SerializeField]
        private TMP_Text _englishName;

        [SerializeField]
        private TMP_Text _category;

        [SerializeField]
        private GameObject _selection;

        public RectTransform Rect => (RectTransform)transform;

        /// <summary>
        /// 一覧内の位置と、既存クリック処理の参照を設定します。
        /// </summary>
        public void Configure(PranamProfileView view, int index, float spacing)
        {
            Rect.anchoredPosition = new Vector2(0.0f, -index * spacing);
            _click.ConfigureRow(view, index);
        }

        /// <summary>
        /// 行の表示を更新し、絞り込みに該当しない余剰行を非表示にします。
        /// </summary>
        public void Show(PranamData weapon, bool selected)
        {
            gameObject.SetActive(weapon != null);

            if (weapon == null)
            {
                return;
            }

            _icon.sprite = weapon.Icon;
            _icon.enabled = weapon.Icon != null;
            _name.text = weapon.DisplayName;
            _englishName.text = weapon.ProfileEnglishName;
            _category.text = PranamProfileMetrics.Create(weapon).Category;
            _selection.SetActive(selected);
        }
    }
}
