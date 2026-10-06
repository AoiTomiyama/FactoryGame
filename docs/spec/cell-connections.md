# セル接続の契約

タグ: `現行仕様` `セル接続` `4方向` `配置` `切断`

## 接続

`ConnectableCellBaseAdapter.InitializeSystem` は `GridFieldDatabaseAdapter` の座標から東、西、北、南の順に隣接セルを読む。グリッド外と空セルは接続しない。接続可能なセル同士は、両者の `allowedDirections` が向かい合う方向を許す場合だけ接続する。資源セルなど接続機能を持たないセルは、元のセルの隣接参照にだけ保持する。

接続可能なセル同士では、双方の `AdjacentCells` を先に設定し、各側の `OnGetConnectedCell` を一度ずつ通知する。スロットは `+X, -X, +Z, -Z` に固定し、向かい側のスロットを対にする。セルの向きは `allowedDirections` の世界座標への変換に使用する。

## 切断

`CellPlacerAdapter` がセルを置き換える前に `OnDisconnect` を呼ぶ。切断時は自身と接続相手の参照を消し、相手の `OnLostConnectedCell` を一度通知する。元のセルだけが保持していた接続対象も、自身の参照から消す。再配置されたセルは `InitializeSystem` で現在の4方向を読み直す。

## 実装と検証

- 実装: `Assets/Scripts/Cells/Adapter/ConnectableCellBaseAdapter.cs`、`Assets/Scripts/Grid/Adapter/GridFieldDatabaseAdapter.cs`。
- 検証: Unity 6000.3.8f1 の Edit Mode バッチ実行 `CellConnectionVerifierEditor.Run` で4方向、通知、削除、再配置、90度回転、グリッド端を確認。入口は `Assets/Scripts/Grid/Editor/CellConnectionVerifierEditor.cs`。
