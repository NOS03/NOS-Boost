using Microsoft.Maui.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VPN_Gaming;

public class AppItem : INotifyPropertyChanged
{
    public string AppName { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public ImageSource? AppIcon { get; set; }

    private bool isUISelected;
    public bool IsUISelected
    {
        get => isUISelected;
        set
        {
            if (isUISelected != value)
            {
                isUISelected = value;
                OnPropertyChanged();
            }
        }
    }

    private bool isSwitchVisible = true;
    public bool IsSwitchVisible
    {
        get => isSwitchVisible;
        set
        {
            if (isSwitchVisible != value)
            {
                isSwitchVisible = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsVpnSelected { get; set; } = false;
    public bool IsFreezeSelected { get; set; } = false;

    public bool IsAllowed
    {
        get => IsVpnSelected;
        set { IsVpnSelected = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}