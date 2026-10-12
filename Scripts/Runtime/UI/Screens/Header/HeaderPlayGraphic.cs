using UnityEngine;
using UnityEngine.UI;

namespace PranaTactics.UI.Screens.Header
{
    /// <summary>
    /// 中央のPLAYタブを下辺の短い台形として描画します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeaderPlayGraphic : MaskableGraphic
    {
        /// <summary>
        /// 矩形サイズに追従する台形の頂点を作成します。
        /// </summary>
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = rectTransform.rect;
            float inset = Mathf.Min(rect.height * 0.75f, rect.width * 0.2f);
            helper.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.zero);
            helper.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.zero);
            helper.AddVert(new Vector3(rect.xMax - inset, rect.yMin), color, Vector2.zero);
            helper.AddVert(new Vector3(rect.xMin + inset, rect.yMin), color, Vector2.zero);
            helper.AddTriangle(0, 1, 2);
            helper.AddTriangle(2, 3, 0);
        }
    }
}
