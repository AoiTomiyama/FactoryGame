using System.Collections.Generic;
using UnityEngine;

public interface IExportable
{
    [Tooltip("輸送先の経路リスト")]
    public HashSet<(int length, List<ConnectableCellBase> path)> ExportPaths { get; }

    public void AddPath(int length, List<ConnectableCellBase> path);
    public void RefreshPath();
    
    public ResourceType ResourceType { get; }
}