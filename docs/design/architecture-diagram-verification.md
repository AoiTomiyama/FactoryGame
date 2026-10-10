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

## 再生成

下記のPython例は文書の再生成手順。原本Markdownからブロックを抽出し、インストール済みOrcaのASAR内のES moduleをlocalhostだけで提供して、既存Chromeの隔離された一時profileによるheadless描画を行う。新規依存は導入せず、製品コードをimport・実行しない。ASARとChromeの位置を引数で指定する。

```powershell
python <下記Python例の保存先> --docs docs/design --asar <既存Orcaのresources/app.asar> --browser <既存Chrome実行ファイル>
```

出力は `docs/design/diagrams/*.svg` と `verification.json`。`index.html` は説明付きの静的閲覧索引で、新しい図を追加した場合はその参照も更新する。SVGは生成物であり、修正はMarkdownのMermaidへ行う。Orca更新で同梱asset名が変わる場合はツールがprefixから見つけるが、ASAR構造変更時には既存CLIの利用可能性を再確認する。レンダラーの成功と設計説明の正確性は別で、変更時にはコード関係も照合する。

## 未確認と再開情報

今回の成果は静的設計説明。Unityのロード順・実入力・見た目を新たに実行検証したとは扱わない。Start経路間の総順序を保証しない。未コミットの描画差分の体験品質と未使用ServiceLocatorの採否も判定対象外。[拡張レビュー](../tasks/extension-readiness-review.md)の未完了課題は継続している。本図式を閲覧・更新する次担当は対象コードHEADと当日のGit差分を先に照合する。

## 再生成用Python例

これは図の生成手順であり、製品スクリプトではない。Python標準ライブラリと既存のChrome・Orcaのみを使う。

```python
"""Render canonical Markdown Mermaid blocks with an existing Orca ASAR and headless Chrome.
No package installation or GUI operation. Outputs are document artifacts only.
"""
import argparse
import http.server
import json
import pathlib
import re
import struct
import subprocess
import tempfile
import threading
import time

parser = argparse.ArgumentParser()
parser.add_argument("--docs", required=True, type=pathlib.Path)
parser.add_argument("--asar", required=True, type=pathlib.Path)
parser.add_argument("--browser", required=True, type=pathlib.Path)
args = parser.parse_args()
root = args.docs.resolve()
out = root / "diagrams"
out.mkdir(exist_ok=True)
with args.asar.open("rb") as f:
    header = struct.unpack("<4I", f.read(16))
    tree = json.loads(f.read(header[3]))
asset_tree = tree["files"]["out"]["files"]["renderer"]["files"]["assets"]["files"]
base = 8 + header[1]
mermaid = next(k for k in asset_tree if k.startswith("mermaid.core-") and k.endswith(".js"))
blocks = []
for source in sorted(root.glob("*.md")):
    for i, code in enumerate(re.findall(r"```mermaid\n(.*?)```", source.read_text(encoding="utf-8"), re.S), 1):
        blocks.append({"id": source.stem + "-" + str(i), "source": source.name, "code": code})
page = """<!doctype html><meta charset="utf-8"><style>body{font-family:Meiryo,sans-serif}</style>
<script type="module">
import mermaid from '/assets/MODULE';
mermaid.initialize({startOnLoad:false,theme:'default',securityLevel:'strict',fontFamily:'Meiryo, sans-serif',flowchart:{htmlLabels:false},sequence:{useMaxWidth:false}});
const blocks=BLOCKS;const results=[];
for(const b of blocks){try{await mermaid.parse(b.code);const {svg}=await mermaid.render('diagram'+results.length,b.code);results.push({...b,ok:true,svg});}catch(e){results.push({...b,ok:false,error:String(e)});}}
await fetch('/result',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(results)});
document.body.innerText=JSON.stringify(results.map(({id,ok,error})=>({id,ok,error})));
</script>""".replace("MODULE", mermaid).replace("BLOCKS", json.dumps(blocks, ensure_ascii=False))
finished = threading.Event()
results = []
class Handler(http.server.BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass
    def do_GET(self):
        if self.path == "/":
            data = page.encode("utf-8"); mime = "text/html; charset=utf-8"
        elif self.path.startswith("/assets/") and self.path[8:] in asset_tree:
            entry = asset_tree[self.path[8:]]
            with args.asar.open("rb") as f:
                f.seek(base + int(entry["offset"])); data = f.read(entry["size"])
            mime = "application/javascript"
        else:
            self.send_error(404); return
        self.send_response(200); self.send_header("Content-Type", mime)
        self.end_headers(); self.wfile.write(data)
    def do_POST(self):
        if self.path != "/result":
            self.send_error(404); return
        results.extend(json.loads(self.rfile.read(int(self.headers["Content-Length"]))))
        self.send_response(200); self.end_headers(); self.wfile.write(b"ok")
        finished.set()
server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Handler)
thread = threading.Thread(target=server.serve_forever, daemon=True)
thread.start()
started = time.monotonic()
try:
    with tempfile.TemporaryDirectory(prefix="factory-diagrams-") as profile:
        command = [str(args.browser), "--headless=new", "--no-first-run", "--no-default-browser-check", "--disable-background-networking", "--user-data-dir="+profile, "--dump-dom", "--virtual-time-budget=30000", "--timeout=60000", "http://127.0.0.1:"+str(server.server_port)+"/"]
        with tempfile.TemporaryFile() as log:
            process = subprocess.Popen(command, stdout=log, stderr=log, creationflags=getattr(subprocess,"CREATE_NO_WINDOW",0))
            if not finished.wait(60):
                process.kill(); process.wait(); log.seek(0)
                raise RuntimeError("Renderer timeout: " + log.read().decode(errors="replace")[-2000:])
            process.wait(timeout=15)
        for result in results:
            if result["ok"]:
                (out / (result["id"]+".svg")).write_text(result.pop("svg"), encoding="utf-8")
            result.pop("code",None)
        record={"blocks":len(blocks),"results":results,"seconds":round(time.monotonic()-started,2),"engine":"Orca bundled "+mermaid,"browser":"headless Chrome", "source":"Markdown mermaid blocks"}
        (out / "verification.json").write_text(json.dumps(record,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
        print(json.dumps(record,ensure_ascii=False,indent=2))
        if len(results)!=len(blocks) or not all(x["ok"] for x in results):
            raise SystemExit(1)
finally:
    server.shutdown(); server.server_close()
```
