# 初期特異点と出荷セル

タグ: `現行仕様` `換金` `出荷` `特異点` `全量予約` `初期配置`
更新日: 2026-11-11。

## 役割の分離

特異点は5×5・外周20入力の吸収先、出荷セルは1×1・四方向入力の換金先である。特異点へ搬送しても共有残高は増えない。出荷セルで演出成功後に搬入が確定したとき、既存売価で共有残高へ入金する。出荷から納品進捗は増えない。累計納品数と段階目標は次工程で実装する。

[ShippingCellAdapter](../../Assets/Scripts/Economy/Adapter/ShippingCellAdapter.cs)は[IContainableApplication](../../Assets/Scripts/Transfers/Application/IContainableApplication.cs)を通じて既存の搬送へ接続する。[ShippingIntakeApplication](../../Assets/Scripts/Economy/Application/ShippingIntakeApplication.cs)が各ポートの全量・種別・換金額と入金余地を予約する。確定・取消・無効化・撤去の一回性と、共有残高上限の契約を既存の換金処理から引き継いだ。

[SingularityCellAdapter](../../Assets/Scripts/Economy/Adapter/SingularityCellAdapter.cs)は[AbsorptionIntakeApplication](../../Assets/Scripts/Economy/Application/AbsorptionIntakeApplication.cs)へ吸収予約を渡す。数量と種別が一致する搬送だけを確定・取消する。予約を外してからAbsorbedを通知し、再入・繰返し確定を防ぐ。通知の例外は記録し、ほかの購読者への通知を続ける。売価のない定義済み資源も吸収できる。

両者とも資源IDを所有しない。演出失敗や切断では送り元がIDを保持し、成功終端の返却・削除は[共有Coordinator](../../Assets/Scripts/Transfers/Application/ResourceTransferCoordinatorApplication.cs)が一度だけ行う。無効化時は新規予約を止め、接続先の取消を伝えてから自身の未確定枠を解除する。再有効化では同じIdentityで接続を再開する。

## 構成と購入

[GridFieldGeneratorAdapter](../../Assets/Scripts/Grid/Adapter/GridFieldGeneratorAdapter.cs)が残高・価格カタログを構成し、全所有セルの登録後、各出荷セルへ同じ残高と価格を渡す。追加配置では[CellPlacerAdapter](../../Assets/Scripts/Grid/Adapter/CellPlacerAdapter.cs)が登録後・接続開始前にConfigureする。登録・初期化が失敗した場合は既存の設置取引が復元する。

[通貨設定](../../Assets/Scripts/ScriptableObject/CurrencyEconomy.asset)の初期残高100と売価は保持した。出荷セルの追加購入価格は30で、既存の配置・回転・無料撤去を使う。特異点の保護は維持する。出荷先の残高や内部予約数は詳細欄へ重複表示せず、共有残高は既存HUDの表示を使う。

## 初期配置と換金経路

中心を(c,c)とすると特異点の占有はc-2..c+2、初期出荷先は(c,c-3)。木材を(c,c-7)、抽出候補を(c,c-6)、搬出候補を(c,c-5)、通常コンベア候補を(c,c-4)に確保する。出荷セルは初期配置なので購入費を払わない。抽出設備20・搬出5・搬送5の計30で最初の換金経路を作れる。

100×100なら特異点は(50,50)、初期出荷先は(50,47)、木材は(50,43)。抽出を(50,44)へ180度、搬出を(50,45)、搬送を(50,46)へ配置する。最小フィールドは14×14となる。

[SingularityStartDefinition](../../Assets/Scripts/Economy/Definitions/SingularityStartDefinition.cs)が参照・占有・出荷先・木材種別・初回経路と予算を検証する。初期生成時は元の地形を優先配置で置き換える。MainSceneの既存Overrideは保存し直さず、現在のPrepareInitialLayout経路で初期出荷先を構成する。

## Prefabと編集元

[ShippingCellModel](../../Assets/Prefabs/Models/ShippingCellModel.prefab)は独立した外観の編集元である。[Field](../../Assets/Prefabs/Field/General/F_ShippingCell.prefab)は白いF_EmptyCell、[Placeholder](../../Assets/Prefabs/Placeholder/General/P_ShippingCell.prefab)は緑のP_EmptyCellを基底にする。既存Prefab Variant Creatorで作成し、同じModelを入れ子にした。四方向の受入口を持つ暫定モデルで、コンセプト画像の最終造形とは区別する。

[ShippingSetupEditor](../../Assets/Scripts/Economy/Editor/ShippingSetupEditor.cs)はPrefabと既存のCellDatabase・通貨・初期配置定義を接続する。Sceneの保存や新しい独立設定資産の作成は行わない。

## 検証

[ShippingVerifierEditor.Run](../../Assets/Scripts/Economy/Editor/ShippingVerifierEditor.cs)は保存資産・吸収の境界と通知を確認し、既存SingularityVerifierEditorへ進む。通貨上限、数量、再入、初期生成、四方向出荷、追加購入、回転、複数出荷先、無効化・撤去・再接続、実搬送を検証する。EconomyUIVerifierEditorでPC2寸法と購読寿命、CraftingVerifierEditorで加工品の換金と5×5特異点への吸収、EconomyLoopVerifierEditorで実収益からの再投資を確認する。自動再生は消音し、ビルドは明示依頼時のみ行う。

対象差分・保存ファイルのハッシュ・結果・代表画像はローカルのdocs/verification/shipping/へ集約する。
