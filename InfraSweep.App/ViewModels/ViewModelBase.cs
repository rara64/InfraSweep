using CommunityToolkit.Mvvm.ComponentModel;
using InfraSweep.App.Services;

namespace InfraSweep.App.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    public static LocalizationService? Locale {get; set;}
}
