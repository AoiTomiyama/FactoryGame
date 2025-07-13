using System.Collections.Generic;

public interface ICellDataProvider
{
    public Dictionary<Label, UIElementDataBase> CreateUIElementData();
    public void UpdateData(Label label, UIElementDataBase data);
}