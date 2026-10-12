# Prana Tactics — ソースコード公開リポジトリ

Prana Tacticsは、移動経路を考える戦略パートと、キャラクター視点で射撃・防御を操作するアクションパートが交互に進む、チーム戦術アクションゲームです。

プレイヤーはAIの仲間と協力しながら、限られた行動力を移動・攻撃・防御にどう配分するかを判断します。移動計画では、目的地と方針に応じて経路づくりを補助する機能も備えています。

このリポジトリは、技育展への応募にあたり、作品の実装内容や設計を確認していただくために公開しています。

## 作品の遊び方

1. **移動を計画する**：ヘックス（六角形のマス）で構成された盤面を見て、目的地や移動経路を選びます。
2. **計画した経路を進み、戦う**：キャラクター視点に切り替わり、移動中に兵装を選択して射撃・防御などを操作します。
3. **次の作戦を考える**：戦闘後の位置や敵の状況を踏まえ、次のターンの移動計画を立てます。

移動と兵装の使用には共通の「行動力」を消費します。移動に多く使うか、攻撃・防御に残すかという判断が、戦略パートとアクションパートをつないでいます。チームで敵を倒し、試合終了時の得点順位を競います。

ソースコード内では、移動計画を行う戦略パートを **Macro**、キャラクター視点で戦うアクションパートを **Micro** と表記しています。

## 公開している範囲と、その理由

本リポジトリには、開発用Unityプロジェクトの `Assets/Scripts` 内のコードを、ディレクトリ構造を保ったまま収録しています。Unityの `.meta` ファイルは除外しています。

公開範囲をScripts内に限定しているのは、ゲームの仕組みやAI、操作、通信など、ソースコードから確認できる実装に焦点を当てるためです。画像・3Dモデル・音声などの素材や、シーン・Prefab・各種設定、外部SDK・パッケージ本体は収録していません。

元のディレクトリ構造を維持しているのは、機能のまとまりやクラス間の関係を、開発時の構成に沿って追えるようにするためです。応募用にコードを1つのフォルダーへ集約したり、紹介向けの断片だけに置き換えたりせず、Scripts内での配置を保っています。

`.meta` は、Unityがアセットの識別やインポート設定に使用するファイルです。今回はソースコードの閲覧を目的とするため、公開対象から除外しています。

そのため、本リポジトリはUnityプロジェクト一式ではありません。クローンしてUnityで開くだけでは、ゲームの起動やビルドはできません。また、`.meta` ファイルを含まないため、元プロジェクトのアセット参照をそのまま復元する用途にも対応していません。コードの閲覧と実装の確認を目的とした公開です。

## ディレクトリ構成

以下は、元プロジェクトの `Assets/Scripts` を起点にした構成です。

```text
Scripts/
├── Runtime/      ゲーム実行時の処理
│   ├── AI/          状況把握・移動計画・戦闘判断
│   │   ├── Core/
│   │   ├── Planning/
│   │   └── Combat/
│   ├── Match/       試合のルール・進行・結果
│   │   ├── Bootstrap/
│   │   ├── Modes/
│   │   ├── Phases/
│   │   ├── Rules/
│   │   ├── Results/
│   │   └── Solo/
│   ├── GameFlow/    画面とゲーム全体の状態遷移
│   ├── Board/       ヘックス盤面・罠
│   ├── Unit/        キャラクターの状態・移動・戦闘
│   ├── Controls/    プレイヤー・AIからの入力
│   ├── Pranam/      兵装・能力の発動元
│   ├── Prana/       弾・エネルギー・シールドなど
│   ├── Networking/ 接続・ロビー・マッチング・通信
│   ├── UI/         画面・HUD・操作部品
│   └── ...         その他の機能
├── Editor/       Unity Editor上の編集・検証を補助する処理
└── Debug/        開発中の動作確認用コード
```

`Runtime` は機能ごとに分割しています。

| ディレクトリ | 主な役割 |
| --- | --- |
| `AI` | AIの状況把握、移動計画、味方との連携、戦闘判断 |
| `Board` | ヘックス盤面、マス情報、盤面上の罠、盤面に関する通信 |
| `Match` | 試合の初期化、ルール、戦略・アクションパートの進行、結果、ソロステージ |
| `GameFlow` | タイトル、キャラクター選択、ゲームプレイ、結果などの画面・状態遷移 |
| `Unit` | キャラクターの生成、移動、状態、戦闘、視認、カメラ、通信 |
| `Controls` | プレイヤーとAIの入力、操作に必要な情報の受け渡し |
| `Pranam` | 装備・兵装・能力のデータ、使用処理、照準、通信 |
| `Prana` | 兵装から生成される弾、エネルギー、シールドなどの挙動 |
| `Combat` | 戦闘に関する共通処理や脅威情報の扱い |
| `Networking` | オンライン接続、認証、ロビー、マッチング、メッセージ、通信サービス |
| `UI` | 各画面、戦闘中のHUD、UI操作、表示部品 |
| `Tutorial` | チュートリアルの進行と操作案内 |
| `Core` | 共通データ、シリアライズ、状態機械などの基盤 |
| `Settings` | 設定やローカライズに関する処理 |
| `Animation` / `Audio` / `Rendering` | アニメーション、音声、描画に関する処理 |
| `Validation` | 実行時の検証に関する処理 |
| `Promo` | 紹介・撮影用途に関する処理 |

## 設計と、注目していただきたい実装

機能ごとの分割に加え、各機能内でも、状態・判断・表示・通信などの役割に応じてクラスやディレクトリを整理しています。以下は、作品の特徴とコードの対応が分かりやすい実装です。

### 戦略パートとアクションパートの切り替え

`Match/Phases` では、移動経路を計画するMacroパートと、キャラクターを操作して戦うMicroパートの進行を扱っています。作品の中心となる「計画してから実行する」流れを確認できます。

`GameFlow` がタイトルやキャラクター選択などを含むゲーム全体の進行を担当する一方、`Match/Phases` は試合中の各段階を担当します。画面間の遷移と、試合内部の進行を分けて構成しています。

| 主なコード | 確認できる内容 |
| --- | --- |
| `GameFlow/GameFlowManager.cs` | ゲーム全体の進行管理 |
| `GameFlow/StateMachine/GameStateMachine.cs` | ゲーム状態の切り替え |
| `Match/Phases/MatchPhaseManager.cs` | 試合中のフェーズ管理 |
| `Match/Phases/States/MacroPhaseState.cs` | 移動計画パートの処理 |
| `Match/Phases/States/MicroPhaseState.cs` | アクションパートの処理 |
| `Match/Phases/MacroPlanningSession.cs` | 今回の移動計画に対する決定状態の管理 |

オンラインの計画決定では、ターンと計画識別子に対応するプレイヤーの応答を確認します。以前の決定状態と今回の決定状態を区別し、AIの計画完了も含めて次の段階へ進めるかを判断しています。

### プレイヤーの移動経路構築補助

目的地を選択した際の経路づくりは、`Unit/Behavior` 内のコードで扱っています。移動可能な範囲と行動力を踏まえ、距離や累積危険度から経路を評価します。

- `MacroUnitBehavior.cs`：Macroパートでのユニットの振る舞い。
- `MacroRouteSearch.cs`：到達範囲と経路の探索。
- `MacroRoutePolicy.cs`：最短・バランス・安全優先という探索方針。
- `MacroPathHistory.cs`：経路編集の履歴管理。
- `MacroPathSnapshot.cs`：経路情報を保持するためのデータ表現。

探索クラスには、隣接マスの取得、移動可否の判定、危険度の評価を外部から渡します。探索の手順と、盤面・危険度に関する具体的な判断を分ける構成です。また、一定の探索ノード数ごとに処理を分割することで、探索中もフレームをまたいで進められるようにしています。

「安全優先」は、把握している敵情報などから推定した危険度を使う方針です。敵の攻撃を必ず回避できることを保証するものではありません。

### AIの移動計画とチーム連携

`AI/Planning` では、危険度の評価、移動先の選択、プレイヤーの援護、見失った敵の捜索などを扱っています。`AiReservationTable` は移動ステップと座標を組み合わせて予約を管理し、味方AI同士の移動の競合を抑える役割を持ちます。

`AI/Combat` では、視認情報、兵装選択、生存判断など、アクションパートでの戦闘判断を扱っています。計画と戦闘の処理を分けて構成しています。

| 分類 | 主なコード | 役割 |
| --- | --- | --- |
| 全体管理 | `AI/SoloAiDirector.cs` | ソロプレイ中のAI管理と、計画・戦闘処理の調整 |
| 情報 | `AI/Core/AiWorldStateProvider.cs` | AIが判断に使う公開状態の提供 |
| 設定 | `AI/Core/AiDefinitionCatalog.cs` | AIの定義データの管理 |
| 経路計画 | `AI/Planning/AiMacroPlanner.cs` | Macroパートでの移動計画 |
| 危険度 | `AI/Planning/AiDangerEvaluator.cs` | 公開された敵情報に基づく危険度評価 |
| 移動予約 | `AI/Planning/AiReservationTable.cs` | 移動ステップと座標・移動区間の予約 |
| 援護 | `AI/Planning/AiPlayerSupportPlanner.cs` | プレイヤーを支援するための計画 |
| 捜索 | `AI/Planning/AiSearchPlanner.cs` | 見失った敵の捜索計画 |
| 兵装選択 | `AI/Combat/AiWeaponSelector.cs` | 戦闘状況に応じた兵装の選択 |
| 視認 | `AI/Combat/AiVisibilityEvaluator.cs` | 戦闘時の視認評価 |
| 生存判断 | `AI/Combat/AiSurvivalPolicy.cs` | 生存に関する判断 |

AIは、公開された情報を判断の入口としています。危険度の評価では、未確認の敵を対象から除外し、最後に確認した位置については接触の確信度を反映します。未発見の敵の現在位置を、そのまま経路判断に利用する構成にはしていません。

計算負荷への対策として、同一ユニットの計画中に評価したマスの危険度を再利用するキャッシュや、探索ノード数・処理時間による計画の予算管理も実装しています。AIの判断内容だけでなく、複数のAIの計画を制限時間内に進めるための処理も確認できます。

### プレイヤー入力とAI入力の共通化

`Controls/Abstractions/IUnitInput.cs` は、現在の入力状態を取得する共通の窓口です。プレイヤー操作は `Controls/Player/PlayerUnitInput.cs`、AIからの入力は `Controls/AI/AIUnitInput.cs` に分けています。

入力状態は `Controls/Data/UnitInputState.cs` で表現し、入力の生成元と、その入力を使用するユニット側の処理の接点を整理しています。

### ゲーム処理と通信実装の境界

ユニット、盤面、兵装の通信には、`IUnitNetwork`、`IBoardNetwork` などのインターフェースと、Local・Photon・EOS向けの実装を用意しています。通信方式に関する処理を各実装にまとめ、ゲーム側との接点を明確にする構成です。

```text
Unit/Networking/
├── IUnitNetwork.cs
├── Local/
├── Photon/
└── EOS/
```

`Board/Networking` と `Pranam/Networking` にも、通信方式ごとの実装を配置しています。さらに、`Networking/Services/IOnlineService.cs` は接続状態、プレイヤー、ルーム、ロビー操作などのサービス境界を定義しています。

`Networking` には認証、接続、マッチングなどの共通基盤を、各機能内の `Networking` にはユニットや盤面などの同期処理を置いています。サービス全体に関する処理と、ゲーム内の個別機能の同期を分けて読むことができます。

Local・Photon・EOSのコードが収録されていることは、それぞれの動作環境やSDKがこのリポジトリに付属していることを意味しません。

### 兵装と、そこから生成されるものの分離

作品内では、装備・兵装・能力などの発動元を「Pranam」、そこから生成される弾・エネルギー・シールドなどを「Prana」と呼んでいます。ディレクトリもこの役割に合わせて分け、兵装を使用する処理と、生成後の挙動を整理しています。

`Pranam` 側には定義データ、使用処理、戦闘コンテキスト、ターゲット選択、通信などを配置しています。`Prana` 側には生成物の基底処理と、攻撃・射撃・防御などに応じた挙動を配置しています。

例として、兵装を「いつ・どの対象に向けて使用するか」と、生成された弾が「どう進み、どう衝突するか」は、異なる役割として読み進められます。

### UI・チュートリアル・開発用ツール

`UI/Screens` にはタイトル、ロビー、キャラクター選択、結果画面などを、`UI/Gameplay` には試合中のHUDや兵装・ユニット状態の表示を配置しています。表示部品、画面の切り替え、UI操作に関する処理もそれぞれ分けています。

`Tutorial` には、進行を管理する `TutorialStateMachine.cs`、各段階を表す `TutorialLessonState.cs`、表示を扱う `TutorialView.cs`、達成条件を扱う `TutorialPageCondition.cs` などがあります。操作説明の表示に加え、プレイヤーの操作に応じて進行するための実装を確認できます。

`Editor` には、AIや兵装のデータ編集補助、参照切れの確認、経路や戦闘・UIの検証、紹介映像の撮影補助などを配置しています。ゲーム実行時の機能に加え、制作・確認作業を支えるツールも収録しています。

## コードを読む際の案内

ゲーム全体の流れを確認する場合は、`GameFlow` → `Match` → `Unit`・`Controls`・`Pranam` の順に読むと、画面遷移から試合中の操作までを追いやすくなります。

AIに関心がある場合は、`AI/SoloAiDirector.cs` と `AI/Planning`・`AI/Combat`、通信に関心がある場合は、`Networking/Services` と各機能の `Networking` ディレクトリを入口にしてください。

ここで挙げているパスは、特記がない限り `Scripts/Runtime` からの相対パスです。`Editor` と `Debug` には開発・検証用のコードも含まれています。

### 関心別の読み方

| 確認したい内容 | 入口 | 次に読む場所 |
| --- | --- | --- |
| ゲーム全体の進行 | `GameFlow/GameFlowManager.cs` | `GameFlow/States`、`Match/Phases` |
| 作戦とアクションの切り替え | `Match/Phases/MatchPhaseManager.cs` | `MacroPhaseState.cs`、`MicroPhaseState.cs` |
| 移動経路の構築補助 | `Unit/Behavior/MacroUnitBehavior.cs` | `MacroRouteSearch.cs`、`MacroRoutePolicy.cs`、`MacroPathHistory.cs` |
| AIの計画とチーム連携 | `AI/SoloAiDirector.cs` | `AI/Planning`、`AI/Core` |
| AIの戦闘判断 | `AI/Combat/SoloAiAgent.cs` | `AiWeaponSelector.cs`、`AiVisibilityEvaluator.cs`、`AiSurvivalPolicy.cs` |
| 入力の受け渡し | `Controls/Abstractions/IUnitInput.cs` | `Controls/Player`、`Controls/AI`、`Unit/Behavior` |
| 兵装と生成物 | `Pranam/Base/PranamBase.cs` | `Pranam/Behavior`、`Prana/Base/PranaBase.cs`、各兵種の実装 |
| オンライン機能 | `Networking/Services/IOnlineService.cs` | `Networking/EOS`、`Networking/Photon`、各機能の `Networking` |
| ソロステージ | `Match/Solo/Stages` | ステージ定義、進行状況、保存処理 |
| 操作説明と進行条件 | `Tutorial/TutorialStateMachine.cs` | `TutorialLessonStates.cs`、`TutorialPageCondition.cs`、`TutorialView.cs` |

## 開発環境・依存関係

- Unity 6
- 元プロジェクトで使用しているEditor：6000.6.3f1
- C#
- Photon、Epic Online Services（EOS）などの外部サービス・SDK

コードにはUnityのシーン、Prefab、設定データや外部ライブラリに依存する部分があります。必要な素材・設定・依存パッケージは本リポジトリに含まれていないため、この公開範囲だけでの実行環境構築手順は提供していません。

Inspectorから設定される参照や値、ScriptableObjectによる定義データなどは、コードだけでは元の設定内容を再現できません。また、Editor用の検証コードにも、元プロジェクトのシーンやオブジェクト構成を前提とするものがあります。本READMEの説明は実装構成を案内するものであり、本リポジトリ単体でのビルド成功や検証コードの実行を示すものではありません。

## 開発者・作品動画

- 開発者：安井義人
- チーム名：PRANA LAB
- 開発体制：1人
- 開発期間：約1年半

[作品動画を見る（Google Drive）](https://drive.google.com/file/d/1ksxswILIUOcls6UunERWLcDpLaFZGJ73/view)
