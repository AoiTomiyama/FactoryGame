public interface IDataProvidableApplication
{
    public bool IsUIActive { set; }
    public IUIDataProviderApplication GetDataProvider();
}