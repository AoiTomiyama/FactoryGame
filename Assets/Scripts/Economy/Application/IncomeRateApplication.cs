using System;

/// <summary>一プレイの確定収入を実時間で計測する。時刻は呼び出し側から受け取る。</summary>
public sealed class IncomeRateApplication
{
    private readonly CurrencyWalletApplication _wallet;
    private double _sampleTime;
    private decimal _sampleTotal;

    public decimal IncomePerSecond { get; private set; }

    public IncomeRateApplication(CurrencyWalletApplication wallet, double startTime)
    {
        _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        _sampleTime = startTime;
        _sampleTotal = wallet.TotalIncome;
    }

    public void Advance(double now)
    {
        var elapsed = now - _sampleTime;
        if (elapsed < 1) return;

        // フレームが遅れた場合も、実際に経過した区間で平均を取る。
        var total = _wallet.TotalIncome;
        IncomePerSecond = (total - _sampleTotal) / (decimal)elapsed;
        _sampleTime = now;
        _sampleTotal = total;
    }
}
