namespace InfraSweep.App.Helpers;

public class ResourceHelper
{
    public static string GetString(string key)
    {
        if (Avalonia.Application.Current?.TryGetResource(key, null, out var value) == true)
            return value as string ?? key;
        
        return key;
    }
}