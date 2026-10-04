# 資源搬入の数量契約

タグ: `現行仕様` `資源搬送` `予約` `保管セル` `加工セル` `交差セル`

## 予約と確定

- `IContainable.AllocateStorage(dir, amount, resourceType)` は、`amount` が正で資源種別が有効な場合に、指定量の全量を確保できるときだけ成功する。失敗時は資源種別・現在量・予約量を変更しない。
- 空き容量は「容量 − 現在量 − 予約量」で判定する。複数の搬送が同時に予約しても、この値が負にならないようにする。
- `StoreResource(dir, amount)` は予約済みの量だけを確定し、現在量を増やすと同時に予約量を同じだけ減らす。未予約量や非正の量は受け入れない。
- `CancelStorage(dir, amount, resourceType)` は成功した未確定の予約を取り消す。呼び出し側は一件の予約につき、確定か取消のどちらか一方を一度だけ行う。保管セルと加工セルは予約量が 0 になり現在量も 0 の場合、資源種別を `None` に戻す。
- 保管セルは搬送中の予約がある間、現在量が 0 になっても資源種別を保持する。別種の資源が割り込むことを防ぐ。
- コンベアは一度に一件の搬送を全量受け入れる。交差セルは接続先の全量予約結果をそのまま返す。
- `ResourceReservation.TryCreate` は `AllocateStorage` に成功した予約一件に一意の ID を付け、受け取り先・方向・種別・数量を保持する。`TryCommit` または `TryCancel` の先着一回だけが受け付けられ、後続の呼び出しは `false` を返す。受け取り先が削除済みの場合、取消状態へ進むが削除済みセルの数量は操作しない。
- 受け取り側の予約量は引き続き方向・種別・数量で集計される。予約 ID は搬送側で操作の重複を防ぐためのものであり、受け取り側の `StoreResource`・`CancelStorage` の結果通知ではない。

## 数量計算とセルの境界

`ResourceStorageRules` は Unity を参照せず、容量と `Stock`（資源種別・現在量・予約量の読み取り専用の値）から次の状態を計算する。操作に失敗した場合は `false` を返し、出力の状態も入力と同じ値にする。セルは成功した結果だけを自身の状態へ反映して UI を更新する。

| 操作 | 共通ルール | 利用箇所 |
| --- | --- | --- |
| 空き容量・種別別の予約可能量 | `GetAvailableCapacity`、`GetReservableAmount` | `TryReserve` の全量予約判定 |
| 予約・確定・取消 | `TryReserve`、`TryCommit`、`TryCancel` | 保管セルと加工セルの方向別入力 |
| 搬出と空になった種別の解放 | `TryExport` | 保管セル |
| 要求量と保有量から搬出量を計算 | `GetExportableAmount` | 保管セルと加工セルの完成品出力 |

容量 5 に対して 3 と 2 を予約した場合、片方の 3 を確定しても現在量 3・予約量 2 となり、空き容量は 0 のままになる。予約を取り消した量だけ空きが戻る。現在量を全て搬出しても予約が残る間は種別を保持する。

ゼロ・負数の要求、資源種別 `None` の予約、異なる種別への予約は拒否する。無効な負の状態量や容量不足に対して予約可能量は 0 とし、容量計算は合計値の加算による整数の桁あふれを避ける。加工のレシピ判定・素材消費・完成品生成はタスク F で扱う。

実装参照: `Assets/Scripts/Interface/IContainable.cs`、`Assets/Scripts/Cells/ResourceStorageRules.cs`、`Assets/Scripts/Cells/ResourceReservation.cs`、`Assets/Scripts/Cells/StorageCell.cs`、`Assets/Scripts/Cells/CrafterCell.cs`、`Assets/Scripts/Cells/ConveyorCell.cs`、`Assets/Scripts/Cells/CrossingCell.cs`。

検証コード: `Assets/Scripts/Editor/ResourceReservationVerifier.cs`。共通ルールの検証はセルやシーンを生成せず、数値と種別だけで行う。続いて保管・加工セルへの反映、コンベア・交差セルとの予約連携を確認する。Unity Editor ではメニュー `Tools/FactoryGame/Verify Resource Reservations`、バッチでは `-executeMethod ResourceReservationVerifier.Run` を使用する。

搬送中断時、送り元は生存する搬送先の予約を取り消す。搬送先だけが削除された場合は資源 ID を送り元に保持し、送り元自身が削除された場合は ID をプールへ一度だけ返す。演出の中断は搬入確定を起こさない。交差セルは委譲先の予約を取消し、保持中の ID を解放する。実装と検証結果は `docs/tasks/design-review.md` のタスク2を参照する。

搬送進行の順序は `ResourceTransferCoordinator` が管理する。演出の正常完了後だけ搬入を確定する契約と表示の寿命は `docs/spec/resource-presentation.md` を参照する。
