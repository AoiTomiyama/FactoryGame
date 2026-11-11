# フィールドの描画除外

タグ: `現行仕様` `描画` `GPU` `検証`

## 設定と責務

PCのGPU Resident DrawerとGPU Occlusion Cullingは既定で無効とする。比較では一人称のGPU可視個数が減った一方、現行の軽いセルモデルではGame ViewのGPU処理時間が増え、GPU描画への切替による明るさの差も再現したため、一律の有効化を採用しない。

比較用に有効化するときは既存のForward+、Render Graph、SRP Batcherを使う。BatchRendererGroup Variantsは検証中だけKeep Allとし、終了前に元の値へ戻す。小さな搬送資源をサイズだけで消さないよう、Small Mesh Screen Percentageは0を保つ。

画面外のMeshRendererはUnity標準のフラスタムカリングで除外する。GPUオクルージョンは、画面内で他の不透明な物体に完全に隠れた対応MeshRendererを追加で除外する。セルの配置・撤去・移動は既存のRenderer更新を通じて反映される。セルや資源のGameObjectを非アクティブ化せず、画面外でも採取・搬送・加工・通貨取引と入力用Colliderは動作を続ける。既存のカメラと俯瞰・散策の切替を使う。

セルは実行中に配置・撤去されるため、セルを固定の遮蔽物としてベイクする方式を採用しない。GPU方式は現在と前フレームの深度を用いて保守的に判定する。透過プレビューや対応外の描画はUnityの通常経路に任せる。独自の画面外判定、常時走査するスクリプト、設定資産、遮蔽用の代理モデルは追加しない。

## 検証と比較

[FieldRenderingVerifierEditor.Run](../../Assets/Scripts/Grid/Editor/FieldRenderingVerifierEditor.cs)は保存済みMainSceneを再生し、実行時だけ複製したURP設定で次の3条件を比較する。起動は既存の検証コピーで行い、比較用のシェーダー保持設定も成功・失敗のどちらも終了前に復元する。保存済みのScene・PrefabとURP設定へ比較条件を書き戻さない。

| 条件 | GPU描画 | GPUオクルージョン |
| --- | --- | --- |
| 従来設定 | 無効 | 無効 |
| GPU描画のみ | 有効 | 無効 |
| GPU描画と遮蔽除外 | 有効 | 有効 |

通常俯瞰、広い俯瞰、散策開始位置、フィールド端からの透視投影を同じ1280×720で測る。各条件は45フレームの準備後に60フレームを測り、順序を反転して2回行う。GPU可視個数と三角形数は別途デバッグ統計を有効にして取得し、統計収集の負荷を時間計測へ混ぜない。バッチEditorではカメラを明示的に描画する。-batchmodeを付けずに同じ入口を呼ぶとGame Viewの通常描画で比較し、FrameTimingManagerのGPU時間も取得する。測定した時間はEditorの値であり、製品版のFPSとは区別する。GPU時間を取得できない環境では0を欠測値として記録する。

GPU描画のみとGPUオクルージョン有効時の画像を比較する。RGBの最大差が8を超える画素の割合を1%未満とし、設備Prefabの追加・移動・破棄後も確認する。Unity内部のGPU可視統計への参照はEditor検証内に限定し、Unity更新時に取得先と意味を再照合する。従来設定とGPU描画による明るさの差も別項目で記録する。SSAOとLight Probe Usageの個別切分けでも差が残り、原因を特定した扱いにしない。カメラとHUDの復元は既存FirstPersonVerifierEditorで確認する。ビルドは明示的に依頼された場合だけ行う。結果・画像・入力ハッシュと計測環境はローカルの`docs/verification/field-rendering/`に集約する。

## Unityの参照

- [Unity 6.3 GPU Resident Drawer](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/gpu-resident-drawer.html)
- [Unity 6.3 GPUオクルージョン](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/gpu-culling.html)
- [フラスタムカリングとベイク式オクルージョン](https://docs.unity3d.com/6000.3/Documentation/Manual/OcclusionCulling.html)
