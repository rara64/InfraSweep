using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InfraSweep.App.Services;

public partial class LocalizationService : ObservableObject
{
    private Dictionary<string, string> localizationStrings = [];

    [ObservableProperty]
    private string currentCulture = "en";

    public string this[string key] =>
        localizationStrings.TryGetValue(key, out var value) ? value : key;

    public void LoadCulture(string cultureCode)
    {
        CurrentCulture = cultureCode;
        CultureInfo.CurrentUICulture = new CultureInfo(CurrentCulture);

        var uri = new Uri($"avares://InfraSweep.App/Assets/Locales/{cultureCode}.json");
        using var stream = AssetLoader.Open(uri);
        using var reader = new StreamReader(stream);

        localizationStrings = JsonSerializer.Deserialize<Dictionary<string, string>>(
            reader.ReadToEnd()
        ) ?? [];

        OnPropertyChanged();
    }
}