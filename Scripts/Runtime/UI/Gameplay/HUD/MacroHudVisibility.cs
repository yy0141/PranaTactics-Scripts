using UnityEngine;
using UnityEngine.UI;

using PranaTactics.Match.Phases;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// マクロ中の情報表示をカーソル位置で薄くし、背面のマスへの入力を通します。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class MacroHudVisibility : MonoBehaviour
    {
        /// <summary>
        /// マスを確認するときの表示濃度です。
        /// </summary>
        [SerializeField, Range(0.0f, 1.0f)]
        private float _hoverOpacity = 0.15f;

        /// <summary>
        /// 表示と入力透過を制御するグループです。
        /// </summary>
        private CanvasGroup _group;

        /// <summary>
        /// カーソルとの重なりを確認する表示要素です。
        /// </summary>
        private Graphic[] _graphics;

        /// <summary>
        /// 通常時のグループ濃度です。
        /// </summary>
        private float _originalAlpha;

        /// <summary>
        /// 通常時の入力遮断設定です。
        /// </summary>
        private bool _originalBlocksRaycasts;

        /// <summary>
        /// 表示部品と復元用の設定を取得します。
        /// </summary>
        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _graphics = GetComponentsInChildren<Graphic>(true);
            _originalAlpha = _group.alpha;
            _originalBlocksRaycasts = _group.blocksRaycasts;
        }

        /// <summary>
        /// 表示専用HUDの背後をマクロ中に確認・選択できるようにします。
        /// </summary>
        private void LateUpdate()
        {
            bool isMacro = MatchPhaseManager.Instance != null &&
                MatchPhaseManager.Instance.CurrentPhase == MatchPhase.Macro;
            _group.blocksRaycasts = !isMacro && _originalBlocksRaycasts;
            _group.alpha = _originalAlpha * (isMacro && IsPointerOverGraphics() ? _hoverOpacity : 1.0f);
        }

        /// <summary>
        /// レイキャスト設定に依存せず、表示要素の矩形との重なりを調べます。
        /// </summary>
        private bool IsPointerOverGraphics()
        {
            foreach (Graphic graphic in _graphics)
            {
                if (graphic == null || !graphic.isActiveAndEnabled || graphic.canvas == null)
                {
                    continue;
                }

                Canvas canvas = graphic.canvas;
                Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                if (RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform, Input.mousePosition, camera))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 非表示やシーン終了時に元の表示・入力設定へ戻します。
        /// </summary>
        private void OnDisable()
        {
            if (_group != null)
            {
                _group.alpha = _originalAlpha;
                _group.blocksRaycasts = _originalBlocksRaycasts;
            }
        }
    }
}
