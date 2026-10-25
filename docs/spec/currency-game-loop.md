# 基礎ゲームループの統合

タグ: `現行仕様` `ECO-06` `MainScene` `換金` `再投資` `統合検証`

## 初期状態と進行

MainSceneは共有残高100、中央の特異点1基、木材と初回経路を持つ。Generatorが残高・価格・強化・共有スキルを構成し、Placerと経済パネルへ同じ取引先を渡す。初期配置と生成時の検証は[特異点仕様](currency-singularity.md)、設置は[設備設置仕様](currency-placement.md)、操作表示は[経済UI仕様](currency-loop-ui.md)を参照する。

100×100のフィールドでは、木材(50,46)へ向けて抽出設備を(50,47)に180度で設置し、搬出コンベア(50,48)、通常コンベア(50,49)を中央(50,50)へ向ける。設置費20+5+5=30を支払った残高70から、採取・搬送・換金によって残高が増える。

収益を[個別強化](currency-equipment-upgrade.md)と[共有スキル](skill-tree.md)へ再投資する。抽出設備のLv.1→2→3は20+40、基礎研究は50、採取・搬送・保管の各枝はI=50、II=100。全枝IIまで取得して通常コンベアを1基追加すると、経路を含む総支出は595となる。収支は常に初期100＋換金収益−支出で照合する。既設と新設の設備は同じ共有能力規則を使う。

## 検証入口と判定

[EconomyLoopVerifierEditor.Run](../../Assets/Scripts/Economy/Editor/EconomyLoopVerifierEditor.cs)はMainSceneをPlay Modeで開き、実際のPointer操作入口で経路を設置する。初回換金後、経済パネルから強化と全3枝のI・IIを購入し、既設能力、新設設備への効果、購入後の継続収益、収支と通知例外を確認する。資金は採取からの換金で得る。

初回換金は通常速度、その後の蓄積はTime.timeScale=20で短縮する。設備の数量・周期・価格定義はそのまま使い、終了時に時間倍率を戻す。再生前にEditorの消音を設定し、購入資金の待機には実時間180秒の上限を設ける。

直接関連する回帰は次の既存入口を使う。

| 対象 | 入口 |
| --- | --- |
| 搬送・予約・資源ID寿命 | EconomyLoopVerifierEditor.RunTransfers → ResourceTransferVerifierEditor.Run。内部でResourceReservationVerifierEditor.Runも実行 |
| 加工の実行・中断 | EconomyLoopVerifierEditor.RunCrafting → CraftingVerifierEditor.RunPlay |
| 状態UI・抽出寿命・数量・加工規則 | EconomyUIVerifierEditor.RunLegacyUI → UIStatusVerifierEditor.Run |
| 経済表示・購入操作・購読寿命 | EconomyUIVerifierEditor.Run |
| 設置・接続・取引失敗時の復元 | EconomyUIVerifierEditor.RunPlacement |
| Windows 64-bit通常ビルド | GridFieldGeneratorVerifierEditor.RunPlayerBuild |

実行条件、対象コードのハッシュ、終了結果とログはローカル管理の`docs/verification/eco06-loop.json`へ集約する。描画の目視と生成プレイヤーの手操作は別の確認範囲となる。
