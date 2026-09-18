namespace obd_car_dangerous.Services
{
    /// <summary>One entry of the OBD2 code dictionary.</summary>
    internal sealed record CatalogEntry(string Code, string Description)
    {
        /// <summary>P, B, C or U.</summary>
        public char Letter => Code[0];

        public string Category => Letter switch
        {
            'P' => "Powertrain",
            'B' => "Body",
            'C' => "Chassis",
            _ => "Network",
        };
    }

    /// <summary>
    /// Generic SAE J2012 / ISO 15031-6 trouble codes. Descriptions keep the English wording that
    /// scan tools print, so a code looked up here matches what a workshop quotes.
    /// </summary>
    internal static class DtcCatalog
    {
        private static readonly Lazy<IReadOnlyList<CatalogEntry>> Entries = new(Build);

        public static IReadOnlyList<CatalogEntry> All => Entries.Value;

        public static int Count => Entries.Value.Count;

        public static CatalogEntry? Find(string code) =>
            Entries.Value.FirstOrDefault(e => string.Equals(e.Code, code, StringComparison.OrdinalIgnoreCase));

        /// <summary>Filters by category letter ('\0' = all) and a free text query over code and description.</summary>
        public static List<CatalogEntry> Search(string query, char letter)
        {
            IEnumerable<CatalogEntry> source = Entries.Value;

            if (letter != '\0')
            {
                source = source.Where(e => e.Letter == letter);
            }

            query = query.Trim();
            if (query.Length > 0)
            {
                source = source.Where(e =>
                    e.Code.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    e.Description.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            return source.ToList();
        }

        /// <summary>Plain English note about the family a code belongs to.</summary>
        public static string FamilyOf(string code)
        {
            if (code.Length < 3)
            {
                return string.Empty;
            }

            if (code[0] == 'P')
            {
                return code[1] == '0' || code[1] == '2'
                    ? code[2] switch
                    {
                        '0' => "Fuel and air metering, auxiliary emission controls",
                        '1' => "Fuel and air metering",
                        '2' => "Fuel and air metering - injector circuits",
                        '3' => "Ignition system or misfire",
                        '4' => "Auxiliary emission controls",
                        '5' => "Vehicle speed, idle control and auxiliary inputs",
                        '6' => "Computer output circuits and module communication",
                        '7' or '8' => "Transmission",
                        _ => "Powertrain",
                    }
                    : "Manufacturer specific powertrain code";
            }

            return code[0] switch
            {
                'B' => "Body - airbags, lighting, comfort and interior modules",
                'C' => "Chassis - braking, steering, suspension and wheel speed",
                'U' => "Network - module communication over the CAN bus",
                _ => string.Empty,
            };
        }

        private static List<CatalogEntry> Build()
        {
            var list = new List<CatalogEntry>(700);

            foreach (string block in new[] { Powertrain0, Powertrain1, Powertrain2, Powertrain3, Powertrain4, Powertrain5, Powertrain6, Powertrain7, Powertrain2X, Network, Body, Chassis })
            {
                foreach (string line in block.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    int split = line.IndexOf('|');
                    if (split > 0)
                    {
                        list.Add(new CatalogEntry(line[..split], line[(split + 1)..]));
                    }
                }
            }

            // Repeating families are generated so the table stays readable.
            for (int cylinder = 1; cylinder <= 12; cylinder++)
            {
                list.Add(new CatalogEntry($"P0{200 + cylinder}", $"Injector Circuit/Open - Cylinder {cylinder}"));
                list.Add(new CatalogEntry($"P0{300 + cylinder}", $"Cylinder {cylinder} Misfire Detected"));

                int injector = 261 + (cylinder - 1) * 3;
                list.Add(new CatalogEntry($"P0{injector}", $"Cylinder {cylinder} Injector Circuit Low"));
                list.Add(new CatalogEntry($"P0{injector + 1}", $"Cylinder {cylinder} Injector Circuit High"));
                list.Add(new CatalogEntry($"P0{injector + 2}", $"Cylinder {cylinder} Contribution/Balance Fault"));

                char coil = (char)('A' + cylinder - 1);
                list.Add(new CatalogEntry($"P0{350 + cylinder}", $"Ignition Coil {coil} Primary/Secondary Circuit"));
            }

            return list
                .GroupBy(e => e.Code)
                .Select(g => g.First())
                .OrderBy(e => e.Code, StringComparer.Ordinal)
                .ToList();
        }

        private const string Powertrain0 = @"
P0001|Fuel Volume Regulator Control Circuit/Open
P0002|Fuel Volume Regulator Control Circuit Range/Performance
P0003|Fuel Volume Regulator Control Circuit Low
P0004|Fuel Volume Regulator Control Circuit High
P0005|Fuel Shutoff Valve A Control Circuit/Open
P0006|Fuel Shutoff Valve A Control Circuit Low
P0007|Fuel Shutoff Valve A Control Circuit High
P0008|Engine Position System Performance Bank 1
P0009|Engine Position System Performance Bank 2
P0010|A Camshaft Position Actuator Circuit Bank 1
P0011|A Camshaft Position Timing Over-Advanced Bank 1
P0012|A Camshaft Position Timing Over-Retarded Bank 1
P0013|B Camshaft Position Actuator Circuit Bank 1
P0014|B Camshaft Position Timing Over-Advanced Bank 1
P0015|B Camshaft Position Timing Over-Retarded Bank 1
P0016|Crankshaft Position - Camshaft Position Correlation Bank 1 Sensor A
P0017|Crankshaft Position - Camshaft Position Correlation Bank 1 Sensor B
P0018|Crankshaft Position - Camshaft Position Correlation Bank 2 Sensor A
P0019|Crankshaft Position - Camshaft Position Correlation Bank 2 Sensor B
P0020|A Camshaft Position Actuator Circuit Bank 2
P0021|A Camshaft Position Timing Over-Advanced Bank 2
P0022|A Camshaft Position Timing Over-Retarded Bank 2
P0023|B Camshaft Position Actuator Circuit Bank 2
P0024|B Camshaft Position Timing Over-Advanced Bank 2
P0025|B Camshaft Position Timing Over-Retarded Bank 2
P0026|Intake Valve Control Solenoid Circuit Range/Performance Bank 1
P0027|Exhaust Valve Control Solenoid Circuit Range/Performance Bank 1
P0028|Intake Valve Control Solenoid Circuit Range/Performance Bank 2
P0029|Exhaust Valve Control Solenoid Circuit Range/Performance Bank 2
P0030|HO2S Heater Control Circuit Bank 1 Sensor 1
P0031|HO2S Heater Control Circuit Low Bank 1 Sensor 1
P0032|HO2S Heater Control Circuit High Bank 1 Sensor 1
P0033|Turbocharger Bypass Valve Control Circuit
P0034|Turbocharger Bypass Valve Control Circuit Low
P0035|Turbocharger Bypass Valve Control Circuit High
P0036|HO2S Heater Control Circuit Bank 1 Sensor 2
P0037|HO2S Heater Control Circuit Low Bank 1 Sensor 2
P0038|HO2S Heater Control Circuit High Bank 1 Sensor 2
P0039|Turbocharger Bypass Valve Control Circuit Range/Performance
P0040|O2 Sensor Signals Swapped Bank 1 Sensor 1 / Bank 2 Sensor 1
P0041|O2 Sensor Signals Swapped Bank 1 Sensor 2 / Bank 2 Sensor 2
P0042|HO2S Heater Control Circuit Bank 1 Sensor 3
P0043|HO2S Heater Control Circuit Low Bank 1 Sensor 3
P0044|HO2S Heater Control Circuit High Bank 1 Sensor 3
P0045|Turbocharger Boost Control Solenoid Circuit/Open
P0046|Turbocharger Boost Control Solenoid Circuit Range/Performance
P0047|Turbocharger Boost Control Solenoid Circuit Low
P0048|Turbocharger Boost Control Solenoid Circuit High
P0049|Turbocharger Turbine Overspeed
P0050|HO2S Heater Control Circuit Bank 2 Sensor 1
P0051|HO2S Heater Control Circuit Low Bank 2 Sensor 1
P0052|HO2S Heater Control Circuit High Bank 2 Sensor 1
P0053|HO2S Heater Resistance Bank 1 Sensor 1
P0054|HO2S Heater Resistance Bank 1 Sensor 2
P0055|HO2S Heater Resistance Bank 1 Sensor 3
P0056|HO2S Heater Control Circuit Bank 2 Sensor 2
P0057|HO2S Heater Control Circuit Low Bank 2 Sensor 2
P0058|HO2S Heater Control Circuit High Bank 2 Sensor 2
P0059|HO2S Heater Resistance Bank 2 Sensor 1
P0060|HO2S Heater Resistance Bank 2 Sensor 2
P0061|HO2S Heater Resistance Bank 2 Sensor 3
P0062|HO2S Heater Control Circuit Bank 2 Sensor 3
P0063|HO2S Heater Control Circuit Low Bank 2 Sensor 3
P0064|HO2S Heater Control Circuit High Bank 2 Sensor 3
P0065|Air Assisted Injector Control Range/Performance
P0066|Air Assisted Injector Control Circuit or Circuit Low
P0067|Air Assisted Injector Control Circuit High
P0068|MAP/MAF - Throttle Position Correlation
P0069|Manifold Absolute Pressure - Barometric Pressure Correlation
P0070|Ambient Air Temperature Sensor Circuit
P0071|Ambient Air Temperature Sensor Range/Performance
P0072|Ambient Air Temperature Sensor Circuit Low
P0073|Ambient Air Temperature Sensor Circuit High
P0074|Ambient Air Temperature Sensor Circuit Intermittent
P0075|Intake Valve Control Solenoid Circuit Bank 1
P0076|Intake Valve Control Solenoid Circuit Low Bank 1
P0077|Intake Valve Control Solenoid Circuit High Bank 1
P0078|Exhaust Valve Control Solenoid Circuit Bank 1
P0079|Exhaust Valve Control Solenoid Circuit Low Bank 1
P0080|Exhaust Valve Control Solenoid Circuit High Bank 1
P0081|Intake Valve Control Solenoid Circuit Bank 2
P0082|Intake Valve Control Solenoid Circuit Low Bank 2
P0083|Intake Valve Control Solenoid Circuit High Bank 2
P0084|Exhaust Valve Control Solenoid Circuit Bank 2
P0085|Exhaust Valve Control Solenoid Circuit Low Bank 2
P0086|Exhaust Valve Control Solenoid Circuit High Bank 2
P0087|Fuel Rail/System Pressure Too Low
P0088|Fuel Rail/System Pressure Too High
P0089|Fuel Pressure Regulator 1 Performance
P0090|Fuel Pressure Regulator 1 Control Circuit
P0091|Fuel Pressure Regulator 1 Control Circuit Low
P0092|Fuel Pressure Regulator 1 Control Circuit High
P0093|Fuel System Leak Detected - Large Leak
P0094|Fuel System Leak Detected - Small Leak
P0095|Intake Air Temperature Sensor 2 Circuit
P0096|Intake Air Temperature Sensor 2 Circuit Range/Performance
P0097|Intake Air Temperature Sensor 2 Circuit Low
P0098|Intake Air Temperature Sensor 2 Circuit High
P0099|Intake Air Temperature Sensor 2 Circuit Intermittent/Erratic
";

        private const string Powertrain1 = @"
P0100|Mass or Volume Air Flow Sensor A Circuit
P0101|Mass Air Flow (MAF) Sensor Circuit Range/Performance
P0102|Mass Air Flow (MAF) Sensor Circuit Low Input
P0103|Mass Air Flow (MAF) Sensor Circuit High Input
P0104|Mass Air Flow (MAF) Sensor Circuit Intermittent
P0105|Manifold Absolute Pressure/Barometric Pressure Circuit
P0106|Manifold Absolute Pressure Sensor Range/Performance
P0107|Manifold Absolute Pressure Sensor Circuit Low Input
P0108|Manifold Absolute Pressure Sensor Circuit High Input
P0109|Manifold Absolute Pressure Sensor Circuit Intermittent
P0110|Intake Air Temperature Sensor 1 Circuit
P0111|Intake Air Temperature Sensor 1 Circuit Range/Performance
P0112|Intake Air Temperature Sensor 1 Circuit Low
P0113|Intake Air Temperature Sensor 1 Circuit High
P0114|Intake Air Temperature Sensor 1 Circuit Intermittent
P0115|Engine Coolant Temperature Sensor 1 Circuit
P0116|Engine Coolant Temperature Sensor 1 Circuit Range/Performance
P0117|Engine Coolant Temperature Sensor 1 Circuit Low
P0118|Engine Coolant Temperature Sensor 1 Circuit High
P0119|Engine Coolant Temperature Sensor 1 Circuit Intermittent
P0120|Throttle/Pedal Position Sensor/Switch A Circuit
P0121|Throttle/Pedal Position Sensor/Switch A Circuit Range/Performance
P0122|Throttle/Pedal Position Sensor/Switch A Circuit Low
P0123|Throttle/Pedal Position Sensor/Switch A Circuit High
P0124|Throttle/Pedal Position Sensor/Switch A Circuit Intermittent
P0125|Insufficient Coolant Temperature for Closed Loop Fuel Control
P0126|Insufficient Coolant Temperature for Stable Operation
P0127|Intake Air Temperature Too High
P0128|Coolant Thermostat Below Regulating Temperature
P0129|Barometric Pressure Too Low
P0130|O2 Sensor Circuit Bank 1 Sensor 1
P0131|O2 Sensor Circuit Low Voltage Bank 1 Sensor 1
P0132|O2 Sensor Circuit High Voltage Bank 1 Sensor 1
P0133|O2 Sensor Circuit Slow Response Bank 1 Sensor 1
P0134|O2 Sensor Circuit No Activity Detected Bank 1 Sensor 1
P0135|O2 Sensor Heater Circuit Bank 1 Sensor 1
P0136|O2 Sensor Circuit Bank 1 Sensor 2
P0137|O2 Sensor Circuit Low Voltage Bank 1 Sensor 2
P0138|O2 Sensor Circuit High Voltage Bank 1 Sensor 2
P0139|O2 Sensor Circuit Slow Response Bank 1 Sensor 2
P0140|O2 Sensor Circuit No Activity Detected Bank 1 Sensor 2
P0141|O2 Sensor Heater Circuit Bank 1 Sensor 2
P0142|O2 Sensor Circuit Bank 1 Sensor 3
P0143|O2 Sensor Circuit Low Voltage Bank 1 Sensor 3
P0144|O2 Sensor Circuit High Voltage Bank 1 Sensor 3
P0145|O2 Sensor Circuit Slow Response Bank 1 Sensor 3
P0146|O2 Sensor Circuit No Activity Detected Bank 1 Sensor 3
P0147|O2 Sensor Heater Circuit Bank 1 Sensor 3
P0148|Fuel Delivery Error
P0149|Fuel Timing Error
P0150|O2 Sensor Circuit Bank 2 Sensor 1
P0151|O2 Sensor Circuit Low Voltage Bank 2 Sensor 1
P0152|O2 Sensor Circuit High Voltage Bank 2 Sensor 1
P0153|O2 Sensor Circuit Slow Response Bank 2 Sensor 1
P0154|O2 Sensor Circuit No Activity Detected Bank 2 Sensor 1
P0155|O2 Sensor Heater Circuit Bank 2 Sensor 1
P0156|O2 Sensor Circuit Bank 2 Sensor 2
P0157|O2 Sensor Circuit Low Voltage Bank 2 Sensor 2
P0158|O2 Sensor Circuit High Voltage Bank 2 Sensor 2
P0159|O2 Sensor Circuit Slow Response Bank 2 Sensor 2
P0160|O2 Sensor Circuit No Activity Detected Bank 2 Sensor 2
P0161|O2 Sensor Heater Circuit Bank 2 Sensor 2
P0162|O2 Sensor Circuit Bank 2 Sensor 3
P0163|O2 Sensor Circuit Low Voltage Bank 2 Sensor 3
P0164|O2 Sensor Circuit High Voltage Bank 2 Sensor 3
P0165|O2 Sensor Circuit Slow Response Bank 2 Sensor 3
P0166|O2 Sensor Circuit No Activity Detected Bank 2 Sensor 3
P0167|O2 Sensor Heater Circuit Bank 2 Sensor 3
P0168|Fuel Temperature Too High
P0169|Incorrect Fuel Composition
P0170|Fuel Trim Malfunction Bank 1
P0171|System Too Lean (Bank 1)
P0172|System Too Rich (Bank 1)
P0173|Fuel Trim Malfunction Bank 2
P0174|System Too Lean (Bank 2)
P0175|System Too Rich (Bank 2)
P0176|Fuel Composition Sensor Circuit
P0177|Fuel Composition Sensor Circuit Range/Performance
P0178|Fuel Composition Sensor Circuit Low
P0179|Fuel Composition Sensor Circuit High
P0180|Fuel Temperature Sensor A Circuit
P0181|Fuel Temperature Sensor A Circuit Range/Performance
P0182|Fuel Temperature Sensor A Circuit Low
P0183|Fuel Temperature Sensor A Circuit High
P0184|Fuel Temperature Sensor A Circuit Intermittent
P0185|Fuel Temperature Sensor B Circuit
P0186|Fuel Temperature Sensor B Circuit Range/Performance
P0187|Fuel Temperature Sensor B Circuit Low
P0188|Fuel Temperature Sensor B Circuit High
P0189|Fuel Temperature Sensor B Circuit Intermittent
P0190|Fuel Rail Pressure Sensor Circuit
P0191|Fuel Rail Pressure Sensor Circuit Range/Performance
P0192|Fuel Rail Pressure Sensor Circuit Low
P0193|Fuel Rail Pressure Sensor Circuit High
P0194|Fuel Rail Pressure Sensor Circuit Intermittent
P0195|Engine Oil Temperature Sensor Circuit
P0196|Engine Oil Temperature Sensor Range/Performance
P0197|Engine Oil Temperature Sensor Low
P0198|Engine Oil Temperature Sensor High
P0199|Engine Oil Temperature Sensor Circuit Intermittent
";

        private const string Powertrain2 = @"
P0200|Injector Circuit/Open
P0213|Cold Start Injector 1 Malfunction
P0214|Cold Start Injector 2 Malfunction
P0215|Engine Shutoff Solenoid Malfunction
P0216|Injection Timing Control Circuit Malfunction
P0217|Engine Over Temperature Condition
P0218|Transmission Over Temperature Condition
P0219|Engine Overspeed Condition
P0220|Throttle/Pedal Position Sensor/Switch B Circuit
P0221|Throttle/Pedal Position Sensor/Switch B Circuit Range/Performance
P0222|Throttle/Pedal Position Sensor/Switch B Circuit Low
P0223|Throttle/Pedal Position Sensor/Switch B Circuit High
P0224|Throttle/Pedal Position Sensor/Switch B Circuit Intermittent
P0225|Throttle/Pedal Position Sensor/Switch C Circuit
P0226|Throttle/Pedal Position Sensor/Switch C Circuit Range/Performance
P0227|Throttle/Pedal Position Sensor/Switch C Circuit Low
P0228|Throttle/Pedal Position Sensor/Switch C Circuit High
P0229|Throttle/Pedal Position Sensor/Switch C Circuit Intermittent
P0230|Fuel Pump Primary Circuit Malfunction
P0231|Fuel Pump Secondary Circuit Low
P0232|Fuel Pump Secondary Circuit High
P0233|Fuel Pump Secondary Circuit Intermittent
P0234|Turbocharger Overboost Condition
P0235|Turbocharger Boost Sensor A Circuit
P0236|Turbocharger Boost Sensor A Circuit Range/Performance
P0237|Turbocharger Boost Sensor A Circuit Low
P0238|Turbocharger Boost Sensor A Circuit High
P0239|Turbocharger Boost Sensor B Circuit
P0240|Turbocharger Boost Sensor B Circuit Range/Performance
P0241|Turbocharger Boost Sensor B Circuit Low
P0242|Turbocharger Boost Sensor B Circuit High
P0243|Turbocharger Wastegate Solenoid A
P0244|Turbocharger Wastegate Solenoid A Range/Performance
P0245|Turbocharger Wastegate Solenoid A Low
P0246|Turbocharger Wastegate Solenoid A High
P0247|Turbocharger Wastegate Solenoid B
P0248|Turbocharger Wastegate Solenoid B Range/Performance
P0249|Turbocharger Wastegate Solenoid B Low
P0250|Turbocharger Wastegate Solenoid B High
P0251|Injection Pump Fuel Metering Control A Malfunction
P0252|Injection Pump Fuel Metering Control A Range/Performance
P0253|Injection Pump Fuel Metering Control A Low
P0254|Injection Pump Fuel Metering Control A High
P0255|Injection Pump Fuel Metering Control A Intermittent
P0256|Injection Pump Fuel Metering Control B Malfunction
P0257|Injection Pump Fuel Metering Control B Range/Performance
P0258|Injection Pump Fuel Metering Control B Low
P0259|Injection Pump Fuel Metering Control B High
P0260|Injection Pump Fuel Metering Control B Intermittent
P0297|Vehicle Overspeed Condition
P0298|Engine Oil Over Temperature
P0299|Turbocharger Underboost Condition
";

        private const string Powertrain3 = @"
P0300|Random/Multiple Cylinder Misfire Detected
P0313|Misfire Detected with Low Fuel Level
P0314|Single Cylinder Misfire (Cylinder Not Specified)
P0315|Crankshaft Position System Variation Not Learned
P0316|Misfire Detected on Startup (First 1000 Revolutions)
P0320|Ignition/Distributor Engine Speed Input Circuit
P0321|Ignition/Distributor Engine Speed Input Circuit Range/Performance
P0322|Ignition/Distributor Engine Speed Input Circuit No Signal
P0323|Ignition/Distributor Engine Speed Input Circuit Intermittent
P0325|Knock Sensor 1 Circuit Bank 1
P0326|Knock Sensor 1 Circuit Range/Performance Bank 1
P0327|Knock Sensor 1 Circuit Low Bank 1
P0328|Knock Sensor 1 Circuit High Bank 1
P0329|Knock Sensor 1 Circuit Intermittent Bank 1
P0330|Knock Sensor 2 Circuit Bank 2
P0331|Knock Sensor 2 Circuit Range/Performance Bank 2
P0332|Knock Sensor 2 Circuit Low Bank 2
P0333|Knock Sensor 2 Circuit High Bank 2
P0334|Knock Sensor 2 Circuit Intermittent Bank 2
P0335|Crankshaft Position Sensor A Circuit
P0336|Crankshaft Position Sensor A Circuit Range/Performance
P0337|Crankshaft Position Sensor A Circuit Low
P0338|Crankshaft Position Sensor A Circuit High
P0339|Crankshaft Position Sensor A Circuit Intermittent
P0340|Camshaft Position Sensor A Circuit Bank 1
P0341|Camshaft Position Sensor A Circuit Range/Performance Bank 1
P0342|Camshaft Position Sensor A Circuit Low Bank 1
P0343|Camshaft Position Sensor A Circuit High Bank 1
P0344|Camshaft Position Sensor A Circuit Intermittent Bank 1
P0345|Camshaft Position Sensor A Circuit Bank 2
P0346|Camshaft Position Sensor A Circuit Range/Performance Bank 2
P0347|Camshaft Position Sensor A Circuit Low Bank 2
P0348|Camshaft Position Sensor A Circuit High Bank 2
P0349|Camshaft Position Sensor A Circuit Intermittent Bank 2
P0350|Ignition Coil Primary/Secondary Circuit
P0365|Camshaft Position Sensor B Circuit Bank 1
P0366|Camshaft Position Sensor B Circuit Range/Performance Bank 1
P0367|Camshaft Position Sensor B Circuit Low Bank 1
P0368|Camshaft Position Sensor B Circuit High Bank 1
P0369|Camshaft Position Sensor B Circuit Intermittent Bank 1
P0370|Timing Reference High Resolution Signal A Malfunction
P0371|Timing Reference High Resolution Signal A Too Many Pulses
P0372|Timing Reference High Resolution Signal A Too Few Pulses
P0373|Timing Reference High Resolution Signal A Intermittent Pulses
P0374|Timing Reference High Resolution Signal A No Pulses
P0375|Timing Reference High Resolution Signal B Malfunction
P0380|Glow Plug/Heater Circuit A
P0381|Glow Plug/Heater Indicator Circuit
P0385|Crankshaft Position Sensor B Circuit
P0386|Crankshaft Position Sensor B Circuit Range/Performance
P0387|Crankshaft Position Sensor B Circuit Low
P0388|Crankshaft Position Sensor B Circuit High
P0389|Crankshaft Position Sensor B Circuit Intermittent
P0390|Camshaft Position Sensor B Circuit Bank 2
P0391|Camshaft Position Sensor B Circuit Range/Performance Bank 2
P0392|Camshaft Position Sensor B Circuit Low Bank 2
P0393|Camshaft Position Sensor B Circuit High Bank 2
";

        private const string Powertrain4 = @"
P0400|Exhaust Gas Recirculation Flow Malfunction
P0401|Exhaust Gas Recirculation Flow Insufficient Detected
P0402|Exhaust Gas Recirculation Flow Excessive Detected
P0403|Exhaust Gas Recirculation Control Circuit
P0404|Exhaust Gas Recirculation Control Circuit Range/Performance
P0405|Exhaust Gas Recirculation Sensor A Circuit Low
P0406|Exhaust Gas Recirculation Sensor A Circuit High
P0407|Exhaust Gas Recirculation Sensor B Circuit Low
P0408|Exhaust Gas Recirculation Sensor B Circuit High
P0409|Exhaust Gas Recirculation Sensor A Circuit
P0410|Secondary Air Injection System Malfunction
P0411|Secondary Air Injection System Incorrect Flow Detected
P0412|Secondary Air Injection System Switching Valve A Circuit
P0413|Secondary Air Injection System Switching Valve A Circuit Open
P0414|Secondary Air Injection System Switching Valve A Circuit Shorted
P0415|Secondary Air Injection System Switching Valve B Circuit
P0416|Secondary Air Injection System Switching Valve B Circuit Open
P0417|Secondary Air Injection System Switching Valve B Circuit Shorted
P0418|Secondary Air Injection System Control A Circuit
P0419|Secondary Air Injection System Control B Circuit
P0420|Catalyst System Efficiency Below Threshold (Bank 1)
P0421|Warm Up Catalyst Efficiency Below Threshold Bank 1
P0422|Main Catalyst Efficiency Below Threshold Bank 1
P0423|Heated Catalyst Efficiency Below Threshold Bank 1
P0424|Heated Catalyst Temperature Below Threshold Bank 1
P0425|Catalyst Temperature Sensor Bank 1
P0426|Catalyst Temperature Sensor Range/Performance Bank 1
P0427|Catalyst Temperature Sensor Low Bank 1
P0428|Catalyst Temperature Sensor High Bank 1
P0430|Catalyst System Efficiency Below Threshold (Bank 2)
P0431|Warm Up Catalyst Efficiency Below Threshold Bank 2
P0432|Main Catalyst Efficiency Below Threshold Bank 2
P0433|Heated Catalyst Efficiency Below Threshold Bank 2
P0434|Heated Catalyst Temperature Below Threshold Bank 2
P0440|Evaporative Emission Control System Malfunction
P0441|Evaporative Emission Control System Incorrect Purge Flow
P0442|Evaporative Emission Control System Leak (Small Leak)
P0443|Evaporative Emission Control System Purge Control Valve Circuit
P0444|Evaporative Emission Control System Purge Control Valve Circuit Open
P0445|Evaporative Emission Control System Purge Control Valve Circuit Shorted
P0446|Evaporative Emission Control System Vent Control Circuit
P0447|Evaporative Emission Control System Vent Control Circuit Open
P0448|Evaporative Emission Control System Vent Control Circuit Shorted
P0449|Evaporative Emission Control System Vent Valve/Solenoid Circuit
P0450|Evaporative Emission Control System Pressure Sensor
P0451|Evaporative Emission Control System Pressure Sensor Range/Performance
P0452|Evaporative Emission Control System Pressure Sensor Low Input
P0453|Evaporative Emission Control System Pressure Sensor High Input
P0454|Evaporative Emission Control System Pressure Sensor Intermittent
P0455|Evaporative Emission Control System Leak (Large Leak)
P0456|Evaporative Emission Control System Leak (Very Small Leak)
P0457|Evaporative Emission Control System Leak (Fuel Cap Loose/Off)
P0458|Evaporative Emission System Purge Control Valve Circuit Low
P0459|Evaporative Emission System Purge Control Valve Circuit High
P0460|Fuel Level Sensor Circuit
P0461|Fuel Level Sensor Circuit Range/Performance
P0462|Fuel Level Sensor Circuit Low Input
P0463|Fuel Level Sensor Circuit High Input
P0464|Fuel Level Sensor Circuit Intermittent
P0465|Purge Flow Sensor Circuit
P0466|Purge Flow Sensor Circuit Range/Performance
P0467|Purge Flow Sensor Circuit Low Input
P0468|Purge Flow Sensor Circuit High Input
P0469|Purge Flow Sensor Circuit Intermittent
P0470|Exhaust Pressure Sensor Malfunction
P0471|Exhaust Pressure Sensor Range/Performance
P0472|Exhaust Pressure Sensor Low
P0473|Exhaust Pressure Sensor High
P0474|Exhaust Pressure Sensor Intermittent
P0475|Exhaust Pressure Control Valve Malfunction
P0476|Exhaust Pressure Control Valve Range/Performance
P0477|Exhaust Pressure Control Valve Low
P0478|Exhaust Pressure Control Valve High
P0479|Exhaust Pressure Control Valve Intermittent
P0480|Cooling Fan 1 Control Circuit
P0481|Cooling Fan 2 Control Circuit
P0482|Cooling Fan 3 Control Circuit
P0483|Cooling Fan Rationality Check
P0484|Cooling Fan Circuit Over Current
P0485|Cooling Fan Power/Ground Circuit
";

        private const string Powertrain5 = @"
P0500|Vehicle Speed Sensor A
P0501|Vehicle Speed Sensor A Range/Performance
P0502|Vehicle Speed Sensor A Circuit Low Input
P0503|Vehicle Speed Sensor A Intermittent/Erratic/High
P0505|Idle Air Control System
P0506|Idle Air Control System RPM Lower Than Expected
P0507|Idle Air Control System RPM Higher Than Expected
P0508|Idle Air Control System Circuit Low
P0509|Idle Air Control System Circuit High
P0510|Closed Throttle Position Switch
P0512|Starter Request Circuit
P0513|Incorrect Immobilizer Key
P0515|Battery Temperature Sensor Circuit
P0516|Battery Temperature Sensor Circuit Low
P0517|Battery Temperature Sensor Circuit High
P0520|Engine Oil Pressure Sensor/Switch Circuit
P0521|Engine Oil Pressure Sensor/Switch Range/Performance
P0522|Engine Oil Pressure Sensor/Switch Low Voltage
P0523|Engine Oil Pressure Sensor/Switch High Voltage
P0524|Engine Oil Pressure Too Low
P0530|A/C Refrigerant Pressure Sensor A Circuit
P0531|A/C Refrigerant Pressure Sensor A Circuit Range/Performance
P0532|A/C Refrigerant Pressure Sensor A Circuit Low
P0533|A/C Refrigerant Pressure Sensor A Circuit High
P0534|Air Conditioner Refrigerant Charge Loss
P0540|Intake Air Heater A Circuit
P0541|Intake Air Heater A Circuit Low
P0542|Intake Air Heater A Circuit High
P0545|Exhaust Gas Temperature Sensor Circuit Low Bank 1 Sensor 1
P0546|Exhaust Gas Temperature Sensor Circuit High Bank 1 Sensor 1
P0547|Exhaust Gas Temperature Sensor Circuit Low Bank 2 Sensor 1
P0548|Exhaust Gas Temperature Sensor Circuit High Bank 2 Sensor 1
P0550|Power Steering Pressure Sensor Circuit
P0551|Power Steering Pressure Sensor Circuit Range/Performance
P0552|Power Steering Pressure Sensor Circuit Low Input
P0553|Power Steering Pressure Sensor Circuit High Input
P0554|Power Steering Pressure Sensor Circuit Intermittent
P0560|System Voltage Malfunction
P0561|System Voltage Unstable
P0562|System Voltage Low
P0563|System Voltage High
P0565|Cruise Control On Signal Malfunction
P0566|Cruise Control Off Signal Malfunction
P0567|Cruise Control Resume Signal Malfunction
P0568|Cruise Control Set Signal Malfunction
P0569|Cruise Control Coast Signal Malfunction
P0570|Cruise Control Accel Signal Malfunction
P0571|Brake Switch A Circuit Malfunction
P0572|Brake Switch A Circuit Low
P0573|Brake Switch A Circuit High
P0574|Cruise Control System - Vehicle Speed Too High
P0575|Cruise Control Input Circuit
P0576|Cruise Control Input Circuit Low
P0577|Cruise Control Input Circuit High
P0580|Cruise Control Multi-Function Input A Circuit Low
P0581|Cruise Control Multi-Function Input A Circuit High
P0590|Cruise Control Multi-Function Input B Circuit
";

        private const string Powertrain6 = @"
P0600|Serial Communication Link Malfunction
P0601|Internal Control Module Memory Check Sum Error
P0602|Control Module Programming Error
P0603|Internal Control Module Keep Alive Memory (KAM) Error
P0604|Internal Control Module Random Access Memory (RAM) Error
P0605|Internal Control Module Read Only Memory (ROM) Error
P0606|ECM/PCM Processor Fault
P0607|Control Module Performance
P0608|Control Module VSS Output A Malfunction
P0609|Control Module VSS Output B Malfunction
P0610|Control Module Vehicle Options Error
P0611|Fuel Injector Control Module Performance
P0612|Fuel Injector Control Module Relay Control
P0613|TCM Processor
P0614|ECM/TCM Incompatible
P0615|Starter Relay Circuit
P0616|Starter Relay Circuit Low
P0617|Starter Relay Circuit High
P0620|Generator Control Circuit Malfunction
P0621|Generator Lamp/L Terminal Circuit Malfunction
P0622|Generator Field/F Terminal Circuit Malfunction
P0623|Generator Lamp Control Circuit
P0624|Fuel Cap Lamp Control Circuit
P0625|Generator Field/F Terminal Circuit Low
P0626|Generator Field/F Terminal Circuit High
P0627|Fuel Pump A Control Circuit/Open
P0628|Fuel Pump A Control Circuit Low
P0629|Fuel Pump A Control Circuit High
P0630|VIN Not Programmed or Incompatible - ECM/PCM
P0631|VIN Not Programmed or Incompatible - TCM
P0632|Odometer Not Programmed - ECM/PCM
P0633|Immobilizer Key Not Programmed - ECM/PCM
P0634|PCM/ECM/TCM Internal Temperature Too High
P0635|Power Steering Control Circuit
P0645|A/C Clutch Relay Control Circuit
P0646|A/C Clutch Relay Control Circuit Low
P0647|A/C Clutch Relay Control Circuit High
P0650|Malfunction Indicator Lamp (MIL) Control Circuit
P0651|Sensor Reference Voltage B Circuit/Open
P0652|Sensor Reference Voltage B Circuit Low
P0653|Sensor Reference Voltage B Circuit High
P0654|Engine RPM Output Circuit
P0655|Engine Hot Lamp Output Control Circuit
P0656|Fuel Level Output Circuit
P0660|Intake Manifold Tuning Valve Control Circuit/Open Bank 1
P0661|Intake Manifold Tuning Valve Control Circuit Low Bank 1
P0662|Intake Manifold Tuning Valve Control Circuit High Bank 1
P0685|ECM/PCM Power Relay Control Circuit/Open
P0686|ECM/PCM Power Relay Control Circuit Low
P0687|ECM/PCM Power Relay Control Circuit High
P0688|ECM/PCM Power Relay Sense Circuit/Open
P0691|Fan 1 Control Circuit Low
P0692|Fan 1 Control Circuit High
P0693|Fan 2 Control Circuit Low
P0694|Fan 2 Control Circuit High
";

        private const string Powertrain7 = @"
P0700|Transmission Control System (MIL Request)
P0701|Transmission Control System Range/Performance
P0702|Transmission Control System Electrical
P0703|Torque Converter/Brake Switch B Circuit
P0704|Clutch Switch Input Circuit Malfunction
P0705|Transmission Range Sensor Circuit (PRNDL Input)
P0706|Transmission Range Sensor Circuit Range/Performance
P0707|Transmission Range Sensor Circuit Low
P0708|Transmission Range Sensor Circuit High
P0709|Transmission Range Sensor Circuit Intermittent
P0710|Transmission Fluid Temperature Sensor A Circuit
P0711|Transmission Fluid Temperature Sensor A Range/Performance
P0712|Transmission Fluid Temperature Sensor A Circuit Low
P0713|Transmission Fluid Temperature Sensor A Circuit High
P0714|Transmission Fluid Temperature Sensor A Circuit Intermittent
P0715|Input/Turbine Speed Sensor A Circuit
P0716|Input/Turbine Speed Sensor A Range/Performance
P0717|Input/Turbine Speed Sensor A Circuit No Signal
P0718|Input/Turbine Speed Sensor A Circuit Intermittent
P0719|Torque Converter/Brake Switch B Circuit Low
P0720|Output Speed Sensor Circuit
P0721|Output Speed Sensor Circuit Range/Performance
P0722|Output Speed Sensor Circuit No Signal
P0723|Output Speed Sensor Circuit Intermittent
P0724|Torque Converter/Brake Switch B Circuit High
P0725|Engine Speed Input Circuit
P0726|Engine Speed Input Circuit Range/Performance
P0727|Engine Speed Input Circuit No Signal
P0728|Engine Speed Input Circuit Intermittent
P0730|Incorrect Gear Ratio
P0731|Gear 1 Incorrect Ratio
P0732|Gear 2 Incorrect Ratio
P0733|Gear 3 Incorrect Ratio
P0734|Gear 4 Incorrect Ratio
P0735|Gear 5 Incorrect Ratio
P0736|Reverse Incorrect Ratio
P0740|Torque Converter Clutch Circuit Malfunction
P0741|Torque Converter Clutch Circuit Performance or Stuck Off
P0742|Torque Converter Clutch Circuit Stuck On
P0743|Torque Converter Clutch Circuit Electrical
P0744|Torque Converter Clutch Circuit Intermittent
P0745|Pressure Control Solenoid A Malfunction
P0746|Pressure Control Solenoid A Performance or Stuck Off
P0747|Pressure Control Solenoid A Stuck On
P0748|Pressure Control Solenoid A Electrical
P0749|Pressure Control Solenoid A Intermittent
P0750|Shift Solenoid A Malfunction
P0751|Shift Solenoid A Performance or Stuck Off
P0752|Shift Solenoid A Stuck On
P0753|Shift Solenoid A Electrical
P0754|Shift Solenoid A Intermittent
P0755|Shift Solenoid B Malfunction
P0756|Shift Solenoid B Performance or Stuck Off
P0757|Shift Solenoid B Stuck On
P0758|Shift Solenoid B Electrical
P0759|Shift Solenoid B Intermittent
P0760|Shift Solenoid C Malfunction
P0761|Shift Solenoid C Performance or Stuck Off
P0762|Shift Solenoid C Stuck On
P0763|Shift Solenoid C Electrical
P0764|Shift Solenoid C Intermittent
P0765|Shift Solenoid D Malfunction
P0766|Shift Solenoid D Performance or Stuck Off
P0767|Shift Solenoid D Stuck On
P0768|Shift Solenoid D Electrical
P0769|Shift Solenoid D Intermittent
P0770|Shift Solenoid E Malfunction
P0771|Shift Solenoid E Performance or Stuck Off
P0772|Shift Solenoid E Stuck On
P0773|Shift Solenoid E Electrical
P0774|Shift Solenoid E Intermittent
P0775|Pressure Control Solenoid B Malfunction
P0776|Pressure Control Solenoid B Performance or Stuck Off
P0777|Pressure Control Solenoid B Stuck On
P0778|Pressure Control Solenoid B Electrical
P0779|Pressure Control Solenoid B Intermittent
P0780|Shift Malfunction
P0781|1-2 Shift Malfunction
P0782|2-3 Shift Malfunction
P0783|3-4 Shift Malfunction
P0784|4-5 Shift Malfunction
P0785|Shift/Timing Solenoid Malfunction
P0786|Shift/Timing Solenoid Range/Performance
P0787|Shift/Timing Solenoid Low
P0788|Shift/Timing Solenoid High
P0789|Shift/Timing Solenoid Intermittent
P0790|Normal/Performance Switch Circuit Malfunction
P0791|Intermediate Shaft Speed Sensor A Circuit
P0792|Intermediate Shaft Speed Sensor A Circuit Range/Performance
P0793|Intermediate Shaft Speed Sensor A Circuit No Signal
P0794|Intermediate Shaft Speed Sensor A Circuit Intermittent
P0795|Pressure Control Solenoid C Malfunction
P0796|Pressure Control Solenoid C Performance or Stuck Off
P0797|Pressure Control Solenoid C Stuck On
P0798|Pressure Control Solenoid C Electrical
P0799|Pressure Control Solenoid C Intermittent
P0801|Reverse Inhibit Control Circuit
P0803|1-4 Upshift (Skip Shift) Solenoid Control Circuit
P0805|Clutch Position Sensor Circuit
P0810|Clutch Position Control Error
P0812|Reverse Input Circuit
P0815|Upshift Switch Circuit
P0816|Downshift Switch Circuit
P0820|Gear Lever X-Y Position Sensor Circuit
P0830|Clutch Pedal Switch A Circuit
P0850|Park/Neutral Switch Input Circuit
P0863|TCM Communication Circuit
P0868|Transmission Fluid Pressure Low
P0869|Transmission Fluid Pressure High
P0882|TCM Power Input Signal Low
P0883|TCM Power Input Signal High
P0884|TCM Power Input Signal Intermittent
P0890|TCM Power Relay Low
P0891|TCM Power Relay Always On
P0897|Transmission Fluid Deteriorated
";

        private const string Powertrain2X = @"
P2000|NOx Adsorber Efficiency Below Threshold Bank 1
P2001|NOx Adsorber Efficiency Below Threshold Bank 2
P2002|Diesel Particulate Filter Efficiency Below Threshold Bank 1
P2003|Diesel Particulate Filter Efficiency Below Threshold Bank 2
P2004|Intake Manifold Runner Control Stuck Open Bank 1
P2005|Intake Manifold Runner Control Stuck Open Bank 2
P2006|Intake Manifold Runner Control Stuck Closed Bank 1
P2007|Intake Manifold Runner Control Stuck Closed Bank 2
P2008|Intake Manifold Runner Control Circuit/Open Bank 1
P2009|Intake Manifold Runner Control Circuit Low Bank 1
P2010|Intake Manifold Runner Control Circuit High Bank 1
P2014|Intake Manifold Runner Position Sensor/Switch Circuit Bank 1
P2015|Intake Manifold Runner Position Sensor/Switch Range/Performance Bank 1
P2016|Intake Manifold Runner Position Sensor/Switch Circuit Low Bank 1
P2017|Intake Manifold Runner Position Sensor/Switch Circuit High Bank 1
P2031|Exhaust Gas Temperature Sensor Circuit Bank 1 Sensor 2
P2032|Exhaust Gas Temperature Sensor Circuit Low Bank 1 Sensor 2
P2033|Exhaust Gas Temperature Sensor Circuit High Bank 1 Sensor 2
P2036|Exhaust Gas Temperature Sensor Circuit Bank 2 Sensor 2
P2047|Reductant Injector Circuit/Open Bank 1 Unit 1
P2048|Reductant Injector Circuit Low Bank 1 Unit 1
P2049|Reductant Injector Circuit High Bank 1 Unit 1
P2080|Exhaust Gas Temperature Sensor Circuit Range/Performance Bank 1 Sensor 1
P2096|Post Catalyst Fuel Trim System Too Lean Bank 1
P2097|Post Catalyst Fuel Trim System Too Rich Bank 1
P2098|Post Catalyst Fuel Trim System Too Lean Bank 2
P2099|Post Catalyst Fuel Trim System Too Rich Bank 2
P2100|Throttle Actuator Control Motor Circuit/Open
P2101|Throttle Actuator Control Motor Circuit Range/Performance
P2102|Throttle Actuator Control Motor Circuit Low
P2103|Throttle Actuator Control Motor Circuit High
P2104|Throttle Actuator Control System - Forced Idle
P2105|Throttle Actuator Control System - Forced Engine Shutdown
P2106|Throttle Actuator Control System - Forced Limited Power
P2107|Throttle Actuator Control Module Processor
P2108|Throttle Actuator Control Module Performance
P2109|Throttle/Pedal Position Sensor A Minimum Stop Performance
P2110|Throttle Actuator Control System - Forced Limited RPM
P2111|Throttle Actuator Control System - Stuck Open
P2112|Throttle Actuator Control System - Stuck Closed
P2119|Throttle Actuator Control Throttle Body Range/Performance
P2120|Throttle/Pedal Position Sensor/Switch D Circuit
P2121|Throttle/Pedal Position Sensor/Switch D Circuit Range/Performance
P2122|Throttle/Pedal Position Sensor/Switch D Circuit Low Input
P2123|Throttle/Pedal Position Sensor/Switch D Circuit High Input
P2125|Throttle/Pedal Position Sensor/Switch E Circuit
P2126|Throttle/Pedal Position Sensor/Switch E Circuit Range/Performance
P2127|Throttle/Pedal Position Sensor/Switch E Circuit Low Input
P2128|Throttle/Pedal Position Sensor/Switch E Circuit High Input
P2135|Throttle/Pedal Position Sensor/Switch A/B Voltage Correlation
P2138|Throttle/Pedal Position Sensor/Switch D/E Voltage Correlation
P2146|Fuel Injector Group A Supply Voltage Circuit/Open
P2147|Fuel Injector Group A Supply Voltage Circuit Low
P2148|Fuel Injector Group A Supply Voltage Circuit High
P2149|Fuel Injector Group B Supply Voltage Circuit/Open
P2181|Cooling System Performance
P2184|Engine Coolant Temperature Sensor 2 Circuit Low
P2185|Engine Coolant Temperature Sensor 2 Circuit High
P2187|System Too Lean at Idle Bank 1
P2188|System Too Rich at Idle Bank 1
P2189|System Too Lean at Idle Bank 2
P2190|System Too Rich at Idle Bank 2
P2195|O2 Sensor Signal Stuck Lean Bank 1 Sensor 1
P2196|O2 Sensor Signal Stuck Rich Bank 1 Sensor 1
P2197|O2 Sensor Signal Stuck Lean Bank 2 Sensor 1
P2198|O2 Sensor Signal Stuck Rich Bank 2 Sensor 1
P2270|O2 Sensor Signal Stuck Lean Bank 1 Sensor 2
P2271|O2 Sensor Signal Stuck Rich Bank 1 Sensor 2
P2272|O2 Sensor Signal Stuck Lean Bank 2 Sensor 2
P2273|O2 Sensor Signal Stuck Rich Bank 2 Sensor 2
P2279|Intake Air System Leak
P2282|Air Leak Between Throttle Body and Intake Valves
P2404|EVAP Leak Detection Pump Sense Circuit Range/Performance
P2413|Exhaust Gas Recirculation System Performance
P2418|Evaporative Emission System Switching Valve Control Circuit/Open
P2422|Evaporative Emission System Vent Valve Stuck Closed
P2440|Secondary Air Injection System Switching Valve Stuck Open Bank 1
P2503|Charging System Voltage Low
P2504|Charging System Voltage High
P2610|ECM/PCM Internal Engine Off Timer Performance
P2646|A Rocker Arm Actuator System Performance or Stuck Off Bank 1
P2647|A Rocker Arm Actuator System Stuck On Bank 1
P2716|Pressure Control Solenoid D Electrical
P2757|Torque Converter Clutch Pressure Control Solenoid Control Circuit Performance
";

        private const string Network = @"
U0001|High Speed CAN Communication Bus
U0002|High Speed CAN Communication Bus Performance
U0003|High Speed CAN Communication Bus (+) Open
U0004|High Speed CAN Communication Bus (+) Low
U0005|High Speed CAN Communication Bus (+) High
U0010|Medium Speed CAN Communication Bus
U0073|Control Module Communication Bus A Off
U0074|Control Module Communication Bus B Off
U0075|Control Module Communication Bus C Off
U0100|Lost Communication With ECM/PCM 'A'
U0101|Lost Communication With TCM
U0102|Lost Communication With Transfer Case Control Module
U0103|Lost Communication With Gear Shift Module
U0104|Lost Communication With Cruise Control Module
U0105|Lost Communication With Fuel Injector Control Module
U0106|Lost Communication With Glow Plug Control Module
U0107|Lost Communication With Throttle Actuator Control Module
U0108|Lost Communication With Alternative Fuel Control Module
U0109|Lost Communication With Fuel Pump Control Module
U0110|Lost Communication With Drive Motor Control Module
U0111|Lost Communication With Battery Energy Control Module A
U0112|Lost Communication With Battery Energy Control Module B
U0113|Lost Communication With Emissions Critical Control Information
U0114|Lost Communication With Four-Wheel Drive Clutch Control Module
U0115|Lost Communication With ECM/PCM 'B'
U0121|Lost Communication With Anti-Lock Brake System (ABS) Control Module
U0122|Lost Communication With Vehicle Dynamics Control Module
U0123|Lost Communication With Yaw Rate Sensor Module
U0124|Lost Communication With Lateral Acceleration Sensor Module
U0125|Lost Communication With Multi-axis Acceleration Sensor Module
U0126|Lost Communication With Steering Angle Sensor Module
U0128|Lost Communication With Park Brake Control Module
U0129|Lost Communication With Brake System Control Module
U0131|Lost Communication With Power Steering Control Module
U0140|Lost Communication With Body Control Module
U0141|Lost Communication With Body Control Module A
U0142|Lost Communication With Body Control Module B
U0146|Lost Communication With Gateway A
U0151|Lost Communication With Restraints Control Module
U0155|Lost Communication With Instrument Panel Cluster Control Module
U0159|Lost Communication With Parking Assist Control Module
U0164|Lost Communication With HVAC Control Module
U0167|Lost Communication With Vehicle Immobilizer Control Module
U0184|Lost Communication With Radio
U0199|Lost Communication With Door Control Module A
U0300|Internal Control Module Software Incompatibility
U0301|Software Incompatibility with ECM/PCM
U0302|Software Incompatibility with TCM
U0305|Software Incompatibility with Cruise Control Module
U0315|Software Incompatibility with ABS Control Module
U0401|Invalid Data Received From ECM/PCM A
U0402|Invalid Data Received From TCM
U0415|Invalid Data Received From ABS Control Module
U0418|Invalid Data Received From Brake System Control Module
U0422|Invalid Data Received From Body Control Module
U0428|Invalid Data Received From Steering Angle Sensor Module
U1000|Manufacturer Specific CAN Communication Fault
";

        private const string Body = @"
B0001|Driver Frontal Stage 1 Deployment Control
B0002|Driver Frontal Stage 2 Deployment Control
B0010|Passenger Frontal Stage 1 Deployment Control
B0012|Passenger Frontal Stage 2 Deployment Control
B0020|Left Side Airbag Deployment Control
B0022|Left Side Curtain Airbag Deployment Control
B0028|Right Side Airbag Deployment Control
B0030|Right Side Curtain Airbag Deployment Control
B0051|Driver Seatbelt Pretensioner Deployment Control
B0053|Passenger Seatbelt Pretensioner Deployment Control
B0081|Driver Seat Position Sensor Circuit
B0083|Passenger Seat Position Sensor Circuit
B0092|Passenger Presence System
B0100|Electronic Front Blower Motor Control Circuit
B0122|Steering Wheel Rotation Sensor Circuit
B1000|Electronic Control Unit Internal Fault
B1001|Option Configuration Error
B1004|Instrument Cluster Internal Fault
B1200|Climate Control Push Button Circuit
B1318|Battery Voltage Low
B1342|Electronic Control Unit Defective
B1601|PATS Received Incorrect Key Code
B2205|Immobilizer Control Module Communication Error
B2477|Module Configuration Failure
B2799|Engine Immobilizer System Malfunction
";

        private const string Chassis = @"
C0035|Left Front Wheel Speed Sensor Circuit
C0036|Left Front Wheel Speed Sensor Circuit Range/Performance
C0040|Right Front Wheel Speed Sensor Circuit
C0041|Right Front Wheel Speed Sensor Circuit Range/Performance
C0045|Left Rear Wheel Speed Sensor Circuit
C0046|Left Rear Wheel Speed Sensor Circuit Range/Performance
C0050|Right Rear Wheel Speed Sensor Circuit
C0051|Right Rear Wheel Speed Sensor Circuit Range/Performance
C0060|Left Front ABS Solenoid 1 Circuit
C0065|Left Front ABS Solenoid 2 Circuit
C0070|Right Front ABS Solenoid 1 Circuit
C0075|Right Front ABS Solenoid 2 Circuit
C0080|Left Rear ABS Solenoid 1 Circuit
C0085|Left Rear ABS Solenoid 2 Circuit
C0090|Right Rear ABS Solenoid 1 Circuit
C0095|Right Rear ABS Solenoid 2 Circuit
C0110|ABS Pump Motor Circuit Malfunction
C0121|Valve Relay Circuit Malfunction
C0128|Low Brake Fluid Circuit
C0131|ABS/TCS Control Valve Malfunction
C0161|ABS/TCS Brake Switch Circuit
C0186|Lateral Accelerometer Sensor Circuit
C0196|Yaw Rate Sensor Circuit
C0242|PCM Indicated Traction Control Malfunction
C0265|Electronic Brake Control Module Relay Circuit
C0267|Pump Motor Circuit Open
C0300|Rear Speed Sensor Circuit
C0550|Electronic Control Unit Internal Performance
C0561|System Disabled Information Stored
C1095|ABS Hydraulic Pump Motor Failure
C1201|Engine Control System Malfunction
C1223|Traction Control System Malfunction
C1241|Low or High Battery Voltage
C1336|Steering Angle Sensor Zero Point Not Learned
";
    }
}
