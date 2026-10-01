# 資源搬送の責務と処理順

タグ: `現行設計` `資源搬送` `予約` `中断` `設計判断`

確認日: 2026-10-01。以下の「着手前の動作」はタスク2着手前のコードを読んだ結果であり、Unity Editor での再生結果ではない。数量の仕様は `docs/spec/resource-reservations.md` を参照する。

## 着手前の動作

| 操作 | 呼び出し元と担当 | 保持する状態 |
| --- | --- | --- |
| 搬出 | `ExportConveyorCell.TakeResourceAsync` が `IExportable.TryExport` を呼ぶ | `StorageCell` または `CrafterCell` の現在量が減り、コンベアが資源 ID を保持する |
| 予約 | `ConveyorCell.StoreResourceAsync` が前方の `IContainable.AllocateStorage` を呼ぶ | 受け取り側の予約量、コンベアの `HasResource` と `ResourceId` |
| 表示移動 | コンベアまたは交差セルが `ResourceItemObjectPool.Transfer` を待つ | プールが ID、表示オブジェクト、種別、数量を対応付ける |
| 確定 | コンベアまたは交差セルが `IContainable.StoreResource` を呼ぶ | 受け取り側の現在量が増え、予約量が減る |
| ID の引継ぎ・返却 | `IResourceReusable.Reuse` または `ResourceItemObjectPool.DisposeId` を呼ぶ | 次のコンベアが ID を保持するか、表示オブジェクトをプールへ戻す |

`CrossingCell.AllocateStorage` は接続先へ予約を委譲し、`StoreResourceAsync` は演出後に同じ接続先へ確定する。交差セル自身に資源量はない。`ConnectableCellBase.OnDisconnect` はセル削除時に呼び出され、コンベアと交差セルは登録したキャンセルトークンを取消す。

## 着手前のコードから確認できた中断時の問題

- `ConveyorCell.StoreResourceAsync` は演出後の `finally` で `StoreResource` を呼ぶため、演出が中断しても搬入を確定する経路がある。
- `ResourceItemObjectPool.Transfer` は中断時に表示オブジェクトを返すが、ID の登録は残る。呼び出し側も `DisposeId` すると二重返却になり得る。
- `IContainable` には予約取消の操作がない。搬送を止めたときの予約量を戻せない。
- 隣接セルの削除は `AdjacentCells` の参照を消すが、コンベアや交差セルが保持する搬送先の参照を明示的に更新しない。

## 改修目標

一件の搬送は「予約待ち → 予約済み → 演出中 → 確定」または「予約済み・演出中 → 取消」で終了させる。予約した受け取り先と数量を搬送中に固定し、成功時だけ確定する。取消時は生存する受け取り先の予約を解除し、表示オブジェクトの所有者が一度だけ返却する。送り元が残る場合の資源 ID の保持と、送り元自身が削除された場合の破棄を分ける。

この改修目標は `docs/tasks/design-review.md` のタスク2で検証する。A の記録完了は実装完了を意味しない。

## タスク2の進行状況

`IContainable.CancelStorage`、コンベアと交差セルの中断経路、プールの ID 返却責務を変更中。予約取消の境界チェックは Unity Editor のバッチで通過した。実際の搬送中に送り元・受け取り先を削除する再生検証が完了するまでは、タスク2を完了扱いにしない。
