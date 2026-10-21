# 設計レビュー改修タスク

タグ: `設計レビュー` `資源搬送` `非同期処理` `プレイヤービルド` `セル接続`

状態: タスク1は2026-09-30、タスク2〜5は2026-10-01に修正・検証済み。既存の設計レビュー改修タスクは完了。追加タスクは `docs/tasks/architecture-extension.md` を参照する。

拡張性に関する追加タスク A〜H は `docs/tasks/architecture-extension.md` を参照する。タスク2の中断処理は同文書の C・G の最初の適用箇所とし、同じ完了条件を重複して計上しない。

## 現行原本とID（2026-10-15）

全26項目のカテゴリ・優先順・進捗は [タスクリスト原本](task-list.md) で管理する。既存18項目は完了し、2026-10-15に採用した通貨ゲームループの8項目は未着手。仕様・依存順・完了条件は [実装計画](currency-game-loop-plan.md) を参照する。搬送先の具体セル基底への制約を除く `TRN-01`（uid: `R4`）の契約と検証は [搬送状態の契約](../spec/resource-transfer-state.md) に記録した。セル定義 `GRD-01`、抽出寿命 `RES-01`、専用状態取得 `UIS-01` は完了している。

各項目の表示IDはカテゴリ英字3文字とカテゴリ内優先順2桁で表す。旧IDは固定uidとして原本に保存し、以下の完了記録と対応付ける。

## 新担当が状態と作成根拠を照合する手順

タグ: `引継ぎ` `採用判断` `タスク原本` `Chronos`。照合版: `1f415e4`。照合当時の原本はv2・18項目で全件done。照合当時の原本SHA256は `66d20848f96a7ed9fce53178d174e239c2923929c34ac36eb564b0da525ec9dd`。再開時は現在のinventoryを優先する。

1. `git status --short` とHEADを確認し、現在の依頼・原本・関連コードを照合する。着手前の差分は内容と所有者を確認する。
2. [設計方針](../design/architecture-guidelines.md) と [現行設計索引](../design/current-architecture.md) から対象機能の仕様・コード・検証入口へ進む。仕様の契約と過去検証の実行版を区別する。
3. Chronosのinventoryでid・uid・カテゴリ・状態・本文を取得する。過去文書の旧IDはuidと照合する。完了済み項目の再実装や新規項目の重複を防ぐため、問題・対象・完了条件も比較する。
4. 新しいタスク提案には、コード・既存仕様・確認結果を根拠として問題、解決策、完了条件、仕様判断を示す。ユーザーが採用した範囲を原本へ登録する。仕様が未決定の採用項目はblockedと判断待ちを簡潔に記す。
5. 依頼された工程を実施し、変更に直接関係する検証、現行仕様、原本の状態を揃えてコミットする。中断時は差分・検証結果・残作業を関連仕様へ記録する。

### 採用済み項目の根拠

| 現行ID | 作成根拠・仕様・証拠の入口 |
| --- | --- |
| TRN-01 / TRN-05 / TRN-06 | [搬送状態と接点契約](../spec/resource-transfer-state.md)。非セルの搬送、予約Tokenと進行管理の一回性 |
| TRN-02 / TRN-03 / TRN-04 / RES-02 | [数量契約](../spec/resource-reservations.md)、本書の旧1・2、[追加実装](architecture-extension.md) の旧C・E。全量予約、取消、数量計算 |
| TRN-07 | [資源と表示の寿命](../spec/resource-presentation.md)。IDデータと表示返却の分離 |
| TRN-08 | [搬送の責務](../design/resource-transfer-responsibilities.md) と追加実装の旧A。採用時の責務調査を記録し、現行の契約は上記仕様を参照 |
| RES-01 | [抽出寿命](../spec/extractor-lifecycle.md)。対象変更と無効化、中断進捗の破棄 |
| GRD-01 / GRD-02 | [セル定義](../spec/cell-definitions.md)、[セル接続](../spec/cell-connections.md)。辞書初期化、4方向の直接参照 |
| UIS-01 / UIS-02 / UIS-03 | [UI契約](../spec/cell-status-ui.md)、[アイコン](../spec/resource-icons.md)。専用取得、表示境界と購読寿命、資産参照 |
| CRF-01 | [加工](../spec/crafting.md)。固定計画と再検証、消費・確定・取消 |
| ECO-01〜06 / PRG-01〜02 | [通貨ゲームループ実装計画](currency-game-loop-plan.md)。ユーザーの計画・登録依頼と曖昧点補完の許可に基づく採用仕様、搬送換金、支出取引、設備強化、共有スキルと依存順 |
| DEV-01 / DEV-02 | 本書の旧3、[配置・命名](../design/script-organization.md)、[グリッド検証入口](../../Assets/Scripts/Grid/Editor/GridFieldGeneratorVerifierEditor.cs)。Editor依存分離、GUID維持と配置整理 |

上表と原本のuidから旧1〜5、A〜H、ORG-1、R1〜R4の記録へ到達できる。旧文書の「問題」「方針」「未着手」は記録当時の状態として読み、現行進捗は原本を使う。

### カテゴリと優先順の判断

2026-10-13のID移行で、担当が機能の責務ごとに6カテゴリへ分類し、ハブの切替依頼に従って反映した。番号はカテゴリ内の優先順で01が最優先。移行時の残作業R4をTRN-01へ置き、現在は完了している。

| 英字 / カテゴリ | 優先順を決めた理由 |
| --- | --- |
| TRN / 搬送 | 移行時の残作業、数量整合、中断、予約、状態、進行、表示寿命、責務記録の順 |
| RES / 資源 | 稼働中の抽出寿命、共通の数量計算の順 |
| GRD / グリッド | 初期化・配置を成立させる定義、隣接探索の順 |
| UIS / 状態表示 | 専用取得の拡張、取得・描画境界、資源アイコンの順 |
| CRF / 加工 | レシピ判定・素材消費・確定と取消を一項目に集約 |
| DEV / 開発基盤 | プレイヤービルドの成立、スクリプト配置・命名の順 |

分類・再採番では英大文字3文字とカテゴリ名を一対一に対応させ、カテゴリ内の01〜99を使う。idが変わってもuidを固定し、移行済みの旧IDを別タスクへ再利用しない。数量や型名を含む本文の参照置換は共通CLIに任せて結果を照合する。

2026-10-15にECO / 経済（残高、換金、設置、強化、表示、統合検証）とPRG / 成長（能力合成、共有ツリー）を追加した。カテゴリ内は基盤から利用側へ並べ、カテゴリ間の依存順は通貨ゲームループ実装計画を参照する。

### 原本の更新

仕様は [ChronosカテゴリID契約](../../../../.codex/session-routing/docs/routing/chronos-category-ids.md)、形式は [タスクテンプレート](../../../../.codex/atlas/docs/chronos/chronos-task-list-template.md)、CLIは [Atlas更新仕様](../../../../.codex/atlas/docs/chronos/chronos-update-tool.md) と現行 `--help` を確認する。ローカル配信入口は `C:/Users/Taomo/.codex/atlas/chronos.py`。通信・担当管理は [ハブREADME](../../../../.codex/session-routing/README.md) から確認する。

```powershell
python -X utf8 C:/Users/Taomo/.codex/atlas/chronos.py inventory --project factory
python -X utf8 C:/Users/Taomo/.codex/atlas/chronos.py apply --project factory --request <更新JSON>
python -X utf8 C:/Users/Taomo/.codex/atlas/chronos.py apply --project factory --request <更新JSON> --write
python -X utf8 C:/Users/Taomo/.codex/atlas/chronos.py validate --project factory
```

共有CLI仕様にあるv1の例と、カテゴリID移行後のv2は版が異なる。現行原本の更新JSONはschema_version=2、最新inventoryのbase_revision、tasksを指定する。tasksのキーは表示idでなく固定uid。値は変更するid/category/title/status/sectionsを指定し、sectionsの問題・解決策・結論を簡潔に更新する。既存のuidを保持し、updated_atはCLIが変更した項目へ自動設定する。新規登録では一意のuid、id・category・title・statusと3節を揃える。プレビューの範囲、数量と意味の保持を確認してからwriteし、validateを通す。

原本は採用済みの実施項目を一プロジェクト一ファイルで管理し、完了項目も保持する。候補の評価は根拠文書へ記録し、ユーザーの採用判断を受けて原本へ反映する。タスク本文には問題・解決策・現在の結論を記し、詳細な検証や設計判断は関連仕様へリンクする。

### 採用済み判断と再開時の注意

- 抽出の中断では未完了進捗を破棄し、抽出済み在庫・抽出量・容量を維持する。ユーザー回答を実装した `686ae6b` と抽出寿命の仕様が根拠。
- 専用状態取得はCreateCustomの接続契約を提供する。新設備の仕様と既存4種類の全面移行はUIS-01の範囲から外れ、追加依頼の対象となる。`1a689b5` とUI契約が根拠。
- 搬送接点は固定Identityの参照一致と実装側のIsAliveを使う。セルの判定はUnity破棄で、無効化の扱いは既存の接続通知・取消経路を維持する。`1f415e4` と搬送状態の契約が根拠。
- ID移行は `901e72b`。状態・本文・数量を保持し、旧IDをuidへ固定した。ハブのカテゴリID契約と原本が根拠。
- MotionBlur設定の差分、未使用ServiceLocatorとそのmeta、IDEの4 XMLは着手前から保持している。採用状況と既存差分レビューは [UI契約の記録](../spec/cell-status-ui.md) を参照する。新担当はgit statusと現在の参照先を再確認する。

既存18項目の完了記録を保持し、追加8項目は採用済み・未着手として管理する。実装への着手は現在のユーザー依頼の工程に従う。

## 1. 資源の予約量と搬入量を一致させる

- [x] `IContainableApplication.AllocateStorage` の契約を決め、送り元・受け取り先・交差セルへ反映する。
- 問題: `StorageCellAdapter` と `CrafterCellAdapter` は空き容量分だけ予約して `true` を返す。一方、`ConveyorCellAdapter` は要求した全量を `StoreResource` に渡す。保管セルは予約量を超えた搬入を拒否し、加工セルは容量を超えて受け入れる経路がある。
- 方針: 全量予約のみ許可するか、実際の予約量を返して残量を送り元に保持するかを選び、予約・搬送・確定の数量を一致させる。
- 完了条件: 空き容量が要求量より少ない場合は予約を拒否し、数量・資源種別を変えない。保管セルと加工セルの確定量、交差セル経由の予約量を確認し、容量・予約量を超過しない。
- 採用した設計: 現行の `bool` 戻り値を維持し、全量予約のみ成功させる。交差セルは接続先の結果を委譲する。仕様は `docs/spec/resource-reservations.md`。
- 検証証拠: Unity 6000.3.8f1 のバッチモードで `-executeMethod ResourceReservationVerifier.Run` を実行。スクリプトコンパイル成功、保管・加工・交差・コンベアの予約境界チェック成功。交差セルについては予約委譲を確認し、アニメーションを伴う搬送中断の検証はタスク2で行う。
- 参照: `Assets/Scripts/Transfers/Application/IContainableApplication.cs`、`Assets/Scripts/Cells/Adapter/StorageCellAdapter.cs`、`Assets/Scripts/Crafting/Adapter/CrafterCellAdapter.cs`、`Assets/Scripts/Cells/Adapter/ConveyorCellAdapter.cs`、`Assets/Scripts/Cells/Adapter/CrossingCellAdapter.cs`。

## 2. 搬送中断時の予約解除とプール返却を整理する

- [x] 搬送成功時だけ受け取り先へ格納し、中断時は予約解除と表示オブジェクト返却をそれぞれ一度だけ行う。
- 問題: `ConveyorCellAdapter.StoreResourceAsync` は `finally` 内で搬入を確定する。`ResourceItemObjectPoolAdapter.Transfer` は中断時に表示オブジェクトを返却するが、呼び出し側も ID を破棄して返却するため、二重返却の経路がある。現在の `ConveyorCellAdapter` と `ResourceItemObjectPoolAdapter` には未コミット変更が含まれる。
- 方針: 予約の所有者とキャンセル時の処理順を明示し、ID の解放を冪等にする。セル削除・接続解除・転送中断が重なっても処理結果を一意にする。
- 完了条件: 搬送途中で送り元または受け取り先を削除しても、予約量と資源量が整合し、二重返却や破棄済みセルへの搬入が起きない。
- 採用した設計: 送り元は資源 ID を搬送確定まで保持し、搬送先の接続が失われた場合は予約を取り消して次の接続を待つ。送り元自身が削除された場合は ID を返却する。交差セルは委譲先の予約と引き継いだ ID を保持し、削除時に解放する。表示アニメーションは中断を呼び出し側へ伝え、ID を直接返却しない。
- 検証証拠: Unity 6000.3.8f1 のバッチモードで `ResourceReservationVerifier.Run` を実行し、保管・加工・コンベア・交差セルの予約取消を確認。Play Mode で `ResourceTransferVerifier.Run` を実行し、通常搬送、搬送先の切断・削除、送り元の削除、交差セルの通常搬送・削除で、予約量・確定量・表示 ID の後始末を確認した。両検証とも成功ログを確認した。メインシーンを手操作する目視確認とプレイヤービルドは行っていない。
- 参照: `Assets/Scripts/Cells/Adapter/ConveyorCellAdapter.cs`、`Assets/Scripts/Cells/Adapter/ExportConveyorCellAdapter.cs`、`Assets/Scripts/Cells/Adapter/CrossingCellAdapter.cs`、`Assets/Scripts/Transfers/Adapter/ResourceItemObjectPoolAdapter.cs`。

## 3. 実行時コードから UnityEditor 依存を分離する

- [x] フィールド生成の Editor 専用処理をプレイヤー用コードから切り離す。
- 問題: `Assets/Scripts/Grid/Adapter/GridFieldGeneratorAdapter.cs` は `using UnityEditor` と `PrefabUtility.InstantiatePrefab` を使用している。通常のゲームコードに Editor 専用 API があるため、プレイヤービルドでコンパイルできない構成になっている。
- 方針: 生成処理を `Assets/Scripts/Editor/` の Editor 用クラスへ移すか、Editor 専用部分を条件付きコンパイルに分離する。実行時のグリッド初期化は維持する。
- 完了条件: Unity Editor でフィールド生成が使え、プレイヤービルドのスクリプトコンパイルが通る。
- 採用した設計: 既存シーンに保存された `GridFieldGeneratorAdapter` のコンポーネントと設定値を維持し、生成・消去・グリッドライン・ノイズ計算を `UNITY_EDITOR` 条件付きコードに限定する。プレイヤーには `Start` のグリッド初期化を残す。
- 検証証拠: Unity 6000.3.8f1 のバッチモードで `GridFieldGeneratorVerifier.RunGeneration` を実行し、2×2 セルのプレハブ参照、グリッドライン、消去を確認。続けて `GridFieldGeneratorVerifier.RunPlayerBuild` を実行し、`MainScene` を含む Windows 64-bit プレイヤービルドが成功した。Play Mode での手操作は行っていない。
- 参照: `Assets/Scripts/Grid/Adapter/GridFieldGeneratorAdapter.cs`、`Assets/Scripts/Grid/Editor/GridGeneratorEditor.cs`、`Assets/Scripts/Grid/Editor/GridFieldGeneratorVerifierEditor.cs`。

## 4. 隣接セル探索を4方向の直接参照にする

- [x] セル接続時の近傍探索を見直し、接続・切断の双方を検証する。
- 問題: `ConnectableCellBaseAdapter` は隣接4方向の確認に `GridFieldDatabaseAdapter.TryGetCellFromRange` を繰り返し使う。この関数は呼び出しごとにフィールド全体の探索用配列を作る。メインシーンのグリッド設定は100×100。
- 方針: 上下左右の座標から `GetCell` で直接取得し、双方の接続参照と通知を対称に更新する。
- 完了条件: 配置、回転、削除、再接続の結果が現行の意図に沿い、隣接確認でグリッド全体の探索用配列を生成しない。
- 採用した設計: 4方向を固定スロットとして座標から `GetCell` で直接取得し、グリッド外は先に除外する。接続・切断では両側の参照を更新してから通知する。仕様は `docs/spec/cell-connections.md`。
- 検証証拠: Unity 6000.3.8f1 のバッチモードで `CellConnectionVerifier.Run` を実行し、コンパイルと Edit Mode の4方向接続・相互通知・削除・再接続・90度回転・グリッド端の確認が成功。接続処理から `TryGetCellFromRange` の呼び出しがなくなった。Play Mode とプレイヤービルドは今回実行していない。
- 参照: `Assets/Scripts/Cells/Adapter/ConnectableCellBaseAdapter.cs`、`Assets/Scripts/Grid/Adapter/GridFieldDatabaseAdapter.cs`、`Assets/Scripts/Grid/Editor/CellConnectionVerifierEditor.cs`。

## 5. 資源アイコンの参照を確認する

- [x] 木材・石材のアイコンを表示する仕様を確認し、必要な参照を復元する。
- 問題: 未コミット差分の `ResourceDB.asset` では、木材・石材の `icon` が両方とも `{fileID: 0}` になっている。`RecipeElementUIAdapter` と `StorageUIStatusRowAdapter` はこの値をそのまま画像へ渡すため、従来のアイコンが表示されない。
- 方針: アイコンを使う場合は参照を設定し、意図的に文字のみへ変更する場合は UI の画像処理も合わせて変更する。
- 完了条件: レシピ欄と保管セルの状態欄を Unity Editor で確認し、意図した表示になっている。
- 採用した設計: 既存の画像表示を維持し、木材・石材の `icon` に従来の Unity 組み込み Sprite 参照を戻す。資源データ名変更など進行中の別作業は変更しない。仕様は `docs/spec/resource-icons.md`。
- 検証証拠: Unity 6000.3.8f1 の Play Mode バッチ実行 `ResourceIconVerifier.Run` で `ResourceDB.asset` の2種の Sprite が解決でき、`Recipe.prefab` の材料・成果物行と `StorageParamLine.prefab` の状態欄に Sprite と資源名が設定されたことを確認。参照先の Sprite 名は石材が `Checkmark`、木材が `Background`。画面の目視確認とプレイヤービルドは実施していない。
- 参照: `Assets/Scripts/ScriptableObject/ResourceDB.asset`、`Assets/Scripts/UI/Adapter/RecipeElementUIAdapter.cs`、`Assets/Scripts/UI/Adapter/StorageUIStatusRowAdapter.cs`、`Assets/Scripts/Resources/Editor/ResourceIconVerifierEditor.cs`。
