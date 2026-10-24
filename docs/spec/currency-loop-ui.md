# 通貨・設備強化・スキルの表示と操作

タグ: `現行仕様` `ECO-05` `経済UI` `購入可否` `購読寿命`
更新日: 2026-10-25

## 表示と操作

[GridFieldGeneratorAdapter](../../Assets/Scripts/Grid/Adapter/GridFieldGeneratorAdapter.cs)が一プレイの共有残高・スキル・設備強化を[EconomyPanelAdapter](../../Assets/Scripts/Economy/Adapter/EconomyPanelAdapter.cs)へ渡す。MainSceneの起動時に画面左上へuGUIの経済パネルを生成する。「経済・成長」のボタンで開閉し、スクロールで全項目を操作する。見出しと本文の文字サイズを分け、日本語を含むOSフォントを使う。

| 項目 | 表示・操作 |
| --- | --- |
| 通貨 | 残高、使用可能額、支出予約、換金待ち。残高の増加通知から直前の入金額を表示し、換金予約の取消では入金を表示しない。 |
| 設置・撤去 | CellPlacerの選択設備の単価、範囲個数・総費用、確定した個数・支出と停止理由。残高不足では途中停止を予告する。撤去は無料・返金なしを表示する。 |
| 個別強化 | 設備を右クリックして選択し、座標、レベル、有効能力、次の強化費と可否を表示する。ボタンから共有Upgradesへ購入を渡す。最大レベルでは費用を「—」と表示する。 |
| スキル | 定義の順で名称、価格、前提名、能力の加算率、取得状態・可否を表示する。根は枝の解禁、枝は該当能力の加算率を示す。固定uidで購入を要求する。 |

強化とスキルのボタンは、それぞれApplicationのCanPurchaseがSuccessの場合に有効になる。残高不足・前提不足・取得済み・最大レベルなどを文字でも表示する。購入時はApplicationが対象と状態を再確認し、失敗結果を日本語で表示する。支出予約中の使用可能額も判定へ反映される。能力の値はセルの有効値を読み、計算をUIへ複製しない。交差設備は上流からの搬送量を中継する。

既存の設置見積り用OnGUIは経済パネルへ統合した。UIRaycasterはEventSystemの全Canvasを調べ、追加したパネル上の操作をフィールド入力から遮る。

## 専用セル表示と寿命

[SingularityCellAdapter](../../Assets/Scripts/Economy/Adapter/SingularityCellAdapter.cs)はIDataProvidableApplicationを実装し、[SingularityProviderDefinition](../../Assets/Scripts/UI/Definitions/SingularityProviderDefinition.cs)のラベルと専用ReaderをCreateCustomへ渡す。施設名、座標、残高、換金待ちを新しいスナップショットとして返す。表示取得で取引を変更しない。セル有効中は残高変更をStatusChangedへ接続し、無効化・破棄時に解除する。専用ラベル定義はセルが所有し、破棄時に解放する。

[CellStatusViewAdapter](../../Assets/Scripts/UI/Adapter/CellStatusViewAdapter.cs)はSelectedCellとSelectionChangedを公開する。既存Providerを持たない搬送・交差設備も選択でき、経済パネルへ強化対象を渡す。選択解除、非表示、無効化、破棄で選択通知を送り、前のセルの購読と表示行を解放する。削除した選択セルはLateUpdateで閉じる。設備の無効化中は選択を保持し、強化を購入不可にする。

経済パネルは残高・スキル変更・選択変更と、選択セルのStatusChangedを購読する。選択切替では前のセル、パネルを閉じたときと無効化・破棄時にはすべての購読を解除する。再表示は現在の残高と選択セルから読み直す。設置プレビューはLateUpdateで読み、各フレームの状態を表示する。

## 検証

[EconomyUIVerifierEditor.Run](../../Assets/Scripts/Economy/Editor/EconomyUIVerifierEditor.cs)がMainSceneで、残高と予約額、換金の予約・取消・確定、専用Reader、設備選択・強化・上限、支出予約による購入不可、実Buttonのスキル取得と前提解除、選択・非表示・無効化・削除時の購読解除、UIの入力遮断を検証する。再生前にEditorの音声を消音する。

実行条件・対象コード・結果・ログはローカルの`docs/verification/eco05-ui.json`へ集約する。既存UIと設置の回帰を直接依存する検証として実行する。Windowsビルドとゲームループ全体の統合検証はECO-06で扱う。
