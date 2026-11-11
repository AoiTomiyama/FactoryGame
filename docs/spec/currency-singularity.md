# 初期特異点と出荷セル

タグ: `現行仕様` `換金` `出荷` `特異点` `全量予約` `初期配置` `累計納品` `段階目標`
更新日: 2026-11-11。

## 役割の分離

特異点は5×5・外周20入力の吸収先、出荷セルは1×1・四方向入力の換金先である。特異点へ搬送しても共有残高は増えない。出荷セルで演出成功後に搬入が確定したとき、既存売価で共有残高へ入金する。出荷から納品進捗は増えない。特異点への確定納品を品目別に累計し、段階・最終目標を判定する。

[ShippingCellAdapter](../../Assets/Scripts/Economy/Adapter/ShippingCellAdapter.cs)は[IContainableApplication](../../Assets/Scripts/Transfers/Application/IContainableApplication.cs)を通じて既存の搬送へ接続する。[ShippingIntakeApplication](../../Assets/Scripts/Economy/Application/ShippingIntakeApplication.cs)が各ポートの全量・種別・換金額と入金余地を予約する。確定・取消・無効化・撤去の一回性と、共有残高上限の契約を既存の換金処理から引き継いだ。

[SingularityCellAdapter](../../Assets/Scripts/Economy/Adapter/SingularityCellAdapter.cs)は[AbsorptionIntakeApplication](../../Assets/Scripts/Economy/Application/AbsorptionIntakeApplication.cs)へ吸収予約を渡す。数量と種別が一致する搬送だけを確定・取消する。全ポート共通の納品状態へ全量を予約し、ポート予約を外してから累計を確定する。その後にAbsorbedを通知し、再入・繰返し確定を防ぐ。通知の例外は記録し、ほかの購読者への通知を続ける。売価のない定義済み資源も吸収できる。

両者とも資源IDを所有しない。演出失敗や切断では送り元がIDを保持し、成功終端の返却・削除は[共有Coordinator](../../Assets/Scripts/Transfers/Application/ResourceTransferCoordinatorApplication.cs)が一度だけ行う。無効化時は新規予約を止め、接続先の取消を伝えてから自身の未確定枠を解除する。再有効化では同じIdentityで接続を再開する。

## 構成と購入

[GridFieldGeneratorAdapter](../../Assets/Scripts/Grid/Adapter/GridFieldGeneratorAdapter.cs)が残高・価格カタログ・納品進行を構成し、全所有セルの登録後、各出荷セルへ同じ残高と価格を渡す。追加配置では[CellPlacerAdapter](../../Assets/Scripts/Grid/Adapter/CellPlacerAdapter.cs)が登録後・接続開始前にConfigureする。登録・初期化が失敗した場合は既存の設置取引が復元する。

[通貨設定](../../Assets/Scripts/ScriptableObject/CurrencyEconomy.asset)の初期残高100と売価は保持した。出荷セルの追加購入価格は30で、既存の配置・回転・無料撤去を使う。特異点の保護は維持する。出荷先の残高や内部予約数は詳細欄へ重複表示せず、共有残高は既存HUDの表示を使う。

## 初期配置と換金経路

中心を(c,c)とすると特異点の占有はc-2..c+2、初期出荷先は(c,c-3)。木材を(c,c-7)、抽出候補を(c,c-6)、搬出候補を(c,c-5)、通常コンベア候補を(c,c-4)に確保する。出荷セルは初期配置なので購入費を払わない。抽出設備20・搬出5・搬送5の計30で最初の換金経路を作れる。

100×100なら特異点は(50,50)、初期出荷先は(50,47)、木材は(50,43)。抽出を(50,44)へ180度、搬出を(50,45)、搬送を(50,46)へ配置する。最小フィールドは14×14となる。

[SingularityStartDefinition](../../Assets/Scripts/Economy/Definitions/SingularityStartDefinition.cs)が参照・占有・出荷先・木材種別・初回経路と予算を検証する。初期生成時は元の地形を優先配置で置き換える。MainSceneの既存Overrideは保存し直さず、現在のPrepareInitialLayout経路で初期出荷先を構成する。

## Prefabと編集元

[ShippingCellModel](../../Assets/Prefabs/Models/ShippingCellModel.prefab)は独立した外観の編集元である。[Field](../../Assets/Prefabs/Field/General/F_ShippingCell.prefab)は白いF_EmptyCell、[Placeholder](../../Assets/Prefabs/Placeholder/General/P_ShippingCell.prefab)は緑のP_EmptyCellを基底にする。既存Prefab Variant Creatorで作成し、同じModelを入れ子にした。四方向の受入口を持つ暫定モデルで、コンセプト画像の最終造形とは区別する。

[ShippingSetupEditor](../../Assets/Scripts/Economy/Editor/ShippingSetupEditor.cs)はPrefabと既存のCellDatabase・通貨・初期配置定義を接続する。Sceneの保存や新しい独立設定資産の作成は行わない。

## 累計納品と段階達成

[DeliveryProgressApplication](../../Assets/Scripts/Progression/Application/DeliveryProgressApplication.cs)が一回のプレイの品目別累計を所有する。搬送の全量予約では、確定済みと全ポートの未確定分を合わせてlong上限を検証する。確定した全量を一度だけ加え、取消・接続解除・無効化では未確定分を解放する。確定済みの納品は接続解除や段階遷移でも残す。プレイを再開始すると初期化し、セーブデータへの永続化は今回の範囲に含めない。

現在の段階の全条件を満たすと次へ進み、一度の納品で複数段階を達成できる。目標を超えた量も累計に残し、後の段階で同じ品目を要求する場合も同じ累計を参照する。後段の品目を先に納品しても記録する。最終達成後も定義済みの全資源を吸収・記録し、工場と散策を続けられる。

変更・段階達成・最終達成は順番に通知し、通知から納品が再入しても達成通知を重複させない。購読者の例外はLastNotificationExceptionへ記録し、後続の通知を続ける。出荷の確定はこの状態を変更せず、納品の確定も通貨を変更しない。

## 目標の編集

[DeliveryGoals.asset](../../Assets/Scripts/ScriptableObject/DeliveryGoals.asset)を選び、Stagesの各要素でid、label、requirementsを編集する。requirementsのresourceが品目、cumulativeAmountがその品目の累計必要数である。最後の要素が最終目標になる。初期配置・価格と独立して段階構成と数量を調整するため、専用の設定資産を一つ追加した。新しいUI生成系統は追加していない。

[DeliveryGoalsDefinition](../../Assets/Scripts/Progression/Definitions/DeliveryGoalsDefinition.cs)から[DeliveryGoalCatalogDomain](../../Assets/Scripts/Progression/Domain/DeliveryGoalCatalogDomain.cs)へコピーし、段階IDの重複、空の名前・条件、品目の重複、無効な品目、0以下の必要数、同じ品目の累計目標の減少を開始前に検出する。実行中の累計や予約をアセットへ書き戻さない。変更は次回プレイ開始時に反映する。

仮の初期目標は次のとおり。到達時間と数量の調整は通し確認で行う。

| 段階 | 品目と累計必要数 |
| --- | --- |
| 基礎投入 | 木材40、石40 |
| 加工投入 | 板材30、石レンガ20 |
| 複合加工投入（最終） | 建築部材20 |

## 納品UI

[DeliveryGoalsViewAdapter](../../Assets/Scripts/Progression/Adapter/DeliveryGoalsViewAdapter.cs)が既存の上部常時表示へ現在の段階と必要品の確定数／必要数を渡す。特異点の選択時には右の既存詳細枠へ同じ目標を表示し、スキルの適用ボタンを隠す。スキルへ戻ると適用操作を復帰する。段階達成時に段階表示を一度だけ軽く拡縮し、最終達成後は「目標達成」を常時表示する。

上部は既存の行Prefab・折返しを使い、スクロールしない。長い詳細は右枠の既存スクロールで末尾へ移動する。納品更新で左タブや選択を切り替えない。散策中は残高だけを残して納品表示を隠し、戻ると最新状態を表示する。EconomyPanelAdapterの有効化・無効化・再構成に合わせて購読を接続・解除する。内部予約数は表示しない。

## 検証

[ShippingVerifierEditor.Run](../../Assets/Scripts/Economy/Editor/ShippingVerifierEditor.cs)は保存資産・吸収の境界と通知を確認し、既存SingularityVerifierEditorへ進む。通貨上限、数量、再入、初期生成、四方向出荷、追加購入、回転、複数出荷先、無効化・撤去・再接続、実搬送を検証する。EconomyUIVerifierEditorでPC2寸法と購読寿命、CraftingVerifierEditorで加工品の換金と5×5特異点への吸収、EconomyLoopVerifierEditorで実収益からの再投資を確認する。自動再生は消音し、ビルドは明示依頼時のみ行う。

対象差分・保存ファイルのハッシュ・結果・代表画像はローカルのdocs/verification/shipping/へ集約する。

[DeliveryGoalsVerifierEditor.Run](../../Assets/Scripts/Progression/Editor/DeliveryGoalsVerifierEditor.cs)は納品規則、設定の編集・Undo／Redo・保存と再読込、実Sceneの特異点・HUD接続、1600×900／1366×768での長い品目名・7品目・long上限付近の数量、詳細末尾へのスクロール、一人称からの復帰を対象検証する。画像取得は既存EconomyUIVerifierEditorの手順を共用する。実行記録と代表画像はローカルのdocs/verification/delivery-goals/へ集約する。通しプレイと目標数のバランス調整は次工程とする。
