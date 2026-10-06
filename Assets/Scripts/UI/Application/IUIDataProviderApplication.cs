using System.Collections.Generic;

public interface IUIDataProviderApplication
{
    IReadOnlyDictionary<LabelEnumApplication, UIElementDataBaseApplication> ReadData();
}
