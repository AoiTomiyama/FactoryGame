# 資源搬送の責務と処理順

タグ: `現行設計` `資源搬送` `予約` `中断` `設計判断`

確認日: 2026-10-01。以下の「着手前の動作」はタスク2着手前のコードを読んだ結果であり、Unity Editor での再生結果ではない。数量の仕様は `docs/spec/resource-reservations.md` を参照する。

## 着手前の動作

| 操作 | 呼び出し元と担当 | 保持する状態 |
| --- | --- | --- |
| 搬出 | `ExportConveyorCellAdapter.TakeResourceAsync` が `IExportableApplication.TryExport` を呼ぶ | `StorageCellAdapter` または `CrafterCellAdapter` の現在量が減り、コンベアが資源 ID を保持する |
| 予約 | `ConveyorCellAdapter.StoreResourceAsync` が前方の `IContainableApplication.AllocateStorage` を呼ぶ | 受け取り側の予約量、コンベアの `HasResource` と `ResourceId` |
| 表示移動 | コンベアまたは交差セルが `ResourceItemObjectPoolAdapter.Transfer` を待つ | プールが ID、表示オブジェクト、種別、数量を対応付ける |
| 確定 | コンベアまたは交差セルが `IContainableApplication.StoreResource` を呼ぶ | 受け取り側の現在量が増え、予約量が減る |
| ID の引継ぎ・返却 | `IResourceReusableApplication.Reuse` または `ResourceItemObjectPoolAdapter.DisposeId` を呼ぶ | 次のコンベアが ID を保持するか、表示オブジェクトをプールへ戻す |

`CrossingCellAdapter.AllocateStorage` は接続先へ予約を委譲し、`StoreResourceAsync` は演出後に同じ接続先へ確定する。交差セル自身に資源量はない。`ConnectableCellBaseAdapter.OnDisconnect` はセル削除時に呼び出され、コンベアと交差セルは登録したキャンセルトークンを取消す。

## 着手前のコードから確認できた中断時の問題

- `ConveyorCellAdapter.StoreResourceAsync` は演出後の `finally` で `StoreResource` を呼ぶため、演出が中断しても搬入を確定する経路がある。
- `ResourceItemObjectPoolAdapter.Transfer` は中断時に表示オブジェクトを返すが、ID の登録は残る。呼び出し側も `DisposeId` すると二重返却になり得る。
- `IContainableApplication` には予約取消の操作がない。搬送を止めたときの予約量を戻せない。
- 隣接セルの削除は `AdjacentCells` の参照を消すが、コンベアや交差セルが保持する搬送先の参照を明示的に更新しない。

## 改修目標

一件の搬送は「予約待ち → 予約済み → 演出中 → 確定」または「予約済み・演出中 → 取消」で終了させる。予約した受け取り先と数量を搬送中に固定し、成功時だけ確定する。取消時は生存する受け取り先の予約を解除し、表示オブジェクトの所有者が一度だけ返却する。送り元が残る場合の資源 ID の保持と、送り元自身が削除された場合の破棄を分ける。

この改修目標は `docs/tasks/design-review.md` のタスク2で検証する。A の記録完了は実装完了を意味しない。

## タスク2の進行状況

`IContainableApplication.CancelStorage`、コンベアと交差セルの中断経路、プールの ID 返却責務を変更した。予約取消の境界チェックと、Play Mode の通常搬送・送り元削除・搬送先削除・交差セル削除の検証が通過した。詳細は `docs/tasks/design-review.md` のタスク2を参照する。

## 追加タスクBの進行状況

コンベアと交差セルは、搬送一件の送り元・搬送先・資源ID・種別・数量・進行段階を `ResourceTransferOperationApplication` で保持する。成功と取消は各記録につき一度だけ終端段階へ進む。搬送先の切断後、コンベアは同じIDを保持して次の試行を作り、交差セルは同じ記録の搬送先を更新する。現行契約は `docs/spec/resource-transfer-state.md` を参照する。

## 追加タスクCの進行状況

コンベアと交差セルが搬送先へ確保する予約は `ResourceReservationApplication` で識別する。予約の確定か取消は各予約につき一度だけ行い、交差セルの搬送先切断では旧予約を取り消して再接続先に別の予約を作る。数量契約は `docs/spec/resource-reservations.md` を参照する。

## 追加タスクDの進行状況

`ResourceTransferCoordinatorApplication` がコンベアと交差セルの一回の搬送試行を進める。予約取得後に資源データと接続先を確認し、表示移動が終わってから搬入を確定する。失敗時の予約取消、搬送先変更時の旧予約取消も同じ型に集めた。セルは接続先の選択、再接続待ち、削除時に自身が保持する資源 ID の返却を行う。詳細は `docs/spec/resource-transfer-state.md` を参照する。

## 追加タスクGの進行状況

搬送データは Unity 非依存の `ResourceTransitStoreApplication` が保持し、表示の貸出と返却は `ResourceItemObjectPoolAdapter`、移動は `ResourceItemAnimationAdapter` が担当する。進行管理は `IResourceTransferPresentationApplication` から演出結果を受け取り、正常完了後だけ予約を確定する。表示の返却や欠落は資源データを変更せず、資源所有者が搬送終了時にデータの終了を決める。復元・再試行を含む現行契約は `docs/spec/resource-presentation.md` を参照する。
