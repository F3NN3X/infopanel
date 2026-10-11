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
    /// HTTP API (http://{host}:{port}/api/push, from the DataBridge settings;
    /// default 127.0.0.1:3921) so web dashboards can consume the full native
    /// dataset.
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

            // 2) LibreHardwareMonitor values. "{hardware} {sensor}" is not unique
            //    (e.g. "GPU Core" is a temperature, a clock and a load), so the
            //    keys are disambiguated deterministically — see LibreKeys.
            foreach (var (key, name, sensor) in LibreKeys())
            {
                try
                {
                    sensors[key] = new
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
                    sensors[key] = new { text = FlattenTable(table.Value), name = display };
                }
            }

            if (sensors.Count == 0) return;

            var pushUrl = GetPushUrl();
            if (pushUrl == null) return;

            var payload = JsonSerializer.Serialize(new { source = "infopanel", sensors });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            await _http.PostAsync(pushUrl, content, token);
        }

        // Which reading keeps the bare "libre.{slug}" key when several sensor
        // types share a name. Temperature first: that is what the existing
        // dashboard layouts bind the bare GPU/CPU keys to.
        private static readonly SensorType[] LibreKeyPrecedence =
        [
            SensorType.Temperature, SensorType.Load, SensorType.Power, SensorType.Clock,
            SensorType.Fan, SensorType.Control, SensorType.Voltage, SensorType.Current,
            SensorType.Throughput, SensorType.Data, SensorType.SmallData,
        ];

        /// <summary>
        /// Assigns every LibreHardwareMonitor sensor a unique, stable key. The
        /// highest-precedence sensor of a name keeps "libre.{slug}" (so existing
        /// layouts keep working); the others get "_{sensortype}" appended, and
        /// "_{index}" on top if that still collides.
        /// </summary>
        private static List<(string Key, string Name, ISensor Sensor)> LibreKeys()
        {
            var named = new List<(string Slug, string Name, ISensor Sensor)>();
            foreach (var sensor in LibreMonitor.SENSORHASH.Values)
            {
                try
                {
                    string name = CleanName($"{sensor.Hardware.Name} {sensor.Name}".Trim());
                    string slug = Slug(name);
                    if (slug.Length > 0) named.Add((slug, name, sensor));
                }
                catch
                {
                    // Skip malformed entries
                }
            }

            static int Rank(SensorType type)
            {
                int i = Array.IndexOf(LibreKeyPrecedence, type);
                return i < 0 ? LibreKeyPrecedence.Length : i;
            }

            var result = new List<(string, string, ISensor)>(named.Count);
            var used = new HashSet<string>();
            foreach (var group in named.GroupBy(n => n.Slug))
            {
                var ordered = group
                    .OrderBy(n => Rank(n.Sensor.SensorType))
                    .ThenBy(n => n.Sensor.Identifier.ToString(), StringComparer.Ordinal);

                bool first = true;
                foreach (var (slug, name, sensor) in ordered)
                {
                    string key = $"libre.{slug}";
                    if (!first)
                    {
                        key = $"{key}_{sensor.SensorType.ToString().ToLowerInvariant()}";
                        if (used.Contains(key)) key = $"{key}_{sensor.Index}";
                    }
                    first = false;
                    used.Add(key);
                    result.Add((key, name, sensor));
                }
            }
            return result;
        }

        /// <summary>
        /// Flattens a plugin table to multi-line text: one line per row, cells
        /// joined with ", " (the same shape as the bridge's own forecast text).
        /// </summary>
        private static string FlattenTable(System.Data.DataTable? table)
        {
            if (table == null || table.Rows.Count == 0) return "-";

            var lines = new List<string>(table.Rows.Count);
            foreach (System.Data.DataRow row in table.Rows)
            {
                var cells = new string[table.Columns.Count];
                for (int i = 0; i < cells.Length; i++)
                {
                    cells[i] = row[i] switch
                    {
                        IPluginText text => text.Value,
                        IPluginSensor sensor => sensor.ToString() ?? "",
                        var other => other?.ToString() ?? ""
                    };
                }
                lines.Add(string.Join(", ", cells));
            }
            return string.Join('\n', lines);
        }

        /// <summary>
        /// Builds the push endpoint from the DataBridge host/port settings,
        /// read every cycle so a change applies without a restart. Returns
        /// null when the configured target is not a usable address.
        /// </summary>
        private static Uri? GetPushUrl()
        {
            var settings = ConfigModel.Instance.Settings;
            var host = settings.DataBridgeExportHost?.Trim();
            var port = settings.DataBridgeExportPort;

            if (string.IsNullOrEmpty(host) || port is < 1 or > 65535) return null;

            try
            {
                return new UriBuilder(Uri.UriSchemeHttp, host, port, "/api/push").Uri;
            }
            catch (UriFormatException)
            {
                return null;
            }
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
