using UnityEngine;
using UnityEngine.UI;

namespace PranaTactics.UI.Screens.CharacterProfile
{
    /// <summary>
    /// キャラクター画像と情報パネルをつなぐ水平グラデーションを描画します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProfileGradient : MaskableGraphic
    {
        [SerializeField]
        private Color _leftColor = Color.clear;

        [SerializeField]
        private Color _rightColor = new Color(0.055f, 0.07f, 0.08f, 1.0f);

        /// <summary>
        /// 四隅の頂点色を使用して、追加テクスチャなしで濃淡を描画します。
        /// </summary>
        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect rect = rectTransform.rect;
            vertexHelper.AddVert(new Vector3(rect.xMin, rect.yMin), _leftColor, Vector2.zero);
            vertexHelper.AddVert(new Vector3(rect.xMin, rect.yMax), _leftColor, Vector2.up);
            vertexHelper.AddVert(new Vector3(rect.xMax, rect.yMax), _rightColor, Vector2.one);
            vertexHelper.AddVert(new Vector3(rect.xMax, rect.yMin), _rightColor, Vector2.right);
            vertexHelper.AddTriangle(0, 1, 2);
            vertexHelper.AddTriangle(2, 3, 0);
        }
    }
}
