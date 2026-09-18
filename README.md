# OBD2 Car Dangerous System

Windows Forms (.NET 8) vehicle monitoring dashboard. Every screen from the design set is implemented
as a custom-painted page, and the app runs borderless full screen on any resolution.

## Running

```
dotnet run --project obd-car-dangerous
```

The splash screen appears first (click or press a key to skip), then the main shell opens full screen.

### Keyboard

| Key | Action |
| --- | --- |
| `F1` / `F2` / `F3` / `F4` | Home / Diagnostics / Live Data / DTC Codes |
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
- `Services/` - `Telemetry` (simulated ELM327 stream plus 10 minutes of history per PID), `DtcStore`
  (fault codes and the alarm log), `ConnectionService`, `AppSettings` (persisted to
  `%AppData%\ObdCarDangerous\settings.json`) and `AppState` which ties them together.

### Connecting a real adapter

All screens read from `AppState.Telemetry`. Replace the body of `Telemetry.Step()` with real PID
reads (mode 01) from an ELM327 serial/Bluetooth stream and keep the property names; replace
`DtcStore.Seed()`/`Rescan()` with mode 03/07/0A reads and `ClearAll()` with mode 04. `Telemetry.Pids`
is the single table describing each parameter, its range, units and which tab it appears on.

## Developer helper

Render every screen to PNG without opening a window:

```
obd-car-dangerous.exe --render <folder> [width] [height] [--dark]
```
