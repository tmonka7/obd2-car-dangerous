using System.Text;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace obd_car_dangerous.Services.Obd
{
    /// <summary>
    /// ELM327 over Bluetooth Classic (serial port profile). Windows can expose a paired SPP adapter
    /// as a COM port, but going straight to RFCOMM means the adapter can be picked by name, paired
    /// from inside the app, and used without any COM port set up at all.
    /// </summary>
    internal sealed class RfcommObdTransport : IObdTransport
    {
        private readonly string deviceId;
        private readonly TimeSpan connectTimeout;
        private readonly object gate = new();
        private readonly StringBuilder inbox = new();
        private readonly AutoResetEvent dataArrived = new(false);

        private StreamSocket? socket;
        private DataWriter? writer;
        private DataReader? reader;
        private CancellationTokenSource? pump;

        public RfcommObdTransport(string deviceId, string name, TimeSpan? connectTimeout = null)
        {
            this.deviceId = deviceId;
            Name = name;
            this.connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(10);
        }

        public string Name { get; }

        public bool IsOpen { get; private set; }

        public void Open()
        {
            RfcommDeviceService? service = Wait(RfcommDeviceService.FromIdAsync(deviceId), connectTimeout);
            if (service is null)
            {
                throw new IOException("Bluetooth serial service not available - is the adapter paired and powered?");
            }

            socket = new StreamSocket();
            Task connect = socket.ConnectAsync(service.ConnectionHostName, service.ConnectionServiceName).AsTask();
            if (!connect.Wait(connectTimeout))
            {
                throw new IOException("Adapter did not accept the connection");
            }

            if (connect.IsFaulted)
            {
                throw new IOException(connect.Exception?.GetBaseException().Message ?? "Connection failed");
            }

            writer = new DataWriter(socket.OutputStream);
            reader = new DataReader(socket.InputStream) { InputStreamOptions = InputStreamOptions.Partial };

            pump = new CancellationTokenSource();
            _ = Task.Run(() => PumpAsync(pump.Token));

            IsOpen = true;
        }

        /// <summary>Background read loop: everything the adapter sends lands in the inbox.</summary>
        private async Task PumpAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && reader is not null)
                {
                    uint read = await reader.LoadAsync(256).AsTask(token).ConfigureAwait(false);
                    if (read == 0)
                    {
                        continue;
                    }

                    var bytes = new byte[read];
                    reader.ReadBytes(bytes);

                    lock (gate)
                    {
                        inbox.Append(Encoding.ASCII.GetString(bytes));
                    }

                    dataArrived.Set();
                }
            }
            catch (Exception)
            {
                // Socket closed or the adapter went out of range; ReadUntilPrompt will time out.
            }
        }

        public void Write(string text)
        {
            if (writer is null)
            {
                throw new IOException("Adapter is not open");
            }

            writer.WriteBytes(Encoding.ASCII.GetBytes(text));
            writer.StoreAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
        }

        public string ReadUntilPrompt(TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;

            while (DateTime.UtcNow < deadline)
            {
                lock (gate)
                {
                    int prompt = inbox.ToString().IndexOf('>');
                    if (prompt >= 0)
                    {
                        string response = inbox.ToString(0, prompt);
                        inbox.Remove(0, prompt + 1);
                        return response;
                    }
                }

                dataArrived.WaitOne(20);
            }

            lock (gate)
            {
                string partial = inbox.ToString();
                inbox.Clear();
                return partial;
            }
        }

        public void DiscardBuffers()
        {
            lock (gate)
            {
                inbox.Clear();
            }
        }

        public void Close()
        {
            pump?.Cancel();
            pump = null;

            writer?.Dispose();
            reader?.Dispose();
            socket?.Dispose();

            writer = null;
            reader = null;
            socket = null;
            IsOpen = false;
        }

        public void Dispose()
        {
            try
            {
                Close();
            }
            catch (Exception)
            {
                // Already gone; nothing to release.
            }

            dataArrived.Dispose();
        }

        // ---- discovery and pairing -------------------------------------------

        /// <summary>Paired Bluetooth Classic devices that offer a serial port service.</summary>
        public static List<(string Id, string Name)> PairedDevices()
        {
            try
            {
                DeviceInformationCollection? found = Wait(
                    DeviceInformation.FindAllAsync(RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort)),
                    TimeSpan.FromSeconds(6));

                return found is null
                    ? new List<(string, string)>()
                    : found.Where(d => !string.IsNullOrWhiteSpace(d.Name))
                        .Select(d => (d.Id, CleanName(d.Name)))
                        .ToList();
            }
            catch (Exception)
            {
                return new List<(string, string)>();
            }
        }

        /// <summary>
        /// Bluetooth Classic devices in range that are not paired yet. Windows runs an inquiry for
        /// this, which takes a few seconds.
        /// </summary>
        public static async Task<List<(string Id, string Name)>> ScanAsync()
        {
            try
            {
                DeviceInformationCollection found = await DeviceInformation
                    .FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(false))
                    .AsTask()
                    .ConfigureAwait(false);

                return found
                    .Where(d => !string.IsNullOrWhiteSpace(d.Name))
                    .Select(d => (d.Id, CleanName(d.Name)))
                    .ToList();
            }
            catch (Exception)
            {
                return new List<(string, string)>();
            }
        }

        /// <summary>
        /// Pairs an adapter using the PINs the clones ship with, so the user does not have to go
        /// through Windows Bluetooth settings. Returns true when the device ends up paired.
        /// </summary>
        public static async Task<bool> TryPairAsync(string deviceId, IEnumerable<string> pins)
        {
            DeviceInformation device;
            try
            {
                device = await DeviceInformation.CreateFromIdAsync(deviceId).AsTask().ConfigureAwait(false);
            }
            catch (Exception)
            {
                return false;
            }

            if (device.Pairing.IsPaired)
            {
                return true;
            }

            foreach (string pin in pins)
            {
                DeviceInformationCustomPairing custom = device.Pairing.Custom;

                void OnPairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
                {
                    if (args.PairingKind == DevicePairingKinds.ProvidePin)
                    {
                        args.Accept(pin);
                    }
                    else
                    {
                        args.Accept();
                    }
                }

                custom.PairingRequested += OnPairingRequested;
                try
                {
                    DevicePairingResult result = await custom
                        .PairAsync(DevicePairingKinds.ProvidePin | DevicePairingKinds.ConfirmOnly)
                        .AsTask()
                        .ConfigureAwait(false);

                    if (result.Status is DevicePairingResultStatus.Paired or DevicePairingResultStatus.AlreadyPaired)
                    {
                        return true;
                    }
                }
                catch (Exception)
                {
                    // Try the next PIN.
                }
                finally
                {
                    custom.PairingRequested -= OnPairingRequested;
                }
            }

            return false;
        }

        /// <summary>Windows appends the service name to RFCOMM entries; keep just the device name.</summary>
        private static string CleanName(string name)
        {
            int dash = name.IndexOf(" - ", StringComparison.Ordinal);
            return dash > 0 ? name[..dash] : name;
        }

        private static T? Wait<T>(Windows.Foundation.IAsyncOperation<T> operation, TimeSpan? timeout = null)
        {
            Task<T> task = operation.AsTask();
            return task.Wait(timeout ?? TimeSpan.FromSeconds(10)) ? task.Result : default;
        }
    }
}
