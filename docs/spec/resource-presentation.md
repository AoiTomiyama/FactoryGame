# 搬送資源データと表示の寿命

タグ: `現行仕様` `資源搬送` `演出` `プール` `ID` `中断`

## 責務と所有者

| 担当 | 責務 |
| --- | --- |
| `ResourceTransitStore` | Unity 非依存の C# で搬送 ID、資源種別、数量を保持する。ID は表示の再利用に関係なく新規発行する |
| `ResourceItemObjectPool` | シーンに既存の設定を持つ窓口。資源ストアを所有し、ID に対応する表示の貸出・復元・返却を行う |
| `ResourceItemAnimation` | 貸出中の表示一つを DOTween で移動し、正常完了・中断・表示対象なしを返す。資源数量や貸出登録を変更しない |
| `IResourceTransferPresentation` | 進行管理から利用する表示の契約。移動結果、位置の復帰、表示の返却を提供する |
| `ResourceTransferCoordinator` | 演出が正常完了し、接続先も有効なときだけ予約を確定する。終点セルへ届けた資源データを終了し、表示を返却する |
| コンベア・交差セル | 搬送資源の所有者。切断時は資源を保持し、削除時は所有するデータと表示を終了する |

搬出コンベアは `TryExport` に成功した時点で送り元から取り出した数量を搬送データに保持する。表示の作成・移動に失敗しても、同じ数量を送り元から再取得しない。演出の正常完了まで前方へ送り出さない。

## 演出結果と後始末

- `Completed`: 移動の完了コールバックを確認し、取消されていない。通常終了した await だけでは成功と判断しない。
- `Cancelled`: トークンによる取消、表示返却、外部からの Tween 停止などで正常完了できなかった。予約は確定しない。
- `MissingVisual`: ID に対応する表示がない、または表示オブジェクトが破棄されている。資源データが消えたことを意味しない。

`ReleaseVisual(id)` は表示登録を先に外して移動を中断し、一度だけプールへ返す。資源ストアは変更しない。中断した Tween は終了させるため、同じ GameObject を次に貸し出しても旧演出は動かさない。

`TryRestoreVisual(id)` は残っている資源データから表示を再作成し、ID・種別・数量を維持する。演出が失敗した搬送は予約を取り消し、100 ミリ秒間隔で再試行する。自動的に表示を復元する処理はなく、復元された表示を次の試行で利用する。

`DisposeId(id)` は資源所有者が終了を決めたときにデータの削除と表示返却を行う。繰り返しても二重返却しない。プールの破棄時は残っている表示を返却し、セッションの搬送データを終了する。演出の後始末から保管・加工セルの数量を変更しない。

## 実装と検証

実装: `Assets/Scripts/Cells/ResourceTransitStore.cs`、`Assets/Scripts/Cells/ResourceTransferCoordinator.cs`、`Assets/Scripts/Interface/IResourceTransferPresentation.cs`、`Assets/Scripts/Manager/ResourceItemAnimation.cs`、`Assets/Scripts/Manager/ResourceItemObjectPool.cs`。

検証入口: Unity 6000.3.8f1 の `ResourceReservationVerifier.Run` と Play Mode の `ResourceTransferVerifier.Run`。演出取消、外部からの Tween 停止、返却中の表示再利用、表示欠落と復元、通常搬送・切断・削除時の数量と ID を確認する。
