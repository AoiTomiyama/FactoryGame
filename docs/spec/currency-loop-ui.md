# 通貨・設備強化・スキルの表示と操作

タグ: `現行仕様` `ECO-05` `ECO-07` `経済UI` `購入可否` `購読寿命`
更新日: 2026-11-07

## 表示と操作

[GridFieldGeneratorAdapter](../../Assets/Scripts/Grid/Adapter/GridFieldGeneratorAdapter.cs)が一プレイの共有残高・スキル・設備強化を[EconomyPanelAdapter](../../Assets/Scripts/Economy/Adapter/EconomyPanelAdapter.cs)へ渡す。上部に残高などの常時表示枠、左側に「配置」「レシピ」「スキル」の直接タブ、右側に選択セルの状態と設備強化、下部にUIS-04の操作帯を置く。同じタブの再押下または内容の「閉じる」で左ページを閉じる。他のタブは1回で対象ページを開く。開閉後も表示ページ・選択設備・配置方向を保持する。

配置ページは[SelectButtonBuilderAdapter](../../Assets/Scripts/UI/Adapter/SelectButtonBuilderAdapter.cs)が既存Prefabとセル定義から生成する。撤去・保管・加工・搬送を直接選択し、採取設備は既存サブメニューから木材・石材を選ぶ。固定資源と特異点は候補に含めない。単価・範囲費用・確定結果を表示し、選択・タブ切替では支払を行わない。各ページはスクロールでき、書体は日本語を含む同じOSフォント、本文・操作は16、主な見出しは18、通知は14を使う。

[RecipeReferenceViewAdapter](../../Assets/Scripts/UI/Adapter/RecipeReferenceViewAdapter.cs)は既存RecipeDatabaseDefinitionとResourceDefinitionから、レシピ名・加工時間・材料名と必要数・成果物名と数量・資源アイコンを表示する。参照ページの表示は加工処理と独立しており、現行の自動判定を保つ。

[PersistentSummaryViewAdapter](../../Assets/Scripts/UI/Adapter/PersistentSummaryViewAdapter.cs)は残高・使用可能額・支出予約・換金待ちを表示する。SetWalletOrderで4項目の順を、SetItemsで追加項目と順を指定する。追加に応じて枠を伸ばし、論理高さ220を超える内容は枠内でスクロールする。左右パネルはその下へ追従し、下端108以上を保つ。共通色と文字・ボタン生成は[HudUIAdapter](../../Assets/Scripts/UI/Adapter/HudUIAdapter.cs)へ集約する。

UI上で始めたクリックは配置確定へ進めず、フィールドからの配置ドラッグをUI上で離した場合はプレビューを取り消す。[PlayerCursorBehaviourAdapter](../../Assets/Scripts/Input/Adapter/PlayerCursorBehaviourAdapter.cs)が既存Raycasterの結果で判定する。

| 項目 | 表示・操作 |
| --- | --- |
| 通貨 | 残高、使用可能額、支出予約、換金待ち。残高の増加通知から直前の入金額を表示し、換金予約の取消では入金を表示しない。 |
| 設置・撤去 | CellPlacerの選択設備の単価、範囲個数・総費用、確定した個数・支出と停止理由。残高不足では途中停止を予告する。撤去は無料・返金なしを表示する。 |
| 個別強化 | 設備を右クリックして選択し、右側のセル情報に設備名、座標、レベル、在庫・稼働状態を表示し、その下に現在と強化後の能力、費用と可否を置く。ボタンから共有Upgradesへ購入を渡す。最大レベルでは費用を「—」と表示する。 |
| スキル | 定義の順で名称、価格、前提名、能力の加算率、取得状態・可否を表示する。根は枝の解禁、枝は該当能力の加算率を示す。固定uidで購入を要求する。 |

強化とスキルのボタンは、それぞれApplicationのCanPurchaseがSuccessの場合に有効になる。残高不足・前提不足・取得済み・最大レベルなどを文字でも表示する。購入時はApplicationが対象と状態を再確認し、失敗結果を日本語で表示する。支出予約中の使用可能額も判定へ反映される。能力の値はセルの有効値を読み、計算をUIへ複製しない。交差設備は上流からの搬送量を中継する。

既存の設置見積り用OnGUIは配置ページへ統合した。UIRaycasterはEventSystemの全Canvasを調べ、追加したパネル上の操作をフィールド入力から遮る。

## 専用セル表示と寿命

[SingularityCellAdapter](../../Assets/Scripts/Economy/Adapter/SingularityCellAdapter.cs)はIDataProvidableApplicationを実装し、[SingularityProviderDefinition](../../Assets/Scripts/UI/Definitions/SingularityProviderDefinition.cs)のラベルと専用ReaderをCreateCustomへ渡す。施設名、座標、残高、換金待ちを新しいスナップショットとして返す。表示取得で取引を変更しない。セル有効中は残高変更をStatusChangedへ接続し、無効化・破棄時に解除する。専用ラベル定義はセルが所有し、破棄時に解放する。

[CellStatusViewAdapter](../../Assets/Scripts/UI/Adapter/CellStatusViewAdapter.cs)はSelectedCellとSelectionChangedを公開する。既存Providerを持たない搬送・交差設備も選択でき、EquipmentDetailsViewAdapterへ同じ強化対象を渡す。選択解除、非表示、無効化、破棄で選択通知を送り、前のセルの購読と表示行を解放する。削除した選択セルはLateUpdateで閉じる。設備の無効化中は選択を保持し、強化を購入不可にする。

経済UIは残高・スキル変更・選択変更と、選択セルのStatusChangedを購読する。左ページの開閉では購読を保ち、残高と右側情報を更新する。選択切替では前のセル、無効化・破棄時にはすべての購読を解除する。再有効化時は現在の残高と選択セルから読み直す。ページ切替では購読を保ち、切替先を先頭へスクロールする。設置プレビューはLateUpdateで読み、各フレームの状態を表示する。

## 検証

[EconomyUIVerifierEditor.Run](../../Assets/Scripts/Economy/Editor/EconomyUIVerifierEditor.cs)がMainSceneで、残高と予約額、換金の予約・取消・確定、専用Reader、設備選択・強化・上限、支出予約による購入不可、実Buttonのスキル取得と前提解除、選択・非表示・無効化・削除時の購読解除、UIの入力遮断を検証する。ECO-07では既存配置ボタンと採取サブメニューによる選択、ページ切替時の購読数、スキル連打時の支払一回性、UIクリックとドラッグ終了による誤配置防止も確認する。1600×900・1366×768はCanvasScalerの高さ基準720に換算した論理サイズで、各ページの文字高さ・ボタン幅・操作領域・スクロール領域の画面内配置を検査する。描画の目視確認とは区別する。再生前にEditorの音声を消音する。

実行条件・対象コード・結果・ログはローカルの`docs/verification/eco05-ui.json`、統合UIへの変更分は`docs/verification/eco07-ui.json`へ集約する。既存UIと設置の回帰を直接依存する検証として実行する。Windowsビルドとゲームループ全体の統合検証はECO-06の記録を参照する。

## 画面下の操作帯（UIS-04）

タグ: `操作案内` `回転` `視点切替` `UIS-04`

[OperationBarAdapter](../../Assets/Scripts/UI/Adapter/OperationBarAdapter.cs)はカーソルの起動時に、画面下へ操作案内と「設備回転 [R]」「視点切替 [L]」を生成する。左ドラッグの配置、右クリックの設備情報、中ドラッグの移動、ホイールの拡大・縮小を短い説明で案内する。ボタンは既存カーソルの90度回転とカメラの視点切替を呼び、R・Lの入力設定を保持する。

操作帯は高さ720の論理画面で下端12〜96に配置する。経済パネルと右側セル情報は下端108以上へ収める。セル情報は既存Canvasの高さへ換算して余白を確保する。操作帯の背景とボタンも既存UIRaycasterの判定対象となり、UI上のクリックとドラッグ終了は既存の配置取消規則に従う。サイドタブの再構成はUIS-05の採用方針を参照する。

`EconomyUIVerifierEditor.Run`で、回転ボタンの90度回転と4回での復帰、視点の往復、R・Lの有効なバインド、各ボタン上の配置遮断・ドラッグ取消、PC2寸法の文字と操作領域、左右HUDの下余白を検査する。実行結果はローカルの`docs/verification/uis04-ui.json`へ集約する。

## 採用済みのサイドタブ統合方針

タグ: `サイドタブ` `直接アクセス` `常時表示` `表示項目の追加`
採用日: 2026-11-07。ユーザーが1B・2Aを採用した。目的の機能へ少ない操作で到達できることを優先する。

- 以前のサイドタブの開閉UIを使い、側面の「配置」「レシピ」「スキル」から対応する内容を直接開く。同時に表示する内容は1ページとし、現在の独立した「配置・成長」パネルを置き換える。
- 設備選択・撤去とスキル購入、各操作に必要な費用・購入可否・換金などの詳細を対応するページへ移す。閉じても選択設備・設置方向・表示ページを保持する。
- 「レシピ」は既存のレシピ定義から材料の種類・必要数、成果物の種類・数量、加工時間を一覧表示する参照用ページとする。材料・成果物には既存の資源名とアイコンを使い、一覧をスクロールできるようにする。加工は現行の自動判定を維持する。右側のセル情報・設備強化と併用でき、タブ切替・開閉後もレシピを再確認できる。レシピ件数や材料数が増えた代表例とPC2寸法で表示の収容とスクロール到達を確認し、UI操作による誤配置を防ぐ。
- 設備を右クリックすると右側のセル情報ウィンドウを開き、上部に設備名・現在レベル・在庫・稼働状態、下部に強化後の能力・費用・強化ボタンを表示する。情報表示と強化は同じ選択セルへ追従し、強化対象外のセルは状態表示だけとする。左パネルと併用でき、費用不足や最大レベルの表示・購入制御は既存の規則を保つ。
- 残高はタブの開閉にかかわらず共通の常時表示枠に残す。常時表示する項目の追加と並びの変更に対応できる構成にし、項目が増えた場合も狭い画面で文字・操作を収め、サイドタブや画面下の操作帯との重なりを防ぐ。
- 画面下の短い操作説明と回転・視点切替ボタンはUIS-04で扱い、サイドタブの配置と合わせて確認する。
- 直接アクセス、タブ切替・開閉後の選択保持、既存の購入・費用表示・スクロール、UI操作による誤配置の防止を検証する。常時表示枠へ項目を追加した代表例とPC2寸法で、欠け・重なり・操作到達を確認する。
- UIS-04・UIS-05の配置は、上部の常時表示枠・左側の直接アクセス用タブと内容パネル・右側のセル情報と設備強化・下部の操作ボタンと短い説明を基準とする。2026-11-07の配置確認図は配置だけを採用する。プロトタイプでは読みやすさと機能・状態の識別に必要な最低限の色を使い、色指定を少数の共通設定へまとめて後から変更しやすくする。

## UIS-05の責務と検証

EconomyPanelAdapterは共有状態の配線・購読、タブ切替、配置・スキル操作を管理する。レシピ参照・常時表示・設備強化の表示はそれぞれのViewへ委譲する。購入と支払の判定は既存ApplicationとEquipmentUpgradeAdapterを使う。

[EquipmentDetailsViewAdapter](../../Assets/Scripts/Economy/Adapter/EquipmentDetailsViewAdapter.cs)は選択セルの見出しと強化比較・購入ボタンを所有する。セルの能力はレベルを指定する読み取りメソッドを通じて同じEffectiveStatsRulesDomainから計算する。比較表示でセルの実レベルや在庫を書き換えない。強化対象外のセルでは強化欄を隠す。CellStatusViewAdapterは既存の選択・Provider・行プールを保ち、[HudStatusProviderAdapter](../../Assets/Scripts/UI/Adapter/HudStatusProviderAdapter.cs)と[HudStatusRowAdapter](../../Assets/Scripts/UI/Adapter/HudStatusRowAdapter.cs)で在庫と進捗を共通書体の行へ変換する。セル名・座標は見出しへ集約する。右側の「閉じる」は選択と購読を解除する。

EconomyUIVerifierEditor.Runは、既存の購入・配置・入力遮断に加え、直接タブ、開閉後の設備・配置方向の保持、強化前後の能力、右側の実ボタンによる購入、在庫保持、対象外セルの状態表示を検査する。24レシピ・各6材料、常時表示への12項目追加と順序変更を代表例とし、1600×900・1366×768に相当する論理サイズで文字の収容・左右の非重複・最終項目と強化ボタンへの到達を確認する。RunRenderedは同じ検査に加えて実HUDを画像へ書き出す。実行条件・結果・画像・対象差分はローカルの`docs/verification/uis05-ui.json`へ集約する。
