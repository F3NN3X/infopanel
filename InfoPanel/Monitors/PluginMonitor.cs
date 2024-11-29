using LibreHardwareMonitor.Hardware;
using Microsoft.VisualBasic.Devices;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System;
using System.Diagnostics;

using System.Timers;
using System.Threading;
using System.Linq;
using InfoPanel.Contract;
using Prise;
using System.IO;
using System.Windows.Media;
namespace InfoPanel.Monitors
{
    internal class PluginMonitor
    {
        private static System.Timers.Timer aTimer = new(1000);
        private static ConcurrentDictionary<Guid, string> HEADER_DICT = new();
        public static ConcurrentDictionary<Guid, PluginData> SENSORHASH = new();

        private static IPluginLoader PluginLoader;
        private static Thread? CoreThread;

        private static volatile bool IsOpen = false;
        private static readonly object _lock = new();

        public static bool Launch(IPluginLoader pluginLoader)
        {
            if (CoreThread != null)
            {
                return true;
            }

            PluginLoader = pluginLoader;

            CoreThread = new Thread(TimedStart);
            CoreThread.Start();

            return true;
        }

        private static IEnumerable<IPanelData>? Plugins;

        private static async void Prepare()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var pluginPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InfoPanel", "plugins");
            var scanResult = await PluginLoader.FindPlugin<IPanelData>(pluginPath);
          
            Plugins = await PluginLoader.LoadPlugins<IPanelData>(scanResult);
            stopwatch.Stop();
            Trace.WriteLine($"Plugins loaded: {stopwatch.ElapsedMilliseconds}ms");

            aTimer.Start();
        }

        public static void TimedStart()
        {
            Prepare();
            aTimer.Elapsed += new ElapsedEventHandler(PollSensorData);
            aTimer.Enabled = true;
        }

        private static volatile bool polling = false;

        private static void PollSensorData(object? source, ElapsedEventArgs e)
        {
            if (polling)
            {
                return;
            }

            polling = true;
            Monitor();
            polling = false;
        }

        private static UpdateVisitor updateVisitor = new();

        public async static void Monitor()
        {
            if(Plugins != null)
            {
                foreach (var plugin in Plugins)
                {
                    var data = await plugin.GetData();
                    if (data != null)
                    {
                        foreach(var entry in data.EntryList)
                        {
                            SENSORHASH[entry.Guid] = new PluginData
                            {
                                PluginId = plugin.GetPluginId(),
                                PluginName = plugin.GetName(),
                                EntryId = entry.Guid,
                                CollectionName = data.CollectionName,
                                Name = entry.Name,
                                Value = entry.Value
                            };
                        }


                    }
                }
            }
        }

        public static List<PluginData> GetOrderedList()
        {
            List<PluginData> OrderedList = [.. SENSORHASH.Values.OrderBy(x => x.PluginName).ThenBy(x => x.CollectionName)];
            return OrderedList;
        }

        public struct PluginData
        {
            public Guid PluginId;
            public string PluginName;
            public Guid EntryId;
            public string CollectionName;
            public string Name;
            public string Value;
        }
    }
}
