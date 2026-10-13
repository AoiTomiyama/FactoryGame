/// <summary>搬送接点の識別と寿命。実装側が自身の破棄・終了を判定する。</summary>
public interface IResourceTransferEndpointApplication
{
    /// <summary>同じ接点の寿命中は固定し、別の接点へ再利用しない識別子。参照一致で比較する。</summary>
    object Identity { get; }

    /// <summary>予約・確定・取消の操作を受け付けられる寿命内ならtrue。</summary>
    bool IsAlive { get; }
}
