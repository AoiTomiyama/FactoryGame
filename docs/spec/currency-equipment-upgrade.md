# 通貨による設備の個別強化

タグ: `現行仕様` `ECO-04` `設備強化` `支出取引` `期待レベル` `状態維持`
更新日: 2026-10-24

## 対象と費用

設置済み設備を同じインスタンスのままレベル1→2→3へ強化する。次のレベルの費用は[通貨設定](currency-wallet.md)の設置価格×現在レベル。抽出設備は20・40、通常・搬出コンベアは5・10、交差は10・20、保管は15・30、加工は30・60を支払う。最大レベル、不足、価格のlong上限超過を購入前に拒否する。

空・資源・特異点セルは購入対象から外す。交差セルはレベルを保持し、上流から受け取った数量をそのまま中継する。[有効能力](effective-stats.md)を持つ設備は個別レベルを数量・容量へ合成する。

## 購入入口と取引

[Generator](../../Assets/Scripts/Grid/Adapter/GridFieldGeneratorAdapter.cs)が一プレイの`Upgrades`を生成し、設置・特異点・スキルと同じ共有残高と固定価格カタログを渡す。[EquipmentUpgradeAdapter](../../Assets/Scripts/Economy/Adapter/EquipmentUpgradeAdapter.cs)の`CanPurchase(cell, expectedLevel, out cost)`で費用と操作可否を読み、`Purchase(cell, expectedLevel)`で購入する。`expectedLevel`には表示・見積り時のレベルを渡す。同じレベルを期待する連続入力は一度だけ成功し、次段階を購入するときは新しいレベルで見積もる。購入ボタンと統合表示は[経済UI](currency-loop-ui.md)へ接続した。

[EquipmentUpgradeApplication](../../Assets/Scripts/Economy/Application/EquipmentUpgradeApplication.cs)は対象確認、全額支出予約、対象再確認、通知を伴わないレベル更新、支出確定、能力変更通知の順で同期的に進める。Adapterはセルの生存、有効状態、固定座標とグリッド登録参照の一致、実際の設備種別を提供する。支出予約通知中の削除・置換・無効化・座標変更・レベル変更では予約を取り消す。基礎能力の検証失敗でも更新前のレベルと残高を保持する。

支出確定通知からは更新済みレベルと残高を読める。能力変更通知も支出確定後に行う。通知が完了するまで同じ購入Applicationへの再入を`Busy`で拒否する。通知例外で成立済み取引を取り消さず、`LastException`へ記録する。可否判定は通知中も現在の状態を返す。結果は`Success`、`Busy`、`InvalidTarget`、`LevelChanged`、`MaxLevel`、`InvalidPrice`、`InsufficientFunds`、`Failed`で区別する。

## 保持する状態

購入ではセルを生成・再初期化しない。座標・方向・接続・Identity・在庫・予約・保持IDと稼働状態を保持する。容量は直ちに増加し、稼働中の抽出量・搬送量・加工計画は開始時の値を使う。次の抽出周期・搬送取得から新しい数量を使う。予約の確定・取消は既存契約に従う。

`TrySetEquipmentLevel`は能力基盤の設定入口で、費用を伴う購入は`Upgrades.Purchase`を使う。実行時レベルはセルが所有し、新設セルと再開始したプレイでは1から始まる。

## 検証入口

[EquipmentUpgradeVerifierEditor.Run](../../Assets/Scripts/Economy/Editor/EquipmentUpgradeVerifierEditor.cs)は価格、不足、上限、long境界、重複要求、予約競合、通知再入・例外、変更後の再確認を検証する。MainSceneで全7種の価格と段階、同一インスタンス・位置・方向、削除・置換・無効化・座標変更、不正能力の失敗復元、交差の接続・予約・保持IDを確認する。

同じ起動で[既存能力検証](../../Assets/Scripts/Progression/Editor/EffectiveStatsVerifierEditor.cs)を購入経由で実行し、抽出周期・搬送IDと数量・加工計画、在庫と予約、共有効果との合成を確認する。既存の数量・加工規則・接続の検証も含む。再生前にEditor音声を消音する。実行条件、対象コード、結果と最終ログはローカルの`docs/verification/eco04-equipment-upgrade.json`へ集約する。

[SkillTreeVerifierEditor.Run](../../Assets/Scripts/Progression/Editor/SkillTreeVerifierEditor.cs)では、交差セルの個別レベル対応と共有効果を分けて確認する。共有スキルI/IIの取得前に予約した中継数量5と同じ処理・個別レベルを維持し、受け取り側の容量だけが共有効果で増加すること、取得後も元の予約を取り消せることを確認する。
