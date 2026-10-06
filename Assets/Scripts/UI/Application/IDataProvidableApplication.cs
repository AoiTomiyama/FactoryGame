using System;

/// <summary>表示値の取得と変更通知だけの契約。表示側へ数量変更の操作を渡さない。</summary>
public interface IDataProvidableApplication
{
    event Action StatusChanged;
    IUIDataProviderApplication GetDataProvider();
}
