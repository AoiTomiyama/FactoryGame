# 共有スキルツリー

タグ: `現行仕様` `成長` `通貨` `共有効果` `PRG-02`

## 取得条件と効果

一プレイで一つの取得状態を持ち、MainSceneの共有残高から購入する。取得状態と効果は再開始で初期化する。定義は [SkillTree.asset](../../Assets/Scripts/ScriptableObject/SkillTree.asset) に集約し、起動時に不変カタログへコピーする。

| 固定uid | 名称 | 価格 | 前提uid | 効果 |
| --- | --- | --- | --- | --- |
| basic-research | 基礎研究 | 50 | なし | 各枝のIを解禁 |
| extraction-1 | 採取効率 I | 50 | basic-research | 抽出量+25% |
| extraction-2 | 採取効率 II | 100 | extraction-1 | 抽出量をさらに+25% |
| transfer-1 | 搬送効率 I | 50 | basic-research | コンベア・搬出コンベアの搬送量+25% |
| transfer-2 | 搬送効率 II | 100 | transfer-1 | 搬送量をさらに+25% |
| storage-1 | 保管効率 I | 50 | basic-research | 抽出在庫・保管・加工入出力容量+25% |
| storage-2 | 保管効率 II | 100 | storage-1 | 容量をさらに+25% |

同じ枝のIとIIは加算して50%になる。基礎値と個別レベルとの合成、整数切り捨て、能力を持たないセルと交差セルの扱い、稼働中の数量固定は [有効能力仕様](effective-stats.md) に従う。全設備で同じ `SharedStatEffectsApplication` を参照するため、既設・非アクティブ後の再有効化・新設セルに同じ規則が適用される。

## 定義と購入の検証

Domainは空定義、nullノード、空uid・名称、価格0以下、uid重複、前提欠落、自己参照・循環を拒否する。効果の枝が範囲外、効果量が正でない、効果なしのノードに効果量がある、同じ枝の合計が50%を超える定義も拒否する。価格は正のlongで、最大値でも既存の支出予約で不足を判定する。

`SkillTreeApplication` はカタログと取得状態を公開し、`CanPurchase(uid)` で購入可否、`Purchase(uid)` で購入結果を返す。結果は成功、購入処理中、未知uid、取得済み、前提不足、残高不足、共有効果の不一致を区別する。表示は左の樹形図と右の詳細欄に分ける。ノード選択は詳細表示だけを行い、右の「適用する」で購入を要求する。未解放ノードも選択でき、前提と不足理由を確認できる。関係は接続線、状態は明暗・選択枠・適用済み記号で示す。設備とスキルの詳細は同じ右枠を使い、選択切替と閉じる操作で前の購読・選択を解除する。

購入は次の順で進む。

1. uid、取得状態、前提、共有効果、支出予約を除いた利用可能残高を確認する。
2. 支出を予約し、予約通知から戻った後に取得状態・前提・共有効果を再確認する。不成立なら予約を取り消す。
3. 取得状態と枝の効果を更新し、支出を確定する。この確定通知からは更新後の取得状態・能力・残高を読める。
4. 枝の能力変更、ツリー変更を通知する。根の購入はツリー変更だけを通知する。

全通知が終わるまで購入の再入を拒否し、取得済みuidへの再購入を拒否する。`CanPurchase` は通知中も更新後の取得条件を返すため、UIが次の操作可否を読み取れる。通知先の例外は `LastNotificationException` へ記録し、残りの購読者へ通知する。共有効果の通知も同じ規則で、セル表示の失敗による通知中断を防ぐ。

共有効果はこのツリーが購入によって更新する。別経路から共有効果が変わり取得状態と一致しない場合、購入を拒否する。既存の能力検証用の直接設定入口は保持している。

## 所有と依存

- [定義](../../Assets/Scripts/Progression/Definitions/SkillTreeDefinition.cs): ScriptableObjectの入力を不変ノードへ変換する。
- [カタログ](../../Assets/Scripts/Progression/Domain/SkillTreeCatalogDomain.cs): 固定uid、前提、価格、効果の定義規則を検証する。
- [購入処理](../../Assets/Scripts/Progression/Application/SkillTreeApplication.cs): 取得状態、購入可否、残高予約と共有効果の確定、変更通知を担う。
- [構成入口](../../Assets/Scripts/Grid/Adapter/GridFieldGeneratorAdapter.cs): 共有残高とグリッドの共有効果をツリーへ渡し、`Skills` として保持する。MainSceneはSkillTree.assetを参照する。
- [セル登録](../../Assets/Scripts/Grid/Adapter/GridFieldDatabaseAdapter.cs): 初期配置と新設の両方で同じ共有効果を渡す。

## 検証入口

[SkillTreeVerifierEditor](../../Assets/Scripts/Progression/Editor/SkillTreeVerifierEditor.cs) の `Run` は定義不正、前提、不足、重複、long最大価格、通知再入と例外、予約後の再確認を検証する。同じ起動で通貨・資源予約の既存検証を実行し、MainSceneのPlay Modeで共有残高から全7ノードを購入する。抽出2種、通常・搬出コンベア、保管、加工についてI・IIそれぞれの既設・再有効化・新設セル、個別レベルの合成、在庫・予約・Identityの維持を確認する。

`RunRegression` は消音を確認して既存 `EffectiveStatsVerifierEditor.Run` を実行し、稼働中の抽出・搬送・加工、予約確定、数量表示通知と購読寿命を確認する。実行条件・対象版・結果・ログはローカルの `docs/verification/prg02-skill-tree.json` に記録する。

ECO-04の[交差セル対応](currency-equipment-upgrade.md)では、`Run`で個別レベルと共有数量効果を区別し、I/II取得前後の中継予約維持と取消を確認する。追加検証の結果はローカルの`docs/verification/eco04-equipment-upgrade.json`を参照する。
