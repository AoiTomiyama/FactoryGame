# 搬送一件の状態

タグ: `現行仕様` `資源搬送` `状態遷移` `コンベア` `交差セル`

`ResourceTransferOperation` は、一件の現在の送り元セル、搬送先セル、資源ID、資源種別、数量、進行段階を保持する。コンベアでは送り元は自身、交差セルでは下流へ送り出す自身を指す。種別と数量は生成後に変えない。ID は Unity の `GetInstanceID()` から得られるため負数も有効で、`0` だけを未設定とする。交差セルは予約後に上流から ID を一度受け取る。

進行段階は `WaitingForReservation → Reserved → Animating → Completed`。予約待ち、予約済み、演出中のどこからでも `Cancelled` に進める。`Completed` と `Cancelled` は終端であり、再度の完了・取消は拒否する。交差セルは搬送先を失うと同じ ID と数量を保持したまま予約待ちに戻り、次の接続先を設定できる。コンベアは切断された搬送試行を取り消し、保持した ID で次の試行を作る。

予約・確定・取消の呼び出し順と表示オブジェクトの返却は現時点ではセルが担う。検証は Unity 6000.3.8f1 の `ResourceReservationVerifier.Run` と Play Mode の `ResourceTransferVerifier.Run` で行い、状態と ID の対応、完了・取消の一度だけの遷移、通常搬送・切断・削除を確認した。
