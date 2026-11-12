/// <summary>搬出路Prefabの初期役割。搬送状態と取り出し処理は共通の本体が保持する。</summary>
public sealed class ExportConveyorCellAdapter : ConveyorCellAdapter
{
    protected override bool StartsAsExporter => true;
}
