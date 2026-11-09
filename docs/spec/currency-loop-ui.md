# 通貨・設備強化・スキルの表示と操作

タグ: `現行仕様` `ECO-05` `ECO-07` `経済UI` `購入可否` `購読寿命`
更新日: 2026-11-07

## 表示と操作

[GridFieldGeneratorAdapter](../../Assets/Scripts/Grid/Adapter/GridFieldGeneratorAdapter.cs)が一プレイの共有残高・スキル・設備強化を[EconomyPanelAdapter](../../Assets/Scripts/Economy/Adapter/EconomyPanelAdapter.cs)へ渡す。上部に残高などの常時表示枠、左側に「配置」「レシピ」「スキル」の直接タブ、右側に選択セルの状態、または選択スキルの詳細と適用操作、下部にUIS-04の操作帯を置く。同じタブの再押下または内容の「閉じる」で左ページを閉じる。他のタブは1回で対象ページを開く。開閉後も表示ページ・選択設備・配置方向を保持する。

配置ページは[SelectButtonBuilderAdapter](../../Assets/Scripts/UI/Adapter/SelectButtonBuilderAdapter.cs)が既存Prefabとセル定義から生成する。撤去・保管・加工・搬送を直接選択し、採取設備は既存サブメニューから木材・石材を選ぶ。固定資源と特異点は候補に含めない。単価・範囲費用・確定結果を表示し、選択・タブ切替では支払を行わない。各ページはスクロールでき、書体は日本語を含む同じOSフォント、本文・操作は16、主な見出しは18、通知は14を使う。

[RecipeReferenceViewAdapter](../../Assets/Scripts/UI/Adapter/RecipeReferenceViewAdapter.cs)は既存RecipeDatabaseDefinitionとResourceDefinitionから、レシピ名・加工時間・材料名と必要数・成果物名と数量・資源アイコンを表示する。参照ページの表示は加工処理と独立しており、現行の自動判定を保つ。

[PersistentSummaryViewAdapter](../../Assets/Scripts/UI/Adapter/PersistentSummaryViewAdapter.cs)は確定した残高を表示する。SetItemsで追加項目と順を指定する。残高と追加項目を横へ並べ、画面幅を超えると折り返して枠の高さを増やす。常時表示枠にはスクロールや切り抜きを付けず、全項目を表示する。左右パネルはその下へ追従し、下端108以上を保つ。固定枠と行Prefabの参照は[HudLayoutAdapter](../../Assets/Scripts/UI/Adapter/HudLayoutAdapter.cs)へ集約する。ボタン・タブ・生成行のボタン・操作帯のボタンは同AdapterのIdleTabColor、選択・強調・押下はSelectedTabColorを使う。スキルの前提未解放は暗いグレーを使い、選択・ホバー中も保つ。黒背景パネルと操作帯の背景はPrefabで保存した色を保つ。文字・資源アイコン・進捗ゲージ・無効状態の色は各部品の設定を保つ。

UI上で始めたクリックは配置確定へ進めず、フィールドからの配置ドラッグをUI上で離した場合はプレビューを取り消す。[PlayerCursorBehaviourAdapter](../../Assets/Scripts/Input/Adapter/PlayerCursorBehaviourAdapter.cs)が既存Raycasterの結果で判定する。

| 項目 | 表示・操作 |
| --- | --- |
| 通貨 | 確定した残高を表示する。使用可能額・支出予約・換金待ちと直前の換金通知は内部管理とし、HUDへ表示しない。 |
| 設置・撤去 | 配置タブはセル選択専用とし、選択設備と単価を下部へ表示し、ドラッグ中は範囲個数・総費用、残高不足と既存セルとの重なりはUIへ表示し、それ以外のシステム上の失敗はConsoleへ記録する。残高不足では途中停止を予告する。撤去は無料・返金なしを表示する。 |
| 種類別強化 | セル状態欄は種類名と共有レベル・在庫・稼働状態の読み取り専用とする。強化はスキルの種類別I/IIから適用し、同じ種類の既設・新設セルすべてへ反映する。 |
| スキル | 前提関係を樹形図で示す。ノードには名称を置き、未解放は暗いグレー、解放済みの残高不足は通常色、選択中は白枠、適用済みは記号で示す。押すと右側へ効果と価格付きの「適用する」ボタンを表示する。 |

スキルの適用ボタンは、それぞれApplicationのCanPurchaseがSuccessの場合に有効になる。スキルのノードは全状態で選択でき、選択だけでは支払わない。前提不足・残高不足は右側へ理由を表示し、適用済みは操作ボタンの状態とノードの記号で示す。購入時はApplicationが対象と状態を再確認し、残高不足と配置の重なりはプレイヤーが修正できる条件としてUIに表示する。生成・初期化のエラーや対象の消失など、システム上の失敗はUnity Consoleへ日本語の警告として記録する。前提不足や残高不足などの事前の操作条件は、選択欄とボタンの状態へ反映する。支出予約中の使用可能額も判定へ反映される。能力の値はセルの有効値を読み、計算をUIへ複製しない。交差設備は上流からの搬送量を中継する。

既存の設置見積り用OnGUIは配置ページへ統合した。UIRaycasterはEventSystemの全Canvasを調べ、追加したパネル上の操作をフィールド入力から遮る。

## 専用セル表示と寿命

[SingularityCellAdapter](../../Assets/Scripts/Economy/Adapter/SingularityCellAdapter.cs)はIDataProvidableApplicationを実装し、[SingularityProviderDefinition](../../Assets/Scripts/UI/Definitions/SingularityProviderDefinition.cs)のラベルと専用ReaderをCreateCustomへ渡す。施設名、座標、残高、換金待ちを新しいスナップショットとして返す。HUDでは予約値を省いて表示し、Readerの契約は保持する。表示取得で取引を変更しない。セル有効中は残高変更をStatusChangedへ接続し、無効化・破棄時に解除する。専用ラベル定義はセルが所有し、破棄時に解放する。

[CellStatusViewAdapter](../../Assets/Scripts/UI/Adapter/CellStatusViewAdapter.cs)はSelectedCellとSelectionChangedを公開する。既存Providerを持たない搬送・交差設備も選択でき、EquipmentDetailsViewAdapterへ同じ強化対象を渡す。選択解除、非表示、無効化、破棄で選択通知を送り、前のセルの購読と表示行を解放する。削除した選択セルはLateUpdateで閉じる。設備の無効化中は選択を保持し、強化を購入不可にする。

経済UIは残高・スキル変更・選択変更と、選択セルのStatusChangedを購読する。左ページの開閉では購読を保ち、残高と右側情報を更新する。選択切替では前のセル、無効化・破棄時にはすべての購読を解除する。再有効化時は現在の残高と選択セルから読み直す。ページ切替では購読を保ち、切替先を先頭へスクロールする。設置プレビューはLateUpdateで読み、各フレームの状態を表示する。

## 検証

[EconomyUIVerifierEditor.Run](../../Assets/Scripts/Economy/Editor/EconomyUIVerifierEditor.cs)がMainSceneで、残高と予約額、換金の予約・取消・確定、専用Reader、設備選択・種類別I/II・上限、支出予約による購入不可、実Buttonのスキル取得と前提解除、選択・非表示・無効化・削除時の購読解除、UIの入力遮断を検証する。ECO-07では既存配置ボタンと採取サブメニューによる選択、ページ切替時の購読数、スキル連打時の支払一回性、UIクリックとドラッグ終了による誤配置防止も確認する。1600×900・1366×768はCanvasScalerの高さ基準720に換算した論理サイズで、各ページの文字高さ・ボタン幅・操作領域・スクロール領域の画面内配置を検査する。描画の目視確認とは区別する。再生前にEditorの音声を消音する。

実行条件・対象コード・結果・ログはローカルの`docs/verification/eco05-ui.json`、統合UIへの変更分は`docs/verification/eco07-ui.json`へ集約する。既存UIと設置の回帰を直接依存する検証として実行する。Windowsビルドとゲームループ全体の統合検証はECO-06の記録を参照する。

## 画面下の操作帯（UIS-04）

タグ: `操作案内` `回転` `視点切替` `UIS-04`

[OperationBarAdapter](../../Assets/Scripts/UI/Adapter/OperationBarAdapter.cs)はシーンのOperationBar.prefabを参照し、画面下の「設備回転 [R]」「視点切替 [L]」へ既存入力を接続する。配置情報・枠・ボタンはPrefabに保存する。下部は左列に操作説明と回転・視点切替ボタン、右列に選択セルと単価、ドラッグ中の予定個数・費用をまとめる。配置タブが開いている間だけ操作帯を表示し、設置・撤去を有効にする。レシピ・スキルへの切替、左パネルを閉じる操作、HUDの無効化で設置を止め、ドラッグ範囲とプレビューを取り消し、カーソル位置のセル表示を戻す。配置タブへ戻った後は新しいドラッグから設置する。ボタンは既存カーソルの90度回転とカメラの視点切替を呼び、R・Lの入力設定を保持する。

操作帯は高さ720の論理画面で下端12〜96に配置する。経済パネルと右側セル情報は下端108以上へ収める。セル情報は既存Canvasの高さへ換算して余白を確保する。操作帯の背景とボタンも既存UIRaycasterの判定対象となり、UI上のクリックとドラッグ終了は既存の配置取消規則に従う。サイドタブの再構成はUIS-05の採用方針を参照する。

`EconomyUIVerifierEditor.Run`で、回転ボタンの90度回転と4回での復帰、視点の往復、R・Lの有効なバインド、各ボタン上の配置遮断・ドラッグ取消、PC2寸法の文字と操作領域、左右HUDの下余白を検査する。実行結果はローカルの`docs/verification/uis04-ui.json`へ集約する。

## 採用済みのサイドタブ統合方針

タグ: `サイドタブ` `直接アクセス` `常時表示` `表示項目の追加`
採用日: 2026-11-07。ユーザーが1B・2Aを採用した。目的の機能へ少ない操作で到達できることを優先する。

- 以前のサイドタブの開閉UIを使い、側面の「配置」「レシピ」「スキル」から対応する内容を直接開く。同時に表示する内容は1ページとする。内容パネルと「閉じる」は左の画面外から約0.2秒のEase Out Cubicで減速しながらスライドし、閉じる際は逆方向へ退場する。タブは常に前面へ残し、閉じた後は全タブを通常色へ戻して選択フォーカスを解除する。
- 配置タブは設置するセルと撤去の選択に絞る。配置中の費用は下部、スキルの詳細と適用は右側へ置く。閉じても選択設備・設置方向・表示ページを保持する。
- 「レシピ」は既存のレシピ定義から材料の種類・必要数、成果物の種類・数量、加工時間を一覧表示する参照用ページとする。材料・成果物には既存の資源名とアイコンを使い、一覧をスクロールできるようにする。加工は現行の自動判定を維持する。右側のセル情報・設備強化と併用でき、タブ切替・開閉後もレシピを再確認できる。レシピ件数や材料数が増えた代表例とPC2寸法で表示の収容とスクロール到達を確認し、UI操作による誤配置を防ぐ。
- 設備を右クリックすると右側のセル情報ウィンドウを開き、上部に設備名・現在レベル・在庫・稼働状態、下部に強化後の能力・費用・強化ボタンを表示する。情報表示と強化は同じ選択セルへ追従し、強化対象外のセルは状態表示だけとする。左パネルと併用でき、費用不足や最大レベルの表示・購入制御は既存の規則を保つ。
- 開閉途中の再操作は現在位置から反転する。ゲームの一時停止中も動き、移動中は内容の購入操作を無効にして、見えているUIの背後へ配置入力を通さない。退場後は非表示にして入力を通す。残高はタブの開閉にかかわらず共通の常時表示枠に残す。残高と追加項目は横並び・折返しで画面幅へ収め、常時表示枠全体にスクロールを使わない。常時表示する項目の追加と並びの変更に対応できる構成にし、項目が増えた場合も狭い画面で文字・操作を収め、サイドタブや画面下の操作帯との重なりを防ぐ。
- 画面下の短い操作説明と回転・視点切替ボタンはUIS-04で扱い、サイドタブの配置と合わせて確認する。
- 直接アクセス、タブ切替・開閉後の選択保持、既存の購入・費用表示・スクロール、UI操作による誤配置の防止を検証する。常時表示枠へ12項目を追加した代表例とPC2寸法で、残高を含む全項目の表示、文字と左右パネル・操作帯の非重複、スクロール操作への非依存を確認する。
- UIS-04・UIS-05の配置は、上部の常時表示枠・左側の直接アクセス用タブと内容パネル・右側のセル情報と設備強化・下部の操作ボタンと短い説明を基準とする。2026-11-07の配置確認図は配置だけを採用する。プロトタイプでは読みやすさと機能・状態の識別に必要な最低限の色を使い、色指定を少数の共通設定へまとめて後から変更しやすくする。

## UIS-05の責務と検証

EconomyPanelAdapterは共有状態の配線・購読、タブ切替、配置・スキル操作を管理する。レシピ参照・常時表示・設備強化の表示はそれぞれのViewへ委譲する。購入と支払の判定は既存ApplicationとEquipmentUpgradeAdapterを使う。

[EquipmentDetailsViewAdapter](../../Assets/Scripts/Economy/Adapter/EquipmentDetailsViewAdapter.cs)は保存済みの右側詳細枠を保持する。[EquipmentDetailsPresenterAdapter](../../Assets/Scripts/Economy/Adapter/EquipmentDetailsPresenterAdapter.cs)はセルの種類名・共有レベルと加工や停止の状態を読み取り専用で表示する。セル選択時は購入欄を隠し、スキル選択時だけ同じ欄で種類別レベルと倍率・適用ボタンを表示する。個別購入経路は通常UIから外す。スキルの効果・価格と検証は[種類別スキル仕様](skill-tree.md)に従う。樹形図の枝は読みやすい幅を保ち、樹形図内の横ドラッグとスクロールバーで移動する。

CellStatusViewAdapterは既存の選択・Provider・行プールを保ち、HudStatusProviderAdapterとHudStatusRowAdapterで在庫と進捗を表示する。内部予約値、座標、重複説明、待機中の残り時間0秒を省く。右側の「閉じる」は選択と購読を解除する。

EconomyUIVerifierEditor.Runは、既存の購入・配置・入力遮断に加え、直接タブ、開閉後の設備・配置方向の保持、種類別レベル、スキル詳細の実ボタンによる購入、在庫保持、対象外セルの状態表示を検査する。24レシピ・各6材料、常時表示への12項目追加と順序変更を代表例とし、1600×900・1366×768に相当する論理サイズで文字の収容・左右の非重複・レシピ末尾と強化ボタンへの到達を確認する。常時表示は残高と追加12項目を全て枠内に収め、項目間の非重複とホイール操作前後の位置保持を検査する。RunRenderedは同じ検査に加えて実HUDを画像へ書き出す。実行条件・結果・画像・対象差分はローカルの`docs/verification/uis05-ui.json`へ集約する。

## HUDのPrefabと編集範囲（UIS-06）

タグ: `Prefab` `UGUI` `Editor編集` `旧資産統合`

MainSceneは[FactoryHud.prefab](../../Assets/Prefabs/UI/FactoryHud.prefab)と[OperationBar.prefab](../../Assets/Prefabs/UI/OperationBar.prefab)を直接参照する。上部の常時表示、左3タブと閉じるボタン、右のセル情報・設備強化、下部操作帯はEditorで配置・文字・色を編集できる。GeneratorとCursorには接続先のシーン参照を保存し、起動時は共有状態と操作を接続する。固定枠を生成・移動する移行処理は実行経路に置かない。

左右のパネルは保存した位置・寸法を基準に、常時表示の折返しで増えた高さと画面幅の差分だけを反映する。レイアウトグループによる子要素の整列と、表示文の高さへの追従は継続する。通常色と選択色はHudLayoutAdapterの既存IdleTabColor・SelectedTabColorを固定HUDのボタン/タブ・配置/スキルの生成ボタン・採取サブメニュー・操作帯のボタンで共用する。標準Inspectorで2色を変えると、開いているSceneまたはPrefab Modeのボタンとタブに反映する。同じSceneにある操作帯のボタンも追従する。共有行Prefabのアセットは変更せず、生成時に2色を使う。ButtonはColorTintで反映し、色の二重乗算を避ける。各Textの書体・サイズと背景ImageはPrefabの値を使う。HudFont.fontsettingsは従来のOSフォント名と描画用Material・Textureを保存し、Editorと再生で同じ参照を使う。

| アセット | 再利用・変更内容 |
| --- | --- |
| SelectButton.prefab | GUIDと選択Adapter・Button・背景を継承。旧TMP文字と空アイコンを共通書体のTextへ整理し、実行時の文字部品の追加を撤去した。 |
| Recipe.prefab | GUIDと資源アイコン・名称・数量を表示する用途を継承。材料数の増加を縦に収容する資源行へ変更し、見出し・加工時間はHudRecipeHeading.prefabへ分けた。 |
| HudStatusRow.prefab | 既存Providerと行プールから在庫・進捗を表示する。文字・アイコン・ゲージを保存し、Configureで部品を生成しない。 |
| HudTextRow / HudRecipeHeading / HudSkillRow | 常時表示の追加項目、レシピ見出し、スキルの変動一覧用。定義や表示項目数に応じて複製する。 |
| UITab.prefab・旧Canvas | 旧タブの再押下による開閉を3タブへ引き継ぎ、重複するMainSceneのCanvasと未参照のUITab資産を撤去した。 |

旧RecipeUIBuilder・RecipeElementUI・ResourceRowUI、HudUIAdapterの実行時生成、旧ページの非表示化、セル情報の移設・復元処理を撤去した。Text/Gauge/StorageParamLineは独立UIの契約と既存検証で使う行Prefabとして保持する。経済パネルの終了時は購読と購入リスナーを解除し、保存済みHUDを非表示にする。旧UIの復元や二重生成は行わない。

配置タブ枠とレシピ1行を旧Prefabと比較してから全体へ展開した。代表比較と変更理由、対象差分、実行結果はローカルの`docs/verification/uis06-ui.json`に集約する。EconomyUIVerifierEditorはHudPrefabVerifierEditorの保存参照・Editor編集保持・再設定時の同一部品保持を加え、既存のPC2寸法、24レシピ・6材料、常時表示の追加12項目、購入、入力遮断、購読寿命を検証する。最終画像は既存固定名`docs/verification/uis05/hud-1600x900.png`と`hud-1366x768.png`を更新する。

左ページのスライドはHudLayoutAdapterが保存位置と表示割合から計算し、常時表示の折返しと画面幅の調整を合成する。EconomyPanelAdapterは開閉の目標を渡す。内容と閉じるボタンのCanvasGroupは既存FactoryHud.prefabへ保存する。移動・途中反転・一時停止・閉じた後の入力・無効化後の復帰を既存EconomyUIVerifierEditorで検証し、実行記録はローカルの`docs/verification/side-panel-slide.json`へ保存する。
