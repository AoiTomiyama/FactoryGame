using System.Collections.Generic;
using UnityEngine;

public interface IContainable
{
    public int StorageAmount { get; set; }
    public List<ConnectableCellBase> networkPath { get; set; }
}
