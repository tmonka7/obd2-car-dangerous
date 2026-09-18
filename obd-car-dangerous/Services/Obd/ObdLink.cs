using System.Collections.Concurrent;

namespace obd_car_dangerous.Services.Obd
{
    internal enum EndpointKind
    {
        /// <summary>USB cable or a Bluetooth Classic (SPP) pairing, both of which are COM ports.</summary>
        Serial,

        /// <summary>Bluetooth Low Energy adapter, reached over GATT - no COM port involved.</summary>
        Ble,

        Demo,
    }

    /// <summary>Somewhere an adapter might be: a COM port, a BLE device, or the built in demo feed.</summary>
    internal sealed record ObdEndpoint(string Name, EndpointKind Kind, string Address, int Baud = 38400)
    {
        public string Transport => Kind switch
        {
            EndpointKind.Serial => "USB / Bluetooth SPP",
            EndpointKind.Ble => "Bluetooth LE",
            _ => "Demo",
        };
    }

    /// <summary>Latest values read from the car. Floats are written by the worker, read by the UI.</summary>
    internal sealed class ObdSnapshot
    {
        private readonly ConcurrentDictionary<string, float> values = new();

        public DateTime LastUpdate { get; set; }

        public float? Get(string key) => values.TryGetValue(key, out float value) ? value : null;

        public void Set(string key, float value)
        {
            values[key] = value;
            LastUpdate = DateTime.Now;
        }

        public bool Has(string key) => values.ContainsKey(key);

        public void Clear() => values.Clear();
    }

    /// <summary>
    /// Owns the adapter connection and the polling loop. One worker thread does all ELM327 traffic;
    /// the UI reads <see cref="Snapshot"/> and queues one-off jobs through <see cref="RequestAsync"/>.
    /// </summary>
    internal sealed class ObdLink : IDisposable
    {
        private readonly ConcurrentQueue<Job> jobs = new();
        private readonly object gate = new();

        private Thread? worker;
        private volatile bool running;
        private Elm327? device;
        private int responsesOk;
        private int responsesFailed;

        /// <summary>Raised on the worker thread whenever the connection state or status text changes.</summary>
        public event EventHandler? Changed;

        public ObdSnapshot Snapshot { get; } = new();

        public bool IsConnected { get; private set; }

        public bool IsBusy { get; private set; }

        public ObdEndpoint? Endpoint { get; private set; }

        public string Status { get; private set; } = string.Empty;

        public string Firmware { get; private set; } = string.Empty;

        public string Protocol { get; private set; } = string.Empty;

        public string? Vin { get; private set; }

        public string? CalibrationId { get; private set; }

        public IReadOnlyCollection<byte> SupportedPids { get; private set; } = Array.Empty<byte>();

        /// <summary>Share of PID requests the car answered, used as a link quality indicator.</summary>
        public int ResponseRate
        {
            get
            {
                int total = responsesOk + responsesFailed;
                return total == 0 ? 0 : (int)(responsesOk * 100f / total);
            }
        }

        // ---- discovery -------------------------------------------------------

        /// <summary>
        /// Adapters we can offer: every COM port (USB cable or Bluetooth SPP pairing) and, when
        /// <paramref name="includeBluetooth"/> is set, the paired Bluetooth LE devices. Enumerating
        /// BLE takes a moment, so the UI only asks for it off the UI thread.
        /// </summary>
        public static List<ObdEndpoint> Discover(bool includeBluetooth = true)
        {
            var found = new List<ObdEndpoint>();

            foreach (string port in SerialObdTransport.PortNames())
            {
                found.Add(new ObdEndpoint(port, EndpointKind.Serial, port));
            }

            if (includeBluetooth)
            {
                List<(string Id, string Name)> paired = BleObdTransport.PairedDevices();

                // Show likely adapters only; if nothing looks like one, show everything paired so
                // an oddly named dongle can still be picked.
                var likely = paired.Where(d => BleObdTransport.LooksLikeAdapter(d.Name)).ToList();
                foreach ((string id, string name) in likely.Count > 0 ? likely : paired)
                {
                    found.Add(new ObdEndpoint(name, EndpointKind.Ble, id, 0));
                }
            }

            found.Add(new ObdEndpoint("Demo (simulated)", EndpointKind.Demo, "demo"));
            return found;
        }

        /// <summary>Advertisement scan, which also finds BLE adapters that are not paired yet.</summary>
        public static async Task<List<ObdEndpoint>> ScanBluetoothAsync(TimeSpan duration)
        {
            List<(string Id, string Name)> seen = await BleObdTransport.ScanAsync(duration).ConfigureAwait(false);

            return seen
                .Where(d => BleObdTransport.LooksLikeAdapter(d.Name))
                .Select(d => new ObdEndpoint(d.Name, EndpointKind.Ble, d.Id, 0))
                .ToList();
        }

        // ---- connect / disconnect -------------------------------------------

        /// <summary>
        /// Connects on a background thread. <paramref name="progress"/> receives the same short
        /// messages the splash screen shows. Returns true once live data is flowing.
        /// </summary>
        /// <param name="quickProbe">
        /// True while hunting for an adapter at start up: only the two usual baud rates are tried and
        /// the handshake window is short, so a printer on COM3 costs a second rather than a minute.
        /// </param>
        public Task<bool> ConnectAsync(ObdEndpoint endpoint, IProgress<string>? progress = null, bool quickProbe = false)
        {
            Disconnect();

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Endpoint = endpoint;
            IsBusy = true;
            SetStatus($"Connecting to {endpoint.Name}");

            running = true;
            worker = new Thread(() => Run(endpoint, progress, completion, quickProbe))
            {
                IsBackground = true,
                Name = "OBD2 link",
            };
            worker.Start();

            return completion.Task;
        }

        public void Disconnect()
        {
            running = false;
            Thread? current = worker;
            worker = null;

            if (current is not null && current.IsAlive && current != Thread.CurrentThread)
            {
                current.Join(TimeSpan.FromSeconds(3));
            }

            lock (gate)
            {
                device?.Dispose();
                device = null;
            }

            while (jobs.TryDequeue(out Job? job))
            {
                job.Fail(new OperationCanceledException("Adapter disconnected"));
            }

            IsConnected = false;
            IsBusy = false;
            Snapshot.Clear();
            SetStatus("Disconnected");
        }

        /// <summary>Runs a one-off command (read codes, clear codes, VIN) on the worker thread.</summary>
        public Task<T> RequestAsync<T>(Func<Elm327, T> work)
        {
            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (!IsConnected)
            {
                completion.SetException(new InvalidOperationException("No adapter connected"));
                return completion.Task;
            }

            jobs.Enqueue(new Job(elm => completion.SetResult(work(elm)), completion.SetException));
            return completion.Task;
        }

        // ---- worker ----------------------------------------------------------

        private void Run(ObdEndpoint endpoint, IProgress<string>? progress, TaskCompletionSource<bool> completion, bool quickProbe)
        {
            Elm327? elm = null;

            try
            {
                elm = OpenAdapter(endpoint, progress, quickProbe);
                if (elm is null)
                {
                    IsBusy = false;
                    SetStatus("No adapter found");
                    completion.TrySetResult(false);
                    return;
                }

                lock (gate)
                {
                    device = elm;
                }

                Firmware = elm.Firmware;
                Protocol = elm.Protocol;
                IsConnected = true;
                IsBusy = false;

                progress?.Report($"{elm.Firmware} on {endpoint.Name}");
                SetStatus("Connected");

                progress?.Report("Reading supported sensors");
                SupportedPids = elm.SupportedPids();

                progress?.Report("Reading vehicle information");
                Vin = elm.ReadVin();
                CalibrationId = elm.ReadCalibrationId();

                completion.TrySetResult(true);

                Poll(elm);
            }
            catch (Exception ex)
            {
                SetStatus($"Link lost: {ex.Message}");
                completion.TrySetResult(false);
            }
            finally
            {
                lock (gate)
                {
                    if (ReferenceEquals(device, elm))
                    {
                        device = null;
                    }
                }

                elm?.Dispose();
                IsConnected = false;
                IsBusy = false;
                running = false;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Opens the endpoint, trying the usual baud rates for a serial adapter.</summary>
        private Elm327? OpenAdapter(ObdEndpoint endpoint, IProgress<string>? progress, bool quickProbe)
        {
            TimeSpan handshake = quickProbe ? TimeSpan.FromMilliseconds(1200) : TimeSpan.FromSeconds(3);

            if (endpoint.Kind == EndpointKind.Ble)
            {
                // BLE needs a longer handshake than a COM port: connecting, discovering services
                // and subscribing all happen inside Initialize.
                progress?.Report($"Connecting to {endpoint.Name} over Bluetooth LE");
                var transport = new BleObdTransport(endpoint.Address, endpoint.Name,
                    TimeSpan.FromSeconds(quickProbe ? 5 : 10));
                var elm = new Elm327(transport);

                if (elm.Initialize(out string error, TimeSpan.FromSeconds(quickProbe ? 4 : 6)))
                {
                    progress?.Report($"{endpoint.Name} ready (GATT {transport.Profile})");
                    return elm;
                }

                elm.Dispose();
                SetStatus(error);
                return null;
            }

            // Remembered baud first, then the two rates nearly every clone ships with.
            IEnumerable<int> bauds = quickProbe
                ? new[] { 38400, 115200 }
                : SerialObdTransport.CommonBaudRates;

            if (endpoint.Baud > 0)
            {
                bauds = new[] { endpoint.Baud }.Concat(bauds.Where(b => b != endpoint.Baud));
            }

            string lastError = "No ELM327 answer";
            foreach (int baud in bauds)
            {
                if (!running)
                {
                    return null;
                }

                progress?.Report($"Probing {endpoint.Address} at {baud} baud");
                var elm = new Elm327(new SerialObdTransport(endpoint.Address, baud));
                if (elm.Initialize(out string error, handshake))
                {
                    Endpoint = endpoint with { Baud = baud };
                    return elm;
                }

                lastError = error;
                elm.Dispose();
            }

            SetStatus(lastError);
            return null;
        }

        /// <summary>Reads PIDs in a rotation until the link is closed.</summary>
        private void Poll(Elm327 elm)
        {
            ObdReading[] readings = ObdPids.All
                .Where(r => SupportedPids.Count == 0 || SupportedPids.Contains(r.Pid))
                .ToArray();

            // Some cars report a wide band sensor instead of the narrow band one.
            if (!readings.Any(r => r.Key == "o2") && SupportedPids.Contains(ObdPids.WideBandO2.Pid))
            {
                readings = readings.Append(ObdPids.WideBandO2).ToArray();
            }

            long cycle = 0;

            while (running && elm.IsOpen)
            {
                while (jobs.TryDequeue(out Job? job))
                {
                    job.Run(elm);
                }

                foreach (ObdReading reading in readings)
                {
                    if (!running)
                    {
                        return;
                    }

                    bool due = reading.Rate switch
                    {
                        0 => true,
                        1 => cycle % 4 == 0,
                        _ => cycle % 20 == 0,
                    };

                    if (!due)
                    {
                        continue;
                    }

                    byte[]? data = elm.ReadPid(reading.Pid);
                    float? value = data is null ? null : reading.Decode(data);

                    if (value.HasValue && !float.IsNaN(value.Value))
                    {
                        Snapshot.Set(reading.Key, value.Value);
                        responsesOk++;
                    }
                    else
                    {
                        responsesFailed++;
                    }
                }

                // The adapter itself can report battery voltage when the ECU will not.
                if (cycle % 20 == 0 && !Snapshot.Has("battery"))
                {
                    float? volts = elm.ReadBatteryVoltage();
                    if (volts.HasValue)
                    {
                        Snapshot.Set("battery", volts.Value);
                    }
                }

                if (responsesOk + responsesFailed > 400)
                {
                    responsesOk /= 2;
                    responsesFailed /= 2;
                }

                cycle++;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        private void SetStatus(string status)
        {
            Status = status;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() => Disconnect();

        private sealed class Job
        {
            private readonly Action<Elm327> work;
            private readonly Action<Exception> onError;

            public Job(Action<Elm327> work, Action<Exception> onError)
            {
                this.work = work;
                this.onError = onError;
            }

            public void Run(Elm327 elm)
            {
                try
                {
                    work(elm);
                }
                catch (Exception ex)
                {
                    onError(ex);
                }
            }

            public void Fail(Exception ex) => onError(ex);
        }
    }
}
