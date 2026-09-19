# OBD2 Car Dangerous System

Windows Forms (.NET 8) vehicle monitoring dashboard. Every screen from the design set is implemented
as a custom-painted page, and the app runs borderless full screen on any resolution.

## Running

```
dotnet run --project obd-car-dangerous
```

The splash screen runs the real start sequence - find adapters, open the ELM327, detect the
protocol, read the VIN and the stored fault codes - and reports each step. Click or press a key to
skip ahead; the sequence carries on behind the shell.

## Supported adapters

| Adapter | How it connects | Supported |
| --- | --- | --- |
| HH OBD Advanced (Bluetooth scan tool) | BLE version advertises as `OBDBLE` / `IOS-Vlink`; older ones are Bluetooth serial | Yes |
| Mini ELM327 (blue dongle) | Bluetooth Classic serial, PIN 1234/0000/6789 | Yes |
| "OBDII Interface" box (orange/blue) | Bluetooth Classic serial | Yes |
| ELM327 USB cable (blue, CH340/FTDI) | COM port once the chip's driver is installed | Yes |
| Autel MaxiVCI / AP200 / BT506 | Bluetooth, but Autel's own protocol | **No** - needs Autel software |
| VAG-COM KKL 409.1 cable | Looks like the USB cable above but has no ELM327 firmware | **No** - K-line only |

Anything else is tried as an ELM327: the app recognises the model where it can
(`Services/Obd/AdapterCatalog.cs`) and falls back to a generic attempt otherwise. Autel-style
interfaces are marked "Not ELM327" in the list and skipped during auto-connect, but you can still
tap one and choose "Try anyway" - if it happens to accept ELM327 commands it will work.

## Connecting a real ELM327

Tested against the protocol, not against every clone: the driver targets ELM327 v1.3-v2.x, which
covers the common v1.5 clones, over three transports.

**Bluetooth LE (BLE 4.0 dongles)** - `Services/Obd/BleObdTransport.cs`. A BLE adapter never becomes
a COM port; it exposes a GATT service with a write characteristic and a notify characteristic, so
the app speaks GATT through WinRT. Paired devices are listed by name; "Scan for adapters" also
listens for advertisements, which finds adapters that were never paired. Known GATT profiles:

| Service | Write | Notify | Typical adapters |
| --- | --- | --- | --- |
| `FFF0` | `FFF2` | `FFF1` | Vgate iCar Pro BLE and most "ELM327 v1.5 BLE 4.0" clones |
| `FFE0` | `FFE1` | `FFE1` | HM-10 style modules (one characteristic both ways) |
| `18F0` | `2AF1` | `2AF0` | vLinker and several Chinese dongles |
| Nordic UART | `...0002` | `...0003` | a few newer dongles |

If yours uses none of these, the driver falls back to any vendor service that has a writable and a
notifying characteristic, and reports which profile matched.

**Bluetooth Classic (serial port profile)** - `Services/Obd/RfcommObdTransport.cs`. The blue mini
dongles and the boxed "OBDII interface" adapters use this. The app connects straight over RFCOMM,
so no COM port has to be set up, and it pairs on the spot using the PINs the clones ship with
(1234, 0000, 6789) - no trip to Windows Bluetooth settings. Unpaired adapters in range are found by
the inquiry that "Scan for adapters" runs.

**USB cable** - `Services/Obd/ObdTransport.cs`. The blue ELM327 USB cable appears as a COM port once
its chip driver (CH340, FTDI, Prolific or CP210x) is installed; the list shows the Windows device
name, so `USB-SERIAL CH340 (COM3)` is recognisable rather than a bare `COM3`. An SPP adapter already
bound to a COM port works here too. Baud probing tries 38400 and 115200 during start up, the full
list on a manual connect.

Beware the lookalike: a blue USB cable sold as **VAG-COM KKL 409.1** is a plain K-line interface
with no ELM327 firmware. It is listed as "Not ELM327", and if one is tried anyway the failure says
so rather than leaving you guessing at the driver.

Turn the ignition on (engine running or key in position II) before connecting - with the ignition
off the adapter answers but the ECU does not.

Wi-Fi/TCP adapters are not supported.

The link handles: ATZ/ATE0/ATL0/ATS0/ATH0/ATAT1/ATSP0 handshake, supported-PID discovery
(0100/0120/0140), a polling rotation (fast values every cycle, temperatures every 20th), mode
03/07/0A fault codes, mode 04 clear, mode 02 freeze frame and mode 09 VIN and calibration id.

**When no adapter answers the app starts in demo mode** with a simulated drive cycle, so the
screens are still usable. Demo mode is labelled everywhere it matters - amber connection chip,
"Demo mode" in the sidebar, "Simulated data" on the connection screen - and never mixes simulated
fault codes with codes read from a car.

Two things OBD2 itself cannot give you, so the app derives them and says so:
severity (High/Medium/Low) comes from the code family, not from the ECU; and clearing a single code
is not possible - mode 04 clears everything, which the confirmation dialog states when you are
connected to a car.

### Keyboard

| Key | Action |
| --- | --- |
| `F1` / `F2` / `F3` / `F4` | Home / Diagnostics / Live Data / DTC Codes |
| Typing (Dictionary page) | Searches codes; `Backspace` deletes, `Delete` clears |
| `F11` | Toggle full screen |
| `Esc` | Close the danger alert, else leave full screen, else go back |
| `Alt+Left`, `Backspace` | Back |
| `Ctrl+D` | Toggle dark mode |
| `Ctrl+Q` | Quit |

## Screens

| Page | File | Notes |
| --- | --- | --- |
| Home | `Pages/HomePage.cs` | Status card, quick tiles, live value strip |
| Vehicle Health | `Pages/DiagnosticsPage.cs` | Health ring, per-system rows, animated Full Scan |
| System detail | `Pages/SystemDetailPage.cs` | One module: its codes and its live readings |
| Live Data | `Pages/LiveDataPage.cs` | Engine / Sensors / Fuel / Emission / Other tabs |
| Live Data Graph | `Pages/LiveGraphPage.cs` | One parameter over 1, 5 or 10 minutes |
| DTC Codes | `Pages/DtcCodesPage.cs` | Current / Pending / History, clear codes |
| DTC Details | `Pages/DtcDetailPage.cs` | Causes, effect, freeze frame |
| OBD2 Dictionary | `Pages/DictionaryPage.cs` | ~890 generic codes, type-to-search, category filters |
| Danger Alert | `Pages/DangerOverlay.cs` | Full screen warning over everything |
| Fuel Consumption | `Pages/FuelPage.cs` | Average / Instant / Trip |
| Trip Information | `Pages/TripPage.cs` | Distance, time, speeds, reset |
| Alarm History | `Pages/AlarmHistoryPage.cs` | All / Warning / Critical |
| Settings | `Pages/SettingsPage.cs` | General, OBD2 Connection, Alerts, Units, Vehicle Info, About |

## How it is put together

- `Ui/PageBase.cs` - every page paints into a design space **800 units high** and as wide as the
  window needs, so nothing is letterboxed or stretched. It also provides hit testing, hover states,
  wheel scrolling, the header bar, tabs, buttons and the confirmation dialog.
- `Ui/Theme.cs` - light and dark palettes; changing `Theme.Dark` repaints the whole app.
- `Ui/Draw.cs`, `Ui/Icons.cs`, `Ui/Charts.cs` - rounded cards, gauges, vector icons, line and bar charts.
- `Ui/Sidebar.cs` - navigation rail; collapses to icons below 1180 px wide or via the hamburger.
- `MainForm.cs` - shell, navigation stack, full screen handling, danger alerts, screen keep-alive.
- `Services/Obd/` - the adapter driver: `ObdTransport` (COM port pipe), `RfcommObdTransport`
  (Bluetooth Classic pipe and pairing), `BleObdTransport` (Bluetooth LE GATT pipe),
  `AdapterCatalog` (which model is which), `Elm327` (handshake, commands, response parsing),
  `ObdPids` (PID table and formulas), `ObdLink` (worker thread, polling rotation, request queue)
  and `ObdSelfTest`.
- `Services/` - `Telemetry` (live values from the link, or a simulated drive cycle, plus 10 minutes
  of history per PID), `DtcStore` (fault codes and the alarm log), `DtcCatalog` (the code
  dictionary), `ConnectionService` (live/demo state), `Loc` (translations), `AppSettings` (persisted
  to `%AppData%\ObdCarDangerous\settings.json`) and `AppState` which ties them together.

## Languages

English, Japanese (日本語) and Chinese (中文), switched under Settings > General > Language and
applied immediately - no restart. All UI text goes through `Loc.T("key")` in
`Services/Localization.cs`; add a language by adding a name to `Loc.Languages`, a font family in
`Loc.Set` and one more entry per row of the table.

Trouble code descriptions stay in the SAE J2012 English wording on purpose: that is what workshops,
manuals and other scan tools quote, so a translated description would be harder to match up. The
dictionary page says so at the bottom of its detail panel.

### Connecting a real adapter

All screens read from `AppState.Telemetry`. Replace the body of `Telemetry.Step()` with real PID
reads (mode 01) from an ELM327 serial/Bluetooth stream and keep the property names; replace
`DtcStore.Seed()`/`Rescan()` with mode 03/07/0A reads and `ClearAll()` with mode 04. `Telemetry.Pids`
is the single table describing each parameter, its range, units and which tab it appears on.

## Developer helpers

Render every screen to PNG without opening a window:

```
obd-car-dangerous.exe --render <folder> [width] [height] [--dark]
```

Check the ELM327 parsing against canned adapter answers (handshake, PID maths, DTC decoding,
multi-frame VIN). No hardware needed; the report lands in `%TEMP%\obd-selftest.txt` and the exit
code is the number of failures:

```
obd-car-dangerous.exe --selftest
```

List every adapter the app can see - COM ports, paired Bluetooth LE devices and a five second
advertisement scan. Start here when an adapter does not show up; the report lands in
`%TEMP%\obd-devices.txt`:

```
obd-car-dangerous.exe --devices
```

### Replacing the simulator entirely

`Telemetry.ReadLive()` copies whatever `ObdLink` last read; `Telemetry.Simulate()` is the demo feed.
Deleting the demo path is a matter of removing `Simulate`, `EnterDemo` and the `Demo` endpoint - no
screen touches either directly.
