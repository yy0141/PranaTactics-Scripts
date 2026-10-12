using UnityEngine;
using UnityEngine.UI;

using System.Collections.Generic;

namespace PranaTactics.UI.Interaction
{
    /// <summary>
    /// マウスオーバー中、自身および子孫に存在する
    /// UIグラフィックの透明度を変更します。
    /// </summary>
    /// <remarks>
    /// <see cref="Graphic"/>を継承する
    /// Image、RawImage、Text、TMP_Textなどが対象になります。
    /// 初期状態の透明度は<see cref="Awake"/>で保存され、
    /// マウスオーバー終了時に個別に復元されます。
    /// </remarks>
    public sealed class MouseOverAlphaChanger :
        MouseOverBehaviorBase
    {
        /// <summary>
        /// マウスオーバー中に設定する透明度です。
        /// </summary>
        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float _mouseOverAlpha = 0.5f;

        /// <summary>
        /// 対象となるグラフィックと、
        /// 変更前の透明度を保存します。
        /// </summary>
        private readonly List<GraphicAlphaState>
            _graphicAlphaStates = new();

        /// <summary>
        /// コンポーネントの初期化時に、
        /// 自身および子孫のグラフィックと透明度を保存します。
        /// </summary>
        private void Awake()
        {
            CaptureOriginalAlphaValues();
        }

        /// <summary>
        /// マウスオーバー状態になったとき、
        /// 対象となるすべてのグラフィックの透明度を変更します。
        /// </summary>
        protected override void OnMouseOverEnter()
        {
            SetAlpha(_mouseOverAlpha);
        }

        /// <summary>
        /// マウスオーバー状態が終了したとき、
        /// 対象となるすべてのグラフィックの透明度を
        /// 初期状態へ戻します。
        /// </summary>
        protected override void OnMouseOverExit()
        {
            RestoreOriginalAlphaValues();
        }

        /// <summary>
        /// 自身および子孫に存在するグラフィックの
        /// 現在の透明度を保存します。
        /// </summary>
        /// <remarks>
        /// 非アクティブな子オブジェクトも保存対象に含まれます。
        /// </remarks>
        public void CaptureOriginalAlphaValues()
        {
            _graphicAlphaStates.Clear();

            Graphic[] graphics =
                GetComponentsInChildren<Graphic>(
                    includeInactive: true);

            foreach (Graphic graphic in graphics)
            {
                if (graphic == null)
                {
                    continue;
                }

                _graphicAlphaStates.Add(
                    new GraphicAlphaState(
                        graphic,
                        graphic.color.a));
            }
        }

        /// <summary>
        /// 保存されているすべてのグラフィックへ、
        /// 指定された透明度を設定します。
        /// </summary>
        /// <param name="alpha">
        /// 設定する透明度です。
        /// </param>
        private void SetAlpha(float alpha)
        {
            float normalizedAlpha =
                Mathf.Clamp01(alpha);

            foreach (GraphicAlphaState state
                     in _graphicAlphaStates)
            {
                if (state.Graphic == null)
                {
                    continue;
                }

                Color color =
                    state.Graphic.color;

                color.a = normalizedAlpha;

                state.Graphic.color = color;
            }
        }

        /// <summary>
        /// 保存されているすべてのグラフィックを、
        /// 変更前の透明度へ戻します。
        /// </summary>
        private void RestoreOriginalAlphaValues()
        {
            foreach (GraphicAlphaState state
                     in _graphicAlphaStates)
            {
                if (state.Graphic == null)
                {
                    continue;
                }

                Color color =
                    state.Graphic.color;

                color.a = state.OriginalAlpha;

                state.Graphic.color = color;
            }
        }

        private readonly struct GraphicAlphaState
        {
            public Graphic Graphic
            {
                get;
            }

            public float OriginalAlpha
            {
                get;
            }

            public GraphicAlphaState(
    Graphic graphic,
    float originalAlpha)
            {
                Graphic = graphic;
                OriginalAlpha = originalAlpha;
            }
        }
    }
}
