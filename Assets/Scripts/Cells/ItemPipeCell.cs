using UnityEngine;

public sealed class ItemPipeCell : ConnectableCellBase
{
    private void OnConnected()
    {
        
    }
}
enum PipeIOType
{
    None,
    Input,
    Output,
    Both,
}