# クラス・スクリプト対応表

タグ: `現行設計` `スクリプト一覧` `責務` `依存` `Editor境界`

対象版・未コミット反映条件は [現行設計索引](current-architecture.md)。全 `Assets/Scripts/**/*.cs` を収録する。主な自作依存はコメントを除いた型参照を照合し、同ファイル内の補助型は型欄に示す。メソッドの全呼出一覧ではなく、設計図から実装を辿るための索引である。外部はusingに現れる名前空間であり、実使用の代表は図式の説明を参照する。入れ子型の名前は所属ファイルで識別する。

確認ファイル数: 86（ECO-01の追加6件、未コミットServiceLocatorを1件含む）。

## ランタイム・定義

| ファイル / 宣言型 | 役割 | 主な自作依存 | 外部境界 |
| --- | --- | --- | --- |
| [CellBaseAdapter](../../Assets/Scripts/Cells/Adapter/CellBaseAdapter.cs)<br>CellBaseAdapter | 座標・モデル・初期化入口と搬送接点の識別・Unity破棄判定 | IResourceTransferEndpointApplication | UnityEngine |
| [ConnectableCellBaseAdapter](../../Assets/Scripts/Cells/Adapter/ConnectableCellBaseAdapter.cs)<br>ConnectableCellBaseAdapter | 4方向の隣接参照と接続通知 | CellBaseAdapter, DirectionsDomain, EmptyCellAdapter, GridFieldDatabaseAdapter | UnityEngine |
| [ConveyorCellAdapter](../../Assets/Scripts/Cells/Adapter/ConveyorCellAdapter.cs)<br>ConveyorCellAdapter, TransferStatus | 一件のID受付・前方搬送・取消と終了 | CellBaseAdapter, ConnectableCellBaseAdapter, DirectionsDomain, IContainableApplication, IResourceReusableApplication, ResourceAnimationResultApplication, ResourceItemObjectPoolAdapter, ResourceTransferCoordinatorApplication, ResourceTransferOperationApplication, ResourceTypeDomain | Cysharp.Threading.Tasks, UnityEngine |
| [CrossingCellAdapter](../../Assets/Scripts/Cells/Adapter/CrossingCellAdapter.cs)<br>CrossingCellAdapter, PendingTransfer | 方向別の下流予約・ID引継ぎ・保留搬送 | CellBaseAdapter, ConnectableCellBaseAdapter, IContainableApplication, IResourceReusableApplication, ResourceAnimationResultApplication, ResourceItemObjectPoolAdapter, ResourceReservationApplication, ResourceTransferCoordinatorApplication, ResourceTransferOperationApplication, ResourceTypeDomain | Cysharp.Threading.Tasks, UnityEngine |
| [EmptyCellAdapter](../../Assets/Scripts/Cells/Adapter/EmptyCellAdapter.cs)<br>EmptyCellAdapter | 空セルの識別 | CellBaseAdapter | 標準C# |
| [ExportConveyorCellAdapter](../../Assets/Scripts/Cells/Adapter/ExportConveyorCellAdapter.cs)<br>ExportConveyorCellAdapter, ExportStatus | 後方から出庫して表示IDを作り前方搬送へ渡す | CellBaseAdapter, ConveyorCellAdapter, DirectionsDomain, IExportableApplication, ResourceAnimationResultApplication, ResourceItemObjectPoolAdapter, ResourceTypeDomain | Cysharp.Threading.Tasks, TMPro, UnityEngine |
| [ExtractorCellAdapter](../../Assets/Scripts/Cells/Adapter/ExtractorCellAdapter.cs)<br>ExtractorCellAdapter | 資源元の初期参照・採取ループ・出庫 | CellBaseAdapter, CellStatusProviderAdapter, ConnectableCellBaseAdapter, ExtractorProviderDefinition, IDataProvidableApplication, IExportableApplication, IUIDataProviderApplication, ResourceCellAdapter, ResourceDefinition, ResourceTypeDomain | Cysharp.Threading.Tasks, DG.Tweening, UnityEngine |
| [PlaceholderCellAdapter](../../Assets/Scripts/Cells/Adapter/PlaceholderCellAdapter.cs)<br>PlaceholderCellAdapter | 配置プレビューRenderer参照 |  | UnityEngine |
| [ResourceCellAdapter](../../Assets/Scripts/Cells/Adapter/ResourceCellAdapter.cs)<br>ResourceCellAdapter | 固定資源種別と表示Provider | CellBaseAdapter, CellStatusProviderAdapter, IDataProvidableApplication, IUIDataProviderApplication, ResourceProviderDefinition, ResourceTypeDomain | UnityEngine |
| [StorageCellAdapter](../../Assets/Scripts/Cells/Adapter/StorageCellAdapter.cs)<br>StorageCellAdapter | 在庫・予約・入出庫と変更通知 | CellStatusProviderAdapter, ConnectableCellBaseAdapter, IContainableApplication, IDataProvidableApplication, IExportableApplication, IUIDataProviderApplication, ResourceStorageRulesDomain, ResourceTypeDomain, StorageProviderDefinition | UnityEngine |
| [CellEnumsDomain](../../Assets/Scripts/Cells/Domain/CellEnumsDomain.cs)<br>DirectionsDomain, ResourceTypeDomain, CellTypeDomain | セル・資源・接続方向の列挙 |  | 標準C# |
| [CrafterCellAdapter](../../Assets/Scripts/Crafting/Adapter/CrafterCellAdapter.cs)<br>CrafterCellAdapter, ResourceInputData | 方向別入力と出力在庫・加工ループ | CellStatusProviderAdapter, ConnectableCellBaseAdapter, CrafterProviderDefinition, CraftingOperationApplication, DirectionsDomain, IContainableApplication, IDataProvidableApplication, IExportableApplication, IUIDataProviderApplication, InlineSOAttributeAdapter, RecipeDatabaseDefinition, RecipeRulesDomain, ResourceStorageRulesDomain, ResourceTypeDomain | Cysharp.Threading.Tasks, DG.Tweening, UnityEngine |
| [CraftingOperationApplication](../../Assets/Scripts/Crafting/Application/CraftingOperationApplication.cs)<br>CraftingOperationApplication, Stage | 加工一件の確定・取消ガード | RecipePlanDomain, RecipeRulesDomain | 標準C# |
| [RecipeDatabaseDefinition](../../Assets/Scripts/Crafting/Definitions/RecipeDatabaseDefinition.cs)<br>RecipeDatabaseDefinition, RecipeDataDefinition, IngredientDefinition | レシピと素材配列の編集定義 | ResourceTypeDomain | UnityEngine |
| [RecipePlanDomain](../../Assets/Scripts/Crafting/Domain/RecipePlanDomain.cs)<br>RecipePlanDomain, Consumption | 素材消費と出力を固定した加工計画 | ResourceTypeDomain | 標準C# |
| [RecipeRulesDomain](../../Assets/Scripts/Crafting/Domain/RecipeRulesDomain.cs)<br>RecipeRulesDomain, Ingredient, Recipe, Input, Output, Commit | 素材適合と計画確定の純粋計算 | RecipePlanDomain, ResourceTypeDomain | 標準C# |
| [CalculationExtensionsAdapter](../../Assets/Scripts/Grid/Adapter/CalculationExtensionsAdapter.cs)<br>CalculationExtensionsAdapter | Unity座標の方向変換補助 |  | UnityEngine |
| [CellPlacerAdapter](../../Assets/Scripts/Grid/Adapter/CellPlacerAdapter.cs)<br>CellPlacerAdapter | 種別選択・プレビュー・ドラッグ範囲・セル置換 | CellBaseAdapter, CellDatabaseDefinition, CellStatusViewAdapter, CellTypeDomain, ConnectableCellBaseAdapter, EmptyCellAdapter, GridFieldDatabaseAdapter | UnityEngine |
| [GridFieldDatabaseAdapter](../../Assets/Scripts/Grid/Adapter/GridFieldDatabaseAdapter.cs)<br>GridFieldDatabaseAdapter | 既存グリッドの座標登録と検索 | CellBaseAdapter, SingletonMonoBehaviourAdapter | UnityEngine |
| [GridFieldGeneratorAdapter](../../Assets/Scripts/Grid/Adapter/GridFieldGeneratorAdapter.cs)<br>GridFieldGeneratorAdapter, PropPrefab | Startの既存階層登録と条件付きEditor生成 | GridFieldDatabaseAdapter | UnityEditor, UnityEngine |
| [CellDatabaseDefinition](../../Assets/Scripts/Grid/Definitions/CellDatabaseDefinition.cs)<br>CellDatabaseDefinition, CellInfoDefinition | セル設定配列と実行時検索辞書 | CellBaseAdapter, CellTypeDomain | UnityEngine |
| [CameraControllerAdapter](../../Assets/Scripts/Input/Adapter/CameraControllerAdapter.cs)<br>CameraControllerAdapter | Cinemachineの移動・回転・ズーム入力 |  | Unity.Cinemachine, UnityEngine, UnityEngine.InputSystem |
| [PlayerCursorBehaviourAdapter](../../Assets/Scripts/Input/Adapter/PlayerCursorBehaviourAdapter.cs)<br>PlayerCursorBehaviourAdapter | InputAction・UI遮断・Raycast・配置操作 | CellPlacerAdapter, UIRaycasterAdapter | UnityEngine, UnityEngine.InputSystem |
| [ResourceDefinition](../../Assets/Scripts/Resources/Definitions/ResourceDefinition.cs)<br>ResourceDefinition, Resource | 資源名・Prefab・Spriteの設定検索 | ResourceTypeDomain | UnityEngine |
| [ResourceStorageRulesDomain](../../Assets/Scripts/Resources/Domain/ResourceStorageRulesDomain.cs)<br>ResourceStorageRulesDomain, Stock | 容量・種別・予約・数量確定の純粋計算 | ResourceTypeDomain | 標準C# |
| [InlineSOAttributeAdapter](../../Assets/Scripts/Shared/Adapter/InlineSOAttributeAdapter.cs)<br>InlineSOAttributeAdapter | SOインライン編集用属性 |  | UnityEngine |
| [InspectorReadOnlyAttributeAdapter](../../Assets/Scripts/Shared/Adapter/InspectorReadOnlyAttributeAdapter.cs)<br>InspectorReadOnlyAttributeAdapter | 読取専用Inspector表示属性 |  | UnityEngine |
| [SingletonMonoBehaviourAdapter](../../Assets/Scripts/Shared/Adapter/SingletonMonoBehaviourAdapter.cs)<br>SingletonMonoBehaviourAdapter | シーン検索式Singletonと重複排除 |  | UnityEngine |
| [TimeScaleTesterAdapter](../../Assets/Scripts/Shared/Adapter/TimeScaleTesterAdapter.cs)<br>TimeScaleTesterAdapter | ContextMenuからTime.timeScale設定 |  | UnityEngine |
| [ResourceItemAnimationAdapter](../../Assets/Scripts/Transfers/Adapter/ResourceItemAnimationAdapter.cs)<br>ResourceItemAnimationAdapter | 一件のTween・取消Tokenと演出結果 | ResourceAnimationResultApplication | Cysharp.Threading.Tasks, DG.Tweening, UnityEngine |
| [ResourceItemObjectPoolAdapter](../../Assets/Scripts/Transfers/Adapter/ResourceItemObjectPoolAdapter.cs)<br>ResourceItemObjectPoolAdapter, RentedObject | 種別別表示プール・貸出管理・資源データ所有 | IResourceTransferPresentationApplication, ResourceAnimationResultApplication, ResourceDefinition, ResourceItemAnimationAdapter, ResourceTransitStoreApplication, ResourceTypeDomain, SingletonMonoBehaviourAdapter | Cysharp.Threading.Tasks, TMPro, UnityEngine, UnityEngine.Pool |
| [IResourceTransferEndpointApplication](../../Assets/Scripts/Transfers/Application/IResourceTransferEndpointApplication.cs)<br>IResourceTransferEndpointApplication | 寿命中に固定するIdentityとIsAliveの契約 |  | 標準C# |
| [IContainableApplication](../../Assets/Scripts/Transfers/Application/IContainableApplication.cs)<br>IContainableApplication | 全量予約・搬入確定・取消と接点寿命の契約 | IResourceTransferEndpointApplication, ResourceTypeDomain | UnityEngine |
| [IExportableApplication](../../Assets/Scripts/Transfers/Application/IExportableApplication.cs)<br>IExportableApplication | 出庫・位置取得の契約 | ResourceTypeDomain | UnityEngine |
| [IResourceReusableApplication](../../Assets/Scripts/Transfers/Application/IResourceReusableApplication.cs)<br>IResourceReusableApplication | 搬送資源IDの引継ぎ契約 |  | UnityEngine |
| [IResourceTransferPresentationApplication](../../Assets/Scripts/Transfers/Application/IResourceTransferPresentationApplication.cs)<br>ResourceAnimationResultApplication, IResourceTransferPresentationApplication | 演出と表示返却・結果の契約 |  | Cysharp.Threading.Tasks, UnityEngine |
| [ResourceReservationApplication](../../Assets/Scripts/Transfers/Application/ResourceReservationApplication.cs)<br>ResourceReservationApplication, Result | 一度きりの予約確定/取消Token | IContainableApplication, ResourceTypeDomain | UnityEngine |
| [ResourceTransferCoordinatorApplication](../../Assets/Scripts/Transfers/Application/ResourceTransferCoordinatorApplication.cs)<br>ResourceTransferCoordinatorApplication | 予約→演出→確定と再待機の順序 | IContainableApplication, IResourceReusableApplication, IResourceTransferPresentationApplication, ResourceAnimationResultApplication, ResourceReservationApplication, ResourceTransferOperationApplication, ResourceTransitStoreApplication | Cysharp.Threading.Tasks, UnityEngine |
| [ResourceTransferOperationApplication](../../Assets/Scripts/Transfers/Application/ResourceTransferOperationApplication.cs)<br>ResourceTransferOperationApplication, Stage | 搬送一件の資源と進行段階 | IContainableApplication, IResourceTransferEndpointApplication, ResourceTypeDomain | 標準C# |
| [ResourceTransitStoreApplication](../../Assets/Scripts/Transfers/Application/ResourceTransitStoreApplication.cs)<br>ResourceTransitStoreApplication, Data | 表示から独立したID別資源データ | ResourceTypeDomain | 標準C# |
| [CellSelectButtonUIAdapter](../../Assets/Scripts/UI/Adapter/CellSelectButtonUIAdapter.cs)<br>CellSelectButtonUIAdapter | ボタンの表示とonClick登録 |  | TMPro, UnityEngine, UnityEngine.UI |
| [CellStatusProviderAdapter](../../Assets/Scripts/UI/Adapter/CellStatusProviderAdapter.cs)<br>CellStatusProviderAdapter | 具体セルを表示読み取りへ変換する静的生成入口 | CellBaseAdapter, CellStatusDataProviderApplication, CrafterCellAdapter, DirectionsDomain, ExtractorCellAdapter, GaugeElementDataApplication, IUIDataProviderApplication, LabelEnumApplication, ProviderBaseDefinition, ResourceCellAdapter, ResourceProviderDefinition, ResourceTypeDomain, StorageCellAdapter, StorageElementDataApplication, TextElementDataApplication, UIElementDataBaseApplication | 標準C# |
| [CellStatusViewAdapter](../../Assets/Scripts/UI/Adapter/CellStatusViewAdapter.cs)<br>CellStatusViewAdapter, StatusRowInfoAdapter | 選択・購読寿命・行プール・表示窓制御 | CellBaseAdapter, IDataProvidableApplication, SingletonMonoBehaviourAdapter, UIElementDataBaseApplication, UIElementRendererAdapter, UIStatusRowBaseAdapter, UIStatusRowTypeApplication | UnityEngine, UnityEngine.Pool |
| [GaugeUIStatusRowAdapter](../../Assets/Scripts/UI/Adapter/GaugeUIStatusRowAdapter.cs)<br>GaugeUIStatusRowAdapter | ゲージImageと数値表示行 | GaugeElementDataApplication, UIElementDataBaseApplication, UIStatusRowBaseAdapter | TMPro, UnityEngine, UnityEngine.UI |
| [RecipeElementUIAdapter](../../Assets/Scripts/UI/Adapter/RecipeElementUIAdapter.cs)<br>RecipeElementUIAdapter | レシピの時間・素材・出力表示 | RecipeDataDefinition, ResourceDefinition, ResourceRowUIAdapter | TMPro, UnityEngine |
| [RecipeUIBuilderAdapter](../../Assets/Scripts/UI/Adapter/RecipeUIBuilderAdapter.cs)<br>RecipeUIBuilderAdapter | レシピDBから一覧表示生成 | RecipeDatabaseDefinition, RecipeElementUIAdapter | UnityEngine |
| [ResourceRowUIAdapter](../../Assets/Scripts/UI/Adapter/ResourceRowUIAdapter.cs)<br>ResourceRowUIAdapter | 資源名・アイコン・数量の表示 |  | TMPro, UnityEngine, UnityEngine.UI |
| [SelectButtonBuilderAdapter](../../Assets/Scripts/UI/Adapter/SelectButtonBuilderAdapter.cs)<br>SelectButtonBuilderAdapter | セルDBから選択ボタン生成・Action登録 | CellDatabaseDefinition, CellPlacerAdapter, CellSelectButtonUIAdapter, CellTypeDomain, SubMenuUIBuilderAdapter | UnityEngine |
| [StorageUIStatusRowAdapter](../../Assets/Scripts/UI/Adapter/StorageUIStatusRowAdapter.cs)<br>StorageUIStatusRowAdapter | 資源アイコン付き保管表示行 | GaugeUIStatusRowAdapter, ResourceDefinition, ResourceTypeDomain, StorageElementDataApplication, UIElementDataBaseApplication | TMPro, UnityEngine, UnityEngine.UI |
| [SubMenuUIBuilderAdapter](../../Assets/Scripts/UI/Adapter/SubMenuUIBuilderAdapter.cs)<br>SubMenuUIBuilderAdapter | サブメニューの未完成な分岐入口 | CellPlacerAdapter, CellSelectButtonUIAdapter, CellTypeDomain | UnityEngine |
| [TextUIStatusRowAdapter](../../Assets/Scripts/UI/Adapter/TextUIStatusRowAdapter.cs)<br>TextUIStatusRowAdapter | TMPの文字列表示行 | TextElementDataApplication, UIElementDataBaseApplication, UIStatusRowBaseAdapter | TMPro, UnityEngine |
| [UIElementRendererAdapter](../../Assets/Scripts/UI/Adapter/UIElementRendererAdapter.cs)<br>UIElementRendererAdapter | Providerの値を対応する行へ描画 | IUIDataProviderApplication, LabelEnumApplication, UIElementDataBaseApplication, UIStatusRowBaseAdapter | 標準C# |
| [UILookAtCameraAdapter](../../Assets/Scripts/UI/Adapter/UILookAtCameraAdapter.cs)<br>UILookAtCameraAdapter | Camera.mainへのビルボード |  | UnityEngine |
| [UIRaycasterAdapter](../../Assets/Scripts/UI/Adapter/UIRaycasterAdapter.cs)<br>UIRaycasterAdapter | GraphicRaycasterでUI上の入力判定 |  | UnityEngine, UnityEngine.EventSystems, UnityEngine.UI |
| [UIStatusRowBaseAdapter](../../Assets/Scripts/UI/Adapter/UIStatusRowBaseAdapter.cs)<br>UIStatusRowBaseAdapter | 表示行の共通描画契約 | UIElementDataBaseApplication | TMPro, UnityEngine |
| [CellStatusDataProviderApplication](../../Assets/Scripts/UI/Application/CellStatusDataProviderApplication.cs)<br>CellStatusDataProviderApplication | 選択専用のラベルと読み取り関数 | IUIDataProviderApplication, LabelEnumApplication, UIElementDataBaseApplication | 標準C# |
| [IDataProvidableApplication](../../Assets/Scripts/UI/Application/IDataProvidableApplication.cs)<br>IDataProvidableApplication | セルのProvider取得・変更通知契約 | IUIDataProviderApplication | 標準C# |
| [IUIDataProviderApplication](../../Assets/Scripts/UI/Application/IUIDataProviderApplication.cs)<br>IUIDataProviderApplication | ラベル別表示値の読み取り契約 | LabelEnumApplication, UIElementDataBaseApplication | 標準C# |
| [UIElementDataBaseApplication](../../Assets/Scripts/UI/Application/UIElementDataBaseApplication.cs)<br>UIElementDataBaseApplication, TextElementDataApplication, GaugeElementDataApplication, StorageElementDataApplication | テキスト・ゲージ・保管表示値 | ResourceTypeDomain, UIStatusRowTypeApplication | 標準C# |
| [UIEnumsApplication](../../Assets/Scripts/UI/Application/UIEnumsApplication.cs)<br>LabelEnumApplication, UIStatusRowTypeApplication | 表示ラベルと行種別 |  | 標準C# |
| [CrafterProviderDefinition](../../Assets/Scripts/UI/Definitions/CrafterProviderDefinition.cs)<br>CrafterProviderDefinition | 加工表示の設定 | ProviderBaseDefinition | UnityEngine |
| [ExtractorProviderDefinition](../../Assets/Scripts/UI/Definitions/ExtractorProviderDefinition.cs)<br>ExtractorProviderDefinition | 採取表示の設定 | ProviderBaseDefinition | UnityEngine |
| [ProviderBaseDefinition](../../Assets/Scripts/UI/Definitions/ProviderBaseDefinition.cs)<br>ProviderBaseDefinition, LabelNameDefinition | 表示ラベル定義の基底 | LabelEnumApplication | UnityEngine |
| [ResourceProviderDefinition](../../Assets/Scripts/UI/Definitions/ResourceProviderDefinition.cs)<br>ResourceProviderDefinition | 資源セル表示の設定 | ProviderBaseDefinition, ResourceDefinition | UnityEngine |
| [StorageProviderDefinition](../../Assets/Scripts/UI/Definitions/StorageProviderDefinition.cs)<br>StorageProviderDefinition | 保管表示の設定 | ProviderBaseDefinition | UnityEngine |

## 条件付き検証とEditor専用

| ファイル / 宣言型 | 役割 | 主な自作依存 | 外部境界 |
| --- | --- | --- | --- |
| [CraftingVerifierEditor](../../Assets/Scripts/Crafting/Editor/CraftingVerifierEditor.cs)<br>CraftingVerifierEditor | 加工計画・在庫・中断の既存検証 | CrafterCellAdapter, CraftingOperationApplication, DirectionsDomain, GridFieldDatabaseAdapter, IngredientDefinition, RecipeDataDefinition, RecipeDatabaseDefinition, RecipeRulesDomain, ResourceReservationApplication, ResourceTypeDomain | Cysharp.Threading.Tasks, DG.Tweening, UnityEditor, UnityEditor.SceneManagement, UnityEngine |
| [CellDefinitionVerifierAdapter](../../Assets/Scripts/Grid/Adapter/CellDefinitionVerifierAdapter.cs)<br>CellDefinitionVerifierAdapter | Editor/Development限定CLI起動のセル定義・配置・描画検証 | CellBaseAdapter, CellDatabaseDefinition, CellInfoDefinition, CellPlacerAdapter, CellSelectButtonUIAdapter, CellTypeDomain, EmptyCellAdapter, GridFieldDatabaseAdapter, StorageCellAdapter | UnityEngine, UnityEngine.Rendering, UnityEngine.UI |
| [CellConnectionVerifierEditor](../../Assets/Scripts/Grid/Editor/CellConnectionVerifierEditor.cs)<br>CellConnectionVerifierEditor | 隣接接続・通知・境界の既存検証 | CellBaseAdapter, ConnectableCellBaseAdapter, DirectionsDomain, EmptyCellAdapter, GridFieldDatabaseAdapter, StorageCellAdapter | UnityEditor, UnityEditor.SceneManagement, UnityEngine |
| [CellDatabaseEditor](../../Assets/Scripts/Grid/Editor/CellDatabaseEditor.cs)<br>CellDatabaseEditor | セルDB編集Inspector | CellBaseAdapter, CellDatabaseDefinition, CellInfoDefinition, CellTypeDomain | UnityEditor, UnityEngine |
| [GenerateVariantWindowEditor](../../Assets/Scripts/Grid/Editor/GenerateVariantWindowEditor.cs)<br>GenerateVariantWindowEditor | Prefab variant生成支援Window |  | UnityEditor, UnityEngine |
| [GridFieldGeneratorVerifierEditor](../../Assets/Scripts/Grid/Editor/GridFieldGeneratorVerifierEditor.cs)<br>GridFieldGeneratorVerifierEditor | 生成条件と座標の既存検証 | CellDatabaseDefinition, CellInfoDefinition, CellTypeDomain, GridFieldGeneratorAdapter | UnityEditor, UnityEditor.Build.Reporting, UnityEngine |
| [GridGeneratorEditor](../../Assets/Scripts/Grid/Editor/GridGeneratorEditor.cs)<br>GridGeneratorEditor | グリッド生成Inspector | GridFieldDatabaseAdapter, GridFieldGeneratorAdapter | Unity.VisualScripting, UnityEditor, UnityEngine |
| [ResourceIconVerifierEditor](../../Assets/Scripts/Resources/Editor/ResourceIconVerifierEditor.cs)<br>ResourceIconVerifierEditor | 資源アイコン・設定参照の既存検証 | RecipeDatabaseDefinition, RecipeElementUIAdapter, ResourceDefinition, ResourceRowUIAdapter, ResourceTypeDomain, StorageElementDataApplication, StorageUIStatusRowAdapter | Cysharp.Threading.Tasks, TMPro, UnityEditor, UnityEditor.SceneManagement, UnityEngine, UnityEngine.UI |
| [InlineSODrawerEditor](../../Assets/Scripts/Shared/Editor/InlineSODrawerEditor.cs)<br>InlineSODrawerEditor | SO属性のDrawer | InlineSOAttributeAdapter | UnityEditor, UnityEngine |
| [InspectorReadOnlyDrawerEditor](../../Assets/Scripts/Shared/Editor/InspectorReadOnlyDrawerEditor.cs)<br>InspectorReadOnlyDrawerEditor | 読取専用属性のDrawer | InspectorReadOnlyAttributeAdapter | UnityEditor, UnityEngine |
| [ResourceReservationVerifierEditor](../../Assets/Scripts/Transfers/Editor/ResourceReservationVerifierEditor.cs)<br>ResourceReservationVerifierEditor | 数量・予約・取消の既存検証 | CellBaseAdapter, ConveyorCellAdapter, CrafterCellAdapter, CrossingCellAdapter, IContainableApplication, ResourceReservationApplication, ResourceStorageRulesDomain, ResourceTransferOperationApplication, ResourceTypeDomain, StorageCellAdapter | UnityEditor, UnityEngine |
| [ResourceTransferVerifierEditor](../../Assets/Scripts/Transfers/Editor/ResourceTransferVerifierEditor.cs)<br>ResourceTransferVerifierEditor | 非セル接点・搬送・接続喪失・ID寿命の検証 | CellBaseAdapter, ConnectableCellBaseAdapter, ConveyorCellAdapter, CrossingCellAdapter, ExportConveyorCellAdapter, IContainableApplication, IResourceReusableApplication, ResourceReservationVerifierEditor, ResourceTransferCoordinatorApplication, ResourceAnimationResultApplication, ResourceDefinition, ResourceItemObjectPoolAdapter, ResourceReservationApplication, ResourceTransferOperationApplication, ResourceTypeDomain, StorageCellAdapter | Cysharp.Threading.Tasks, DG.Tweening, UnityEditor, UnityEditor.SceneManagement, UnityEngine |
| [UIStatusVerifierEditor](../../Assets/Scripts/UI/Editor/UIStatusVerifierEditor.cs)<br>UIStatusVerifierEditor | 選択通知・読み取り・行表示の既存検証 | CellBaseAdapter, CellStatusViewAdapter, CrafterCellAdapter, CrafterProviderDefinition, DirectionsDomain, ExtractorCellAdapter, ExtractorProviderDefinition, GaugeElementDataApplication, GaugeUIStatusRowAdapter, GridFieldDatabaseAdapter, IDataProvidableApplication, IngredientDefinition, LabelEnumApplication, LabelNameDefinition, ProviderBaseDefinition, RecipeDataDefinition, RecipeDatabaseDefinition, ResourceCellAdapter, ResourceDefinition, ResourceProviderDefinition, ResourceTypeDomain, StatusRowInfoAdapter, StorageCellAdapter, StorageProviderDefinition, TextUIStatusRowAdapter, UIStatusRowBaseAdapter, UIStatusRowTypeApplication | Cysharp.Threading.Tasks, TMPro, UnityEditor, UnityEditor.SceneManagement, UnityEngine, UnityEngine.UI |

## 未採用の作業差分

| ファイル / 宣言型 | 役割 | 主な自作依存 | 外部境界 |
| --- | --- | --- | --- |
| [ServiceLocatorAdapter](../../Assets/Scripts/Shared/Adapter/ServiceLocatorAdapter.cs)<br>ServiceLocatorAdapter | 未コミット・未使用の型別サービス登録候補 |  | UnityEngine |

## Editor限定の接続検証用セル

| ファイル / 宣言型 | 役割 | 主な自作依存 | 外部境界 |
| --- | --- | --- | --- |
| [UIStatusFixtureCellAdapter](../../Assets/Scripts/UI/Adapter/UIStatusFixtureCellAdapter.cs)<br>UIStatusFixtureCellAdapter, UIStatusFixtureReaderAdapter | 中央の型分岐にないセルの専用読み取り・通知・在庫不変を確認する例 | CellBaseAdapter, IDataProvidableApplication, CellStatusProviderAdapter, ProviderBaseDefinition, UIElementDataBaseApplication | UNITY_EDITOR限定。Editorフォルダーの型はGameObjectへ追加できないためAdapter側へ配置 |

## 境界と補足

EditorフォルダーのVerifierは既存のEditorメソッドをCLIから呼ぶ検証入口で、独立したゲーム実行サービスではない。Test Runnerの外部パッケージ導入と自作Verifierの存在は区別する。CellDefinitionVerifierAdapterは `UNITY_EDITOR || DEVELOPMENT_BUILD` とコマンドラインflagで有効化する検証コードで、Releaseの通常ゲーム動作には含まれない。GridFieldGeneratorAdapterは同一コンポーネント中の生成処理だけUNITY_EDITOR限定。InlineSO/ReadOnly属性はランタイム側宣言、対応DrawerはEditor側である。ServiceLocatorは参照元がなく、Singletonを置き換えた設計として扱わない。

`.meta`、ScriptableObject `.asset`、外部ライブラリはクラスファイル数に含めない。SO・Prefabの参照は [実際のUnity配線](current-architecture.md#実際のunity配線)を参照する。

## ECO-01の追加スクリプト（2026-10-21）

[通貨取引の仕様と検証](../spec/currency-wallet.md)を参照する。上記の既存一覧に次の6ファイルを追加した。

| ファイル | 責務・依存 |
| --- | --- |
| [CurrencyRulesDomain](../../Assets/Scripts/Economy/Domain/CurrencyRulesDomain.cs) | Unity非依存の価格×数量と上限判定 |
| [CurrencyPriceCatalogDomain](../../Assets/Scripts/Economy/Domain/CurrencyPriceCatalogDomain.cs) | 売価・設備価格の検証と固定。CurrencyRulesDomain、ResourceTypeDomain、CellTypeDomainを使う |
| [CurrencyWalletApplication](../../Assets/Scripts/Economy/Application/CurrencyWalletApplication.cs) | 残高と予約合計、スナップショット通知。CurrencyTransactionApplicationを生成する |
| [CurrencyTransactionApplication](../../Assets/Scripts/Economy/Application/CurrencyTransactionApplication.cs) | 一件の予約額と終端。所有するCurrencyWalletApplicationを更新する |
| [CurrencyEconomyDefinition](../../Assets/Scripts/Economy/Definitions/CurrencyEconomyDefinition.cs) | ScriptableObject設定をCurrencyPriceCatalogDomainへ複製する |
| [CurrencyWalletVerifierEditor](../../Assets/Scripts/Economy/Editor/CurrencyWalletVerifierEditor.cs) | 通貨境界・再入と既存ResourceReservationVerifierEditorの回帰検証 |
