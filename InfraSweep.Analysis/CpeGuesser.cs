using System.Text.Json;
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

    private static (string cpe, double ratio)? RateCpeRelevance(string cpe, string[] queryTokens)
    {
        if (queryTokens.Length == 0)
            return null;

        string[] cpeSplit = cpe.Split(":");

        if (cpeSplit.Length < 5)
            return null;

        string cpeProductName = cpeSplit[4];
        string cpeVendorName = cpeSplit[3];
        
        string[] productTokens = cpeProductName
            .Split(['_', '-', '.'], StringSplitOptions.RemoveEmptyEntries);

        if (productTokens.Length == 0)
            return null;

        int matchedProductTokens = productTokens.Count(pt =>
            queryTokens.Any(qt => pt.StartsWith(qt, StringComparison.OrdinalIgnoreCase)));

        // Discard product names that have too much additional tokens

        if (((double)matchedProductTokens / productTokens.Length) < 0.5)
            return null;

        // Rate cpe based on query token match count

        string cpeInfo = $"{cpeProductName} {cpeVendorName}";

        double ratio = (double)queryTokens.Count(qt => 
            cpeInfo.Contains(qt, StringComparison.OrdinalIgnoreCase)) / queryTokens.Length;

        if (ratio < 0.7)
            return null;

        // Promote when any query token fully matches product

        if (queryTokens.Any(qt => cpeProductName.Equals(qt, StringComparison.OrdinalIgnoreCase)))
        {
            ratio = 1.1;   
        }

        return (cpe, ratio);
    }

    private static string? GetBestCpeFromResponse(string? response, string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(response) || tokens.Length == 0)
            return null;
        
        return ParseCpes(response)
            .Select(cpe => RateCpeRelevance(cpe, tokens))
            .Where(ratedCpe => ratedCpe.HasValue)
            .MaxBy(ratedCpe => ratedCpe!.Value.ratio)?
            .cpe;
    }
}