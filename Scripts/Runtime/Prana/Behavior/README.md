罠の効果クラスはこのフォルダーに追加します。

`PranaTactics.Prana.TrapperPranaBase`を継承し、`ApplyTrapEffect`で確定後のローカル効果を実装してください。派生クラスをアタッチしたResources内のプレハブを`TrapperPranamData`に登録すると、共通の設置・検知・発動処理から使用できます。

詳細はプロジェクト直下の`Documentation/Trapper.md`を参照してください。

`Landmine.cs`は設置地点で爆発する罠の実装です。`ExplosiveTrapperPranamData`で検知・タイミング・威力・爆風・VFXを設定します。
