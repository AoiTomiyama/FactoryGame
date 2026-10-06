using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public enum ResourceAnimationResultApplication { Completed, Cancelled, MissingVisual }

/// <summary>資源数量を変更せず、搬送の表示と演出結果だけを扱う。</summary>
public interface IResourceTransferPresentationApplication
{
    UniTask<ResourceAnimationResultApplication> Transfer(CancellationToken token, Vector3 from, Vector3 to, int id);
    void SetPosition(int id, Vector3 position);
    bool ReleaseVisual(int id);
}
