# 設計図式の検証・提出記録

タグ: `検証証拠` `現行設計` `Mermaid` `SVG` `再開情報`

確認日: 2026-10-11。対象コード: `f8e4daebe53a3b8239dcb2c1de1b9f315386e31b`。[現行設計索引](current-architecture.md)を正本の入口とし、[SVG一覧](diagrams/index.html)は閲覧用とする。

## 確認結果

- Mermaid原本14ブロックをOrcaに既存同梱の `mermaid.core-INM3DhGR.js` でparseし、すべてSVGへrender成功。最終実行3.32秒、結果は [verification.json](diagrams/verification.json)。SVGのXML・viewBoxが有効で、14件のファイルを確認した。Unity配線図はheadless画像化して日本語ラベル・矢印が読めることも目視確認した。
- C#78ファイル（未採用ServiceLocator含む）の [対応表](script-catalog.md) を収録し、ファイル集合との一致を確認。クラス図の継承・能力契約、状態遷移、代表メソッドを現行コードと照合した。ExportConveyorはIExportableを実装せず、後方のIExportableを利用する。
- シーン・Prefab・SOは `.meta` GUIDとシリアライズ参照、シーンoverrideを静的照合。Unityを起動した動作試験ではない。SO/Prefabの変更・再保存は行っていない。
- 文書の相対リンクをファイル存在で確認。Mermaidの表示はSVG原本で閲覧できる。Markdownアンカーは見出しと照合した。
- 着手前7ファイルはSHA-256で前後一致を確認。本依頼の文書だけを独立コミットする。push、ビルド、Unity起動は行わない。

初回はheadlessのDOM出力が非同期描画の完了前に終了し、60秒で受付待機がtimeoutした。virtual-time-budgetを追加した再実行は2.45秒で成功。その後シーケンスの定義取得ラベルをコードに合わせて直したため、必要な再生成を1回実施し3.32秒で成功した。計3試行、成功後の再実行は原本変更に伴う1回。コードやゲーム仕様の手戻りはない。

## 現行の図・説明更新手順

2026-10-11の全図説明追加から、更新は独立ハブが所有する共通CLIへ統一する。[共通CLI契約](../../../../.codex/session-routing/architecture-docs-tool.md) と [説明方針](../../../../.codex/session-routing/requests/architecture-description-policy-proposal.md) を参照する。上記SVGのparse/render記録は作成時の証拠として保持し、旧ASARサーバーとPythonによるSVG生成を通常更新手順には使わない。

説明の正本は [architecture-descriptions.json](architecture-descriptions.json)。図は既存MarkdownのMermaid、対応キーは直前のdiagram-idコメントである。ノードIDは図ごとの名前空間で対応する。SVGとHTML一覧は既存の閲覧用出力で、説明JSONの正本ではない。

```powershell
python C:/Users/Taomo/.codex/session-routing/architecture_docs.py doctor --project factory
python C:/Users/Taomo/.codex/session-routing/architecture_docs.py sync --project factory --write
python C:/Users/Taomo/.codex/session-routing/architecture_docs.py inventory --project factory --output <担当のinventory保存先>
python C:/Users/Taomo/.codex/session-routing/architecture_docs.py apply --project factory --patch <担当のpatch保存先> --write
python C:/Users/Taomo/.codex/session-routing/architecture_docs.py validate --project factory
python C:/Users/Taomo/.codex/session-routing/architecture_docs.py sync --project factory
```

inventoryのbase_revisionで競合を防ぎ、applyには変更する図の概要・ノード説明・根拠だけを指定する。図の変更は同じpatchにmermaidを含める。CLIを担当側で複製・修正せず、不具合はハブへ報告する。書込は--write指定時のみで、CLIは既存の描画資源による内部parse/renderと更新ジャーナルを管理する。今回の処理はSVGを書き出すものではない。

完成時はallow-incompleteを付けないvalidateがok=true、errors=[]、missing=[]であること、およびsyncのプレビューがchanged=[]であることを確認する。根拠は存在だけでなく、説明の状態・寿命・相手との関係を実装本文と照合する。合格は構文・対応・説明網羅の保証であり、Unityの実操作や描画品質の追加検証ではない。原本と関連文書を同じ目的で独立コミットし、ハブへ結果を返す。

## 全図説明の追加記録

2026-10-11、文書開始版0a7daaf、コード説明対象f8e4daeを照合して14図・179ノードへ概要・短い説明・リポジトリ基準の相対根拠を追加した。architecture-cells-grid-1のpilot17件はそのまま保持し、残り13図・162件を共通CLIで追加した。概要は対象・流れ・境界、ノードは役割・状態・相手との関わりを説明し、外部型・開始終了記号も図内の役割を自リポジトリの使用箇所に結び付けた。

doctor、sync、inventory、applyを順に使用し、applyの全件検証はok=true、errors=[]、missing=[]だった。別のvalidateでも全14図・179ノードがok=true、errors=[]、missing=[]（2.35秒）、続くsyncプレビューはchanged=[]（2.35秒）を確認した。276件の根拠参照が存在し、Mermaid原文14図・pilot説明17件・既存7ファイル・SVGの保全も照合した。再検証はこの最終一括確認の1回で、成功後の反復はない。文書コミットと詳細結果は担当の提出JSONに記録する。説明追加では製品コード・Prefab・SO・SVGを変更しない。既存7ファイルの無関係差分を保全し、説明で既存の未完了設計課題を完了扱いにしない。

## 未確認と再開情報

今回の成果は静的設計説明。Unityのロード順・実入力・見た目を新たに実行検証したとは扱わない。Start経路間の総順序を保証しない。未コミットの描画差分の体験品質と未使用ServiceLocatorの採否も判定対象外。[拡張レビュー](../tasks/extension-readiness-review.md)の未完了課題は継続している。本図式を閲覧・更新する次担当は対象コードHEADと当日のGit差分を先に照合する。
