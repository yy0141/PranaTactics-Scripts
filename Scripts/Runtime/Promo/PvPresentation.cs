#if UNITY_EDITOR || PV_CAPTURE
namespace PranaTactics.Promo
{
    /// <summary>
    /// 演出撮影でも通常撮影と共通の照準UIを残す表示制御を使用します。
    /// </summary>
    public sealed class PvPresentation
    {
        private readonly PvGameplayUiVisibility _visibility = new PvGameplayUiVisibility();

        public bool IsVisible => _visibility.IsVisible;

        public void SetVisible(bool visible)
        {
            _visibility.SetVisible(visible);
        }

        public void Refresh()
        {
            _visibility.Refresh();
        }

        public void Restore()
        {
            _visibility.Dispose();
        }
    }
}
#endif
