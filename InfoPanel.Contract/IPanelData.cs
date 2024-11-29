namespace InfoPanel.Contract
{
    public interface IPanelData //interface that both the plugin loader and each plugin must implement
    {
        Guid GetPluginId();
        string GetName();
        Task<PanelData> GetData();
    }
}
