# FactoryGame Codex 作業ガイド

タグ: `作業方針` `Unity` `設計改修` `検証`

## プロジェクト概要

- Unity Editor のバージョンは `ProjectSettings/ProjectVersion.txt` に記録された 6000.3.8f1。
- ゲームの C# コードは `Assets/Scripts/`、メインシーンは `Assets/Scenes/MainScene.unity`。
- `Packages/manifest.json` は UniTask を Git URL から取得する。
- 採用済みタスクの状態は `docs/tasks/task-list.md` を正本とする。作成根拠・カテゴリ順・更新手順は `docs/tasks/design-review.md`、採用時の詳細は `docs/tasks/architecture-extension.md` と `docs/tasks/extension-readiness-review.md` から辿る。
- 今後の追加実装と設計改修は `docs/design/architecture-guidelines.md` の責務分担と依存方向を踏襲する。既存コードは同文書の現在地とタスクリスト正本を照合して改修する。
- スクリプト追加・移動・改名は `docs/design/script-organization.md` に従い、機能 → レイヤーの配置と、レイヤー名に一致する接尾辞を使う。

## 新担当の入口

| 読む目的 | 正本・索引 |
| --- | --- |
| 現行機能の目的と採用した設計 | [設計方針](docs/design/architecture-guidelines.md) |
| 採用済み作業の状態 | [タスクリスト](docs/tasks/task-list.md) |
| タスク作成の根拠、分類と優先順、更新・再開 | [タスク根拠と引継ぎ手順](docs/tasks/design-review.md) |
| 機能別の仕様、コード・配線・検証への参照 | [現行設計索引](docs/design/current-architecture.md) |
| 設計図・説明JSONの更新 | [Metis更新手順](docs/design/architecture-diagram-verification.md) |

タスク提案の採用判断はユーザーが行う。採用内容と依頼の工程を正本へ照合し、許可された範囲を進める。

## 共通規則との関係

このローカル環境では `~/.codex/AGENTS.md` の対話・記録・作業効率・Git運用規則を適用する。報告は `~/.codex/knowledge/policies/review-verification-reporting.md`、Unityの参照・GUID維持と生成物保護は `~/.codex/knowledge/policies/unity-project-workflow.md` を正本とし、作業に該当する規則だけ参照する。別環境でこのガイドを再利用するときは共通規則も併せて提供する。固有条件は以下に残す。

## 作業規則

- 設計改修はタスクリスト正本の優先順と現在の依頼を照合して進める。原本更新はAtlasのChronos共通CLIを使い、変更した仕様と検証証拠を関連文書へ記録する。数量や中断処理の状態変更では境界条件も確認する。
