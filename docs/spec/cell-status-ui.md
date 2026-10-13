# セル状態 UI の取得・通知・描画契約

タグ: `現行仕様` `UI` `状態表示` `読み取り境界` `タスクH` `R3`

更新日: 2026-10-13。既存4種類の状態表示と、セル専用Adapterによる表示拡張を対象とする。

## 責務と依存

| 分類 | 担当 |
| --- | --- |
| Definitions | `ProviderBaseDefinition` と各表示設定。ラベル、表示順、資源定義へのアセット参照を保持する。選択セルや表示中の数量は保持しない |
| Application | `IDataProvidableApplication` の変更通知、`IUIDataProviderApplication.ReadData` の取得契約、`CellStatusDataProviderApplication` の表示セッション。セルや Unity の具体型を参照しない |
| Adapter | `CellStatusProviderAdapter` がセルの読み取り専用プロパティから表示値を作り、`UIElementRendererAdapter` が描画する。`CellStatusViewAdapter` が選択と購読、行の貸出・返却を管理する |

同じ設定アセットを使うセルでも、取得処理は表示セッションごとに作る。ラベルは作成時にコピーし、各 `ReadData` は新しい表示データと読み取り専用の辞書を返す。表示データの編集や再取得で資源量・予約量は変わらない。設定の編集を表示へ反映する場合は選択し直す。

## セル専用の読み取り処理（R3）

追加セルは `IDataProvidableApplication.GetDataProvider` から `CellStatusProviderAdapter.CreateCustom(definition, read)` を呼び、セル専用Adapterの読み取り関数を渡す。既存4種類は従来の `Create(cell, definition)` と具体型分岐を使い続ける。新設備のゲーム仕様、既存セルの全面移行、型の自動登録は追加していない。

`CreateCustom` は設定のラベルをコピーし、既存の `CellStatusDataProviderApplication` を返す。セル名を含む設定済みラベルの変換は専用関数が担当し、中央の `Read` の分岐を変更する必要はない。定義がnullなら従来どおりProviderを返さず、定義があるのに関数がnullなら `ArgumentNullException` とする。新しいメソッド名を使い、既存 `Create(cell, null)` の呼出しを曖昧にしない。

読み取り関数は、呼出しごとに新しい表示データを返し、予約・搬入・搬出などの数量変更を行わない。通知はセル側の `StatusChanged` から既存Viewへ接続する。表示値の取得自体が副作用を持たないことは専用Adapterの責務であり、任意の関数の副作用をAPIが抑止するわけではない。使用できるラベルと表示行は既存の列挙値・Text/Gauge/Storageの範囲とする。

実装は [Provider接続](../../Assets/Scripts/UI/Adapter/CellStatusProviderAdapter.cs)、接続例は [検証専用セルとReader](../../Assets/Scripts/UI/Adapter/UIStatusFixtureCellAdapter.cs)。この例は `UNITY_EDITOR` 限定で、設備Prefabや製品ビルドへ追加しない。検証入口は [UIStatusVerifierEditor.VerifyCustomStatus](../../Assets/Scripts/UI/Editor/UIStatusVerifierEditor.cs)。

## 更新と寿命

- 保管の予約・確定・取消・搬出、加工入力と成果物の更新、抽出と加工の進捗は `StatusChanged` を通知する。通知を受けた選択中の表示が最新値を取得する。セルは UI の Singleton を呼ばない。
- 取得処理はセルの数量変更 API を呼ばない。抽出・加工の確定後は反映済みの数量を表示する。
- 選択を切り替える際は前の購読を解除し、表示行を返却して、新しいセッションへ接続する。
- 非表示・表示コンポーネントの無効化・破棄でも購読を解除する。無効化ではパネルと選択マーカーも閉じ、再有効化後は選択し直せる。選択セルが削除された場合は `LateUpdate` で表示を閉じる。非表示後の再選択は新しいセッションとして扱う。
- 行のラベル・表示順・資源名・アイコン・数量表記・残り秒数と選択操作を維持する。抽出中は毎フレームの演出更新から進捗も通知する。
- 進捗は 0〜1 の比率で扱い、加工待機中など時間がゼロの場合は 0 とする。容量ゼロの行もゼロ除算せず空のゲージとして描画する。
- 設定の型名、シリアライズする `labelEntries` / `resourceDatabase`、既存 `.meta` GUID とアセット参照を維持する。

## 検証証拠（2026-10-06）

対象コード: `a869ffb` からのタスク H 差分（UI/Application・Adapter・Definitions、状態を提供する4種類のセル、UI 検証コード）。Unity 6000.3.8f1、バッチ・描画なし、既存の未コミット変更を保持した環境で実行した。

| 実行入口 | 結果・範囲 | 実測時間 |
| --- | --- | --- |
| `UIStatusVerifierEditor.Run` | 終了コード 0。共有設定のセッション独立、スナップショット、数量の不変性、予約・確定・取消・搬出、切替・非表示・削除、資源名、抽出・加工の進捗と完了、待機中ゲージ。既存の予約検証と加工計算729通りも同じ起動内で成功 | 21.08 秒 |
| `ResourceTransferVerifierEditor.Run` | 終了コード 0。搬送成功・中断、表示復元、接続・削除・予約取消・ID 解放 | 34.03 秒 |
| `UIStatusVerifierEditor.RunAssetsAndBuild` | 終了コード 0。保存済み設定のラベルとセルプレハブの表示参照。既存ビルド入口を再利用し、MainScene を含む Windows 64-bit ビルド成功 | 31.54 秒 |
| `UIStatusVerifierEditor.Run`（最終差分） | 終了コード 0。初回と同じ項目に加え、表示コンポーネントの無効化時にパネル・購読・表示行が解放され、再有効化後に再選択できることを確認 | 21.59 秒 |

タスク H の検証は4起動、計108.23秒。成功済み検証の再実行1回。最終レビューで無効化時に空のパネルが残る経路を修正したため、関連する UI 検証を再実行した。搬送検証とビルドはこの最終寿命修正より前の差分で成功しており、最終差分のコンパイルと Play Mode 動作は最後の UI 検証で確認した。仕様の取り違えによる手戻り0件。新しい運用方針の評価1件目として記録する。実際の文字・画像参照・ゲージ値を確認したが、手操作による画面確認と生成プレイヤーの実行は行っていない。

ビルドには既存の `GridFieldGeneratorAdapter` の未使用フィールド5種類に関する CS0414 警告がある。今回変更したコードの新規コンパイル警告・エラーは検出されなかった。

## 着手前の差分レビュー

- 資源定義 API・設定と利用側: `0d4ae86`。資源アイコンの既存 Play Mode 検証が終了コード0（18.43秒）。取得 API とフィールド名の整合を確認した。
- 貸出管理の内部名称: `5f09402`。名称の置換であり処理順・数量操作は同一。前タスク F と今回の搬送検証で作業内容の動作を確認した。
- StatsPanel の初期非表示: `a869ffb`。既存 Start の非表示処理と整合し、今回の MainScene ビルドも成功した。
- `SampleSceneProfile.asset` の MotionBlur 有効化は意図・描画結果を確認できていないため保留。採用判断には代表場面での描画比較を行う。
- 未追跡 `ServiceLocatorAdapter` は利用箇所がなく、登録解除と破棄されたオブジェクトの扱いが未定。利用契約と寿命の検証を揃えてから採用判断する。
- `.idea` の4ファイルは Copilot 移行完了のローカル状態で、ゲームの成果物ではないため未追跡のまま保持する。

次の実装は別途ユーザーの許可を得てから開始する。

## R3の検証証拠（2026-10-13）

対象は `686ae6b22911e80d6592897af251e66f17712e5f` からのR3差分。Unity 6000.3.8f1の `UIStatusVerifierEditor.Run` を `-batchmode -nographics` で実行した。

- 初回: 終了コード1、50.57秒。Editorフォルダー内の検証用MonoBehaviourをGameObjectへ追加できず失敗した。検証用セルを `UNITY_EDITOR` 限定のAdapterへ配置し直した。
- 修正後: 終了コード0、80.16秒。中央の型分岐にない検証用セルの専用Readerから文字・ゲージを描画し、通知による再取得、共有設定でもセッション・旧スナップショットの独立、設定ラベルのコピー、表示取得とスナップショット変更による在庫非変更を確認した。切替・非表示・無効化・再有効化・削除で購読と行を解放し、null定義・null関数の契約も確認した。
- 同じ起動で既存4種類のUI、R2の抽出寿命、予約・加工ルールの回帰が成功した。新規コンパイルエラー・警告は検出されなかった。既存TMPフォントの日本語文字不足による置換警告は残る。

実行ログはローカル一時領域の `FactoryGame-R3-ui.log` と `FactoryGame-R3-ui-final.log`。実測合計130.73秒、失敗修正後の再検証1回、成功後の反復0回。仕様の取り違えによる手戻り0件。検証用セルの配置修正1件。UIの表示データ・実行時行・購読を確認したが、描画なしのため画面の見え方と手操作は評価していない。Prefab・SO・シーンを変更しておらず、製品ビルドは今回実行していない。

R3は完了。新設備の実装と既存4種類の全面移行は別の依頼で扱う。R4には着手していない。
