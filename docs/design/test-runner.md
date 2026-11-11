# Unity Test Runnerによる検証

タグ: `現行手順` `Unity` `Test Runner` `検証` `納品`
更新日: 2026-11-11。

## 対象と使い方

Unity Test Framework 1.6.0を使用する。特異点の納品検証を、独自のRun・SessionState・Editor終了処理からTest Runnerへ移した。テストごとに成功・失敗・所要時間を確認し、目的に関係するケースを選んで実行する。

Unityの`Window > General > Test Runner`を開く。EditModeで`DeliveryGoalRulesTests`と`DeliveryGoalsDefinitionTests`、PlayModeで`DeliveryGoalsPlayModeTests`と`DeliveryTransferPlayModeTests`を選び、`Run Selected`で実行する。カテゴリ`Delivery`で今回の移行分全体、`Delivery.Layout`で収容、`Delivery.Transfer`で実搬送を絞り込める。

| テスト | 内容 | 保存先 |
| --- | --- | --- |
| DeliveryGoalRulesTests | 累計、段階、全量予約、上限、取消、再入と達成通知、達成後の吸収 | [判定テスト](../../Assets/Tests/Editor/Progression/Delivery/DeliveryGoalRulesTests.cs) |
| DeliveryGoalsDefinitionTests | 目標編集、Undo／Redo、保存と再読込、原本の保持 | [設定テスト](../../Assets/Tests/Editor/Progression/Delivery/DeliveryGoalsDefinitionTests.cs) |
| DeliveryGoalsPlayModeTests | 保存済みMainSceneの納品・換金分離、段階と最終達成のHUD、購読解除、PC2寸法の収容、散策からの復帰 | [Scene・表示テスト](../../Assets/Tests/PlayMode/Progression/DeliveryGoalsPlayModeTests.cs) |
| DeliveryTransferPlayModeTests | 板材・石レンガ→上位加工3×2→搬出路→特異点5×5の実搬送と累計を4回転で確認 | [実搬送テスト](../../Assets/Tests/PlayMode/Progression/DeliveryTransferPlayModeTests.cs) |

実搬送の材料は検証用に搬送路へ投入する。採掘から収益・再投資・最終達成までの通しプレイ、目標数のバランス調整はこのテスト群に含めない。Play ModeテストはEditorで実行し、自動再生を消音する。各ケースの終了時にSceneと一時オブジェクトを破棄し、音量・時間倍率を復元する。設定テストの一時アセットは失敗時にも削除する。通常のテスト実行では画像を書き出さない。

## コマンドライン

既存の検証プロジェクトとLibraryを再利用する。対象プロジェクトを開いているEditorとは別に、同じプロジェクトを同時起動しない。

```powershell
Unity.exe -batchmode -projectPath "<検証プロジェクト>" -runTests -testPlatform EditMode -testCategory Delivery -testResults "<EditMode結果.xml>" -logFile "<EditModeログ>"
Unity.exe -batchmode -projectPath "<検証プロジェクト>" -runTests -testPlatform PlayMode -testCategory Delivery -testResults "<PlayMode結果.xml>" -logFile "<PlayModeログ>"
```

Test Runnerの完了を待ち、終了コードとXMLのresult・passed・failedを照合する。ケースを一つだけ実行する場合は`-testFilter`へ型名または完全なテスト名を指定する。プレイヤービルドは行わない。今回の実行条件・結果・入力ハッシュはローカルの`docs/verification/delivery-goals/test-runner.json`へ集約する。

## コンパイル区分

Test Runner用アセンブリから既定のAssembly-CSharpを直接参照できないため、ゲーム側をFactoryGame.Runtimeへまとめた。機能ごとの細分化は行わず、既存の機能・Domain／Application／Adapter配置を保持する。

| アセンブリ | 配置・参照 |
| --- | --- |
| FactoryGame.Runtime | [ゲーム本体](../../Assets/Scripts/FactoryGame.Runtime.asmdef)。UniTask、UniTask.DOTween、DOTween.Modules、Cinemachine、Input System、uGUI、TextMeshProを参照 |
| FactoryGame.Grid.Editor | [グリッド編集ツール](../../Assets/Scripts/Grid/Editor/FactoryGame.Grid.Editor.asmdef)。ゲーム本体とVisual Scripting Coreを参照し、Editorでのみコンパイル |
| FactoryGame.Shared.Editor | [共通Drawer](../../Assets/Scripts/Shared/Editor/FactoryGame.Shared.Editor.asmdef)。ゲーム本体を参照し、Editorでのみコンパイル |
| DOTween.Modules | [既存DOTweenモジュール](../../Assets/Plugins/Demigiant/DOTween/Modules/DOTween.Modules.asmdef)。ゲーム側からCanvasGroupのDOFadeなどを参照するための定義。DOTweenSettingsのcreateASMDEFも有効にして管理設定を一致させる |
| FactoryGame.Delivery.EditModeTests | [Edit Modeテスト](../../Assets/Tests/Editor/Progression/Delivery/FactoryGame.Delivery.EditModeTests.asmdef)。ゲーム本体とTestAssembliesを参照 |
| FactoryGame.Delivery.PlayModeTests | [Play Modeテスト](../../Assets/Tests/PlayMode/Progression/FactoryGame.Delivery.PlayModeTests.asmdef)。ゲーム本体、UniTask、uGUI、TestAssembliesを参照。保存済みアセットを読むためテストのC#をUNITY_EDITORで限定 |

ゲーム本体からテストを参照しない。既存スクリプトのGUID・フィールド・PrefabとSceneの参照を維持する。残りのVerifierは従来のCLI入口を保持し、関係する機能の変更時に順次移す。Test Runnerのテストを独自Verifierからまとめて起動する互換入口は追加しない。

根拠: [Unity公式のテスト区分とアセンブリ参照](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/edit-mode-vs-play-mode-tests.html)、[Test Runnerでの実行](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/workflow-run-test.html)。

DOTweenのasmdefは[DOTweenSettings](../../Assets/Resources/DOTweenSettings.asset)の`createASMDEF: 1`と合わせて管理する。無効のまま手動でasmdefだけを配置すると、DOTweenによって削除されることがある。設定アセットとasmdef・metaを同じ版で保持する。[DOTween公式の該当説明](https://dotween.demigiant.com/support.php)。
