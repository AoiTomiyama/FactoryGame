# セル接続の契約

タグ: `現行仕様` `セル接続` `ポート` `複数マス` `切断`

更新日: 2026-11-10。

## 接続

Field Prefabの`CellBaseAdapter`が幅・奥行・局所オフセットを持ち、`ConnectableCellBaseAdapter`の`ports`が接点ID・局所マス・外向き方向・入出力用途を持つ。IDは設備内で一意にし、回転しても保持する。接点は占有範囲の外周に置き、外向き方向の隣マスが設備内になる定義を拒否する。

`InitializeSystem`は各ポートの隣マスをグリッドから読み、向かい合う位置・方向と入出力用途が一致したポート同士を接続する。探索量はポート数に対応し、フィールド全体を走査しない。同じ辺の別マスや、角の別方向は別IDで扱う。

`Connections`は自分のポートIDをキーとし、相手の設備本体と相手側ポートIDを保持する。双方の参照を確定した後、それぞれの`OnGetConnectedCell`へ接続情報を通知する。既存1×1設備も同じ経路を使う。採取対象の資源はExtractorが座標から読み、ポート接続とは別に扱う。

## 予約と演出

搬入の`AllocateStorage`・`StoreResource`・`CancelStorage`と表示ID引継ぎの`Reuse`は受け取り側のポートIDを受け取る。`ResourceReservationApplication`は設備のIdentityとPortId・数量・種別を固定し、確定か取消を一度だけ行う。

加工入力はポートIDごとに独立し、保管庫は複数ポートで一つの在庫を共有する。交差セルは入力ポートの反対側へ直進し、下流のポート予約を保持する。搬出契約も出力ポートIDを指定し、演出位置は接点の占有マスから取得する。

## 切断

設備の退役時に`OnDisconnect`を呼び、全ポートと相手側の参照を消して相手へ`OnLostConnectedCell`を通知する。搬送側は該当する予約を取り消し、次の接続まで資源IDを保持する。再初期化は既存の接続を閉じてから外周を読み直す。

## 実装と検証

実装は`Cells/Adapter/ConnectableCellBaseAdapter.cs`、`Cells/Definitions/CellPortDefinition.cs`、`Grid/Adapter/GridFieldDatabaseAdapter.cs`と`Transfers/Application/`に置く。

既存の`CellConnectionVerifierEditor.Run`で、4方向の相互通知・削除・再接続・境界に加え、3×2の同じ辺にある2入力と1出力を4方向回転して確認する。数量・取消・資源IDは既存の`ResourceReservationVerifierEditor`、`ResourceTransferVerifierEditor`、`CraftingVerifierEditor`で検証する。実行結果はローカルの`docs/verification/multiblock-cells.json`へ集約する。
