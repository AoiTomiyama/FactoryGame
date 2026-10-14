# FactoryGame の設計方針

タグ: `設計方針` `目標アーキテクチャ` `Unity` `追加実装` `依存関係`

状態: 2026-10-01に採用した設計方針。2026-10-14にコード `1f415e4` と照合した。作業の状態は [タスクリスト正本](../tasks/task-list.md)、作成根拠と運用は [引継ぎ手順](../tasks/design-review.md) を参照する。

## 現行機能の目的

セルを選択・配置し、資源の抽出・搬送・保管・レシピ加工と状態表示を扱う。配置は [CellPlacerAdapter](../../Assets/Scripts/Grid/Adapter/CellPlacerAdapter.cs)、抽出は [ExtractorCellAdapter](../../Assets/Scripts/Cells/Adapter/ExtractorCellAdapter.cs)、加工は [CrafterCellAdapter](../../Assets/Scripts/Crafting/Adapter/CrafterCellAdapter.cs) が担う。数量の整合と中断時の所有者を明確にし、設備や表示の追加時に責務を追える設計を採用している。

## 到達像

Unity のコンポーネント構成を土台とし、ゲームのルール、処理の進行、Unity との接続を分ける**軽量なレイヤード・アーキテクチャ**を採用する。セルと搬送先を契約でつなぐ部分には Ports and Adapters の考え方を適用する。全面的な Clean Architecture への移行や、階層・インターフェイスの一律追加は目的にしない。

```text
シーン・UI・演出・アセット（Unity）
              ↓
搬送や加工の進行管理
              ↓
資源量・予約・レシピなどのゲームルール
```

矢印は主な依存方向を示す。ゲームルールは、表示オブジェクト、DOTween、UI、シーン上のセルの寿命を知らずに判定できるようにする。境界をまたぐ操作には必要な契約を置き、利用箇所がない抽象化は追加しない。Assembly Definition による依存方向の強制は今回の計画に含めないため、コードレビューと単独の検証で境界を確認する。

## ファイル配置と命名

機能を先に分け、その下に `Domain`・`Application`・`Adapter` を配置する。型とファイル名の接尾辞を所属レイヤーと一致させる。設定は `Definitions` / `Definition`、編集支援は `Editor` / `Editor` とする。分類基準と対応表は [スクリプト配置・命名規則](script-organization.md) を参照する。

## 責務の置き場所

| 領域 | 担当する内容 | 既存コードとの関係 |
| --- | --- | --- |
| ゲームルール | 容量、資源種別、予約可否、数量の確定、レシピの成立と素材消費 | [ResourceStorageRulesDomain](../../Assets/Scripts/Resources/Domain/ResourceStorageRulesDomain.cs) が保管・加工入力の数量を Unity 非依存で計算する。レシピ判定と素材消費は `RecipeRulesDomain` と `RecipePlanDomain` が担う |
| 進行管理 | 搬送一件の状態と、予約・演出・確定・取消の順序 | [ResourceTransferOperationApplication](../../Assets/Scripts/Transfers/Application/ResourceTransferOperationApplication.cs) と [ResourceTransferCoordinatorApplication](../../Assets/Scripts/Transfers/Application/ResourceTransferCoordinatorApplication.cs) が担い、[ResourceTransitStoreApplication](../../Assets/Scripts/Transfers/Application/ResourceTransitStoreApplication.cs) で搬送資源を保持する。加工一件の確定・取消は `CraftingOperationApplication` が担う |
| Unity との接続 | セル配置・接続、非同期処理の寿命、演出、オブジェクトプール、UI 表示 | `MonoBehaviour`、[ResourceItemObjectPoolAdapter](../../Assets/Scripts/Transfers/Adapter/ResourceItemObjectPoolAdapter.cs)、[ResourceItemAnimationAdapter](../../Assets/Scripts/Transfers/Adapter/ResourceItemAnimationAdapter.cs) が担当する。進行管理は `IResourceTransferPresentationApplication` を通じて演出結果を受け取る。UI の取得・通知・描画は [セル状態 UI の契約](../spec/cell-status-ui.md) に従う |
| 設定データ | セル、資源、レシピの編集可能な定義 | `Assets/Scripts/ScriptableObject/` の設定を利用し、実行中の資源量や搬送状態の所有者と区別する |
| Editor 専用機能 | フィールド生成などの編集支援 | タスク3で `GridFieldGeneratorAdapter` の生成メソッドを `UNITY_EDITOR` に限定し、プレイヤー用コードから分離した |

## 追加実装で守る判断基準

1. 新しい資源・セル・レシピの動作を決めるときは、数量、容量、予約、確定、取消の条件と状態の所有者を先に明示する。現行の全量予約契約は [資源搬入の数量契約](../spec/resource-reservations.md) に従う。
2. 判定・計算は可能な範囲で Unity 非依存の C# に置く。セルの `MonoBehaviour` はシーンとの接続と入出力を担い、搬送の処理順は進行管理側に集める。
3. 演出や UI はゲーム状態を表示する。アニメーションの成功・中断は進行管理へ伝え、表示オブジェクトの返却だけで資源量を変更しない。
4. ScriptableObject は編集可能な定義の入力として扱う。実行中に変わる資源量、予約、一件の搬送状態は、その寿命を管理する処理が所有する。
5. UnityEditor API を使う処理は Editor 専用コード、または既存のシリアライズ参照を維持するための `UNITY_EDITOR` 条件付きコードへ置く。Unity のプレイヤー実行に必要なコードから参照しない。
6. 変更した機能の現行仕様とタスクリストを更新し、数量の境界と中断・削除を検証する。Unity Editor のコンパイル、Play Mode、プレイヤービルドで確認した範囲を区別して記録する。
7. 隣接セルの接続は [セル接続の契約](../spec/cell-connections.md) に従い、座標で4方向を直接読み、参照の両側を更新してから接続先へ通知する。

## 現在地と更新方法

2026-10-14時点で採用済み18項目は完了している。搬送先は識別・生存・搬入の契約で扱い、セルはUnityの破棄判定、非セルは自身の寿命を提供する。搬送の方向・位置にはUnity座標型、非同期処理にはUniTaskを使う。境界と証拠は [搬送状態](../spec/resource-transfer-state.md)、[数量契約](../spec/resource-reservations.md)、[資源データと表示寿命](../spec/resource-presentation.md) を参照する。

加工判定と素材消費はUnity非依存のDomain/Application、保管・加工入力の容量と予約計算は共通の数量規則へ分離した。UI/Applicationは表示セッションの読み取りを担い、専用Adapterの取得関数を既存の描画へ渡せる。[加工契約](../spec/crafting.md) と [UI契約](../spec/cell-status-ui.md) に仕様と検証を集約している。抽出の未完了進捗は中断時に破棄するユーザー判断を実装した（[抽出寿命](../spec/extractor-lifecycle.md)）。

新しい機能は上記の境界を使える範囲から適用する。既存コードの移行時は各タスクの完了条件を満たした結果でこの文書の「現在地」と参照を更新する。アーキテクチャの分類を変える設計判断をした場合は、理由と適用範囲をここに記録する。

## 現行設計の参照

2026-10-11にコードとシーン配線を照合した [現行設計図式](current-architecture.md) と [クラス・スクリプト対応表](script-catalog.md) を参照する。目標方針や移動履歴とは区別し、今後の変更時は該当機能図も更新する。
