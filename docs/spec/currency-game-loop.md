# 基礎ゲームループの統合

タグ: `現行仕様` `ECO-06` `MainScene` `換金` `再投資` `統合検証`

## 初期状態と進行

今後の拡張方針は[コンセプト](../design/game-concept.md)と[納品・出荷の分離計画](../tasks/black-hole-loop-plan.md)を参照する。以下は現在動く、出荷セルで換金する基礎ループの説明である。

MainSceneは共有残高100、中央の特異点1基、初期出荷セル1基、木材と初回換金経路を持つ。初期配置と特異点の登録後、カメラの追従先を特異点セルの位置へ一度だけ合わせる。初回は追従の補間をリセットし、その後の手動移動・ズーム・視点切替を保つ。Generatorが残高・価格・種類別の共有スキルを構成し、Placerと経済パネルへ同じ取引先を渡す。初期配置と生成時の検証は[特異点仕様](currency-singularity.md)、設置は[設備設置仕様](currency-placement.md)、操作表示は[経済UI仕様](currency-loop-ui.md)を参照する。

100×100のフィールドでは、木材(50,43)へ向けて抽出設備を(50,44)に180度で設置し、搬出コンベア(50,45)、通常コンベア(50,46)を初期出荷セル(50,47)へ向ける。特異点のアンカーは(50,50)で、占有は48〜52となる。設置費20+5+5=30を支払った残高70から、採取・搬送・出荷によって残高が増える。

収益を[種類別スキル](skill-tree.md)へ再投資する。現行カタログは上位加工の入力・出力も含む10能力系統・計27ノードである。既存の統合検証では、基礎研究50の後、木材採取・石材採取・保管・基本加工・搬出の8能力系統の各Iを50、IIを100で取得する代表経路を使う。この8系統のIIまで取得し、搬出コンベアを1基追加すると経路を含む総支出は1285となる。収支は初期100＋換金収益−支出で照合する。既設・新設の同種セルは同じ能力別設定を読む。

## 検証入口と判定

[EconomyLoopVerifierEditor.Run](../../Assets/Scripts/Economy/Editor/EconomyLoopVerifierEditor.cs)はMainSceneをPlay Modeで開き、実際のPointer操作入口で経路を設置する。初回換金後、経済パネルから全8系統のI・IIを購入し、既設能力、新設設備への効果、購入後の継続収益、収支と通知例外を確認する。資金は採取からの換金で得る。

初回換金は通常速度、その後の蓄積はTime.timeScale=20で短縮する。設備の数量・周期・価格定義はそのまま使い、終了時に時間倍率を戻す。再生前にEditorの消音を設定し、購入資金の待機には実時間180秒の上限を設ける。

直接関連する回帰は次の既存入口を使う。

| 対象 | 入口 |
| --- | --- |
| 搬送・予約・資源ID寿命 | EconomyLoopVerifierEditor.RunTransfers → ResourceTransferVerifierEditor.Run。内部でResourceReservationVerifierEditor.Runも実行 |
| 加工の実行・中断 | EconomyLoopVerifierEditor.RunCrafting → CraftingVerifierEditor.RunPlay |
| 状態UI・抽出寿命・数量・加工規則 | EconomyUIVerifierEditor.RunLegacyUI → UIStatusVerifierEditor.Run |
| 経済表示・購入操作・購読寿命 | EconomyUIVerifierEditor.Run |
| 設置・接続・取引失敗時の復元 | EconomyUIVerifierEditor.RunPlacement |
| Windows 64-bit通常ビルド（明示依頼時のみ） | GridFieldGeneratorVerifierEditor.RunPlayerBuild |

実行条件、対象コードのハッシュ、終了結果とログはローカル管理の`docs/verification/eco06-loop.json`へ集約する。描画の目視と生成プレイヤーの手操作は別の確認範囲となる。

板材・石レンガ・建築部材は[加工レシピ](crafting.md#追加した加工品とレシピ)で生産し、木材・石材と同じ出荷セルへ搬送して換金できる。売価は順に3・4・16で、既存の初期残高・採取経路・購入価格を保持する。
