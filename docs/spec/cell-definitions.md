# セル定義と実行時辞書の契約

タグ: `現行仕様` `セル定義` `初期化` `R1` `検証入口`

## 正本と寿命

`CellDatabaseDefinition.cellPairingInfos` が保存する名前・セル種別・Field Prefab・Placeholder Prefabを正本とする。`_infoLookup` と `isInitialized` は実行時だけのキャッシュで、初期化済み状態をアセットから復元しない。旧アセットに `isInitialized: 1` が残っていてもロード時に辞書を構築する。一括再保存による移行は不要。

`OnEnable` は必ず構築する。`OnValidate` と `SetCellInfos` は辞書をクリアして未構築にし、`TryGetCellInfo` / `GetAllCellInfos` の次回呼出しで再構築する。空配列・未設定のnull配列は正常な空定義。`SetCellInfos(null)` は従来通り入力不正で例外とし、配列の作成に失敗したときは以前の定義を維持する。

重複種別は警告し、最後の登録を採用する。Prefab欠落の警告、公開API、登録の参照とenum値は維持する。既に生成されたUIを定義更新だけで自動再生成する機能は追加しない。

採用理由: 保存データと派生キャッシュの寿命を一致させるため。空辞書による判定は有効な空定義と未初期化を区別できず、毎取得の再構築は不要な走査を増やす。初期化・無効化・取得前の再構築を定義自身に集め、EditorやUI側の手動Validateに依存しない。

## 検証入口

- `GridFieldGeneratorVerifierEditor.RunCellDefinitions`: 旧保存フラグtrue＋空辞書のコールド状態、保存参照、更新・追加・削除、返却Listの独立性、Inspector更新、再有効化、重複、空/nullを検証する。保存済みアセットは複製上で検証し、保存しない。
- `GridFieldGeneratorVerifierEditor.RunCellDefinitionBuild`: 上記、既存グリッド生成、`UIStatusVerifierEditor.VerifyAssets`、MainSceneのWindows Developmentビルドを同じEditor起動で実行する。通常ビルド入口 `RunPlayerBuild` は従来通り非Development。
- 生成プレイヤーへ `--verify-cell-definitions --cell-verification-image=<PNG絶対パス>` を指定する。Development専用の `CellDefinitionVerifierAdapter` が通常のMainScene初期化後に、生成されたStorageButtonのButtonイベント、選択とplaceholder、`SelectGrid` / `PointerBegin` / `PointerDrag` / `PointerEnd` の配置経路、配置後のグリッド・選択参照、描画フレームを検証する。合格はログの `Cell definition PLAYER checks passed` とプロセス終了コード0の両方で判定する。

プレイヤー検証はGUIのクリック位置・入力装置の配送や主観的な操作感を保証するものではなく、実際のUIコールバックと配置処理の接続を検証する。Hidden起動の画面キャプチャが黒になる場合に備え、MainSceneのCameraから `RenderPipeline.StandardRequest` でRenderTextureへ描画し、黒一色・単色の画像を失敗とする。画像でゲームフィールドの描画を確認するが、画面に重ねたUIの見た目全体はこの画像の保証範囲に含めない。通常起動では引数がないので検証オブジェクトを生成しない。Releaseでは検証型自体を含めない。

関連コード: `Assets/Scripts/Grid/Definitions/CellDatabaseDefinition.cs`、`Assets/Scripts/Grid/Editor/GridFieldGeneratorVerifierEditor.cs`、`Assets/Scripts/Grid/Adapter/CellDefinitionVerifierAdapter.cs`、`Assets/Scripts/UI/Editor/UIStatusVerifierEditor.cs`。実施結果・残作業は [R1の再開文書](../tasks/extension-readiness-review.md)を参照。
