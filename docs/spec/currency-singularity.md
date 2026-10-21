# 初期特異点への搬送換金

タグ: `現行仕様` `ECO-02` `通貨` `特異点` `全量予約` `初期配置`
更新日: 2026-10-21

## 現行動作と構成

MainScene中央 `(gridSize/2, gridSize/2)` の特異点1基へ資源を搬送すると、演出成功後の搬入確定時に共有残高へ入金する。初期残高100、木材・石材の売価1は [通貨設定](../../Assets/Scripts/ScriptableObject/CurrencyEconomy.asset) に集約する。売価のない資源は予約を拒否する。

[GridFieldGeneratorAdapter](../../Assets/Scripts/Grid/Adapter/GridFieldGeneratorAdapter.cs) が一プレイの価格カタログと残高を生成し、[SingularityCellAdapter](../../Assets/Scripts/Economy/Adapter/SingularityCellAdapter.cs) へ渡す。全セルをグリッドへ登録した後、中央・単一性・木材・空き経路を検証し、特異点を接続する。`Wallet` と `Prices` は今後の設置・成長・表示から同じインスタンスを利用する入口である。再開始時に新しく生成する。

[SingularityStartDefinition](../../Assets/Scripts/Economy/Definitions/SingularityStartDefinition.cs) は経済設定と特異点・木材・空セルのPrefabを参照する。グリッドサイズ8以上、参照の存在、木材種別、木材の売価、初回設備3種の総額が初期残高以下であることを生成前と起動時に検証する。[設定アセット](../../Assets/Scripts/ScriptableObject/SingularityStart.asset) と [Prefab](../../Assets/Prefabs/Field/General/F_SingularityCell.prefab) をMainSceneへ配線した。[編集入口](../../Assets/Scripts/Economy/Editor/SingularitySetupEditor.cs) は資産作成と中央5セルの初期配置を更新する。

## 入力方向ごとの予約

[SingularityIntakeApplication](../../Assets/Scripts/Economy/Application/SingularityIntakeApplication.cs) が四方向ごとに未確定1件を保持する。方向はX/Z軸の正負だけを受理する。数量は正のint、売価と換金額はcheckedのlongで扱い、数量・種別・換金額・入金余地を全量固定する。同方向の競合は拒否し、四方向の異種資源は独立して予約できる。

搬入確定では方向と全量を、取消では方向・全量・種別を照合する。不一致は予約を維持する。予約枠を外してから通貨取引を確定・取消し、通知への再入でも同じ予約を二度処理しない。残高予約中の通知で無効化された場合も、その予約を解除して失敗を返す。残高上限の余地は他の入金予定を含めて保証し、支出取消後も確定できる。残高の不変条件は [通貨取引仕様](currency-wallet.md) を参照する。

特異点は在庫と搬送IDを保持しない。通常の [Coordinator](../../Assets/Scripts/Transfers/Application/ResourceTransferCoordinatorApplication.cs) が演出成功後に搬入を確定し、資源データと表示を終端で一度返却する。予約Tokenと搬送記録は受け取り側の確定通知前に完了させる。入金通知から切断・破棄・取消が再入しても、確定済み資源を再試行しない。

無効化では新規予約を止め、接続先の取消を先に行い、残る全方向の予約を解放する。再有効化では同じIdentityで再接続する。IsAliveは既存のUnity破棄判定を保つ。破棄時も残る予約を解放する。演出失敗・切断・取消では入金せず、送り元の資源IDを維持する。送り元撤去時のID処分は既存の所有者が担う。

## 初回経路と保護

中央を `(c,c)` とすると、木材を `(c,c-4)`、抽出候補を `(c,c-3)`、搬出候補を `(c,c-2)`、通常コンベア候補を `(c,c-1)` に確保する。抽出設備は木材へ向けて180度回転し、搬出・通常コンベアは特異点へ向ける。設備費は20+5+5=30。生成ノイズより初期配置を優先するため、再生成後も木材と経路を確保する。

CellPlacerは範囲選択と確定の双方で資源セル・特異点の削除と上書きを拒否する。特異点は購入一覧へ登録せず、型選択と直接配置も拒否する。通常設備の既存配置操作は維持する。設置課金とプレビュー費用表示はECO-03以降の作業である。
