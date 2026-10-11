# セルと配置のクラス関係

タグ: `現行設計` `クラス図` `セル` `配置` `Unity配線`

対象版・凡例は [現行設計索引](current-architecture.md)。ここでは管理対象状態と借用参照を区別する。GameObjectの寿命はUnity、配置時の生成・破棄はCellPlacerが制御する。

## セル型と能力契約

<!-- diagram-id: architecture-cells-grid-1 -->
```mermaid
classDiagram
 MonoBehaviour <|-- CellBaseAdapter
 CellBaseAdapter <|-- EmptyCellAdapter
 CellBaseAdapter <|-- ResourceCellAdapter
 CellBaseAdapter <|-- ConnectableCellBaseAdapter
 ConnectableCellBaseAdapter <|-- StorageCellAdapter
 ConnectableCellBaseAdapter <|-- ExtractorCellAdapter
 ConnectableCellBaseAdapter <|-- ConveyorCellAdapter
 ConveyorCellAdapter <|-- ExportConveyorCellAdapter
 ConnectableCellBaseAdapter <|-- CrossingCellAdapter
 ConnectableCellBaseAdapter <|-- CrafterCellAdapter
 class CellBaseAdapter {
  int XIndex
  int ZIndex
  GameObject CellModel
  InitializeSystem()
 }
 class ConnectableCellBaseAdapter {
  CellBaseAdapter[] AdjacentCells
  InitializeSystem()
  OnDisconnect()
 }
 class IContainableApplication {
  <<interface>>
  AllocateStorage()
  StoreResource()
  CancelStorage()
 }
 class IExportableApplication {
  <<interface>>
  TryExport()
 }
 class IResourceReusableApplication {
  <<interface>>
  Reuse()
 }
 class IDataProvidableApplication {
  <<interface>>
  GetDataProvider()
  StatusChanged
 }
 StorageCellAdapter ..|> IContainableApplication
 StorageCellAdapter ..|> IExportableApplication
 StorageCellAdapter ..|> IDataProvidableApplication
 ExtractorCellAdapter ..|> IExportableApplication
 ExtractorCellAdapter ..|> IDataProvidableApplication
 ResourceCellAdapter ..|> IDataProvidableApplication
 ConveyorCellAdapter ..|> IContainableApplication
 ConveyorCellAdapter ..|> IResourceReusableApplication
 CrossingCellAdapter ..|> IContainableApplication
 CrossingCellAdapter ..|> IResourceReusableApplication
 CrafterCellAdapter ..|> IContainableApplication
 CrafterCellAdapter ..|> IExportableApplication
 CrafterCellAdapter ..|> IDataProvidableApplication
 ConnectableCellBaseAdapter --> CellBaseAdapter : 隣接4方向を借用
 ConnectableCellBaseAdapter ..> GridFieldDatabaseAdapter : 座標検索
 StorageCellAdapter ..> ResourceStorageRulesDomain : 数量判定
 ExtractorCellAdapter --> ResourceCellAdapter : 採取元の初期参照
```

[Cellsコード](../../Assets/Scripts/Cells/Adapter/) と [CrafterCell](../../Assets/Scripts/Crafting/Adapter/CrafterCellAdapter.cs) が根拠。ResourceCellは固定資源種別と表示定義、Storageは在庫・予約量、Extractorは採取量・非同期処理、Conveyorは一件の搬送資源ID、Crossingは方向別保留搬送、Crafterは入出庫と加工を担う。ResourceCellは接続型ではなく、隣接に保持されても相互接続通知しない。接続変更はOnGetConnectedCell/OnLostConnectedCell、切断はOnDisconnectedで通知する。Extractorの資源元選択は初期化時で、再接続・無効化の挙動改善案は現行図に含めない。

## グリッド管理と定義

<!-- diagram-id: architecture-cells-grid-2 -->
```mermaid
classDiagram
 MonoBehaviour <|-- CellPlacerAdapter
 MonoBehaviour <|-- GridFieldGeneratorAdapter
 SingletonMonoBehaviourAdapter <|-- GridFieldDatabaseAdapter
 ScriptableObject <|-- CellDatabaseDefinition
 CellDatabaseDefinition *-- CellInfoDefinition : シリアライズ配列と実行時検索辞書
 CellInfoDefinition --> CellBaseAdapter : fieldCellPrefab
 CellInfoDefinition --> GameObject : placeholderCellPrefab
 CellPlacerAdapter --> CellDatabaseDefinition : cellDatabaseArr
 CellPlacerAdapter --> GridFieldDatabaseAdapter : 選択範囲検索とSaveCell
 CellPlacerAdapter --> CellStatusViewAdapter : 配置結果と選択UI
 CellPlacerAdapter --> CellBaseAdapter : 選択セルの借用
 CellPlacerAdapter --> GameObject : 生成したキャッシュとプレビューの寿命管理
 GridFieldDatabaseAdapter --> CellBaseAdapter : 座標配列の借用参照
 GridFieldGeneratorAdapter --> GridFieldDatabaseAdapter : Startで既存階層登録
 class CellDatabaseDefinition {
  OnEnable()
  OnValidate()
  SetCellInfos()
  GetAllCellInfos()
  TryGetCellInfo()
 }
 class CellPlacerAdapter {
  SetSelectedCellType()
  SelectGrid()
  PointerBegin()
  PointerDrag()
  PointerEnd()
 }
```

[CellDatabaseDefinition](../../Assets/Scripts/Grid/Definitions/CellDatabaseDefinition.cs) は設定配列を正本としOnEnableで辞書を必ず作る。SetCellInfos/OnValidateで無効化し、取得時に必要なら再構築する。旧保存フラグは実行時状態に使わない。[GridFieldGenerator](../../Assets/Scripts/Grid/Adapter/GridFieldGeneratorAdapter.cs) の生成関数はUNITY_EDITOR限定、Startは [GridDB](../../Assets/Scripts/Grid/Adapter/GridFieldDatabaseAdapter.cs) に既存子階層を登録する。GridDBは座標を持つがセルを自動生成・破棄しない。[Placer](../../Assets/Scripts/Grid/Adapter/CellPlacerAdapter.cs) は選択した定義の非アクティブなセル複製をキャッシュし、配置時に再複製、旧セル切断、座標登録、有効化、InitializeSystemを行う。
