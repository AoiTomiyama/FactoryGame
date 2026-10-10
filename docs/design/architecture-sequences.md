# 起動・配置・搬送・加工・選択のシーケンス

タグ: `現行設計` `シーケンス` `初期化` `配置` `搬送` `加工` `UI`

対象版・凡例は [現行設計索引](current-architecture.md)。実線は呼出、破線は値/通知。Unityが異なるコンポーネントへ行うStartの総順序は固定されていないため、起動図のparは独立経路であり、行順による実行順保証を意味しない。

## 起動と定義の読み取り

```mermaid
sequenceDiagram
 participant U as Unity
 participant D as CellDatabaseDefinition
 participant G as GridFieldGeneratorAdapter
 participant F as GridFieldDatabaseAdapter
 participant P as CellPlacerAdapter
 participant B as SelectButtonBuilderAdapter
 U->>D: OnEnable
 D->>D: 設定配列から実行時辞書を構築
 par グリッド登録
  U->>G: Start
  G->>F: InitializeCells(gridSize)
  F->>F: 既存子階層からセルと座標を登録
 and 配置用キャッシュ
  U->>P: Start
  P->>D: TryGetCellInfo(Empty)
  D-->>P: field / placeholder Prefab
  P->>P: 複製してセルキャッシュを無効化
 and 選択ボタン
  U->>B: Start
  B->>D: GetAllCellInfos
  D-->>B: 辞書Values由来の定義一覧（重複は後勝ち）
  B->>B: None以外のButtonを生成してAction登録
 end
 Note over D: SetCellInfos / OnValidateで無効化、取得時に必要なら再構築
```

根拠: [CellDB](../../Assets/Scripts/Grid/Definitions/CellDatabaseDefinition.cs)、[Generator](../../Assets/Scripts/Grid/Adapter/GridFieldGeneratorAdapter.cs)、[Builder](../../Assets/Scripts/UI/Adapter/SelectButtonBuilderAdapter.cs)。起動時のグリッド登録は全セルのInitializeSystem呼出ではない。ゲーム開始前の生成はEditor操作の別経路。

## ボタン選択からドラッグ配置

```mermaid
sequenceDiagram
 participant B as Button / CellSelectButtonUIAdapter
 participant P as CellPlacerAdapter
 participant D as CellDatabaseDefinition
 participant I as PlayerCursorBehaviourAdapter
 participant F as GridFieldDatabaseAdapter
 participant O as 旧セル
 participant N as 新セル
 participant V as CellStatusViewAdapter
 B->>P: SetSelectedCellType(type)
 P->>D: TryGetCellInfo(type)
 D-->>P: Prefab参照
 P->>P: 古いプレビュー/キャッシュ破棄、複製
 I->>P: SelectGrid(ray hit)
 I->>P: PointerBegin
 loop 左ボタン保持中
  I->>P: PointerDrag
  P->>F: GetCell(x,z)
  P->>P: 配置可能な範囲とプレビューを更新
 end
 I->>P: PointerEnd
 loop 選択範囲の有効プレビュー
  P->>O: 接続型ならOnDisconnect
  P->>O: Destroy(gameObject)
  P->>N: キャッシュをInstantiate
  P->>F: SaveCell(x,z,newCell)
  P->>N: SetActive(true) / InitializeSystem
  N->>F: 隣接座標を検索し相互参照更新・通知
  P->>V: 状態表示またはEmptyなら閉じる
 end
```

根拠: [Cursor](../../Assets/Scripts/Input/Adapter/PlayerCursorBehaviourAdapter.cs)、[Placer](../../Assets/Scripts/Grid/Adapter/CellPlacerAdapter.cs)、[Connectable](../../Assets/Scripts/Cells/Adapter/ConnectableCellBaseAdapter.cs)。空セルへの配置またはEmptyによる削除だけを許す。UnityのDestroyは破棄予約だが、切断と配列差替えを先に処理する。

## 搬送の予約・演出・確定

```mermaid
sequenceDiagram
 participant S as Conveyor / Crossing
 participant C as ResourceTransferCoordinator
 participant T as IContainable搬送先
 participant R as ResourceReservation
 participant P as IResourceTransferPresentation
 participant D as ResourceTransitStore
 S->>C: RunAsync(resources,presentation,target取得)
 C->>T: AllocateStorage(type,amount,dir)
 T-->>C: 全量予約の可否
 C->>R: 予約Tokenを保持
 C->>P: Transfer(token,from,to,id)
 P-->>C: Completed / Cancelled / MissingVisual
 alt 正常終了かつ搬送先とデータ整合
  opt 搬送先がIResourceReusable
   C->>T: Reuse(dir,id)
  end
  C->>R: TryCommit
  R->>T: StoreResource(type,amount,dir)
  C->>C: OperationをCompletedにする
  opt ID再利用しない終端
   C->>D: Remove(id)
   C->>P: ReleaseVisual(id)
  end
 else 接続喪失や演出中断
  C->>R: TryCancel
  R->>T: 生存中ならCancelStorage
  C->>C: 演出結果失敗またはCrossingの喪失はWaitingへ、ID保持
  C->>P: 表示位置を戻す
 end
 C-->>S: 結果または取消例外
 Note over S,C: Conveyor接続喪失はCTS取消→試行Operation取消、同じIDで新Operation
```

根拠: [Coordinator](../../Assets/Scripts/Transfers/Application/ResourceTransferCoordinatorApplication.cs)、[Reservation](../../Assets/Scripts/Transfers/Application/ResourceReservationApplication.cs)。target取得とisCurrentTargetデリゲートで接続を再確認し、予約失敗は非同期待機する。Crossingは上流の予約時に下流も予約するため図の最初の予約を既に保持した経路を持つ。ExportConveyorの取り込みはTryExport→CreateIdFromResourceData→後方から自セルへの演出→基底の前方搬送という追加経路。所有セル終了時の最終ID処分は上図の待機復帰とは異なる。

## 加工の開始と一度きり確定

```mermaid
sequenceDiagram
 participant C as CrafterCellAdapter
 participant R as RecipeRulesDomain
 participant O as CraftingOperationApplication
 participant T as UniTask / DOTween
 C->>R: TryCreatePlan(入力スナップショット,recipe)
 R-->>C: RecipePlan
 C->>O: 固定計画でOperation生成
 C->>T: 時間進行とキャンセルToken
 alt 正常完了
  C->>O: TryComplete(現在の入力/出力/容量)
  O->>R: TryApply(plan,current)
  R-->>O: 素材消費と出力のCommitまたは失敗
  O-->>C: 一度だけ確定可能な結果
  C->>C: 成功時だけApplyCommit、途中通知なし
 else 無効化・破棄・中断
  C->>O: TryCancel
 end
 C->>C: finallyでTween/Operation掃除
 C-->>C: StatusChangedを通知
```

根拠: [Crafter](../../Assets/Scripts/Crafting/Adapter/CrafterCellAdapter.cs)、[CraftingOperation](../../Assets/Scripts/Crafting/Application/CraftingOperationApplication.cs)。無効化はCTSを取消し未完了の素材を消費しない。正常終了でも現在在庫や容量が不一致なら確定しない。

## 選択UIの読み取りと購読寿命

```mermaid
sequenceDiagram
 participant P as CellPlacerAdapter
 participant V as CellStatusViewAdapter
 participant C as IDataProvidableセル
 participant A as CellStatusProviderAdapter
 participant D as CellStatusDataProviderApplication
 participant R as UIElementRendererAdapter
 participant W as UIStatusRowBaseAdapter
 P->>V: UpdateUIStatusWindow(cell)
 V->>V: 前の購読を解除して行返却
 V->>C: GetDataProvider
 C->>A: Create(cell,definition)
 A-->>V: 選択セル専用Provider
 V->>C: StatusChangedへUpdateUIを登録
 V->>R: InitUI(provider,CreateStatusRow)
 R->>D: ReadData
 D-->>R: ラベル別の表示値
 R->>V: CreateStatusRow(表示値)
 V->>W: プール取得してRenderUIByData
 C-->>V: StatusChanged
 V->>R: UpdateUI
 R->>D: ReadData
 R->>W: 最新値で描画
 Note over V,C: 閉じる・選択変更・OnDisable・OnDestroyで解除
 V->>C: StatusChangedの登録解除
 V->>V: 行を返却しRendererをReset
```

根拠: [View](../../Assets/Scripts/UI/Adapter/CellStatusViewAdapter.cs)、[Renderer](../../Assets/Scripts/UI/Adapter/UIElementRendererAdapter.cs)。同じ選択セルではProvider作成・行借用を繰り返さず更新する。Provider非対応セルは閉じる。ResourceCellの購読アクセサはno-opで初回読み取りだけで固定値を表示する。
