# 抽出セルの稼働と中断

タグ: `現行仕様` `R2` `抽出` `寿命` `進捗` `検証証拠`

対象: [ExtractorCellAdapter](../../Assets/Scripts/Cells/Adapter/ExtractorCellAdapter.cs)。2026-10-13のR2では、未完了進捗を破棄するユーザーの判断に従う。

## 現行契約

- 初期化済みで有効な設備は、前方のグリッド座標にある有効なResourceCellAdapterを読む。資源種別が一致すると抽出を開始する。開始時に対象がなくても、後から置かれた対応資源を検知する。
- 資源セルは非接続型なので、初期化時の隣接配列や接続通知だけに依存しない。Updateで現在の前方座標を再評価し、抽出確定直前にも同じ対象が有効かを確認する。対象の削除・交換・種別変更・無効化で未完了の抽出を取り消す。
- 設備の無効化・破棄・明示的な切断でも処理を取り消す。未完了進捗は0へ戻し、停止処理は抽出済み在庫を変更しない。有効化で初期化済みの設備は再開できる。切断後はInitializeSystemによる再初期化を待つ。
- 一度に実行する非同期の生産処理は一件だけ。再初期化でも切断通知の購読と処理を重複させない。CancellationTokenSource（CTS）の取消は停止処理、DisposeとTweenの後始末はそのCTSを所有する生産処理が担当する。
- Tweenが正常完了し、設備・対象・取消Tokenが有効な場合だけ資源を増やす。外部からのTween停止も未完了として扱う。抽出量は既存の設定値と空き容量の小さい方で、出庫・進捗通知・数量表示の契約は維持する。

資源の有限化、新しい設備、セル種別・Prefab・ScriptableObjectの設定変更はこの修正に含まない。前方の座標と資源種別の既存条件を維持する。

## 検証入口

[UIStatusVerifierEditor.Run](../../Assets/Scripts/UI/Editor/UIStatusVerifierEditor.cs)に通常初期化経由の抽出寿命回帰を組み込み、既存の表示・予約・加工検証と同じ起動で確認する。対象なし・後置き・種別不一致・削除・交換・対象無効化、設備の無効化・再有効化、重複初期化、進捗破棄、在庫・容量・出庫・UI、切断・再初期化・破棄を対象にする。

## 検証結果（2026-10-13）

対象差分: 親HEAD `e732006df5d80f0fdc25a0f8d2337f2ce34e7042` からのExtractorCellAdapterとUIStatusVerifierEditor。Unity 6000.3.8f1、`-batchmode -nographics`、`UIStatusVerifierEditor.Run`でEditorコンパイルとPlay Mode検証を実行した。

初回は終了0・55.83秒、同じ種別の別セルへ交換しても旧抽出が早期確定しないケースを追加した最終実行も終了0・26.51秒。R2寿命検証、既存UI、数量・予約、加工ルールの成功マーカーを確認した。計82.34秒、再検証は追加ケースによる1回。実装仕様の取り違えによる手戻りはない。

既存のTMPフォントに日本語文字が未収録で代替文字になる警告と、ライセンストークン更新エラーがある。ライセンスの解決と検証実行は成功し、R2の実行例外・コンパイルエラーはない。プレイヤービルド・GUI操作は実施していない。新しい見た目の設計は追加しておらず、進捗と数量表示は実UI行の値・通知で検証した。

ログはローカルTEMPの `FactoryGame-R2-lifecycle.log` と `FactoryGame-R2-lifecycle-final.log`。コミットと保全結果は [R2の完了返信JSON](../../../../.codex/session-routing/requests/chronos/start-requests/005532ab-43e4-48f9-87f4-fb0241691c08-reply.json) に記録する。タスクの進捗は[新原本](../tasks/task-list.md)のR2を参照する。
