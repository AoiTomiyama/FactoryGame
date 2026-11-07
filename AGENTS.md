# FactoryGame 作業ガイド

タグ: `Unity` `設計` `検証` `UI`

## 環境と配置

- Unityの版は`ProjectSettings/ProjectVersion.txt`、C#は`Assets/Scripts/`、メインシーンは`Assets/Scenes/MainScene.unity`。UniTaskは`Packages/manifest.json`のGit依存を使う。
- 実装は[設計方針](docs/design/architecture-guidelines.md)の責務と依存方向に従う。スクリプトの追加・移動・改名は[配置・命名規則](docs/design/script-organization.md)を使う。

## 作業の入口

- 対象機能とコード: [現行設計索引](docs/design/current-architecture.md)。
- 採用済み作業と状態: [タスクリスト](docs/tasks/task-list.md)。作成根拠・順序・更新・再開は[タスク運用](docs/tasks/design-review.md)、採用時の詳細は[設計改修](docs/tasks/architecture-extension.md)と[拡張性レビュー](docs/tasks/extension-readiness-review.md)を参照する。
- 図と説明JSON: [Opsis更新手順](docs/design/architecture-diagram-verification.md)。

## 作業規則

- 共通の対話・計画・検証・Git方針は`~/.codex/AGENTS.md`に従う。Taxis経由の依頼・返信・レビューは、同文書から参照する通信手順を使う。別環境へ持ち出す際は参照先も提供する。
- プロトタイプUIは機能確認と変更のしやすさを優先する。色は可読性・操作・選択・警告の識別に絞り、少数の共通設定で管理する。UIS-04/05の配置は[UI仕様](docs/spec/currency-loop-ui.md#採用済みのサイドタブ統合方針未実装)を使う。
- タスクの優先順と現在の依頼を照合し、更新はTaxis CLIで行う。数量・中断処理の変更では境界条件を確認する。
