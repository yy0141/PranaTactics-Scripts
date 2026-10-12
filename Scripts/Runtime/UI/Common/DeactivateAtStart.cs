using UnityEngine;

namespace PranaTactics.UI
{
    /// <summary>
    /// シーンの初期化時に、このコンポーネントが設定された
    /// ゲームオブジェクトを非アクティブ化します。
    /// </summary>
    /// <remarks>
    /// <see cref="Awake"/>で無効化することで、
    /// 初回フレームにUIが一瞬表示されることを防ぎます。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DeactivateAtStart : MonoBehaviour
    {
        /// <summary>
        /// ゲームオブジェクトの初期化時に、
        /// 自身を非アクティブ化します。
        /// </summary>
        private void Awake()
        {
            gameObject.SetActive(
                false);
        }
    }
}
