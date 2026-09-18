using System.Text;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace obd_car_dangerous.Services.Obd
{
    /// <summary>
    /// ELM327 over Bluetooth Low Energy. BLE adapters never appear as a COM port: they expose a
    /// GATT service with one characteristic to write commands to and one that notifies the answer,
    /// so this speaks GATT directly through the Windows Runtime.
    /// </summary>
    internal sealed class BleObdTransport : IObdTransport
    {
        /// <summary>Service/write/notify triples used by the common BLE OBD2 adapters.</summary>
        private static readonly (Guid Service, Guid Write, Guid Notify, string Name)[] KnownProfiles =
        {
            // Vgate iCar Pro BLE, most "ELM327 v1.5 BLE 4.0" clones.
            (Short(0xFFF0), Short(0xFFF2), Short(0xFFF1), "FFF0"),

            // HM-10 style modules: one characteristic does both jobs.
            (Short(0xFFE0), Short(0xFFE1), Short(0xFFE1), "FFE0"),

            // Some Chinese adapters and vLinker units.
            (Short(0x18F0), Short(0x2AF1), Short(0x2AF0), "18F0"),

            // Nordic UART Service, used by a few newer dongles.
            (new Guid("6e400001-b5a3-f393-e0a9-e50e24dcca9e"),
             new Guid("6e400002-b5a3-f393-e0a9-e50e24dcca9e"),
             new Guid("6e400003-b5a3-f393-e0a9-e50e24dcca9e"), "Nordic UART"),
        };

        private readonly string address;
        private readonly object gate = new();
        private readonly StringBuilder inbox = new();
        private readonly AutoResetEvent dataArrived = new(false);

        private BluetoothLEDevice? device;
        private GattCharacteristic? writeCharacteristic;
        private GattCharacteristic? notifyCharacteristic;
        private GattWriteOption writeOption = GattWriteOption.WriteWithoutResponse;

        /// <param name="address">
        /// A Windows device id for a paired adapter, or "addr:XXXXXXXXXXXX" for one found by
        /// scanning, which does not need pairing.
        /// </param>
        public BleObdTransport(string address, string name, TimeSpan? connectTimeout = null)
        {
            this.address = address;
            Name = name;
            this.connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(8);
        }

        private readonly TimeSpan connectTimeout;

        public string Name { get; }

        /// <summary>Which GATT profile matched, useful when an unusual adapter misbehaves.</summary>
        public string Profile { get; private set; } = "unknown";

        public bool IsOpen { get; private set; }

        public void Open()
        {
            device = address.StartsWith("addr:", StringComparison.OrdinalIgnoreCase)
                ? Wait(BluetoothLEDevice.FromBluetoothAddressAsync(
                    ulong.Parse(address[5..], System.Globalization.NumberStyles.HexNumber)), connectTimeout)
                : Wait(BluetoothLEDevice.FromIdAsync(address), connectTimeout);

            if (device is null)
            {
                throw new IOException("Bluetooth LE device not available");
            }

            GattDeviceServicesResult services = Wait(device.GetGattServicesAsync(BluetoothCacheMode.Uncached), connectTimeout)
                ?? throw new IOException("Adapter did not answer - is it powered and in range?");

            if (services.Status != GattCommunicationStatus.Success)
            {
                throw new IOException($"GATT services unavailable ({services.Status})");
            }

            if (!TryKnownProfile(services) && !TryAnyProfile(services))
            {
                throw new IOException("No serial-style GATT characteristics on this device");
            }

            Subscribe();
            IsOpen = true;
        }

        /// <summary>Looks for one of the profiles the usual adapters ship with.</summary>
        private bool TryKnownProfile(GattDeviceServicesResult services)
        {
            foreach ((Guid serviceId, Guid writeId, Guid notifyId, string name) in KnownProfiles)
            {
                GattDeviceService? service = services.Services.FirstOrDefault(s => s.Uuid == serviceId);
                if (service is null)
                {
                    continue;
                }

                GattCharacteristicsResult characteristics = Wait(service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached))!;
                if (characteristics.Status != GattCommunicationStatus.Success)
                {
                    continue;
                }

                GattCharacteristic? write = characteristics.Characteristics.FirstOrDefault(c => c.Uuid == writeId);
                GattCharacteristic? notify = characteristics.Characteristics.FirstOrDefault(c => c.Uuid == notifyId);

                if (write is null || notify is null)
                {
                    continue;
                }

                writeCharacteristic = write;
                notifyCharacteristic = notify;
                Profile = name;
                return true;
            }

            return false;
        }

        /// <summary>Fallback: any vendor service holding a writable and a notifying characteristic.</summary>
        private bool TryAnyProfile(GattDeviceServicesResult services)
        {
            foreach (GattDeviceService service in services.Services)
            {
                GattCharacteristicsResult characteristics = Wait(service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached))!;
                if (characteristics.Status != GattCommunicationStatus.Success)
                {
                    continue;
                }

                GattCharacteristic? write = characteristics.Characteristics.FirstOrDefault(c =>
                    c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write) ||
                    c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse));

                GattCharacteristic? notify = characteristics.Characteristics.FirstOrDefault(c =>
                    c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify) ||
                    c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate));

                if (write is null || notify is null)
                {
                    continue;
                }

                writeCharacteristic = write;
                notifyCharacteristic = notify;
                Profile = service.Uuid.ToString()[..8];
                return true;
            }

            return false;
        }

        private void Subscribe()
        {
            if (writeCharacteristic is null || notifyCharacteristic is null)
            {
                return;
            }

            writeOption = writeCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse)
                ? GattWriteOption.WriteWithoutResponse
                : GattWriteOption.WriteWithResponse;

            notifyCharacteristic.ValueChanged += OnNotification;

            GattClientCharacteristicConfigurationDescriptorValue mode =
                notifyCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify)
                    ? GattClientCharacteristicConfigurationDescriptorValue.Notify
                    : GattClientCharacteristicConfigurationDescriptorValue.Indicate;

            // Some clones stream without the descriptor being written, so a failure here is not fatal.
            Wait(notifyCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(mode));
        }

        private void OnNotification(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            using var reader = DataReader.FromBuffer(args.CharacteristicValue);
            var bytes = new byte[args.CharacteristicValue.Length];
            reader.ReadBytes(bytes);

            lock (gate)
            {
                inbox.Append(Encoding.ASCII.GetString(bytes));
            }

            dataArrived.Set();
        }

        public void Write(string text)
        {
            if (writeCharacteristic is null)
            {
                throw new IOException("Adapter is not open");
            }

            byte[] payload = Encoding.ASCII.GetBytes(text);

            // BLE carries about 20 bytes per packet, so longer commands go out in pieces.
            for (int offset = 0; offset < payload.Length; offset += 20)
            {
                int length = Math.Min(20, payload.Length - offset);
                var writer = new DataWriter();
                writer.WriteBytes(payload.Skip(offset).Take(length).ToArray());
                Wait(writeCharacteristic.WriteValueAsync(writer.DetachBuffer(), writeOption));
            }
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
            if (notifyCharacteristic is not null)
            {
                notifyCharacteristic.ValueChanged -= OnNotification;
                notifyCharacteristic = null;
            }

            writeCharacteristic = null;
            device?.Dispose();
            device = null;
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
                // The adapter may already be out of range; nothing useful to do here.
            }

            dataArrived.Dispose();
        }

        // ---- discovery -------------------------------------------------------

        /// <summary>Bluetooth LE devices already paired in Windows.</summary>
        public static List<(string Id, string Name)> PairedDevices()
        {
            try
            {
                DeviceInformationCollection? found = Wait(
                    DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true)),
                    TimeSpan.FromSeconds(6));

                return found is null
                    ? new List<(string, string)>()
                    : found.Where(d => !string.IsNullOrWhiteSpace(d.Name))
                        .Select(d => (d.Id, d.Name))
                        .ToList();
            }
            catch (Exception)
            {
                // No Bluetooth radio, or the stack is unavailable.
                return new List<(string, string)>();
            }
        }

        /// <summary>
        /// Listens for advertisements, which finds adapters that are powered but not paired -
        /// most BLE ELM327 clones work without pairing.
        /// </summary>
        public static async Task<List<(string Id, string Name)>> ScanAsync(TimeSpan duration)
        {
            var seen = new Dictionary<ulong, string>();
            var watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };

            watcher.Received += (_, args) =>
            {
                string name = args.Advertisement.LocalName;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    lock (seen)
                    {
                        seen[args.BluetoothAddress] = name;
                    }
                }
            };

            try
            {
                watcher.Start();
                await Task.Delay(duration).ConfigureAwait(false);
            }
            catch (Exception)
            {
                return new List<(string, string)>();
            }
            finally
            {
                try
                {
                    watcher.Stop();
                }
                catch (Exception)
                {
                    // Radio removed mid scan.
                }
            }

            lock (seen)
            {
                return seen.Select(kv => ($"addr:{kv.Key:X12}", kv.Value)).ToList();
            }
        }

        /// <summary>True when a device name looks like an OBD2 adapter rather than a headset.</summary>
        public static bool LooksLikeAdapter(string name)
        {
            string upper = name.ToUpperInvariant();
            return upper.Contains("OBD") || upper.Contains("ELM") || upper.Contains("VGATE") ||
                   upper.Contains("ICAR") || upper.Contains("VEEPEAK") || upper.Contains("VLINKER") ||
                   upper.Contains("V-LINK") || upper.Contains("KONNWEI") || upper.Contains("VIECAR") ||
                   upper.Contains("CARISTA") || upper.Contains("LELINK") || upper.Contains("SCAN");
        }

        // ---- WinRT helpers ---------------------------------------------------

        private static Guid Short(ushort id) => new($"0000{id:X4}-0000-1000-8000-00805F9B34FB");

        /// <summary>
        /// Runs a WinRT async call from the OBD worker thread. Safe here because that thread is
        /// not the UI thread - never call this from the UI thread.
        /// </summary>
        private static T? Wait<T>(Windows.Foundation.IAsyncOperation<T> operation, TimeSpan? timeout = null)
        {
            Task<T> task = operation.AsTask();
            return task.Wait(timeout ?? TimeSpan.FromSeconds(10)) ? task.Result : default;
        }
    }
}
