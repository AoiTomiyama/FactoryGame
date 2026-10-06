# 設計レビュー改修タスク

タグ: `設計レビュー` `資源搬送` `非同期処理` `プレイヤービルド` `セル接続`

状態: タスク1は2026-09-30、タスク2〜5は2026-10-01に修正・検証済み。既存の設計レビュー改修タスクは完了。追加タスクは `docs/tasks/architecture-extension.md` を参照する。

拡張性に関する追加タスク A〜H は `docs/tasks/architecture-extension.md` を参照する。タスク2の中断処理は同文書の C・G の最初の適用箇所とし、同じ完了条件を重複して計上しない。

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
