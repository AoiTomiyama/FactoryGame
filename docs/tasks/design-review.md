# 設計レビュー改修タスク

タグ: `設計レビュー` `資源搬送` `非同期処理` `プレイヤービルド` `セル接続`

状態: タスク1は2026-09-30に修正・検証済み。タスク2～5は未着手。以下の順に着手する。

## 1. 資源の予約量と搬入量を一致させる

- [x] `IContainable.AllocateStorage` の契約を決め、送り元・受け取り先・交差セルへ反映する。
- 問題: `StorageCell` と `CrafterCell` は空き容量分だけ予約して `true` を返す。一方、`ConveyorCell` は要求した全量を `StoreResource` に渡す。保管セルは予約量を超えた搬入を拒否し、加工セルは容量を超えて受け入れる経路がある。
- 方針: 全量予約のみ許可するか、実際の予約量を返して残量を送り元に保持するかを選び、予約・搬送・確定の数量を一致させる。
- 完了条件: 空き容量が要求量より少ない場合は予約を拒否し、数量・資源種別を変えない。保管セルと加工セルの確定量、交差セル経由の予約量を確認し、容量・予約量を超過しない。
- 採用した設計: 現行の `bool` 戻り値を維持し、全量予約のみ成功させる。交差セルは接続先の結果を委譲する。仕様は `docs/spec/resource-reservations.md`。
- 検証証拠: Unity 6000.3.8f1 のバッチモードで `-executeMethod ResourceReservationVerifier.Run` を実行。スクリプトコンパイル成功、保管・加工・交差・コンベアの予約境界チェック成功。交差セルについては予約委譲を確認し、アニメーションを伴う搬送中断の検証はタスク2で行う。
- 参照: `Assets/Scripts/Interface/IContainable.cs`、`Assets/Scripts/Cells/StorageCell.cs`、`Assets/Scripts/Cells/CrafterCell.cs`、`Assets/Scripts/Cells/ConveyorCell.cs`、`Assets/Scripts/Cells/CrossingCell.cs`。

## 2. 搬送中断時の予約解除とプール返却を整理する

- [ ] 搬送成功時だけ受け取り先へ格納し、中断時は予約解除と表示オブジェクト返却をそれぞれ一度だけ行う。
- 問題: `ConveyorCell.StoreResourceAsync` は `finally` 内で搬入を確定する。`ResourceItemObjectPool.Transfer` は中断時に表示オブジェクトを返却するが、呼び出し側も ID を破棄して返却するため、二重返却の経路がある。現在の `ConveyorCell` と `ResourceItemObjectPool` には未コミット変更が含まれる。
- 方針: 予約の所有者とキャンセル時の処理順を明示し、ID の解放を冪等にする。セル削除・接続解除・転送中断が重なっても処理結果を一意にする。
- 完了条件: 搬送途中で送り元または受け取り先を削除しても、予約量と資源量が整合し、二重返却や破棄済みセルへの搬入が起きない。
- 参照: `Assets/Scripts/Cells/ConveyorCell.cs`、`Assets/Scripts/Cells/ExportConveyorCell.cs`、`Assets/Scripts/Cells/CrossingCell.cs`、`Assets/Scripts/Manager/ResourceItemObjectPool.cs`。

## 3. 実行時コードから UnityEditor 依存を分離する

- [ ] フィールド生成の Editor 専用処理を実行時アセンブリから切り離す。
- 問題: `Assets/Scripts/GridFieldGenerator.cs` は `using UnityEditor` と `PrefabUtility.InstantiatePrefab` を使用している。通常のゲームコードに Editor 専用 API があるため、プレイヤービルドでコンパイルできない構成になっている。
- 方針: 生成処理を `Assets/Scripts/Editor/` の Editor 用クラスへ移すか、Editor 専用部分を条件付きコンパイルに分離する。実行時のグリッド初期化は維持する。
- 完了条件: Unity Editor でフィールド生成が使え、プレイヤービルドのスクリプトコンパイルが通る。
- 参照: `Assets/Scripts/GridFieldGenerator.cs`、`Assets/Scripts/Editor/GridGeneratorEditor.cs`。

## 4. 隣接セル探索を4方向の直接参照にする

- [ ] セル接続時の近傍探索を見直し、接続・切断の双方を検証する。
- 問題: `ConnectableCellBase` は隣接4方向の確認に `GridFieldDatabase.TryGetCellFromRange` を繰り返し使う。この関数は呼び出しごとにフィールド全体の探索用配列を作る。メインシーンのグリッド設定は100×100。
- 方針: 上下左右の座標から `GetCell` で直接取得し、双方の接続参照と通知を対称に更新する。
- 完了条件: 配置、回転、削除、再接続の結果が現行の意図に沿い、隣接確認でグリッド全体の探索用配列を生成しない。
- 参照: `Assets/Scripts/Cells/ConnectableCellBase.cs`、`Assets/Scripts/Manager/GridFieldDatabase.cs`、`Assets/Scenes/MainScene.unity`。

## 5. 資源アイコンの参照を確認する

- [ ] 木材・石材のアイコンを表示する仕様を確認し、必要な参照を復元する。
- 問題: 未コミット差分の `ResourceDB.asset` では、木材・石材の `icon` が両方とも `{fileID: 0}` になっている。`RecipeElementUI` と `StorageUIStatusRow` はこの値をそのまま画像へ渡すため、従来のアイコンが表示されない。
- 方針: アイコンを使う場合は参照を設定し、意図的に文字のみへ変更する場合は UI の画像処理も合わせて変更する。
- 完了条件: レシピ欄と保管セルの状態欄を Unity Editor で確認し、意図した表示になっている。
- 参照: `Assets/Scripts/ScriptableObject/ResourceDB.asset`、`Assets/Scripts/UI/RecipeElementUI.cs`、`Assets/Scripts/UI/CellStatus/StorageUIStatusRow.cs`。
