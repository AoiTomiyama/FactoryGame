# 追加実装前の拡張性レビューと再開情報

タグ: `設計レビュー` `追加実装` `拡張性` `再開情報` `初期化` `セル接続` `UI`

現在の状態（2026-10-10）: R1は修正・回帰・生成プレイヤー検証完了。R2〜R4は未着手。現行の初期化契約は [セル定義の契約](../spec/cell-definitions.md)、実施証拠は末尾の「R1実装・検証」を参照する。次の候補はR2であり、新しい着手指示まで待機する。

以下の初回レビュー・準備照合は2026-10-06〜07時点の履歴。初回対象版は `8326a8d`、当時の許可は記録のみで、R1の実装は2026-10-10の個別許可で実施した。

準備照合日: 2026-10-07。照合時 HEAD: `5fffd44`、ブランチ: `master`。現在の依頼も文書更新までであり、新機能や R1〜R4 の修正は許可されていない。準備結果は末尾の「追加実装の準備照合」を参照する。

## 判断と確認範囲

資源・レシピ・セルを追加する土台はできている。全面的な設計改修を続ける必要はなく、セル定義の初期化問題を優先して解決し、その後は追加機能に必要な境界から整えることを推奨する。

資源、加工、搬送、セル配置・接続、状態 UI のコードを静的に確認した。不具合候補の実行再現、Unity 検証、生成プレイヤーの実行はこのレビューでは行っていない。過去のビルド成功は、今回指摘した起動時の動作や寿命の正しさを保証しない。過去の検証証拠は [UI の契約](../spec/cell-status-ui.md) と [既存設計タスク](architecture-extension.md) を参照する。

## 指摘と修正候補

### R1：高 — セル定義の保存済み初期化フラグと実行時辞書が不整合

- 状態: [x] 2026-10-10に修正・検証済み。以下の問題経路は修正前の根拠。現行コードは保存フラグを使わず、更新時に辞書を無効化する。
- 根拠: [CellDatabaseDefinition](../../Assets/Scripts/Grid/Definitions/CellDatabaseDefinition.cs) の `isInitialized` はシリアライズされるが、`_infoLookup` は実行時の辞書。`OnEnable` はフラグが true なら再構築を省く。`TryGetCellInfo` と `GetAllCellInfos` も同じフラグに依存する。[GeneralCellDB.asset](../../Assets/Scripts/ScriptableObject/GeneralCellDB.asset) は `isInitialized: 1`。
- 発生条件・影響: 保存済みフラグが true の定義を新しい実行環境で読み込むと、空の辞書を初期化済みとして扱い、セル取得や配置ボタンの生成が機能しない可能性がある。Editor の `OnValidate` などによって見え方が異なり得るため、プレイヤー起動時の実行確認が必要。
- 方針: 初期化状態を実行時だけで管理し、ロード時に辞書を構築する。設定変更後の再構築経路も確認する。保存済み Prefab・セル種別の参照は維持する。
- 完了条件: 保存済み定義をロードして全登録セルを取得できる。定義の更新後に古い辞書が残らない。配置 UI が登録内容を表示し、プレイヤー起動でもセル選択・配置ができる。既存の Editor 検証入口を再利用または拡張し、起動確認とビルド成功を区別して記録する。

### R2：中 — 抽出セルの対象変更・無効化に対する寿命管理が不足

- 状態: [ ] 未修正。有限資源・稼働切替を追加する前に対応を検討する。
- 根拠: [ExtractorCellAdapter](../../Assets/Scripts/Cells/Adapter/ExtractorCellAdapter.cs) は `InitializeSystem` で対象を一度取得するが、生産ループで対象を再確認しない。停止は `OnDestroy` にあり、`OnDisable` と接続変更通知への対応がない。
- 発生条件・影響: 生産開始後の対象削除・交換や設備無効化で、生産が続く可能性がある。開始時に対象がなく、その後対象を配置する経路も追随できるか確認が必要。
- 方針: 開始・停止・再開と対象の有効条件を明示し、接続の再評価を実装する。資源セルは非接続型でもあるため、既存の接続通知だけで対象削除を検知できると仮定しない。
- 完了条件: 対象の削除・交換・後置き、設備無効化・再有効化で仕様通りに生産が止まり再開する。中断時に資源を増やさず、処理が重複起動しない。抽出量・容量・進捗表示を維持する。

### R3：中 — 新しいセルの状態表示に中央の型分岐の変更が必要

- 状態: [ ] 未対応の拡張上の制約。新しい設備の実装時に適用を検討する。
- 根拠: [CellStatusProviderAdapter](../../Assets/Scripts/UI/Adapter/CellStatusProviderAdapter.cs) の `Read` は既存4種類の具体型とラベルを列挙し、未対応の組合せで例外を投げる。
- 影響: 新しいセルがこのファクトリを使う場合、セル側だけでなく中央の分岐も修正する必要がある。H の設定・取得・描画の分離は完了しているが、取得処理の追加方法には制約が残る。
- 方針: 新しいセルの専用状態取得 Adapter から、既存の `IUIDataProviderApplication` と描画処理へ接続する。既存4種類の全面移行や汎用登録機構は、新しい機能の要件を確認してから判断する。
- 完了条件: 新しいセルが独自の表示値を提供でき、既存セルの中央分岐を変更せず表示を追加できる。表示処理が数量を変更せず、購読解除と選択切替が維持される。

### R4：中 — 搬送先インターフェースに具体的な基底クラスの制約が残る

- 状態: [ ] 未対応の拡張上の制約。現在のセル構造を使う追加実装を一律に妨げるものではない。
- 根拠: [ResourceReservationApplication](../../Assets/Scripts/Transfers/Application/ResourceReservationApplication.cs) の `TryCreate` は `IContainableApplication` の実装に加え、`CellBaseAdapter` の継承も要求する。[ResourceTransferCoordinatorApplication](../../Assets/Scripts/Transfers/Application/ResourceTransferCoordinatorApplication.cs) と [ResourceTransferOperationApplication](../../Assets/Scripts/Transfers/Application/ResourceTransferOperationApplication.cs) も具体的なセル型を保持する。
- 影響: 別オブジェクトの倉庫や独立した入出力コンポーネントへ、インターフェース実装だけで搬送を再利用できない。
- 方針: そのような機能を追加する際に、操作・識別・生存確認の契約を切り出す。Unity の破棄済みオブジェクト判定を単純な参照 null 判定へ置き換えない。
- 完了条件: `CellBaseAdapter` を継承しない搬送先でも予約・確定・取消を検証できる。既存の成功・接続解除・削除・再接続・数量・ID・表示返却の動作を維持する。

## 再利用できる範囲

| 追加内容 | 現状と条件 |
| --- | --- |
| 資源 | 列挙型・定義・表示アセットを登録して既存処理を利用できる |
| レシピ | 定義の追加で既存の判定・消費処理を利用できる。計算は Unity 非依存 |
| 保管・容量 | 共通の数量・全量予約ルールを利用できる |
| 新しい設備 | 配置・接続・搬送・表示の土台を利用できる。設備固有の動作と上記制約を確認する |
| 複数フィールド・保存復元 | Singleton 依存と状態の所有・復元手順に追加検討が必要。今回その実装計画は採用していない |

## 既存タスクとの関係

既存タスク1〜5と A〜H の完了条件は維持する。R1・R2 は今回見つかった別の初期化・寿命の問題。R3 は H の取得境界を新しいセルへ拡張する際の制約、R4 は既存の設計方針に記載済みの具体型依存と同じ課題であり、別の重複タスクとして計上しない。Assembly Definition の導入（I）は今回も見送りのまま。

## 再開手順

1. `AGENTS.md` と Git 運用規則を読み、ブランチ・作業ツリー・対象コードがレビュー版 `8326a8d` からどう変わったか確認する。
2. R1の完了証拠と現行コードを照合し、未着手のR2〜R4から次の依頼に関係する範囲を選ぶ。次の候補はR2だが、追加の着手指示まで実装を始めない。
3. R1を変更する場合は、末尾の検証入口でロード・設定更新・選択配置の回帰を確認する。
4. 実装、関連文書、必要な検証を揃えて目的単位でコミットする。結果と次の候補を報告し、次タスクの許可を待つ。

記録時の既存差分は `SampleSceneProfile.asset`、未追跡の `ServiceLocatorAdapter` と `.meta`、IDE の4ファイル。保留理由と次の確認は [UI の契約の差分レビュー](../spec/cell-status-ui.md#着手前の差分レビュー) を参照する。今回のレビューと記録ではこれらを変更しない。実装差分はなく、レビュー記録と既存タスクリストへの参照だけを追加した。

## 追加実装の準備照合（2026-10-07）

### 準備済み範囲と阻害要因

- `git diff 8326a8d HEAD -- Assets/Scripts Assets/Scenes ProjectSettings Packages` は差分なし。R1〜R4 の該当コードと保存済みフラグを再確認し、いずれも未対応である。新しい実行再現は行っていない。
- R1 は配置・選択を使う新機能の動作確認を妨げる可能性があり、先に解決することを推奨する。`SetCellInfos` も配列の置換だけで辞書を無効化しないため、初期ロードだけでなく設定更新後の取得も修正・検証対象に含める。現在の Editor 自動登録は直後に `ValidateAndBuildLookup` を呼ぶので、この API 単独の問題を隠し得る。
- R2 は抽出・有限資源・稼働切替の追加時に優先する。R3 は新しい設備の状態表示を専用 Adapter で追加する判断材料。R4 はセル基底型から独立した搬送先が必要になったときに適用する。R2〜R4 の全面修正を、新機能すべての開始条件にはしない。
- 見送り済みの Assembly Definition、複数フィールド、保存復元は新たに採用していない。機能仕様が未指定のため、個別の設計・実装順を確定できない。

### 追加時に変更する場所

| 追加対象 | 主な変更先と維持する境界 |
| --- | --- |
| 資源 | [CellEnumsDomain](../../Assets/Scripts/Cells/Domain/CellEnumsDomain.cs) の資源種別、[ResourceDefinition](../../Assets/Scripts/Resources/Definitions/ResourceDefinition.cs)、`ResourceDB.asset`、Prefab・アイコン。既存 enum の数値を変える挿入・並べ替えを避け、資源登録と表示参照を確認する |
| レシピ | [RecipeDatabaseDefinition](../../Assets/Scripts/Crafting/Definitions/RecipeDatabaseDefinition.cs) と `Recipes/RecipeDB.asset`。既存仕様のレシピは定義を追加する。新しい判定条件は `Crafting/Domain`、一件の状態遷移は `Crafting/Application` に置く |
| セル・設備 | `Cells/Adapter` または新しい機能の `Adapter`、セル種別、Field/Placeholder Prefab、`GeneralCellDB.asset`。数量計算は `Resources/Domain` を再利用。接続・回転・初期化・削除の契約を確認する |
| 配置・選択 | [CellPlacerAdapter](../../Assets/Scripts/Grid/Adapter/CellPlacerAdapter.cs) と [SelectButtonBuilderAdapter](../../Assets/Scripts/UI/Adapter/SelectButtonBuilderAdapter.cs)。登録済み定義から選択・生成する既存経路を使う。費用・配置制約が必要なら、そのゲームルールの所有者を仕様で決める |
| 状態表示 | `IDataProvidableApplication`、`IUIDataProviderApplication`、セル専用の取得 Adapter と表示設定。既存の `CellStatusDataProviderApplication`・描画・行プールを利用し、設定アセットへ実行中の状態を保存しない |
| 搬送・入出力 | `IContainableApplication` / `IExportableApplication`、予約・進行管理、セル Adapter。全量予約と確定・取消の一回性を維持。独立した搬送先が必要なら R4 の契約変更を先に設計する |

配置は [スクリプト配置規則](../design/script-organization.md)、責務と依存方向は [設計方針](../design/architecture-guidelines.md) を踏襲する。必要な機能だけにフォルダ・型を追加し、既存アセットの GUID とシリアライズ参照を維持する。

### 検証証拠の有効範囲

2026-10-06 の既存記録に加え、準備時に次のローカルログの成功マーカーを再確認した。コードの差分はないが、過去の検証対象外の機能へ結果を拡張しない。

| 既存ログ（`%TEMP%` 内） | 再確認した証拠・制限 |
| --- | --- |
| `FactoryGame-H-ui-final.log` | UI、予約、加工ルールの成功。最終 H 差分の表示セッション・切替・無効化・再有効化・削除・数量・進捗を検証。抽出の生産ループはテストから直接呼び出しており、R2 の接続変更・通常初期化の検証ではない |
| `FactoryGame-H-ResourceTransferVerifierEditor.Run.log` | 搬送成功、中断・削除・再接続、表示復元、予約取消、ID 解放の成功。H 最終の表示無効化修正前の差分を対象とする |
| `FactoryGame-H-UIStatusVerifierEditor.RunAssetsAndBuild.log` | 保存済み UI 設定と Prefab の成功、プレイヤービルド成功、正常なバッチ終了。生成プレイヤーの起動・セル定義のランタイム辞書を検証した証拠ではない |

詳細な終了コード・時間・既存警告は [H の検証記録](../spec/cell-status-ui.md#検証証拠2026-10-06) に集約する。今回の文書準備では Unity を再起動せず、実測検証時間・再実行回数を新しい動作保証として加算しない。

### 再利用する検証入口と不足分

Unity は `ProjectSettings/ProjectVersion.txt` の 6000.3.8f1 を使う。下表の入口は現行 Editor コードに存在することを確認済み。実装時に変更に関係する項目を選び、成功済み検証は追加変更・失敗・未解決の懸念がある範囲だけ再実行する。

| 対象 | 既存入口 |
| --- | --- |
| 資源・表示アセット | `ResourceIconVerifierEditor.Run`（Play Mode） |
| 数量・予約 | `ResourceReservationVerifierEditor.Run`（同期 Editor 検証） |
| レシピ計算・加工寿命 | `CraftingVerifierEditor.RunRules` / `RunPlay` |
| 接続・回転・グリッド端 | `CellConnectionVerifierEditor.Run` |
| 搬送・取消・削除・表示寿命 | `ResourceTransferVerifierEditor.Run`（Play Mode） |
| 状態 UI | `UIStatusVerifierEditor.Run`（Play Mode、予約・加工ルールも同じ起動で実行） |
| 生成・ビルド | `GridFieldGeneratorVerifierEditor.RunGeneration` / `RunPlayerBuild`。`UIStatusVerifierEditor.RunAssetsAndBuild` は表示アセット確認と既存ビルド入口をまとめて実行 |

バッチ実行の基本引数は `-batchmode -nographics -projectPath <project> -executeMethod <entry> -logFile <log>`。同期入口では `-quit` を付ける。Play Mode 入口は再生開始後に検証して自身で終了するため `-quit` を付けない。同じプロジェクトで Unity を同時起動しない。

R1 の保存済みフラグからの起動・設定更新と、R2 の対象削除・交換・無効化に専用の既存検証はない。修正許可後に既存検証基盤へ追加する。R1 の完了にはプレイヤー起動時の取得と選択・配置の確認も必要で、ビルド成功だけで代用しない。見た目や操作感を変える機能では小さな代表場面の描画確認も別に行う。

### ユーザーから必要な仕様と次の操作

新機能の依頼時には、少なくとも次を確定する。

- 対象と目的：何を追加し、プレイヤーがどの操作で使い、何を達成するか。
- 入出力と状態：扱う資源・数量・容量・時間・方向、状態の所有者、既存施設との接続方法。
- 中断と制約：削除・無効化・接続変更時の扱い、配置条件や費用、有限資源・保存復元が必要か。
- 完了条件：正常操作、境界条件、表示内容、代表的な利用場面。必要な画像・Prefabなどの素材の有無。

次の推奨操作は R1 一件の修正許可。新機能の仕様が先に届いた場合は、関連する R2〜R4 と上記変更先を照合して実装案を作る。各タスクの完了報告・コミット後、次タスクの修正許可まで待機する。

準備時に既存差分も再確認した。`SampleSceneProfile.asset` は MotionBlur の有効化と override 無効の filter 追加で描画の採用判断が未確認。`ServiceLocatorAdapter` は登録・解除・取得・クリアの実装のみで利用箇所なし。IDE の4ファイルは Copilot 移行状態 `COMPLETED` のみ。いずれも破棄・変更せず、今回の文書コミットへ含めない。

## R1実装・検証（2026-10-10）

タグ: `R1` `現行仕様` `設計判断` `検証証拠` `再開情報`

対象差分: 親HEAD `caa645eabe06f9fd17897e59f477dc7651a22921` から、`CellDatabaseDefinition.cs`、`GridFieldGeneratorVerifierEditor.cs`、`UIStatusVerifierEditor.cs`、新規 `CellDefinitionVerifierAdapter.cs` と `.meta`、セル定義の契約と関連タスクリスト。これらをR1独立コミットにまとめる。製品のUI・Prefab・シーン・設定アセットは変更していない。

実装: 初期化フラグは非シリアライズ。OnEnableで必ず構築、OnValidate/SetCellInfosでクリアして未構築、取得時に必要な再構築を行う。null/空定義、重複時の最後の登録、警告、公開APIと参照を維持。理由と検証入口は [現行契約](../spec/cell-definitions.md)へ集約した。

| 実行 | 条件・結果・実測時間 |
| --- | --- |
| 旧版再現 | Unity 6000.3.8f1、`GridFieldGeneratorVerifierEditor.RunCellDefinitions`、`-batchmode -nographics -quit`。空辞書＋旧フラグtrueからのOnEnableで取得件数が復元せず、意図したアサーション失敗・終了1、36.27秒。旧版のビルドは実施していない |
| 修正後の初回回帰・ビルド | `RunCellDefinitionBuild`、同じバッチ引数。R1回帰、既存グリッド生成、UI定義/Prefab確認、MainScene Windows64 Developmentビルドが成功・終了0、70.84秒 |
| 初回プレイヤー | Hidden起動、`--verify-cell-definitions`。保存定義、StorageButtonイベント、選択、placeholder、PointerBegin/Drag/End、配置後グリッド/選択参照は成功・終了0、7.84秒。ただし画面キャプチャは黒一色で描画証拠には不採用 |
| 描画検証修正後の最終回帰・ビルド | キャプチャを同じCameraのオフスクリーン描画と非単色判定へ変更したため再実行。`RunCellDefinitionBuild`が全検証・Developmentビルド成功、終了0、34.04秒 |
| 最終プレイヤー | Hidden、1280×720、D3D11 GPU、同じ検証引数。Storageを(0,0)に配置し、登録定義の参照、実Buttonコールバック、選択・placeholder、配置と選択追随を確認。オフスクリーンPNGはゲームフィールドの描画を確認、終了0、7.57秒 |

ログ・画像はローカル `%TEMP%` の `FactoryGame-R1-before.log`、`FactoryGame-R1-after.log`、`FactoryGame-R1-player.log`、`FactoryGame-R1-render-build.log`、`FactoryGame-R1-player-render.log`、`FactoryGame-R1-player-render.png`。生成プレイヤーは `%TEMP%/FactoryGameTask3Build/FactoryGame.exe`。Player引数は `-windowed -screen-width 1280 -screen-height 720 --verify-cell-definitions --cell-verification-image=<PNG絶対パス> -logFile <ログ>`。アセットを再保存して旧フラグを取り除く操作はしておらず、`GeneralCellDB.asset` の旧フラグtrueを維持したまま検証した。

検証範囲の限界: GUI操作は禁止のため物理クリック位置・ポインタ入力配送、主観的操作感の確認は行っていない。自動検証は実際のButtonイベントと配置操作メソッドを通し、生成プレイヤーで選択・配置が成立することを確認した。画像はゲームフィールドの描画証拠で、重ねたUIの見た目全体を保証しない。今回見た目の変更はない。

警告: 既存GridFieldGeneratorAdapterのEditor専用フィールド5件にCS0414、Cinemachineのpartialクラス通知、asmref対象欠落通知、ライセンスAccess token更新エラー（ライセンス検証・実行は成功）がログにある。R1の重複検証によるEmpty種別警告は意図したもの。R1コードの新規コンパイル警告・実行例外はなし。

保全: 既存SampleSceneProfile.asset、未追跡ServiceLocatorAdapterと.meta、IDE XML4件の計7ファイルは作業前後のSHA256一致。Unityのビルドが生成したPC_RPAsset、URP GlobalSettings、ProjectSettingsの差分だけを戻し、既存差分はコミットに含めない。既存GUID変更・アセット移動・一括再保存なし。`git diff --check`成功。

運用評価: Editor起動3回（旧版再現1、修正後2）、生成プレイヤー起動2回。検証実測計156.56秒。成功後の再検証は、初回画像が黒で未解決の描画確認が残り検証コードを修正した1回（Editor/Player各1回）。ゲーム仕様の取り違えによる手戻り0回。初回キャプチャの検証方式の手戻り1回。次はR2の対象変更・無効化の候補を報告し、新しい着手指示まで待機する。
