using UnityEngine;

namespace PranaTactics.Settings
{
    /// <summary>
    /// ビルドに含めるプレイモードの制限を保持します。
    /// </summary>
    [CreateAssetMenu(menuName = "Prana Tactics/Game Build Settings")]
    public sealed class GameBuildSettings : ScriptableObject
    {
        /// <summary>
        /// ソロプレイだけを公開する場合に有効にします。
        /// </summary>
        [SerializeField]
        private bool _soloOnly;

        /// <summary>
        /// Resourcesから読み込んだビルド設定です。
        /// </summary>
        private static GameBuildSettings s_current;

        /// <summary>
        /// Player起動時に保存言語より日本語を優先するかどうかを指定します。
        /// </summary>
        [SerializeField]
        private bool _startInJapanese = true;

        public static bool StartInJapanese
        {
            get
            {
                if (s_current == null)
                {
                    s_current = Resources.Load<GameBuildSettings>("GameBuildSettings");
                }

                return s_current != null && s_current._startInJapanese;
            }
        }

        public static bool IsSoloOnly
        {
            get
            {
                if (s_current == null)
                {
                    s_current = Resources.Load<GameBuildSettings>("GameBuildSettings");
                }

                return s_current != null && s_current._soloOnly;
            }
        }
    }
}
