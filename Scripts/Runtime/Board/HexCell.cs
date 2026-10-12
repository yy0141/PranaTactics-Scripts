using Outline = PranaTactics.Rendering.Outlines.Outline;
using UnityEngine;

namespace PranaTactics.Board
{
    /// <summary>
    /// ヘックスマップを構成する1マス分のセルを管理します。
    /// </summary>
    public sealed class HexCell : MonoBehaviour
    {
        /// <summary>
        /// セルの選択状態などを表示するアウトラインです。
        /// </summary>
        [SerializeField]
        private Outline _outline;

        private bool _hasGuideOutline;

        private bool _isNormalOutlineEnabled;

        private Color _normalOutlineColor;

        private float _normalOutlineWidth;

        /// <summary>
        /// 味方の罠による強調が必要かどうかです。
        /// </summary>
        private bool _isTrapVisible;

        /// <summary>
        /// 設置マスまたは起動範囲の表示色です。
        /// </summary>
        private Color _trapHighlightColor;

        /// <summary>
        /// 実習表示の色です。
        /// </summary>
        private Color _guideOutlineColor;

        /// <summary>
        /// 実習表示の幅です。
        /// </summary>
        private float _guideOutlineWidth;

        /// <summary>
        /// 初期設定の通常表示を保持します。
        /// </summary>
        private void Awake()
        {
            CaptureNormalOutline();
        }

        /// <summary>
        /// 通常表示を一時的な強調で上書きする前に保存します。
        /// </summary>
        private void CaptureNormalOutline()
        {
            if (_outline != null && !_hasGuideOutline && !_isTrapVisible)
            {
                _isNormalOutlineEnabled = _outline.enabled;
                _normalOutlineColor = _outline.OutlineColor;
                _normalOutlineWidth = _outline.OutlineWidth;
            }
        }

        /// <summary>
        /// 設置マスの状態と、観測チーム向けの設置・起動範囲の表示色を更新します。
        /// </summary>
        public void SetTrapHighlight(bool hasTrap, bool isVisible, Color color)
        {
            CaptureNormalOutline();
            _isTrap = hasTrap;
            _isTrapVisible = isVisible;
            _trapHighlightColor = color;
            RefreshOutline();
        }

        /// <summary>
        /// 実習、罠、通常表示の優先順に色と表示状態を合成します。
        /// </summary>
        private void RefreshOutline()
        {
            if (_outline == null)
            {
                return;
            }
            _outline.OutlineColor = _hasGuideOutline ? _guideOutlineColor :
                _isTrapVisible ? _trapHighlightColor : _normalOutlineColor;
            _outline.OutlineWidth = _hasGuideOutline ? _guideOutlineWidth :
                _isTrapVisible ? Mathf.Max(3.0f, _normalOutlineWidth) : _normalOutlineWidth;
            _outline.enabled = _hasGuideOutline || _isTrapVisible || _isNormalOutlineEnabled;
        }

        /// <summary>
        /// セルが配置されている階層を示す値です。
        /// </summary>
        private int _floor;

        /// <summary>
        /// ヘックスマップ上におけるセルの座標です。
        /// </summary>
        private Vector2Int _coordinate;

        /// <summary>
        /// このセルがトラップセルであるかどうかを示します。
        /// </summary>
        private bool _isTrap;

        /// <summary>
        /// このセルへテレポートできるかどうかを示します。
        /// </summary>
        private bool _canTeleport;

        /// <summary>
        /// アウトラインの表示状態を設定します。
        /// </summary>
        /// <param name="isEnabled">
        /// アウトラインを表示する場合は<c>true</c>、
        /// 非表示にする場合は<c>false</c>です。
        /// </param>
        public void SetOutlineEnabled(bool isEnabled)
        {
            if (_outline == null)
            {
                Debug.LogWarning(
                    $"{nameof(HexCell)}にOutlineが設定されていません。",
                    this);

                return;
            }

            CaptureNormalOutline();
            _isNormalOutlineEnabled = isEnabled;
            RefreshOutline();
        }

        /// <summary>
        /// 通常の移動可能表示を保持したまま、実習対象の輪郭を強調します。
        /// </summary>
        public void ShowGuideOutline(Color color, float width)
        {
            if (_outline == null)
            {
                return;
            }

            CaptureNormalOutline();
            _hasGuideOutline = true;
            _guideOutlineColor = color;
            _guideOutlineWidth = width;
            RefreshOutline();
        }

        /// <summary>
        /// 実習対象の強調を解除し、通常の輪郭表示へ戻します。
        /// </summary>
        public void ClearGuideOutline()
        {
            if (!_hasGuideOutline || _outline == null)
            {
                return;
            }

            _hasGuideOutline = false;
            RefreshOutline();
        }

        /// <summary>
        /// セルが配置されている階層を設定します。
        /// </summary>
        /// <param name="floor">
        /// セルに設定する階層です。
        /// </param>
        public void SetFloor(int floor)
        {
            _floor = floor;
        }

        /// <summary>
        /// ヘックスマップ上におけるセルの座標を設定します。
        /// </summary>
        /// <param name="x">
        /// セルのX座標です。
        /// </param>
        /// <param name="y">
        /// セルのY座標です。
        /// </param>
        public void SetCoordinate(int x, int y)
        {
            _coordinate = new Vector2Int(x, y);
        }

        /// <summary>
        /// アウトラインが表示されているかどうかを取得します。
        /// </summary>
        /// <returns>
        /// アウトラインが設定され、表示されている場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        public bool IsOutlineEnabled()
        {
            return _outline != null && _outline.enabled;
        }

        /// <summary>
        /// セルが配置されている階層を取得します。
        /// </summary>
        /// <returns>
        /// セルの階層です。
        /// </returns>
        public int GetFloor()
        {
            return _floor;
        }

        /// <summary>
        /// ヘックスマップ上におけるセルの座標を取得します。
        /// </summary>
        /// <returns>
        /// セルの座標です。
        /// </returns>
        public Vector2Int GetCoordinate()
        {
            return _coordinate;
        }

        /// <summary>
        /// セル中央の床面を、メッシュの上面とワールド変換から取得します。
        /// </summary>
        /// <remarks>
        /// セルの原点は床面ではないため、罠の配置にはこの位置を使用します。
        /// </remarks>
        public bool TryGetSurfacePosition(out Vector3 surfacePosition)
        {
            surfacePosition = default;
            if (!TryGetComponent(out MeshFilter meshFilter) || meshFilter.sharedMesh == null)
            {
                return false;
            }

            Bounds bounds = meshFilter.sharedMesh.bounds;
            surfacePosition = transform.TransformPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
            return true;
        }

        /// <summary>
        /// このセルがトラップセルであるかどうかを取得します。
        /// </summary>
        /// <returns>
        /// トラップセルである場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        public bool IsTrap()
        {
            return _isTrap;
        }

        /// <summary>
        /// このセルへテレポートできるかどうかを取得します。
        /// </summary>
        /// <returns>
        /// テレポートできる場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        public bool CanTeleport()
        {
            return _canTeleport;
        }

        /// <summary>
        /// このセルが持つTransformを取得します。
        /// </summary>
        /// <returns>
        /// このセルがアタッチされているGameObjectのTransformです。
        /// </returns>
        public Transform GetTransform()
        {
            return transform;
        }
    }
}
