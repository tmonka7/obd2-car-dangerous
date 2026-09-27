using System.Text;

namespace obd_car_dangerous.Services
{
    /// <summary>One entry loaded from obd2.csv.</summary>
    internal sealed record CatalogEntry(string Code, string Description, string Korean, string Chinese)
    {
        public char Letter => Code[0];

        public string LocalizedDescription => Loc.DtcText(Description, Korean, Chinese);

        public string Category => Letter switch
        {
            'P' => "Powertrain",
            'B' => "Body",
            'C' => "Chassis",
            _ => "Network",
        };
    }

    /// <summary>Loads the OBD2 dictionary from the editable obd2.csv file.</summary>
    internal static class DtcCatalog
    {
        private static readonly Lazy<IReadOnlyList<CatalogEntry>> Entries = new(Load);

        public static IReadOnlyList<CatalogEntry> All => Entries.Value;

        public static int Count => Entries.Value.Count;

        public static CatalogEntry? Find(string code) =>
            Entries.Value.FirstOrDefault(e => string.Equals(e.Code, code, StringComparison.OrdinalIgnoreCase));

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
                    e.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    e.Korean.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    e.Chinese.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            return source.ToList();
        }

        public static string FamilyOf(string code)
        {
            if (code.Length < 3)
            {
                return string.Empty;
            }

            if (code[0] == 'P')
            {
                if (code.Length >= 3 && code[2] == 'A')
                {
                    return code[1] switch
                    {
                        '0' => "Hybrid / EV drive battery and inverter system",
                        '1' => "Hybrid / EV motor and generator control system",
                        '2' => "Hybrid / EV battery current, isolation and cooling",
                        _ => "Hybrid / EV powertrain"
                    };
                }

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

        private static IReadOnlyList<CatalogEntry> Load()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "obd2.csv");
            if (!File.Exists(path))
            {
                return Array.Empty<CatalogEntry>();
            }

            var entries = new List<CatalogEntry>();
            int englishColumn = 1;
            int koreanColumn = 2;
            int chineseColumn = 3;
            foreach (string line in File.ReadLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] columns = ParseCsvLine(line);
                if (columns.Length < 2)
                {
                    continue;
                }

                string code = columns[0].Trim().ToUpperInvariant();
                if (string.Equals(code, "CODE", StringComparison.OrdinalIgnoreCase))
                {
                    englishColumn = HeaderColumn(columns, "english", englishColumn);
                    koreanColumn = HeaderColumn(columns, "korean", koreanColumn);
                    chineseColumn = HeaderColumn(columns, "chinese", chineseColumn);
                    continue;
                }

                if (code.Length != 5 ||
                    code.Any(c => c is not (>= '0' and <= '9') and not (>= 'A' and <= 'F')) ||
                    code[0] is not ('P' or 'B' or 'C' or 'U'))
                {
                    continue;
                }

                entries.Add(new CatalogEntry(
                    code,
                    Column(columns, englishColumn),
                    Column(columns, koreanColumn),
                    Column(columns, chineseColumn)));
            }

            return entries
                .Where(e => e.Description.Length > 0)
                .GroupBy(e => e.Code, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(e => e.Code, StringComparer.Ordinal)
                .ToList();
        }

        private static int HeaderColumn(string[] columns, string name, int fallback)
        {
            for (int i = 0; i < columns.Length; i++)
            {
                string header = columns[i].Trim().Trim('\uFEFF').ToLowerInvariant();
                if (header == name)
                {
                    return i;
                }
            }

            return fallback;
        }

        private static string Column(string[] columns, int index) =>
            index >= 0 && index < columns.Length ? columns[index].Trim() : string.Empty;

        private static string[] ParseCsvLine(string line)
        {
            var values = new List<string>();
            var value = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < line.Length; i++)
            {
                char current = line[i];
                if (current == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        value.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (current == ',' && !quoted)
                {
                    values.Add(value.ToString());
                    value.Clear();
                }
                else
                {
                    value.Append(current);
                }
            }

            values.Add(value.ToString());
            return values.ToArray();
        }
    }
}
