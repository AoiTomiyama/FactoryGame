# 拡張性を高める設計タスク

タグ: `改修タスク` `アーキテクチャ` `資源搬送` `レシピ` `UI`

状態: 2026-10-01 に A〜H を採用。A〜E・G と既存タスク1〜5は完了済み。I（Assembly Definition）は今回の対象外。次は F から着手する。

到達像と今後の追加実装の判断基準は `docs/design/architecture-guidelines.md` を参照する。この文書は未完了の改修タスクと完了条件を管理する。

## 重複と着手順

- A は搬送の現行動作と責務を記録する。既存タスク2の修正前に、予約から中断までの処理順を確認する。
- C と G のうち、搬送中断時の予約解除、確定抑止、表示オブジェクトの一度だけの返却は既存タスク2の完了条件として扱う。C は予約操作の契約を他のセルにも適用できる形へ整える作業、G は演出と資源データの責務分離を追加範囲とする。
- B と D はタスク2で必要な範囲から適用する。搬送一件の状態と進行管理を独立させる拡張は、既存タスク2が完了してから判断する。
- E は予約・確定の計算、F はレシピの判定・消費、H は表示用データの整理を対象とし、既存タスク3〜5とは別の変更として扱う。
- 残作業の推奨順: F、H。タスクごとに完了を報告し、次の改修はユーザーの許可を得てから着手する。

## A. 搬送の責務と依存関係を記録する

- [x] 予約、移動、確定、取消の担当と処理順を現行コードから整理する。
- 完了条件: 各操作の呼び出し元、状態の所有者、中断時に保持・返却するものが文書から追える。未実装の望ましい動作と現行動作を区別する。
- 検証証拠: 2026-10-01 に対象コードと既存の数量契約を照合し、`docs/design/resource-transfer-responsibilities.md` に現行動作と改修目標を分けて記録した。
- 参照: `Assets/Scripts/Cells/Adapter/ConveyorCellAdapter.cs`、`Assets/Scripts/Cells/Adapter/ExportConveyorCellAdapter.cs`、`Assets/Scripts/Cells/Adapter/CrossingCellAdapter.cs`、`Assets/Scripts/Transfers/Adapter/ResourceItemObjectPoolAdapter.cs`。

## B. 搬送一件の状態を表す型を導入する

- [x] 資源種別、数量、送り元、受け取り先、進行状態を一件として追えるようにする。
- 完了条件: 非同期処理中に ID と数量と搬送先の対応が失われず、終了状態への遷移が一度だけ起きる。既存タスク2で必要な最小の状態管理から導入する。
- 依存: A、既存タスク2。
- 採用した設計: `ResourceTransferOperationApplication` が一件の送り元、搬送先、資源ID、種別、数量、進行段階を保持する。コンベアは搬送先切断時にその試行を取消し、保持したIDで再接続後に新しい試行を作る。交差セルは同じ記録で搬送先を付け替えて予約を待ち直す。仕様は `docs/spec/resource-transfer-state.md`。
- 検証証拠: Unity 6000.3.8f1 のバッチモードで `ResourceReservationVerifier.Run` の状態遷移・容量境界、Play Mode で `ResourceTransferVerifier.Run` のコンベア・交差セルの成功、搬送先切断、送り元・交差セル削除を確認。記録のID・種別・数量・搬送先と、完了・取消が再実行できないことを確認した。プレイヤービルドは今回実施していない。

## C. 予約の確定・取消の契約を整える

- [x] 予約を識別し、確定または取消を一度だけ行える操作を定義する。
- 完了条件: 保管、加工、コンベア、交差セルの予約量と現在量が、成功・中断・セル削除で整合する。既存タスク2の中断修正結果を再利用し、同じ修正を別タスクとして数えない。
- 依存: A、既存タスク2、B の必要部分。
- 採用した設計: `ResourceReservationApplication` が成功した全量予約に一意の ID と受け取り先・方向・種別・数量を付け、確定か取消を一度だけ受け付ける。コンベアと交差セルは搬送先を予約する際にこの操作を保持する。切断後の再予約は別の ID とし、旧予約だけを取消す。受け取り側の数量は従来の `IContainableApplication` 契約に従う。
- 検証証拠: Unity 6000.3.8f1 のバッチモードで `ResourceReservationVerifier.Run` が通過し、保管の同条件二件、加工・コンベア・交差セルの取消と数量を確認した。Play Mode の `ResourceTransferVerifier.Run` が通過し、通常搬送、搬送先・送り元・交差セルの削除、交差セルの切断と再接続で予約量・現在量・IDの扱いを確認した。プレイヤービルドは実施していない。

## D. 搬送の進行管理をセルから分離する

- [x] 予約、演出、確定、取消の順序を一箇所で管理し、セルには接続と入出力の責務を残す。
- 完了条件: 搬送の成功・失敗・中断の各経路が一箇所で確認でき、セルの削除と接続解除でも資源が二重処理されない。
- 依存: B、C、G の必要部分。
- 採用した設計: `ResourceTransferCoordinatorApplication.RunAsync` が搬送一回の予約取得、データ照合、表示移動、搬入確定、失敗時の予約取消を管理する。交差セルの搬送先変更時は `WaitForNewTarget` が旧予約を閉じ、同じ資源 ID で新しい予約を待つ。セルは接続先の提供と削除時の保持 ID 返却を担う。
- 検証証拠: Unity 6000.3.8f1 のバッチモードで `ResourceReservationVerifier.Run` が通過した。Play Mode の `ResourceTransferVerifier.Run` では通常搬送、搬送先・送り元・交差セルの削除、交差セルの上流からの取消・切断と再接続、予約量・現在量・ID 返却を確認した。プレイヤービルドは実施していない。

## E. 資源量の計算と判定を分離する

- [x] 空き容量、予約可否、搬入可能量の計算を Unity のオブジェクト状態から切り離す。
- 完了条件: 容量上限、資源種別の不一致、ゼロ・負数、同時予約の境界を単独で検証でき、現在の全量予約仕様を保つ。
- 依存: 既存タスク2、C。
- 採用した設計: Unity 非依存の `ResourceStorageRulesDomain` が読み取り専用の `Stock`（種別・現在量・予約量）と容量から空き・全量予約・確定・取消・搬出を計算する。保管セルと加工セルは成功した計算結果だけを状態へ反映し、UI を更新する。状態の所有者と方向別入力の対応はセルに残す。数量契約は `docs/spec/resource-reservations.md`。
- 検証証拠: 2026-10-04 に Unity 6000.3.8f1 の `ResourceReservationVerifier.Run` が終了コード 0 で通過した。セルを生成しない数量ルールの検証で容量不足、種別不一致、ゼロ・負数、二件の予約と片方の確定、予約中の搬出、`int.MaxValue` 付近の桁あふれ防止を確認し、保管・加工・コンベア・交差セルの連携も確認した。Play Mode の `ResourceTransferVerifier.Run` は終了コード 0 で通過し、通常搬送、表示欠落・復元、切断・再接続・削除の数量と ID を確認した。既存の未コミット変更を保持した作業ツリーで検証し、プレイヤービルドは実施していない。

## F. レシピ判定と素材消費を分離する

- 実装計画（許可待ち）: [加工処理の責務分離計画](../design/crafting-separation-plan.md)。判定と素材消費に同じ消費計画を使い、Domain の計算、Application の一回性、Adapter の数量反映を分ける。

- [ ] 加工セルのレシピ選択と素材消費に同じ入力の対応付けを使う。
- 完了条件: 同種素材の複数入力、容量不足、レシピ不成立、加工中断の結果を単独で検証できる。
- 参照: `Assets/Scripts/Cells/Adapter/CrafterCellAdapter.cs`。

## G. 搬送演出と資源データの管理を分離する

- [x] アニメーション、表示オブジェクトの貸出・返却、資源数量の確定をそれぞれの責務に整理する。
- 完了条件: 演出の成功・中断が明示的に伝わり、表示オブジェクトの返却は一度だけ行われる。資源量は演出の後始末によって変化しない。
- 依存: 既存タスク2、D の必要部分。
- 採用した設計: `ResourceTransitStoreApplication` が表示と独立した ID・種別・数量を保持し、`ResourceItemObjectPoolAdapter` が表示を貸出・返却する。`ResourceItemAnimationAdapter` は演出結果を明示し、`ResourceTransferCoordinatorApplication` は `IResourceTransferPresentationApplication` を通じて正常完了を確認してから搬入を確定する。表示欠落時は予約を戻して資源を保持し、表示の復元後に再試行できる。仕様は `docs/spec/resource-presentation.md`。
- 検証証拠: Unity 6000.3.8f1 の `ResourceReservationVerifier.Run` と Play Mode の `ResourceTransferVerifier.Run` が終了コード 0 で通過した。トークン取消・外部 Tween 停止、表示返却と再利用、コンベア・交差セル・搬出コンベアの表示欠落と復元、コンベアから交差セルへの ID 引継ぎ、既存の成功・切断・削除経路を確認した。既存の未コミット変更を保持した作業ツリーで検証し、プレイヤービルドは実施していない。

## H. UI 表示用データの取得境界を整理する

- [ ] セルの状態から表示値を作る処理と UI 更新経路を明確にする。
- 完了条件: セルの状態変更後に対象表示が更新され、表示用処理が資源量や予約量を変更しない。
- 参照: `Assets/Scripts/UI/Definitions/`、`Assets/Scripts/UI/Application/`、`Assets/Scripts/UI/Adapter/`。

## ファイル配置・命名の整理（2026-10-06）

- [x] 機能 → レイヤーの配置へ移動し、型・ファイルの接尾辞を所属分類と一致させる。
- 規則・対応表・検証証拠・再開情報: [スクリプト配置・命名規則](../design/script-organization.md)。
- 検証: 初回のライセンス・Git 権限の停止は解消。Unity 6000.3.8f1 の予約・接続・グリッド生成、Play Mode の搬送・資源アイコン、Windows 64-bit プレイヤービルドがすべて終了コード 0。GUID 維持とコミット対象の分離も確認済み。
- F・H の実装分離は未完了のまま維持する。
