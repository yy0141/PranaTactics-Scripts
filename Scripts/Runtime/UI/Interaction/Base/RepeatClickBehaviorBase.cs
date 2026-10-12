namespace PranaTactics.UI.Interaction
{
    /// <summary>
    /// 同じUIオブジェクトがクリック状態である場合でも、
    /// クリック処理を繰り返し実行できるUI動作の基底クラスです。
    /// </summary>
    /// <remarks>
    /// クリック状態の開始、更新および終了処理は、
    /// <see cref="ClickBehaviorBase"/>から継承します。
    ///
    /// 派生クラスでは必要に応じて
    /// <c>OnClickEnter</c>、<c>OnClickUpdate</c>、
    /// <c>OnClickExit</c>をオーバーライドします。
    /// </remarks>
    public abstract class RepeatClickBehaviorBase :
        ClickBehaviorBase,
        IRepeatClick
    {
    }
}
