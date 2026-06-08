using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;

namespace InfraSweep.Analysis;

public partial class Common
{
    [GeneratedRegex(@"cpe:2\.3:[^""]+")]
    public static partial Regex CpeRegex();

    [GeneratedRegex(@"[\s/\\]")]
    public static partial Regex HasPairSeparatorsRegex();
    
    [GeneratedRegex(@"(?<software>[a-zA-Z0-9][^\s\/\\,:]+)[\s\/\\:]+(?<version>[^\s,\/\\]*\d[^\s,\/\\\]\}\)]*)")]
    public static partial Regex SoftwareVersionPairRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9-_]+$")]
    public static partial Regex SoftwareNameRegex();

    public static Dictionary<int, string> IanaPortAssignments = new Func<Dictionary<int,string>>(() =>
    {
        Dictionary<int, string> portAssignments = [];

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(
            "InfraSweep.Analysis.Resources.iana-ports.csv"
        );

        if (stream == null)
            return portAssignments;

        using var reader = new StreamReader(stream);

        using var parser = new TextFieldParser(reader);
        parser.TextFieldType = FieldType.Delimited;
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;
        
        parser.ReadLine();

        while (!parser.EndOfData) {
            string[] columns = parser.ReadFields() ?? [];

            if (columns.Length < 4)
                continue;
            
            if (columns[2] != "tcp")
                continue;

            if (!int.TryParse(columns[1], out var port))
                continue;
            
            portAssignments.TryAdd(port, columns[3]);
        }

        return portAssignments;
    })();

    public static readonly Dictionary<string, string> KnownCpe = new()
    {
        {"linux", "cpe:2.3:o:linux:linux_kernel"},
    };

}