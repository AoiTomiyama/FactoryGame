# セル種類別の強化

タグ: `現行仕様` `PRG-03` `設備強化` `支出取引` `状態維持`

## 採用した操作

2026-11-09のユーザー採用1Aにより、一基ずつの個別強化購入UIを[種類別の共有スキル](skill-tree.md)へ統合した。セルを選んだ右側は種類名・共有レベルと在庫・状態の読み取り専用とし、強化はスキルノードから適用する。

木材採取・石材採取・保管庫・加工設備・搬出路の5種類にI/IIを持つ。基礎研究50の後、I50・II100を一度ずつ支払い、同じ種類の全セルをLv.1→2→3へ上げる。配置済み、非アクティブ、再有効化、新しく配置したセルは、同じ共有レベルを読む。他種類へ効果を波及させない。

## 所有と取引

`GridFieldDatabaseAdapter.StatEffects`が一プレイの種類別レベルを所有し、`SaveCell`が既存・新設セルへ同じ参照を渡す。各セルの`ProgressionType`で型を識別し、`EquipmentLevel`から共有レベルを読む。スキルの取得・支払い・通知は`SkillTreeApplication`へ集約する。Generatorは通常プレイ向けの`EquipmentUpgradeAdapter`を生成しない。

基礎値に種類別レベルを乗じて有効能力を計算する。容量は取得直後から増加し、実行中の抽出量・搬送量・加工計画は開始時の値を使う。次の周期・取得から新しい数量を使う。座標・方向・接続・Identity・在庫・予約・保持IDと稼働状態を保つ。

旧`EquipmentUpgradeApplication`・`EquipmentUpgradeAdapter`と個別レベル設定は、既存の取引境界・能力計算のEditor回帰で使う低水準の入口として保持する。通常UIには接続しない。再開始時は種類別レベル1・未取得状態へ戻る。

## 検証

[SkillTreeVerifierEditor.Run](../../Assets/Scripts/Progression/Editor/SkillTreeVerifierEditor.cs)が種類別の取引と効果範囲、既設・新設・再有効化、実行中の抽出・搬出・加工、予約と在庫の保持を検証する。UIと実際の換金収益による再投資は[スキル仕様](skill-tree.md#検証入口)の既存入口を使う。実行記録はローカルの`docs/verification/type-skills.json`へ集約する。
