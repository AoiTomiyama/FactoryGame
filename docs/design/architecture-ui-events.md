# 状態UIとスクリプト相関

タグ: `現行設計` `UI` `イベント購読` `データ授受` `依存`

対象版・凡例は [現行設計索引](current-architecture.md)。スクリプト全件は [対応表](script-catalog.md)。同名ノードは対応するC#型で、機能をまとめたノードはラベルで区別する。

## 表示セッションと値のクラス関係

<!-- diagram-id: architecture-ui-events-1 -->
```mermaid
classDiagram
 class IDataProvidableApplication {
  <<interface>>
  GetDataProvider()
  StatusChanged
 }
 class IUIDataProviderApplication {
  <<interface>>
  ReadData()
 }
 CellStatusDataProviderApplication ..|> IUIDataProviderApplication
 SingletonMonoBehaviourAdapter <|-- CellStatusViewAdapter
 CellStatusViewAdapter --> IDataProvidableApplication : 選択セルを購読
 CellStatusViewAdapter *-- UIElementRendererAdapter : 表示セッション
 CellStatusViewAdapter *-- ObjectPool : 行種別別
 CellStatusViewAdapter --> UIStatusRowBaseAdapter : 借用行を返却管理
 UIElementRendererAdapter --> IUIDataProviderApplication : 読み取り
 UIElementRendererAdapter --> UIStatusRowBaseAdapter : ラベルと行の対応参照
 CellStatusProviderAdapter ..> CellStatusDataProviderApplication : Create
 CellStatusProviderAdapter ..> ProviderBaseDefinition : ラベル設定
 CellStatusProviderAdapter ..> CellBaseAdapter : 具体セルswitch
 CellStatusDataProviderApplication --> UIElementDataBaseApplication : ReadDataで新しい値を返す
 ScriptableObject <|-- ProviderBaseDefinition
 ProviderBaseDefinition <|-- ResourceProviderDefinition
 ProviderBaseDefinition <|-- StorageProviderDefinition
 ProviderBaseDefinition <|-- ExtractorProviderDefinition
 ProviderBaseDefinition <|-- CrafterProviderDefinition
 UIElementDataBaseApplication <|-- TextElementDataApplication
 UIElementDataBaseApplication <|-- GaugeElementDataApplication
 GaugeElementDataApplication <|-- StorageElementDataApplication
 MonoBehaviour <|-- UIStatusRowBaseAdapter
 UIStatusRowBaseAdapter <|-- TextUIStatusRowAdapter
 UIStatusRowBaseAdapter <|-- GaugeUIStatusRowAdapter
 GaugeUIStatusRowAdapter <|-- StorageUIStatusRowAdapter
```

[UI契約](../spec/cell-status-ui.md)、[Provider変換](../../Assets/Scripts/UI/Adapter/CellStatusProviderAdapter.cs)、[View](../../Assets/Scripts/UI/Adapter/CellStatusViewAdapter.cs)、[Renderer](../../Assets/Scripts/UI/Adapter/UIElementRendererAdapter.cs) が根拠。ProviderDefinitionはラベル設定で、選択セルの参照を保存しない。選択ごとにProviderが作られ、読むたび新しい表示値を返す。表示値はpublicフィールドを持つスナップショットで、不変オブジェクトとは断定しない。Storage行はResourceDefinitionからアイコンを読む。Viewの選択解除・閉じる・OnDisable・OnDestroy時に購読解除と行返却、LateUpdateで破棄セルの選択解除を行う。

## 入力・イベント・データの相関

<!-- diagram-id: architecture-ui-events-2 -->
```mermaid
flowchart LR
 Actions["InputAction"] -. "performed / started / canceled" .-> Cursor["PlayerCursorBehaviourAdapter"]
 Cursor --> Ray["UIRaycaster + Physics.Raycast"]
 Cursor --> Placer["CellPlacerAdapter"]
 Button["CellSelectButtonUIAdapter / Button.onClick"] -. "登録Action実行" .-> Placer
 Builder["SelectButtonBuilderAdapter"] --> DB["CellDatabaseDefinition"]
 Builder --> Button
 Placer --> Grid["GridFieldDatabaseAdapter"]
 Placer --> Cells["CellBase派生 / InitializeSystem / OnDisconnect"]
 Cells -. "接続/喪失/切断通知" .-> Loops["Conveyor・ExportConveyor・Crossingの処理"]
 Loops --> Coord["ResourceTransferCoordinator"]
 Coord --> Target["IContainable / IResourceReusable 搬送先"]
 Coord --> Pool["TransitStoreと演出Pool"]
 Cells -. "StatusChanged" .-> View["CellStatusViewAdapter 選択中のみ購読"]
 Placer --> View
 View --> Renderer["UIElementRendererAdapter"]
 Renderer --> Provider["CellStatusDataProviderApplication.ReadData"]
 Provider --> Bridge["CellStatusProviderAdapter.Read"]
 Bridge --> Cells
 Bridge --> Definition["ProviderBaseDefinition ラベル"]
 Provider -. "ラベル→表示値スナップショット" .-> Renderer
 Renderer --> Rows["Text / Gauge / Storage行"]
 Rows --> UnityUI["TMP / Image"]
 CameraInput["カメラInputAction"] -. "購読とポーリング" .-> Camera["CameraControllerAdapter / Cinemachine"]
```

点線は左から右へのイベント／値の授受、実線は利用側→依存先。InputActionはOnEnableで有効化・購読、OnDisableで解除。左ボタン開始→PointerBegin、保持中Update→PointerDrag、終了→PointerEnd、右操作→TransferDataToUI。回転操作はPlacerの向きを変える。選択ボタンはBuilderがDBから生成し通常ボタンにSetSelectedCellTypeを渡す。SubMenuUIBuilderは未完成の分岐を含み、完全なサブメニュー機構として図に展開しない。接続イベントは隣接参照を更新してから通知し、UIのStatusChangedとは別系統である。
