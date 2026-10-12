namespace PranaTactics.UI.Interaction
{
    public interface IClickableCondition
    {
        /// <summary>
        /// UIオブジェクトが現在クリック可能かどうかを判定します。
        /// </summary>
        /// <returns>
        /// クリック処理を許可する場合は<c>true</c>、
        /// 許可しない場合は<c>false</c>です。
        /// </returns>
        bool CanClick();
    }
}
