# FactoryGame Codex 作業ガイド

タグ: `作業方針` `Unity` `設計改修` `検証`

## プロジェクト概要

- Unity Editor のバージョンは `ProjectSettings/ProjectVersion.txt` に記録された 6000.3.8f1。
- ゲームの C# コードは `Assets/Scripts/`、メインシーンは `Assets/Scenes/MainScene.unity`。
- `Packages/manifest.json` は UniTask を Git URL から取得する。
- 設計レビューで見つかった問題と着手順は `docs/tasks/design-review.md` を参照する。改修したタスクは、完了条件の検証結果とともに同文書の状態を更新する。
- 今後の追加実装と設計改修は `docs/design/architecture-guidelines.md` の責務分担と依存方向を踏襲する。未移行の既存コードは同文書の現在地と各タスクリストを照合し、段階的に改修する。
- スクリプト追加・移動・改名は `docs/design/script-organization.md` に従い、機能 → レイヤーの配置と、レイヤー名に一致する接尾辞を使う。

## 共通規則との関係

このローカル環境では `~/.codex/AGENTS.md` の対話・記録・作業効率・Git運用規則を適用する。報告は `~/.codex/knowledge/policies/review-verification-reporting.md`、Unityの参照・GUID維持と生成物保護は `~/.codex/knowledge/policies/unity-project-workflow.md` を正本とし、作業に該当する規則だけ参照する。別環境でこのガイドを再利用するときは共通規則も併せて提供する。固有条件は以下に残す。

## 作業規則

- 設計改修は `docs/tasks/design-review.md` の順に進め、資源量や中断処理のような状態変更は境界条件も確認する。
