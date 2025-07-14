public interface IDataProvidable
{
    public bool IsUIActive { set; }
    public IUIDataProvider GetDataProvider();
}