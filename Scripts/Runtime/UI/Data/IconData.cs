using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace PranaTactics.UI
{
    /// <summary>
    /// UIで使用するアイコンの名前とSpriteの対応を保持します。
    /// </summary>
    [CreateAssetMenu(
    fileName = "IconData",
    menuName = "Prana Tactics/Data/Icon Data",
    order = 1)]
    public sealed class IconData : ScriptableObject
    {
        /// <summary>
        /// 登録されているアイコンの一覧です。
        /// </summary>
        [FormerlySerializedAs("Icons")]
        [SerializeField]
        private List<Icon> _icons = new();

        public IReadOnlyList<Icon> Icons
        {
            get
            {
                return _icons;
            }
        }

        public int Count
        {
            get
            {
                return _icons.Count;
            }
        }

        /// <summary>
        /// 指定された名前のアイコン情報を取得します。
        /// </summary>
        /// <param name="iconName">
        /// 検索するアイコン名です。
        /// </param>
        /// <param name="icon">
        /// 見つかったアイコン情報です。
        /// </param>
        /// <returns>
        /// 指定された名前のアイコンが見つかった場合は
        /// <c>true</c>です。
        /// </returns>
        public bool TryGetIcon(
            string iconName,
            out Icon icon)
        {
            icon = null;

            if (string.IsNullOrWhiteSpace(
                    iconName))
            {
                return false;
            }

            foreach (Icon registeredIcon in
                    _icons)
            {
                if (registeredIcon == null)
                {
                    continue;
                }

                if (!string.Equals(
                        registeredIcon.Name,
                        iconName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                icon =
                    registeredIcon;

                return true;
            }

            return false;
        }

        /// <summary>
        /// 指定された名前に対応するSpriteを取得します。
        /// </summary>
        /// <param name="iconName">
        /// 検索するアイコン名です。
        /// </param>
        /// <returns>
        /// 見つかったSpriteです。
        /// 対応するアイコンがない場合は<c>null</c>です。
        /// </returns>
        public Sprite GetSprite(
            string iconName)
        {
            return TryGetIcon(
                iconName,
                out Icon icon)
                    ? icon.Sprite
                    : null;
        }

        /// <summary>
        /// 指定されたインデックスのアイコン情報を取得します。
        /// </summary>
        /// <param name="index">
        /// 取得するアイコンのインデックスです。
        /// </param>
        /// <param name="icon">
        /// 取得されたアイコン情報です。
        /// </param>
        /// <returns>
        /// 有効なアイコン情報を取得できた場合は<c>true</c>です。
        /// </returns>
        public bool TryGetIconAt(
            int index,
            out Icon icon)
        {
            icon = null;

            if (index < 0 ||
                index >= _icons.Count)
            {
                return false;
            }

            icon =
                _icons[index];

            return icon != null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorに設定されたアイコン情報を検証します。
        /// </summary>
        private void OnValidate()
        {
            if (_icons == null)
            {
                _icons =
                    new List<Icon>();

                return;
            }

            HashSet<string> registeredNames =
                new(StringComparer.Ordinal);

            for (int index = 0;
                index < _icons.Count;
                index++)
            {
                Icon icon =
                    _icons[index];

                if (icon == null)
                {
                    Debug.LogWarning(
                        $"{nameof(IconData)}: " +
                        $"nullのアイコン情報が登録されています。" +
                        $" Index={index}",
                        this);

                    continue;
                }

                if (string.IsNullOrWhiteSpace(
                        icon.Name))
                {
                    Debug.LogWarning(
                        $"{nameof(IconData)}: " +
                        $"名前が設定されていないアイコンがあります。" +
                        $" Index={index}",
                        this);

                    continue;
                }

                if (!registeredNames.Add(
                        icon.Name))
                {
                    Debug.LogWarning(
                        $"{nameof(IconData)}: " +
                        $"同じ名前のアイコンが複数登録されています。" +
                        $" Name={icon.Name}",
                        this);
                }
            }
        }

#endif

        /// <summary>
        /// アイコン名と表示用Spriteの対応を保持します。
        /// </summary>
        [Serializable]
        public sealed class Icon
        {
            /// <summary>
            /// アイコンを識別する名前です。
            /// </summary>
            [FormerlySerializedAs("NAME")]
            [SerializeField]
            private string _name = string.Empty;

            /// <summary>
            /// UIへ表示するSpriteです。
            /// </summary>
            [FormerlySerializedAs("SPRITE")]
            [SerializeField]
            private Sprite _sprite;

            public string Name
            {
                get
                {
                    return _name;
                }
            }

            public Sprite Sprite
            {
                get
                {
                    return _sprite;
                }
            }
        }
    }
}
