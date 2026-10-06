# スクリプト配置・命名規則

タグ: `現行仕様` `ファイル配置` `命名` `レイヤー`

採用日: 2026-10-06。機能を先に分け、機能内の責務をレイヤーで区分する。

## 分類と接尾辞

| 配置 | 型・ファイルの接尾辞 | 責務 |
| --- | --- | --- |
| `<機能>/Domain/` | `Domain` | Unity 非依存のルール、数量計算、ゲーム上の種別 |
| `<機能>/Application/` | `Application` | 処理の進行、実行時状態、利用側の契約、表示用データ |
| `<機能>/Adapter/` | `Adapter` | シーン、入力、UI、アニメーション、プールとの接続 |
| `<機能>/Definitions/` | `Definition` | ScriptableObject と編集可能な設定型 |
| `<機能>/Editor/` | `Editor` | 編集支援と Editor 検証 |

## 追加実装の判断

- 機能は `Cells`、`Resources`、`Transfers`、`Grid`、`Crafting`、`Input`、`UI`、`Shared`。機能が増えた場合に必要な領域を追加する。
- 名前は `<対象><役割><所属レイヤー>`。例: `ResourceStorageRulesDomain`、`ResourceTransferCoordinatorApplication`、`StorageCellAdapter`。インターフェイスにも同じ接尾辞を使う。
- MonoBehaviour を継承しなくても Unity の演出や表示に接続する型は Adapter。Unity 非依存でも搬送中の状態を管理する型は Application。
- 契約は利用する処理の側へ置く。現在の搬送契約には Unity の座標型が残るが、Application の進行管理が利用する契約として扱う。
- Definition と Editor は実行時の三層とは別の分類。Editor フォルダは Unity のコンパイル区分にも必要。属性クラスは明示的な全型名（例: `[InspectorReadOnlyAttributeAdapter]`）を使う。
- 同じファイルにある関連列挙型・データ型にも同じ分類の接尾辞を使う。入れ子型は所有型の補助として扱い、Stock、Data などに重ねて接尾辞を付けない。
- 役割別の追加階層や空のレイヤーフォルダを作らない。名前空間・Assembly Definition の導入は今回の整理に含まない。
- 既存の設定アセットは `Assets/Scripts/ScriptableObject/` のパスを保持する。スクリプトと .meta は移動し、GUID とシリアライズするフィールド名を維持する。
- 整理は責務の分類を表す。加工判定と UI の実装分離はタスク F・H で行い、配置変更だけで移行完了にしない。

## 移動・改名対応表

| 旧パス | 現在のパス |
| --- | --- |
| `Assets/Scripts/Attributes/InlineSOAttribute.cs` | `Assets/Scripts/Shared/Adapter/InlineSOAttributeAdapter.cs` |
| `Assets/Scripts/Attributes/InspectorReadOnlyAttribute.cs` | `Assets/Scripts/Shared/Adapter/InspectorReadOnlyAttributeAdapter.cs` |
| `Assets/Scripts/CameraController.cs` | `Assets/Scripts/Input/Adapter/CameraControllerAdapter.cs` |
| `Assets/Scripts/CellPlacer.cs` | `Assets/Scripts/Grid/Adapter/CellPlacerAdapter.cs` |
| `Assets/Scripts/Cells/CellBase.cs` | `Assets/Scripts/Cells/Adapter/CellBaseAdapter.cs` |
| `Assets/Scripts/Cells/CellEnums.cs` | `Assets/Scripts/Cells/Domain/CellEnumsDomain.cs` |
| `Assets/Scripts/Cells/ConnectableCellBase.cs` | `Assets/Scripts/Cells/Adapter/ConnectableCellBaseAdapter.cs` |
| `Assets/Scripts/Cells/ConveyorCell.cs` | `Assets/Scripts/Cells/Adapter/ConveyorCellAdapter.cs` |
| `Assets/Scripts/Cells/CrafterCell.cs` | `Assets/Scripts/Cells/Adapter/CrafterCellAdapter.cs` |
| `Assets/Scripts/Cells/CrossingCell.cs` | `Assets/Scripts/Cells/Adapter/CrossingCellAdapter.cs` |
| `Assets/Scripts/Cells/EmptyCell.cs` | `Assets/Scripts/Cells/Adapter/EmptyCellAdapter.cs` |
| `Assets/Scripts/Cells/ExportConveyorCell.cs` | `Assets/Scripts/Cells/Adapter/ExportConveyorCellAdapter.cs` |
| `Assets/Scripts/Cells/ExtractorCell.cs` | `Assets/Scripts/Cells/Adapter/ExtractorCellAdapter.cs` |
| `Assets/Scripts/Cells/PlaceholderCell.cs` | `Assets/Scripts/Cells/Adapter/PlaceholderCellAdapter.cs` |
| `Assets/Scripts/Cells/ResourceCell.cs` | `Assets/Scripts/Cells/Adapter/ResourceCellAdapter.cs` |
| `Assets/Scripts/Cells/ResourceReservation.cs` | `Assets/Scripts/Transfers/Application/ResourceReservationApplication.cs` |
| `Assets/Scripts/Cells/ResourceStorageRules.cs` | `Assets/Scripts/Resources/Domain/ResourceStorageRulesDomain.cs` |
| `Assets/Scripts/Cells/ResourceTransferCoordinator.cs` | `Assets/Scripts/Transfers/Application/ResourceTransferCoordinatorApplication.cs` |
| `Assets/Scripts/Cells/ResourceTransferOperation.cs` | `Assets/Scripts/Transfers/Application/ResourceTransferOperationApplication.cs` |
| `Assets/Scripts/Cells/ResourceTransitStore.cs` | `Assets/Scripts/Transfers/Application/ResourceTransitStoreApplication.cs` |
| `Assets/Scripts/Cells/StorageCell.cs` | `Assets/Scripts/Cells/Adapter/StorageCellAdapter.cs` |
| `Assets/Scripts/Editor/CellConnectionVerifier.cs` | `Assets/Scripts/Grid/Editor/CellConnectionVerifierEditor.cs` |
| `Assets/Scripts/Editor/CellDatabaseEditor.cs` | `Assets/Scripts/Grid/Editor/CellDatabaseEditor.cs` |
| `Assets/Scripts/Editor/GenerateVariantWindow.cs` | `Assets/Scripts/Grid/Editor/GenerateVariantWindowEditor.cs` |
| `Assets/Scripts/Editor/GridFieldGeneratorVerifier.cs` | `Assets/Scripts/Grid/Editor/GridFieldGeneratorVerifierEditor.cs` |
| `Assets/Scripts/Editor/GridGeneratorEditor.cs` | `Assets/Scripts/Grid/Editor/GridGeneratorEditor.cs` |
| `Assets/Scripts/Editor/InlineSODrawer.cs` | `Assets/Scripts/Shared/Editor/InlineSODrawerEditor.cs` |
| `Assets/Scripts/Editor/InspectorReadOnlyDrawer.cs` | `Assets/Scripts/Shared/Editor/InspectorReadOnlyDrawerEditor.cs` |
| `Assets/Scripts/Editor/ResourceIconVerifier.cs` | `Assets/Scripts/Resources/Editor/ResourceIconVerifierEditor.cs` |
| `Assets/Scripts/Editor/ResourceReservationVerifier.cs` | `Assets/Scripts/Transfers/Editor/ResourceReservationVerifierEditor.cs` |
| `Assets/Scripts/Editor/ResourceTransferVerifier.cs` | `Assets/Scripts/Transfers/Editor/ResourceTransferVerifierEditor.cs` |
| `Assets/Scripts/Extentions/CalculationExtensions.cs` | `Assets/Scripts/Grid/Adapter/CalculationExtensionsAdapter.cs` |
| `Assets/Scripts/GridFieldGenerator.cs` | `Assets/Scripts/Grid/Adapter/GridFieldGeneratorAdapter.cs` |
| `Assets/Scripts/Interface/IContainable.cs` | `Assets/Scripts/Transfers/Application/IContainableApplication.cs` |
| `Assets/Scripts/Interface/IDataProvidable.cs` | `Assets/Scripts/UI/Application/IDataProvidableApplication.cs` |
| `Assets/Scripts/Interface/IExportable.cs` | `Assets/Scripts/Transfers/Application/IExportableApplication.cs` |
| `Assets/Scripts/Interface/IResourceReusable.cs` | `Assets/Scripts/Transfers/Application/IResourceReusableApplication.cs` |
| `Assets/Scripts/Interface/IResourceTransferPresentation.cs` | `Assets/Scripts/Transfers/Application/IResourceTransferPresentationApplication.cs` |
| `Assets/Scripts/Interface/IUIDataProvider.cs` | `Assets/Scripts/UI/Application/IUIDataProviderApplication.cs` |
| `Assets/Scripts/Manager/GridFieldDatabase.cs` | `Assets/Scripts/Grid/Adapter/GridFieldDatabaseAdapter.cs` |
| `Assets/Scripts/Manager/ResourceItemAnimation.cs` | `Assets/Scripts/Transfers/Adapter/ResourceItemAnimationAdapter.cs` |
| `Assets/Scripts/Manager/ResourceItemObjectPool.cs` | `Assets/Scripts/Transfers/Adapter/ResourceItemObjectPoolAdapter.cs` |
| `Assets/Scripts/Manager/ServiceLocator.cs` | `Assets/Scripts/Shared/Adapter/ServiceLocatorAdapter.cs` |
| `Assets/Scripts/Manager/SingletonMonoBehaviour.cs` | `Assets/Scripts/Shared/Adapter/SingletonMonoBehaviourAdapter.cs` |
| `Assets/Scripts/Manager/TimeScaleTester.cs` | `Assets/Scripts/Shared/Adapter/TimeScaleTesterAdapter.cs` |
| `Assets/Scripts/PlayerCursorBehaviour.cs` | `Assets/Scripts/Input/Adapter/PlayerCursorBehaviourAdapter.cs` |
| `Assets/Scripts/ScriptableObject/CellDatabaseSO.cs` | `Assets/Scripts/Grid/Definitions/CellDatabaseDefinition.cs` |
| `Assets/Scripts/ScriptableObject/Recipes/RecipeDatabaseSO.cs` | `Assets/Scripts/Crafting/Definitions/RecipeDatabaseDefinition.cs` |
| `Assets/Scripts/ScriptableObject/ResourceSO.cs` | `Assets/Scripts/Resources/Definitions/ResourceDefinition.cs` |
| `Assets/Scripts/ScriptableObject/UIProvider/CrafterProvider.cs` | `Assets/Scripts/UI/Definitions/CrafterProviderDefinition.cs` |
| `Assets/Scripts/ScriptableObject/UIProvider/ExtractorProvider.cs` | `Assets/Scripts/UI/Definitions/ExtractorProviderDefinition.cs` |
| `Assets/Scripts/ScriptableObject/UIProvider/ProviderBase.cs` | `Assets/Scripts/UI/Definitions/ProviderBaseDefinition.cs` |
| `Assets/Scripts/ScriptableObject/UIProvider/ResourceProvider.cs` | `Assets/Scripts/UI/Definitions/ResourceProviderDefinition.cs` |
| `Assets/Scripts/ScriptableObject/UIProvider/StorageProvider.cs` | `Assets/Scripts/UI/Definitions/StorageProviderDefinition.cs` |
| `Assets/Scripts/UI/CellSelectButtonUI.cs` | `Assets/Scripts/UI/Adapter/CellSelectButtonUIAdapter.cs` |
| `Assets/Scripts/UI/CellStatus/CellStatusView.cs` | `Assets/Scripts/UI/Adapter/CellStatusViewAdapter.cs` |
| `Assets/Scripts/UI/CellStatus/GaugeUIStatusRow.cs` | `Assets/Scripts/UI/Adapter/GaugeUIStatusRowAdapter.cs` |
| `Assets/Scripts/UI/CellStatus/StorageUIStatusRow.cs` | `Assets/Scripts/UI/Adapter/StorageUIStatusRowAdapter.cs` |
| `Assets/Scripts/UI/CellStatus/TextUIStatusRow.cs` | `Assets/Scripts/UI/Adapter/TextUIStatusRowAdapter.cs` |
| `Assets/Scripts/UI/CellStatus/UIElementDataBase.cs` | `Assets/Scripts/UI/Application/UIElementDataBaseApplication.cs` |
| `Assets/Scripts/UI/CellStatus/UIElementRenderer.cs` | `Assets/Scripts/UI/Adapter/UIElementRendererAdapter.cs` |
| `Assets/Scripts/UI/CellStatus/UIStatusRowBase.cs` | `Assets/Scripts/UI/Adapter/UIStatusRowBaseAdapter.cs` |
| `Assets/Scripts/UI/RecipeElementUI.cs` | `Assets/Scripts/UI/Adapter/RecipeElementUIAdapter.cs` |
| `Assets/Scripts/UI/RecipeUIBuilder.cs` | `Assets/Scripts/UI/Adapter/RecipeUIBuilderAdapter.cs` |
| `Assets/Scripts/UI/ResourceRowUI.cs` | `Assets/Scripts/UI/Adapter/ResourceRowUIAdapter.cs` |
| `Assets/Scripts/UI/SelectButtonBuilder.cs` | `Assets/Scripts/UI/Adapter/SelectButtonBuilderAdapter.cs` |
| `Assets/Scripts/UI/SubMenuUIBuilder.cs` | `Assets/Scripts/UI/Adapter/SubMenuUIBuilderAdapter.cs` |
| `Assets/Scripts/UI/UIEnums.cs` | `Assets/Scripts/UI/Application/UIEnumsApplication.cs` |
| `Assets/Scripts/UI/UILookAtCamera.cs` | `Assets/Scripts/UI/Adapter/UILookAtCameraAdapter.cs` |
| `Assets/Scripts/UI/UIRaycaster.cs` | `Assets/Scripts/UI/Adapter/UIRaycasterAdapter.cs` |

## 検証記録

- 70 本のスクリプトについて、移動前の作業内容へ名称置換を適用した結果と移動後の内容が一致することを確認した。追跡済み 69 本の .meta GUID はすべて保持され、全ファイルの接尾辞が配置分類と一致した。Domain の UnityEngine 依存はない。
- Assets 内の GUID 重複とシーン・プレハブ・設定アセットのスクリプト参照先、現行文書の C# パスを静的に確認した。
- Unity 6000.3.8f1 付属 Roslyn と既存 Editor 用コンパイル設定で、作業ツリーおよび既存変更を除いたコミット予定内容の Assembly-CSharp / Assembly-CSharp-Editor がすべて終了コード 0。出力先は一時フォルダとし、Library の生成物を編集していない。
- Editor の `ResourceReservationVerifierEditor.Run` はライセンスクライアントの IPC 接続拒否・初期化タイムアウトで実行に到達せず、起動したプロセスを停止した（終了コード -1）。この初回には Editor 検証を完了できなかった。下記の再検証で解消した。
- 過去のタスク文書の検証証拠は実行当時の型名を保持する。再実行時は対応表の現在の Editor 型名を使う。
- 整理開始前から未追跡だった ServiceLocator は Shared/Adapter に移動し、未追跡のまま保持する。

## 再検証結果（2026-10-06）

権限を変更した環境でライセンス接続と Git メタデータへの書き込みが可能になり、前回の停止要因は解消した。Unity 6000.3.8f1 の Editor に再インポート・コンパイルさせ、以下すべての終了コード 0 と成功ログを確認した。

| 実行入口 | 確認した範囲 |
| --- | --- |
| `ResourceReservationVerifierEditor.Run` | 数量ルール、搬送状態、予約 ID、保管・加工・交差・コンベア |
| `CellConnectionVerifierEditor.Run` | 4方向の接続、相互通知、削除、再接続、回転、境界 |
| `GridFieldGeneratorVerifierEditor.RunGeneration` | セル生成、プレハブ参照、グリッドライン、消去 |
| `ResourceTransferVerifierEditor.Run` | Play Mode の正常搬送、演出結果、表示欠落・復元・再利用、切断・削除・再接続、予約取消、ID 解放 |
| `ResourceIconVerifierEditor.Run` | Play Mode のレシピ・保管表示に対する資源アイコンと資源名の設定 |
| `GridFieldGeneratorVerifierEditor.RunPlayerBuild` | MainScene を含む Windows 64-bit プレイヤービルド |

検証は既存の未コミット変更を保持した作業ツリーで実施した。コミット予定のスクリプトは移動前 HEAD の内容へ名称置換を適用したものと照合し、GUID 維持を再確認した。新規フォルダと改名箇所の行末空白を整理し、ステージ済み差分の空白検査も通過した。手操作による画面の目視確認と生成したプレイヤーの実行はこの検証に含めない。

## 再開情報

- 次の実装はタスク F。次タスクの許可を得てから開始する。
- 整理のコミット対象には、既存の資源定義 API 変更、シーンの有効状態変更、資源設定アセット、SampleSceneProfile、IDE ファイル、未追跡の ServiceLocator を含めない。これらは作業ツリーに保持する。
