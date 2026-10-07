using InfoPanel.Monitors;
using InfoPanel.Plugins;
using LibreHardwareMonitor.Hardware;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace InfoPanel
{
    /// <summary>
    /// Periodically exports every aggregated native sensor value (HWiNFO,
    /// LibreHardwareMonitor and all plugin sensors) to the InfoPanel.DataBridge
    /// HTTP API (http://127.0.0.1:3921/api/push) so web dashboards can consume
    /// the full native dataset.
    ///
    /// The bridge is an optional add-on, so every push failure (bridge not
    /// running, network error, ...) is swallowed silently.
    /// </summary>
    public sealed class DataBridgeExportTask : BackgroundTask
    {
        private static readonly ILogger Logger = Log.ForContext<DataBridgeExportTask>();
        private static readonly Lazy<DataBridgeExportTask> _instance = new(() => new DataBridgeExportTask());
        public static DataBridgeExportTask Instance => _instance.Value;

        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };

        private const string PushUrl = "http://127.0.0.1:3921/api/push";
        private const int IntervalMs = 2000;

        private DataBridgeExportTask() { }

        protected override async Task DoWorkAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await PushSnapshotAsync(token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Bridge not running / transient error — stay silent, retry next cycle
                    Logger.Debug("DataBridge export skipped: {Reason}", ex.Message);
                }

                try
                {
                    await Task.Delay(IntervalMs, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private static async Task PushSnapshotAsync(CancellationToken token)
        {
            var sensors = new Dictionary<string, object>();

            // 1) HWiNFO values (local + remote)
            foreach (var reading in HWHash.SENSORHASH.Values)
            {
                string name = CleanName(!string.IsNullOrWhiteSpace(reading.NameCustom) ? reading.NameCustom : reading.NameDefault);
                string slug = Slug(name);
                if (slug.Length == 0) continue;

                string key = reading.RemoteIndex >= 0
                    ? $"hwinfo.r{reading.RemoteIndex}_{slug}"
                    : $"hwinfo.{slug}";
                string display = reading.RemoteIndex >= 0
                    ? $"Remote {reading.RemoteIndex}: {name}"
                    : name;

                sensors[key] = new
                {
                    value = reading.ValueNow,
                    unit = reading.Unit ?? "",
                    min = reading.ValueMin,
                    max = reading.ValueMax,
                    avg = reading.ValueAvg,
                    name = display
                };
            }

            // 2) LibreHardwareMonitor values
            foreach (var (id, sensor) in LibreMonitor.SENSORHASH)
            {
                try
                {
                    string name = CleanName($"{sensor.Hardware.Name} {sensor.Name}".Trim());
                    string slug = Slug(name);
                    if (slug.Length == 0) continue;

                    sensors[$"libre.{slug}"] = new
                    {
                        value = sensor.Value ?? 0,
                        min = (double?)(sensor.Min ?? 0),
                        max = (double?)(sensor.Max ?? 0),
                        unit = sensor.GetUnit() ?? "",
                        name = name
                    };
                }
                catch
                {
                    // Skip malformed entries
                }
            }

            // 3) Plugin sensors — the DataBridge's own sensors are already in its
            //    store, so skip them to avoid duplicates.
            foreach (var (hashKey, reading) in PluginMonitor.SENSORHASH)
            {
                if (string.Equals(reading.PluginId, "data-bridge", StringComparison.OrdinalIgnoreCase)) continue;

                string key = "plugins." + hashKey.TrimStart('/').Replace('/', '.');
                string display = CleanName(reading.Name);
                string container = CleanName(reading.ContainerName);
                if (!string.IsNullOrWhiteSpace(container) && container != display)
                {
                    display = $"{container} · {display}";
                }

                if (reading.Data is IPluginSensor sensor)
                {
                    var fields = new Dictionary<string, object>
                    {
                        ["value"] = (double)sensor.Value,
                        ["unit"] = sensor.Unit ?? "",
                        ["name"] = display
                    };
                    if (sensor.ValueMin > 0) fields["min"] = sensor.ValueMin;
                    if (sensor.ValueMax > 0) fields["max"] = sensor.ValueMax;
                    if (sensor.ValueAvg > 0) fields["avg"] = sensor.ValueAvg;
                    sensors[key] = fields;
                }
                else if (reading.Data is IPluginText text)
                {
                    sensors[key] = new { text = text.Value, name = display };
                }
                else if (reading.Data is IPluginTable table)
                {
                    sensors[key] = new { text = table.ToString(), name = display };
                }
            }

            if (sensors.Count == 0) return;

            var payload = JsonSerializer.Serialize(new { source = "infopanel", sensors });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            await _http.PostAsync(PushUrl, content, token);
        }

        /// <summary>
        /// Removes control characters (null terminators, ...) and collapses
        /// whitespace. Some LibreHardwareMonitor and HWiNFO sensor names carry
        /// raw null bytes from the underlying shared memory.
        /// </summary>
        private static string CleanName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            string cleaned = new string(value.Where(c => c >= 0x20).ToArray());
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            return cleaned;
        }

        /// <summary>Turns an arbitrary display name into a stable sensor-key slug.</summary>
        private static string Slug(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var chars = new char[value.Length];
            for (int i = 0; i < value.Length; i++)
            {
                char c = char.ToLowerInvariant(value[i]);
                chars[i] = char.IsLetterOrDigit(c) ? c : '_';
            }

            return new string(chars).Trim('_');
        }
    }
}
