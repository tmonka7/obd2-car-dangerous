namespace obd_car_dangerous.Services.Obd
{
    /// <summary>What kind of hardware an adapter is, which decides how the app talks to it.</summary>
    internal enum AdapterFamily
    {
        /// <summary>ELM327 clone on Bluetooth Low Energy (GATT).</summary>
        Elm327Ble,

        /// <summary>ELM327 clone on Bluetooth Classic (RFCOMM serial port profile).</summary>
        Elm327Spp,

        /// <summary>ELM327 on a USB cable, which Windows shows as a COM port.</summary>
        Elm327Usb,

        /// <summary>A manufacturer's own interface that does not speak ELM327 AT commands.</summary>
        Proprietary,

        Unknown,
    }

    /// <summary>
    /// What we know about an adapter model: how to recognise it from its Bluetooth name, which
    /// pairing PINs it uses, and whether it speaks ELM327 at all.
    /// </summary>
    internal sealed record AdapterProfile(
        string Model,
        AdapterFamily Family,
        string[] Pins,
        string Note)
    {
        /// <summary>False for interfaces that need their maker's own software.</summary>
        public bool SpeaksElm327 => Family != AdapterFamily.Proprietary;
    }

    /// <summary>Recognises the adapters people actually buy and says how to treat each one.</summary>
    internal static class AdapterCatalog
    {
        /// <summary>PINs the cheap clones ship with, tried in this order when pairing.</summary>
        public static readonly string[] CommonPins = { "1234", "0000", "6789" };

        private static readonly (string[] Patterns, AdapterProfile Profile)[] Known =
        {
            // Autel and other makers' own interfaces: Bluetooth, but not ELM327.
            (new[] { "AUTEL", "MAXI", "AP200", "BT506", "BT609", "MVCI", "VCMINI" },
                new AdapterProfile("Autel MaxiVCI", AdapterFamily.Proprietary, Array.Empty<string>(),
                    "Autel interfaces use Autel's own protocol, not ELM327 AT commands.")),
            (new[] { "VCX", "VXDIAG", "ODIS", "ISTA" },
                new AdapterProfile("Manufacturer interface", AdapterFamily.Proprietary, Array.Empty<string>(),
                    "This interface speaks a manufacturer protocol, not ELM327.")),

            // HH OBD Advanced: the BLE 4.0 version advertises as OBDBLE / IOS-Vlink, the older one as HHOBD.
            (new[] { "OBDBLE", "IOS-VLINK", "V-LINK", "VLINK" },
                new AdapterProfile("HH OBD Advanced (BLE)", AdapterFamily.Elm327Ble, Array.Empty<string>(),
                    "ELM327 clone on Bluetooth LE.")),
            (new[] { "HHOBD", "HH OBD" },
                new AdapterProfile("HH OBD Advanced", AdapterFamily.Elm327Spp, CommonPins,
                    "ELM327 clone; the BLE version appears as OBDBLE.")),

            // Vgate sells both flavours under similar names.
            (new[] { "ICAR PRO", "VGATE" },
                new AdapterProfile("Vgate iCar Pro", AdapterFamily.Elm327Ble, CommonPins,
                    "ELM327 clone, usually Bluetooth LE.")),

            // The classic blue mini dongle and the orange/blue "OBDII Interface" box.
            (new[] { "OBDII", "OBD II", "OBD2", "ELM327", "ELM-327", "MINI327", "OBDCHECK", "VEEPEAK", "KONNWEI", "VIECAR" },
                new AdapterProfile("ELM327 clone", AdapterFamily.Elm327Spp, CommonPins,
                    "Classic ELM327 clone over Bluetooth serial.")),
        };

        /// <summary>Identifies an adapter from its advertised name and the transport it was seen on.</summary>
        public static AdapterProfile Identify(string name, EndpointKind kind)
        {
            if (kind == EndpointKind.Demo)
            {
                return new AdapterProfile("Simulator", AdapterFamily.Unknown, Array.Empty<string>(),
                    "Simulated data, no hardware involved.");
            }

            string upper = (name ?? string.Empty).ToUpperInvariant();

            foreach ((string[] patterns, AdapterProfile profile) in Known)
            {
                if (patterns.Any(p => upper.Contains(p, StringComparison.Ordinal)))
                {
                    // A model sold in both flavours is whatever we actually found it on.
                    return profile.SpeaksElm327 && kind != EndpointKind.Demo
                        ? profile with { Family = FamilyFor(kind, profile.Family) }
                        : profile;
                }
            }

            return new AdapterProfile("Unknown adapter", FamilyFor(kind, AdapterFamily.Unknown), CommonPins,
                "Not a model we know; the app will try it as an ELM327.");
        }

        private static AdapterFamily FamilyFor(EndpointKind kind, AdapterFamily fallback) => kind switch
        {
            EndpointKind.Ble => AdapterFamily.Elm327Ble,
            EndpointKind.BluetoothSpp => AdapterFamily.Elm327Spp,
            EndpointKind.Serial => AdapterFamily.Elm327Usb,
            _ => fallback,
        };

        /// <summary>Short label for the connection list.</summary>
        public static string FamilyLabel(AdapterFamily family) => family switch
        {
            AdapterFamily.Elm327Ble => "ELM327 · Bluetooth LE",
            AdapterFamily.Elm327Spp => "ELM327 · Bluetooth serial",
            AdapterFamily.Elm327Usb => "ELM327 · USB / COM port",
            AdapterFamily.Proprietary => "Proprietary interface",
            _ => "Unknown",
        };

        /// <summary>True when a name looks like an OBD2 adapter rather than a headset or a watch.</summary>
        public static bool LooksLikeAdapter(string name)
        {
            string upper = (name ?? string.Empty).ToUpperInvariant();

            return Known.SelectMany(k => k.Patterns).Any(p => upper.Contains(p, StringComparison.Ordinal)) ||
                   upper.Contains("OBD") || upper.Contains("SCAN") || upper.Contains("CARISTA") ||
                   upper.Contains("LELINK") || upper.Contains("TORQUE");
        }
    }
}
