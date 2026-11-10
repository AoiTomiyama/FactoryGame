# 通貨による設備設置

タグ: `現行仕様` `ECO-03` `設置` `支出取引` `範囲操作` `失敗復元`
更新日: 2026-10-21

## 操作と費用

空セルへ設備を設置すると、[通貨設定](currency-wallet.md)の価格を共有残高から支払う。初期経路の抽出設備・搬出コンベア・通常コンベアは合計30で、初期残高100から設置できる。選択と配置方向の回転は無料。設備の上書きは拒否し、資源セルと特異点は設置・撤去の対象から外す。設備撤去は無料で返金はなく、在庫・予約・保持IDの処分は既存の切断・破棄契約を使う。

ドラッグは既存の主軸判定を使い、X/Zの移動量が同じ場合はX軸を選ぶ。開始位置から終端へ座標順に1セルずつ確定し、残高不足・失敗で停止する。確定済みセルは保持する。範囲外、資源、特異点と上書き対象は候補から除く。範囲を縮めたときに外れた候補は処理しない。終了・設備選択変更・無効化でプレビューを片付け、未設置セルのモデル表示を戻す。

画面左下に候補個数と合計費用、直前に確定した個数・支出額・停止理由を表示する。撤去の費用は0。`PreviewCount`・`PreviewCost`・`PreviewText`と、`LastPlacedCount`・`LastPlacementCost`・`LastPlacementResult`・`ResultText`を表示用の読み取り入口とする。残高・強化・スキルを含む統合表示はECO-05で扱う。

## 取引と復元

[Generator](../../Assets/Scripts/Grid/Adapter/GridFieldGeneratorAdapter.cs)は特異点と同じ共有残高・固定価格カタログを[CellPlacer](../../Assets/Scripts/Grid/Adapter/CellPlacerAdapter.cs)へ渡す。[EquipmentPlacementApplication](../../Assets/Scripts/Economy/Application/EquipmentPlacementApplication.cs)は対象確認、全額支出予約、対象の再確認、設置、確定を同期的に進める。通知からの再入は同じApplicationとPlacerの実行ガードで拒否する。予約通知で対象が無効になった場合は生成せず、予約を取り消す。

Placerは旧セルを保持したまま非アクティブな雛形を複製し、座標登録、有効化、接続初期化を行う。登録された参照と初期化後の生存・有効状態を確認する。生成・登録・初期化で失敗した場合は、新セルの接続を切り、元のグリッド参照を戻し、新セルを無効化・破棄して支出予約を取り消す。旧設備の切断は初期化成功後に行うため、失敗した撤去では旧設備の在庫・接続を保持する。

成功時は旧セルを切断・無効化・破棄し、選択参照を更新してから支出を確定する。状態ウィンドウは確定後に新セルへ切り替え、撤去では閉じる。購読通知・表示更新・退役時の例外で成立済み設置の支払いを取り消さない。Applicationの`LastException`は処理失敗や後処理例外を保持する。

## 検証入口

[EquipmentPlacementVerifierEditor.Run](../../Assets/Scripts/Economy/Editor/EquipmentPlacementVerifierEditor.cs)で価格ちょうどの支払い、不足、生成・登録・初期化の失敗復元、通知再入と対象再確認を検証する。同じ起動で既存の接続・UI資産検証とMainSceneのPlay Modeを実行し、有料の初回換金経路、範囲の正逆順、途中不足、保護セル、回転、無料撤去、選択表示、初期化失敗後の再配置を確認する。失敗注入セルは`UNITY_EDITOR`に限定し、プレイヤーへ含めない。

関連する搬送取消・ID処分は[特異点の検証入口](../../Assets/Scripts/Economy/Editor/SingularityVerifierEditor.cs)でも確認する。実行条件・結果・コード識別情報はローカルの`docs/verification/currency-placement.md`へ記録する。

## プレビュー面の重なり

待機中と範囲選択中のプレビューは、ConfigurePreviewで基準位置からY方向へ0.01上げる。白いフィールドセルと緑のプレビューの面を離し、重なりによる点滅を防ぐ。設置する設備の座標と占有判定は元のグリッドを使う。
