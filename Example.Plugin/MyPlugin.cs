using InfoPanel.Contract;
using Prise.Plugin;

namespace Example.Plugin
{
    [Plugin(PluginType = typeof(IPanelData))]
    public class MyPlugin : IPanelData
    {
        public string GetName()
        {
            return "My Plugin Name";
        }

        public Task<PanelData> GetData()
        {
            var rnd = new Random();

            List<Entry> entries =
            [
                new Entry() { Guid = Guid.Parse("13641d91-5483-4db6-bb7e-c5f5442275d4"), Name = $"Entry-1", Value = rnd.NextDouble().ToString() },
                new Entry() { Guid = Guid.Parse("4fbca7bb-8bf5-458a-8b40-1b83ba1b41d6"), Name = $"Entry-2", Value = rnd.NextDouble().ToString() },
                new Entry() { Guid = Guid.Parse("e889f8c9-0481-4eaa-bbcd-39f1fdcd2c74"), Name = $"Entry-3", Value = rnd.NextDouble().ToString() },
                new Entry() { Guid = Guid.Parse("3ee3f4c7-5bb2-4864-b464-bfee1f5305b1"), Name = $"Entry-4", Value = rnd.NextDouble().ToString() },
                new Entry() { Guid = Guid.Parse("d71cdbd3-4e71-4cb5-8eca-9864a9e0ed88"), Name = $"Entry-5", Value = rnd.NextDouble().ToString() },
            ];

            var data = new PanelData()
            {
                CollectionName = "Collection-Name",
                EntryList = entries
            };

            File.Create("C:\\Users\\Habib\\VisualStudioProjects\\infopanel\\Example.Plugin\\bin\\Debug\\net8.0\\test.txt");

            return Task.FromResult(data);
        }

        public Guid GetPluginId()
        {
            return Guid.Parse("22a54773-8c5c-4b30-ae6d-466be7cbb915");
        }
    }
}