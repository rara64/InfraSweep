using System.Text.RegularExpressions;

namespace InfraSweep.Analysis;

public class CpeGuesser
{
    private static List<string> ParseCpes(string cpeArrayJsonString)
    {
        return [.. Common.CpeRegex().Matches(cpeArrayJsonString)
            .Select(match => match.Value)];
    }

    public static async Task<string?> TryGetMatchingCpeFromStrings(string[] inputs, CancellationToken cancellationToken = default)
    {
        string? cpes = await Api.GuessCpeFromQuery(inputs, cancellationToken);

        if (cpes != null && cpes.Length > 2)
            return ParseCpes(cpes).FirstOrDefault();
        
        return null;
    }

    public static async Task<string?> TryGetMatchingCpeFromTokens(string input, CancellationToken cancellationToken = default)
    {
        string cleanedBanner = input
            .Replace("-", " ")
            .Replace("_", " ");
        
        string[] tokens = cleanedBanner
            .Trim()
            .Split(" ", StringSplitOptions.RemoveEmptyEntries);  

        if (tokens.Length == 0)
            return null;

        string? bestMatch = GetBestCpeFromResponse(await Api.GuessCpeFromQuery(tokens, cancellationToken), tokens);

        if (bestMatch != null)
            return bestMatch;

        foreach (string token in tokens)
        {
            bestMatch = GetBestCpeFromResponse(await Api.GuessCpeFromQuery([token], cancellationToken), tokens);

            if (bestMatch != null)
                return bestMatch;
        }

        return null;
    }

    private static bool CheckCpeRelevance(string cpe, string[] tokens)
    {
        if (tokens.Length == 0)
            return false;

        string[] cpeSplit = cpe.Split(":");

        if (cpeSplit.Length < 5)
            return false;
        
        string product = cpeSplit[4];

        string[] productSegments = [.. product
            .Split('_', '-', '.')
            .Where(s => !string.IsNullOrWhiteSpace(s))];

        int matchedSegments = productSegments.Count(seg =>
            tokens.Any(t => 
                seg.Equals(t, StringComparison.OrdinalIgnoreCase) ||
                seg.StartsWith(t, StringComparison.OrdinalIgnoreCase)));

        return ((double)matchedSegments / productSegments.Length) >= 0.5;
    }

    private static string? GetBestCpeFromResponse(string? response, string[] tokens)
    {
        if (response == null)
            return null;
        
        return ParseCpes(response)
            .Where(cpe => CheckCpeRelevance(cpe, tokens))
            .Select(match =>
            {
                string[] parts =  match.Split(":");
                string product = parts.Length > 4 ? parts[4] : "";

                int matchCount = tokens
                    .Count(t => match.Contains(t, StringComparison.OrdinalIgnoreCase));
                
                double matchRatio = (double)matchCount / tokens.Length;

                bool exactProductMatch = tokens
                    .Any(t => product.Equals(t, StringComparison.OrdinalIgnoreCase));

                return (value: match, count: matchCount, ratio: matchRatio, exactProductMatch);
            })
            .Where(x => x.ratio >= 0.7)
            .OrderByDescending(x => x.exactProductMatch)
            .ThenByDescending(x => x.count)
            .ThenByDescending(x => x.ratio)
            .FirstOrDefault().value;
    }
}