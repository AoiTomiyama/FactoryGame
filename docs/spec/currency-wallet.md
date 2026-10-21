# 通貨残高と入出金取引

タグ: `現行仕様` `通貨` `価格` `予約` `再入` `ECO-01`
更新日: 2026-10-21

## 所有者と利用手順

一プレイの構成入口が `CurrencyEconomyDefinition.CreateCatalog()` で設定のコピーを取得し、`new CurrencyWalletApplication(catalog.InitialBalance)` で共有残高を生成する。同じ残高インスタンスとカタログを利用先へ渡し、再開始時に作り直す。Application/DomainはUnity非依存。操作と購読はUnityのメインスレッドで行う。

ECO-01は取引基盤を提供する。MainSceneへの共有所有者の配線と搬送換金はECO-02、設置課金はECO-03、画面表示はECO-05で接続する。現在のセル配置・搬送動作は既存経路を使う。

## 数値と設定

- 残高は0〜long.MaxValue。初期値は100。売価と設備価格は正のlong、数量は正のintで、積を事前検査してcheckedで計算する。0・負数・桁あふれは金額0とfalseを返す。
- 定義の初期売価は木材・石材が1。設備価格は木材/石材抽出20、通常/搬出コンベア5、交差10、保管15、加工30。UnityのCreateメニュー `FactoryGame/Currency Economy` で設定を作成できる。
- カタログ生成は負の初期残高、nullの価格列、0以下の価格、重複種別、未定義enum、None、設備以外の設置価格を例外で拒否する。売価が未登録の資源・価格が未登録の設備は見積りに失敗する。カタログは入力を複製し、その後の設定変更から独立する。

## 予約と終端

`Current`は確定残高B、予約入金I、予約支出E、利用可能残高B−Eの値スナップショットを返す。予約は全額を確保し、失敗時は状態・通知を変えず、取引をnullで返す。

| 操作 | 受理条件・変更 |
| --- | --- |
| `TryReserveIncome(A, out transaction)` | A > 0、A ≤ long.MaxValue−B−I。IへAを加える |
| `TryReserveExpense(A, out transaction)` | A > 0、A ≤ B−E。EへAを加える |
| 入金の`TryCommit()` | IからAを引き、BへAを加える |
| 支出の`TryCommit()` | EとBからAを引く |
| `TryCancel()` | 対応する予約合計からAを引く |

常に0 ≤ E ≤ B、0 ≤ I ≤ long.MaxValue−Bを保つ。入金予定額は支出へ使えず、支出予約は入金余地を増やさないため、支出取消で入金保証が崩れない。支出確定後に空いた余地は新しい入金予約へ使える。

予約成功時に返す `CurrencyTransactionApplication` は所有する残高、額、入出金区分を固定する。PendingからCommittedかCancelledへの遷移は一回。終端後の確定・取消はfalse。呼出元は操作が失敗・中断したときに予約を取り消す。成立した予約の確定には追加の残高判定を要しない。

## 通知と再入

予約・確定・取消は全状態を更新してから `Changed(Snapshot)` を通知する。取引は通知前に終端へ移るため、同じ取引への再入は失敗する。別取引への再入は受理条件に従って実行し、そのスナップショットをキューへ追加して発生順に通知する。通知中に再入があれば、`Current`は通知引数より新しい状態を返す場合がある。

購読者の例外は `LastNotificationException` へ保持し、後続購読者と後続通知を続行する。取引結果は確定した状態を表す。購読解除は利用側が寿命に合わせて行う。

## コードと検証入口

- [数量計算](../../Assets/Scripts/Economy/Domain/CurrencyRulesDomain.cs)、[価格カタログ](../../Assets/Scripts/Economy/Domain/CurrencyPriceCatalogDomain.cs)
- [共有残高](../../Assets/Scripts/Economy/Application/CurrencyWalletApplication.cs)、[取引](../../Assets/Scripts/Economy/Application/CurrencyTransactionApplication.cs)、[設定定義](../../Assets/Scripts/Economy/Definitions/CurrencyEconomyDefinition.cs)
- [Editor検証](../../Assets/Scripts/Economy/Editor/CurrencyWalletVerifierEditor.cs): `CurrencyWalletVerifierEditor.Run`。数量・価格の境界、定義不正、設定コピー、残高不足・上限、入金競合、支出取消、終端の一回性、再入・通知例外を確認する。4予約の24処理順×16確定/取消の384ケースを独立した残高モデルと照合し、既存 `ResourceReservationVerifierEditor.Run` を同じ起動で実行する。

## 検証証拠

6つのC#ファイルをパス順で並べた `path SHA256\n`（UTF-8）のSHA256は `93ad8064e3e51327230adc58ab8415100dc261dcc698aede9a16566088ce0f2c`。対象コードは `b6a5d8e7dd5514c927446c4adb7803fcefe130ee` からの `Assets/Scripts/Economy/` 追加差分。Unity 6000.3.8f1のバッチ・描画なしで `CurrencyWalletVerifierEditor.Run` を実行し、コンパイル、通貨7,378項目と既存資源予約のEdit Mode検証が成功した。プロセス終了コード0、41.93秒。起動1回、再検証0回、仕様の取り違えによる手戻り0件。

コマンド引数は `-batchmode -nographics -quit -projectPath C:/Users/Taomo/Unity/FactoryGame -executeMethod CurrencyWalletVerifierEditor.Run -logFile <log>`。ログはローカルの `~/.codex/runtime/FactoryGame-ECO01-wallet.log`。CSコンパイル警告・エラーは0件。ライセンス更新のaccess token警告と終了時のCurl error 42が出たが、検証成功ログとUnityプロセス終了コード0を確認した。新規コードはUnity非依存の計算・状態とSO入力であり、Play Mode・プレイヤービルドはECO-01では実行していない。

meta監査は重複GUID・孤立meta・meta不足0件。既存のシーン・Prefab・設定アセットは変更していない。文章検査のerrorは0件。制約に関する警告は予約上限と既存設計の境界を説明するため保持した。

## 着手前の差分

SampleSceneProfileのfilter追加・MotionBlur有効化は描画結果と採用意図が未確認。未使用ServiceLocatorは破棄済みオブジェクトと登録解除の利用契約が未確定。IDEの4 XMLはローカルのCopilot移行記録。既存の[差分レビュー](cell-status-ui.md#着手前の差分レビュー)と照合し、これら7ファイルを保持した。描画比較、ServiceLocatorの採用判断と寿命検証は各変更を扱う際に行う。
