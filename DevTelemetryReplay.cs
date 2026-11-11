using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using SCSSdkClient.Object;

namespace ETSOverlay
{
    /// <summary>
    /// Development builds only: TSW_DEV_TELEMETRY_SCRIPT plays telemetry frames from a file into the
    /// widget instead of the game, so delivery scenarios (load, deliver, pause, rollback) can be
    /// checked without driving. The game, TrucksBook and the telemetry library are not used in
    /// this mode. Release builds have no replay: <see cref="IsActive"/> is always false.
    ///
    /// Script: one JSON object per line.
    ///   {"set": {"path": value, ...}, "add": {"path": number, ...}, "repeat": n, "ms": 50,
    ///    "game": true|false, "note": "text"}
    /// "set" changes fields, "add" adds to numeric fields once per repeat, every repeat sends one
    /// frame and waits "ms". Paths are telemetry properties, e.g.
    /// "SpecialEventsValues.OnJob", "TruckValues.CurrentValues.DashboardValues.Odometer",
    /// "TrailerValues[0].Attached". "game": false makes the game process look closed (no frames
    /// are sent while it is closed). Library events (JobStarted, JobDelivered, JobCancelled, Fined)
    /// are raised on rising flags, like the telemetry library does, including flags that are
    /// already on in the first frame.
    /// </summary>
    internal static class DevTelemetryReplay
    {
#if DEBUG
        private static readonly string? ScriptPath = Environment.GetEnvironmentVariable("TSW_DEV_TELEMETRY_SCRIPT") is { Length: > 0 } p ? p : null;
#else
        private static readonly string? ScriptPath = null;
#endif

        public static bool IsActive => ScriptPath != null;

        /// <summary>The game process as the script says (replay mode only).</summary>
        public static bool GameRunning { get; private set; } = true;

        /// <summary>Starts the replay for the window. False when replay mode is off.</summary>
        public static bool Start(MainWindow window)
        {
            if (!IsActive) return false;
#if DEBUG
            var thread = new Thread(() => Run(window, ScriptPath!)) { IsBackground = true, Name = "TelemetryReplay" };
            thread.Start();
            return true;
#else
            return false;
#endif
        }

#if DEBUG
        private static void Run(MainWindow window, string path)
        {
            Thread.Sleep(1500);
            var state = new Dictionary<string, object?>(StringComparer.Ordinal);
            var lastFlags = new Dictionary<string, bool>();
            int frames = 0;
            try
            {
                foreach (var raw in File.ReadLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("note", out var note)) window.Dispatcher.Invoke(() => window.WriteLog($"[REPLAY] {note.GetString()}"));
                    if (root.TryGetProperty("game", out var game)) GameRunning = game.GetBoolean();
                    if (root.TryGetProperty("set", out var set))
                        foreach (var prop in set.EnumerateObject()) state[prop.Name] = ToValue(prop.Value);
                    int repeat = root.TryGetProperty("repeat", out var r) ? r.GetInt32() : 1;
                    int ms = root.TryGetProperty("ms", out var m) ? m.GetInt32() : 50;
                    root.TryGetProperty("add", out var add);
                    for (int i = 0; i < repeat; i++)
                    {
                        if (add.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in add.EnumerateObject())
                            {
                                double current = state.TryGetValue(prop.Name, out var v) && v != null ? System.Convert.ToDouble(v) : 0;
                                state[prop.Name] = current + prop.Value.GetDouble();
                            }
                        }
                        if (GameRunning)
                        {
                            var data = Build(state);
                            var events = LibraryEvents(data, lastFlags);
                            window.DevFeedTelemetry(data, events);
                            frames++;
                        }
                        Thread.Sleep(ms);
                    }
                }
                window.Dispatcher.Invoke(() => window.WriteLog($"[REPLAY] finished: {frames} frames"));
            }
            catch (Exception ex)
            {
                try { window.Dispatcher.Invoke(() => window.WriteLog($"[REPLAY] failed: {ex}")); } catch { }
            }
        }

        private static object? ToValue(JsonElement e) => e.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => e.GetDouble(),
            JsonValueKind.String => e.GetString(),
            _ => null
        };

        private static List<string> LibraryEvents(SCSTelemetry data, Dictionary<string, bool> last)
        {
            var events = new List<string>();
            var se = data.SpecialEventsValues;
            void Edge(string name, bool now, string ev)
            {
                last.TryGetValue(name, out bool before);
                if (now && !before) events.Add(ev);
                last[name] = now;
            }
            Edge("OnJob", se.OnJob, "JobStarted");
            Edge("JobDelivered", se.JobDelivered, "JobDelivered");
            Edge("JobCancelled", se.JobCancelled, "JobCancelled");
            Edge("Fined", se.Fined, "Fined");
            return events;
        }

        private static SCSTelemetry Build(Dictionary<string, object?> state)
        {
            var data = new SCSTelemetry();
            var trailers = new SCSTelemetry.Trailer[10];
            for (int i = 0; i < trailers.Length; i++)
            {
                trailers[i] = new SCSTelemetry.Trailer();
                SetPath(trailers[i], "DamageValues.Body", 0f);
            }
            SetMember(data, "TrailerValues", trailers);
            SetPath(data, "SpecialEventsValues.OnJob", false);
            SetPath(data, "GamePlay.JobDelivered.Revenue", 0L);
            SetPath(data, "GamePlay.FinedEvent.Amount", 0L);
            SetPath(data, "CommonValues.GameTime.Value", 0u);
            SetPath(data, "NavigationValues.NavigationDistance", 0f);
            SetPath(data, "JobValues.CargoValues.Id", "");
            SetPath(data, "JobValues.DeliveryTime.Value", 0u);
            SetPath(data, "TruckValues.ConstantsValues.Id", "");
            SetPath(data, "TruckValues.CurrentValues.DashboardValues.Speed.Value", 0f);
            SetPath(data, "TruckValues.CurrentValues.DashboardValues.FuelValue.Amount", 0f);
            SetPath(data, "TruckValues.CurrentValues.PositionValue.Position.X", 0.0);
            SetPath(data, "TruckValues.CurrentValues.DamageValues.Cabin", 0f);
            foreach (var (path, value) in state) SetPath(data, path, value);
            return data;
        }

        private static void SetPath(object root, string path, object? value)
        {
            object current = root;
            var parts = path.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                int index = -1;
                int bracket = part.IndexOf('[');
                if (bracket > 0)
                {
                    index = int.Parse(part[(bracket + 1)..part.IndexOf(']')]);
                    part = part[..bracket];
                }
                bool last = i == parts.Length - 1;
                var (type, getter, setter) = Member(current.GetType(), part);
                if (index >= 0)
                {
                    var array = (Array)getter(current)!;
                    if (last) { array.SetValue(Convert(value, type.GetElementType()!), index); return; }
                    current = array.GetValue(index)!;
                    continue;
                }
                if (last) { setter(current, Convert(value, type)); return; }
                var child = getter(current);
                if (child == null)
                {
                    child = Activator.CreateInstance(type)!;
                    setter(current, child);
                }
                current = child;
            }
        }

        private static void SetMember(object target, string name, object value)
        {
            var (_, _, setter) = Member(target.GetType(), name);
            setter(target, value);
        }

        private static (Type type, Func<object, object?> get, Action<object, object?> set) Member(Type type, string name)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var prop = type.GetProperty(name, Flags);
            if (prop != null)
            {
                var setMethod = prop.GetSetMethod(true);
                return (prop.PropertyType, o => prop.GetValue(o), (o, v) =>
                {
                    if (setMethod != null) setMethod.Invoke(o, new[] { v });
                    else type.GetField($"<{name}>k__BackingField", Flags)!.SetValue(o, v);
                });
            }
            var field = type.GetField(name, Flags) ?? throw new MissingMemberException(type.Name, name);
            return (field.FieldType, o => field.GetValue(o), (o, v) => field.SetValue(o, v));
        }

        private static object? Convert(object? value, Type type)
        {
            if (value == null) return type.IsValueType ? Activator.CreateInstance(type) : null;
            if (type.IsEnum) return value is string s ? Enum.Parse(type, s, true) : Enum.ToObject(type, System.Convert.ToInt64(value));
            if (type == typeof(string)) return value.ToString();
            return System.Convert.ChangeType(value, type, System.Globalization.CultureInfo.InvariantCulture);
        }
#endif
    }
}
