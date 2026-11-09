# 設備の有効能力

タグ: `現行仕様` `PRG-01` `能力合成` `能力設定` `共有効果` `予約維持`

## 計算と対象

有効能力は`max(1, floor(基礎値 × 倍率 ＋ 加算値))`とする。基礎値は正、倍率と加算値は0以上で両方0は拒否する。decimalで計算し、int上限超過を拒否する。基礎設定のシリアライズ値は保持し、有効値をプロパティから読む。倍率と加算値は種類・能力ごとに独立し、未取得時は倍率1・加算0となる。

| セル | 独立して設定する能力 |
| --- | --- |
| 木材採取・石材採取 | 各種類の抽出量、抽出在庫容量 |
| 搬出コンベア | 搬出時に取得する数量 |
| 保管 | 容量 |
| 加工 | 入力方向ごとの容量、出力容量 |

空・資源・特異点セルには能力を追加しない。交差セルは上流から渡された一件の数量をそのまま予約・中継するため、独立した搬送量を持たない。通常コンベアも受領済みの一件を分割せず、搬出コンベアが抽出・保管・加工セルから取得する時点で搬送量を使う。

## 実行時状態と反映

グリッドが一プレイの `SharedStatEffectsApplication` を所有し、`SaveCell` で初期配置・新設セルへ同じ参照を渡す。共有設定は種類と能力をキーとして倍率・加算値を保持する。セルの登録時に基礎値を登録し、購入前に既存の有効値の減少と上限超過を検証する。スキルの前提・取得状態・支出は [共有スキルツリー](skill-tree.md) が管理し、取得と支出の確定後に能力変更を通知する。種類別強化の費用と購入条件は[種類別強化仕様](currency-equipment-upgrade.md)に従う。

能力別設定は共有状態が保持し、各セルのProgressionTypeとEquipmentStatDomainから読み取る。通常UIはスキル購入だけを使い、セルを一基ずつ更新しない。交差セルは独立した強化効果を持たず、上流の固定数量を中継する。

容量は更新直後から使い、在庫・資源種別・予約量・座標・方向・接続・Identityを維持する。能力を減らす設定変更や効果の再構成を拒否する。共有効果は非アクティブなセルにも保持され、再有効化で現在値を読む。セル破棄時に共有通知の購読を解除する。数量表示を持つセルは能力変更で `StatusChanged` を通知する。

抽出は周期開始時の抽出量と空き容量から数量を固定する。周期中の強化は次周期へ反映し、輸出で空きが増えても進行中の生産量を変えない。無効化・対象喪失・切断による未完了進捗の破棄は[抽出寿命](extractor-lifecycle.md)に従う。

搬出は `TryExport` 成功時の数量とIDを保持し、演出・予約・中継・確定では能力を再計算しない。次の取得試行で新しい能力を使う。加工は開始時の `CraftingOperationApplication` とレシピ計画を保持し、能力変更で素材量や成果物数を増やさない。既存の予約は増加後の容量で確定・取消できる。

## コードと検証入口

- [計算規則](../../Assets/Scripts/Progression/Domain/EffectiveStatsRulesDomain.cs): 倍率・加算・切り捨て、値の範囲と最大値検証。
- [共有状態](../../Assets/Scripts/Progression/Application/SharedStatEffectsApplication.cs): 種類・能力別の倍率と加算値、変更通知。
- [セル基底](../../Assets/Scripts/Cells/Adapter/CellBaseAdapter.cs): 能力別設定の読取、共有状態の接続、数量表示通知と購読寿命。
- [グリッド](../../Assets/Scripts/Grid/Adapter/GridFieldDatabaseAdapter.cs): セッション状態の所有と登録時の適用。
- [Editor検証](../../Assets/Scripts/Progression/Editor/EffectiveStatsVerifierEditor.cs): `Run`で任意の倍率・加算値の計算境界を検証する。SkillTreeVerifierEditor.Runから同じ境界検証を呼び、MainSceneのPlay Modeで抽出・搬送・加工中の変更、容量・予約維持、新設と再有効化を確認する。

関連契約: [予約](resource-reservations.md)、[搬送状態](resource-transfer-state.md)、[加工](crafting.md)、[状態UI](cell-status-ui.md)。実行結果はローカルの `docs/verification/prg01-effective-stats.json` に記録する。
