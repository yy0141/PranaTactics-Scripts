using UnityEngine;

namespace PranaTactics.UI.Screens
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiSizeLimiter : MonoBehaviour
    {
        [SerializeField]
        private Vector2 _minSize =
            new Vector2(100f, 30f);

        [SerializeField]
        private Vector2 _maxSize =
            new Vector2(300f, 60f);

        private RectTransform _rect;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            ApplySize();
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplySize();
        }

        private void ApplySize()
        {
            if (_rect == null)
            {
                return;
            }

            Vector2 currentSize = _rect.rect.size;

            float width = Mathf.Clamp(
                currentSize.x,
                _minSize.x,
                _maxSize.x);

            float height = Mathf.Clamp(
                currentSize.y,
                _minSize.y,
                _maxSize.y);

            if (!Mathf.Approximately(
                    currentSize.x,
                    width))
            {
                _rect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal,
                    width);
            }

            if (!Mathf.Approximately(
                    currentSize.y,
                    height))
            {
                _rect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    height);
            }
        }
    }
}
