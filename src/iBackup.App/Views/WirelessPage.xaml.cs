namespace iBackup.App.Views;

using iBackup.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

/// <summary>
/// Code-behind for the WirelessPage hosting local Wi-Fi pairing and synchronization.
/// </summary>
public sealed partial class WirelessPage : Page
{
    /// <summary>
    /// Gets the strongly typed view model.
    /// </summary>
    public WirelessViewModel ViewModel { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WirelessPage"/> class.
    /// </summary>
    public WirelessPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<WirelessViewModel>();
    }
}

