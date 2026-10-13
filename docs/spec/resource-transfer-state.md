# 搬送一件の状態

タグ: `現行仕様` `資源搬送` `状態遷移` `コンベア` `交差セル`

`ResourceTransferOperationApplication` は、一件の現在の送り元接点、搬送先接点、資源ID、資源種別、数量、進行段階を保持する。コンベアでは送り元は自身、交差セルでは下流へ送り出す自身を指す。種別と数量は生成後に変えない。資源 ID は `ResourceTransitStoreApplication` が表示オブジェクトと独立して発行し、`0` を未設定とする。状態型は ID の符号に依存しない。交差セルは予約後に上流から ID を一度受け取る。

進行段階は `WaitingForReservation → Reserved → Animating → Completed`。予約待ち、予約済み、演出中のどこからでも `Cancelled` に進める。`Completed` と `Cancelled` は終端であり、再度の完了・取消は拒否する。交差セルは搬送先を失うと同じ ID と数量を保持したまま予約待ちに戻り、次の接続先を設定できる。コンベアは切断された搬送試行を取り消し、保持した ID で次の試行を作る。

`ResourceTransferCoordinatorApplication` は一回の搬送試行について、予約待ち、資源データの照合、表示移動、搬入確定、失敗時の予約取消と状態終了をこの順で実行する。資源ストアと表示契約を呼び出し側から受け取り、演出が `Completed` の場合だけ確定する。演出の失敗では資源を保持し、予約待ちへ戻す。搬送先が表示移動中に失われた場合、交差セルは旧予約を取り消して同じ資源 ID のまま予約待ちへ戻す。コンベアはその試行を取り消し、保持した ID で新しい試行を作る。セルは接続先の提供、切断通知、資源の受け入れ、セル削除時の保持 ID の返却を担う。表示の寿命と復元は [搬送資源データと表示の寿命](resource-presentation.md) を参照する。

検証は Unity 6000.3.8f1 の `ResourceReservationVerifierEditor.Run` と Play Mode の `ResourceTransferVerifierEditor.Run` で行い、状態と ID の対応、完了・取消の一度だけの遷移、通常搬送・切断・削除を確認する。

## 搬送接点の契約（TRN-01、2026-10-14）

[識別・生存契約](../../Assets/Scripts/Transfers/Application/IResourceTransferEndpointApplication.cs) は `Identity` と `IsAlive` を提供する。`Identity` は接点の寿命中に固定する非nullのオブジェクトで、別の接点へ再利用せず参照一致で比較する。`IsAlive` は数量操作を受け付けられる寿命内かを実装側で判定する。非セルの受け取り先はこの寿命を自身で管理し、`IContainableApplication` の全量予約・確定・取消を実装する。

セル基底は `Identity => this` と `IsAlive => this != null` でUnityの破棄判定を提供する。無効化を新しい破棄条件へ加えておらず、既存の接続通知と取消Tokenによる中断を維持する。識別・寿命の判定をAdapterへ置き、Reservation・Operation・Coordinatorは搬送先の具体セル基底を参照する経路を契約へ置き換えた。方向・位置のUnity座標型とUniTaskは継続して使う。

OperationのSourceは識別・生存契約、Targetは搬入契約を借用する。予約Tokenは受け取り先とIdentityを固定し、生存中だけ確定・取消の操作を呼ぶ。寿命終了後の確定は拒否し、取消はTokenだけを閉じる。Tokenは操作前に終端へ進み、再入と反復による二重実行を防ぐ。寿命終了時の受け取り側在庫と予約の後始末は接点の実装側が担当する。

Coordinatorは演出前後に接点の生存・接続・同じ予約Tokenの継続を照合する。交換・破棄・取消Tokenによる中断では旧予約を閉じ、資源IDは所有者へ残す。再利用先は確定前にIDを引き継ぎ、通常終端では資源データと表示を返却する。コンベアと交差セルのシーン上の隣接接続は従来どおりセルを使い、共有の予約・搬送処理を非セルからも利用できる。

検証は [ResourceTransferVerifierEditor](../../Assets/Scripts/Transfers/Editor/ResourceTransferVerifierEditor.cs) の既存Play Mode入口を使う。C#だけの受け取り先で、全量予約、確定・取消と再入、Identityの参照一致、寿命終了、交換後の同じIDによる再試行、取消Token、再利用先へのID引継ぎ、資源データと表示の終了を確認する。同じ起動の既存予約・搬送回帰は、今回変更した共通契約を利用するセルの数量境界と接続寿命を確認するために実行する。

## TRN-01の検証証拠

対象は `901e72b4011dee5f912ec27cb255b2970d421c08` からのTRN-01差分。Unity 6000.3.8f1をバッチ・描画なしで実行した。

| 入口 | 結果 | 実測時間 |
| --- | --- | --- |
| `ResourceTransferVerifierEditor.Run` | 終了コード0。非セルの全量予約・確定・取消・再入・寿命・交換・再試行・ID引継ぎ・表示終了。共通契約に直接依存する既存の数量規則、保管・加工・コンベア・交差セルの予約、搬送成功・切断・削除・再接続とID・表示寿命も成功 | 81.16秒 |
| `GridFieldGeneratorVerifierEditor.RunPlayerBuild` | 終了コード0。MainSceneを含むWindows 64-bit通常ビルド成功。最終差分のEditor/Playerコンパイルを確認 | 76.63秒 |

実測合計157.79秒、失敗0回、成功済み動作検証の反復0回。初回の検証コードに出た参照比較のCS0252警告はIdentityのReferenceEqualsを明示して修正し、次のビルド起動で解消を確認した。製品コードは動作検証後に変更していない。既存GridFieldGeneratorの未使用フィールド5種のCS0414警告、ライセンス更新のAccess token警告は残り、検証とビルドは正常終了した。仕様の取り違えによる手戻り0件。

ログはローカル一時領域の `FactoryGame-TRN01-transfer.log` と `FactoryGame-TRN01-build.log`。ビルド時の設定3ファイル再保存は着手前のハッシュへ復元した。既存未コミット差分7件とPrefab・SO・シーン参照を保持する。画面表示とプレイヤーの操作は今回の変更対象から外れるため、検証は搬送契約の実行・数量・寿命とコンパイルに絞った。
