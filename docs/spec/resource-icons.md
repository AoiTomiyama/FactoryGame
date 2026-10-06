# 資源アイコンの現行表示

タグ: `現行仕様` `資源アイコン` `レシピUI` `保管UI` `検証`

`ResourceDB.asset` は石材と木材の表示名、プレハブ、アイコンを保持する。レシピの材料・成果物行と保管セルの状態欄は、資源種別から同じアイコンと表示名を取得して表示する。

既存表示に戻した Sprite 参照は、石材が Unity 組み込みの `Checkmark`（fileID `10901`）、木材が `Background`（fileID `10907`）。これらは資源専用の図柄ではない。将来、専用アイコンを採用する場合は `ResourceDB.asset` の参照と両 UI の表示を合わせて確認する。

検証: Unity 6000.3.8f1 の Play Mode バッチ実行 `ResourceIconVerifierEditor.Run` で、実際の `Recipe.prefab` と `StorageParamLine.prefab` に石材・木材の Sprite と表示名が設定されることを確認した。画面の目視確認は行っていない。
