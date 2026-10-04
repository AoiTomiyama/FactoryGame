# FactoryGame の設計方針

タグ: `設計方針` `目標アーキテクチャ` `Unity` `追加実装` `依存関係`

状態: 2026-10-01 に採用した追加実装の方針。既存コード全体が移行済みという意味ではない。改修状況と完了条件は [設計レビュー改修タスク](../tasks/design-review.md) と [拡張性を高める設計タスク](../tasks/architecture-extension.md) を正本とする。

## 到達像

Unity のコンポーネント構成を土台とし、ゲームのルール、処理の進行、Unity との接続を分ける**軽量なレイヤード・アーキテクチャ**を採用する。セルと搬送先を契約でつなぐ部分には Ports and Adapters の考え方を適用する。全面的な Clean Architecture への移行や、階層・インターフェイスの一律追加は目的にしない。

```text
シーン・UI・演出・アセット（Unity）
              ↓
搬送や加工の進行管理
              ↓
資源量・予約・レシピなどのゲームルール
```

矢印は主な依存方向を示す。ゲームルールは、表示オブジェクト、DOTween、UI、シーン上のセルの寿命を知らずに判定できるようにする。境界をまたぐ操作には必要な契約を置き、利用箇所がない抽象化は追加しない。Assembly Definition による依存方向の強制は今回の計画に含めないため、コードレビューと単独の検証で境界を確認する。

## 責務の置き場所

| 領域 | 担当する内容 | 既存コードとの関係 |
| --- | --- | --- |
| ゲームルール | 容量、資源種別、予約可否、数量の確定、レシピの成立と素材消費 | [ResourceStorageRules](../../Assets/Scripts/Cells/ResourceStorageRules.cs) が保管・加工入力の数量を Unity 非依存で計算する。レシピ判定と素材消費はタスク F で分離する |
| 進行管理 | 搬送一件の状態と、予約・演出・確定・取消の順序 | [ResourceTransferOperation](../../Assets/Scripts/Cells/ResourceTransferOperation.cs) と [ResourceTransferCoordinator](../../Assets/Scripts/Cells/ResourceTransferCoordinator.cs) が担い、[ResourceTransitStore](../../Assets/Scripts/Cells/ResourceTransitStore.cs) で搬送資源を保持する |
| Unity との接続 | セル配置・接続、非同期処理の寿命、演出、オブジェクトプール、UI 表示 | `MonoBehaviour`、[ResourceItemObjectPool](../../Assets/Scripts/Manager/ResourceItemObjectPool.cs)、[ResourceItemAnimation](../../Assets/Scripts/Manager/ResourceItemAnimation.cs) が担当する。進行管理は `IResourceTransferPresentation` を通じて演出結果を受け取る。UI の境界はタスク H で整理する |
| 設定データ | セル、資源、レシピの編集可能な定義 | `Assets/Scripts/ScriptableObject/` の設定を利用し、実行中の資源量や搬送状態の所有者と区別する |
| Editor 専用機能 | フィールド生成などの編集支援 | タスク3で `GridFieldGenerator` の生成メソッドを `UNITY_EDITOR` に限定し、プレイヤー用コードから分離した |

## 追加実装で守る判断基準

1. 新しい資源・セル・レシピの動作を決めるときは、数量、容量、予約、確定、取消の条件と状態の所有者を先に明示する。現行の全量予約契約は [資源搬入の数量契約](../spec/resource-reservations.md) に従う。
2. 判定・計算は可能な範囲で Unity 非依存の C# に置く。セルの `MonoBehaviour` はシーンとの接続と入出力を担い、搬送の処理順は進行管理側に集める。
3. 演出や UI はゲーム状態を表示する。アニメーションの成功・中断は進行管理へ伝え、表示オブジェクトの返却だけで資源量を変更しない。
4. ScriptableObject は編集可能な定義の入力として扱う。実行中に変わる資源量、予約、一件の搬送状態は、その寿命を管理する処理が所有する。
5. UnityEditor API を使う処理は Editor 専用コード、または既存のシリアライズ参照を維持するための `UNITY_EDITOR` 条件付きコードへ置く。Unity のプレイヤー実行に必要なコードから参照しない。
6. 変更した機能の現行仕様とタスクリストを更新し、数量の境界と中断・削除を検証する。Unity Editor のコンパイル、Play Mode、プレイヤービルドで確認した範囲を区別して記録する。
7. 隣接セルの接続は [セル接続の契約](../spec/cell-connections.md) に従い、座標で4方向を直接読み、参照の両側を更新してから接続先へ通知する。

## 現在地と更新方法

2026-10-04 時点で、設計レビューのタスク1〜5と追加タスク A〜E・G は完了している。追加タスク F・H は未完了であり、現行の `IContainable` は `UnityEngine.Vector3Int` を使い、加工判定や UI 用データもセルと結び付いている。保管・加工入力の容量と予約量の計算は Unity 非依存の共通ルールへ分離した。搬送一件の状態と進行順は [搬送状態の契約](../spec/resource-transfer-state.md)、予約と数量計算の操作は [資源搬入の数量契約](../spec/resource-reservations.md)、資源データと演出の境界は [搬送資源データと表示の寿命](../spec/resource-presentation.md) にまとめた。

新しい機能は上記の境界を使える範囲から適用する。既存コードの移行時は各タスクの完了条件を満たした結果でこの文書の「現在地」と参照を更新する。アーキテクチャの分類を変える設計判断をした場合は、理由と適用範囲をここに記録する。
