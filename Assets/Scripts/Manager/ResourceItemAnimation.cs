using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>一つの貸出表示を移動させる。資源の確定や表示の返却は行わない。</summary>
public sealed class ResourceItemAnimation
{
    private CancellationTokenSource _moveCts;

    public void Cancel() => _moveCts?.Cancel();

    public async UniTask<ResourceAnimationResult> MoveAsync(GameObject visual, CancellationToken token,
        Vector3 from, Vector3 to, float seconds)
    {
        if (token.IsCancellationRequested) return ResourceAnimationResult.Cancelled;
        if (visual == null) return ResourceAnimationResult.MissingVisual;
        if (_moveCts != null) return ResourceAnimationResult.Cancelled;

        using var moveCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _moveCts = moveCts;
        Tween tween = null;
        var completed = false;
        var killed = false;
        try
        {
            visual.transform.position = from;
            tween = visual.transform.DOMove(to, seconds).SetEase(Ease.Linear)
                .OnComplete(() => completed = true).OnKill(() => killed = true);
            // 取消時は待機のコールバックを外してから finally で停止する。
            // OnKill 内で再入して同じ Tween を二度停止することを防ぐ。
            await tween.ToUniTask(TweenCancelBehaviour.CancelAwait, moveCts.Token);
            if (visual == null) return ResourceAnimationResult.MissingVisual;
            return completed && !moveCts.IsCancellationRequested
                ? ResourceAnimationResult.Completed : ResourceAnimationResult.Cancelled;
        }
        catch (OperationCanceledException)
        {
            return ResourceAnimationResult.Cancelled;
        }
        finally
        {
            // 中断したTweenが、次に貸し出された同じ表示を動かし続けることを防ぐ。
            if (tween != null && !killed && tween.IsActive()) tween.Kill();
            if (_moveCts == moveCts) _moveCts = null;
        }
    }
}
