using UnityEngine;

namespace PranaTactics.GameFlow
{
    /// <summary>
    /// ソロプレイで選択されたキャラクター情報を
    /// シーン間で保持します。
    /// </summary>
    public static class SoloCharacterSelectionContext
    {
        /// <summary>
        /// 選択されているキャラクター名です。
        /// </summary>
        private static string _characterName =
            string.Empty;

        public static string CharacterName
        {
            get
            {
                return _characterName;
            }
        }

        public static bool HasSelection
        {
            get
            {
                return !string.IsNullOrWhiteSpace(
                    _characterName);
            }
        }

        /// <summary>
        /// 選択されたキャラクター名を保存します。
        /// </summary>
        /// <param name="characterName">
        /// 選択されたキャラクター名です。
        /// </param>
        public static void SetCharacter(
            string characterName)
        {
            string validCharacterName =
                characterName?.Trim() ??
                string.Empty;

            if (string.IsNullOrEmpty(
                    validCharacterName))
            {
                Debug.LogWarning(
                    $"{nameof(SoloCharacterSelectionContext)}: " +
                    "空のキャラクター名は保存できません。");

                return;
            }

            _characterName =
                validCharacterName;

            Debug.Log(
                $"{nameof(SoloCharacterSelectionContext)}: " +
                "ソロプレイ用キャラクターを保存しました。" +
                $" CharacterName={_characterName}");
        }

        /// <summary>
        /// 保存されているキャラクター選択を解除します。
        /// </summary>
        public static void Clear()
        {
            _characterName =
                string.Empty;
        }
    }
}
