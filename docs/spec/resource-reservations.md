# 資源搬入の数量契約

タグ: `現行仕様` `資源搬送` `予約` `保管セル` `加工セル` `交差セル`

## 予約と確定

- `IContainable.AllocateStorage(dir, amount, resourceType)` は、`amount` が正で資源種別が有効な場合に、指定量の全量を確保できるときだけ成功する。失敗時は資源種別・現在量・予約量を変更しない。
- 空き容量は「容量 − 現在量 − 予約量」で判定する。複数の搬送が同時に予約しても、この値が負にならないようにする。
- `StoreResource(dir, amount)` は予約済みの量だけを確定し、現在量を増やすと同時に予約量を同じだけ減らす。未予約量や非正の量は受け入れない。
- 保管セルは搬送中の予約がある間、現在量が 0 になっても資源種別を保持する。別種の資源が割り込むことを防ぐ。
- コンベアは一度に一件の搬送を全量受け入れる。交差セルは接続先の全量予約結果をそのまま返す。

実装参照: `Assets/Scripts/Interface/IContainable.cs`、`Assets/Scripts/Cells/StorageCell.cs`、`Assets/Scripts/Cells/CrafterCell.cs`、`Assets/Scripts/Cells/ConveyorCell.cs`、`Assets/Scripts/Cells/CrossingCell.cs`。

検証コード: `Assets/Scripts/Editor/ResourceReservationVerifier.cs`。Unity Editor ではメニュー `Tools/FactoryGame/Verify Resource Reservations`、バッチでは `-executeMethod ResourceReservationVerifier.Run` を使用する。

搬送中断時の予約解除とプール返却は `docs/tasks/design-review.md` のタスク2で扱う。
