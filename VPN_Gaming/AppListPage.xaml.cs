#pragma warning disable CA1416
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel; // لجلب اسم حزمة التطبيق الحالي

namespace VPN_Gaming;

public partial class AppListPage : ContentPage
{
    private ObservableCollection<AppItem> DisplayedAppsList = new();
    private bool _isFreezeMode;

    public AppListPage(bool isFreezeMode = false)
    {
        InitializeComponent();
        _isFreezeMode = isFreezeMode;

        // تم تغيير الكلمات لتعكس التنظيف
        Title = _isFreezeMode ? "تحسين وتنظيف" : "قائمة توجيه الإنترنت";

        if (_isFreezeMode)
            ExecuteFreezeAllButton.Text = "تنظيف الكاش للكل 🚀";

        ExecuteFreezeAllButton.IsVisible = _isFreezeMode;

        AllAppsListView.ItemsSource = DisplayedAppsList;
        LoadAppsIntoView();
    }

    private void ShowLocalAlert(string title, string message)
    {
        LocalAlertTitle.Text = title;
        LocalAlertMessage.Text = message;
        LocalAlertOverlay.IsVisible = true;
    }

    private void OnCloseLocalAlertClicked(object? sender, EventArgs e) => LocalAlertOverlay.IsVisible = false;

    private void LoadAppsIntoView()
    {
        DisplayedAppsList.Clear();
        foreach (var app in MainPage.AllAppsList)
        {
            app.IsUISelected = _isFreezeMode ? app.IsFreezeSelected : app.IsVpnSelected;
            app.IsSwitchVisible = !_isFreezeMode;
            DisplayedAppsList.Add(app);
        }
    }

    private void OnSearchBarTextChanged(object? sender, TextChangedEventArgs e)
    {
        var keyword = e.NewTextValue?.Trim() ?? string.Empty;
        DisplayedAppsList.Clear();

        var filtered = string.IsNullOrWhiteSpace(keyword) ?
                       MainPage.AllAppsList :
                       MainPage.AllAppsList.Where(a => a.AppName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);

        foreach (var app in filtered)
        {
            DisplayedAppsList.Add(app);
        }
    }

    private void OnAppRowTapped(object? sender, TappedEventArgs e)
    {
        if (_isFreezeMode) return;

        if (sender is Grid grid && grid.BindingContext is AppItem app)
        {
            app.IsUISelected = !app.IsUISelected;
            app.IsVpnSelected = app.IsUISelected;

            if (app.IsVpnSelected && !AppManager.SelectedVpnApps.Contains(app))
                AppManager.SelectedVpnApps.Add(app);
            else if (!app.IsVpnSelected)
                AppManager.SelectedVpnApps.Remove(app);

            MainPage.SelectedAppsList.Clear();
            foreach (var vpnApp in AppManager.SelectedVpnApps)
            {
                MainPage.SelectedAppsList.Add(vpnApp);
            }

#if ANDROID
            if (AppManager.SelectedVpnApps.Count == 0)
            {
                var stopIntent = new Android.Content.Intent(Android.App.Application.Context, typeof(MyVpnService));
                stopIntent.SetAction("STOP_VPN");
                Android.App.Application.Context.StartService(stopIntent);
            }
            else if (MainPage.IsVpnActive)
            {
                Android.App.Application.Context.StartService(new Android.Content.Intent(Android.App.Application.Context, typeof(MyVpnService)));
            }
#endif
        }
    }

    private async void OnExecuteFreezeAllClicked(object? sender, EventArgs e)
    {
        if (!MainPage.IsAccessibilityEnabled())
        {
            ShowLocalAlert("صلاحية مطلوبة", "يرجى تفعيل خدمة إمكانية الوصول من الإعدادات لإتمام التنظيف.");
            return;
        }

        ExecuteFreezeAllButton.IsEnabled = false;

        foreach (var app in MainPage.AllAppsList)
        {
            // [مهم جداً] استثناء التطبيق نفسه حتى لا ينتحر
            if (app.PackageName == AppInfo.Current.PackageName) continue;

            ExecuteFreezeAllButton.Text = $"جاري تنظيف: {app.AppName}...";
            ForceStopAppViaAccessibility(app.PackageName);

            // زيادة الوقت للسماح للخدمة بالدخول للتخزين ومسح الكاش ثم العودة
            await Task.Delay(2000);
        }

        ExecuteFreezeAllButton.Text = "تم تنظيف وتسريع الجهاز!";
        await Task.Delay(2000);
        ExecuteFreezeAllButton.Text = "تنظيف الكاش للكل 🚀";
        ExecuteFreezeAllButton.IsEnabled = true;
    }

    private void ForceStopAppViaAccessibility(string packageName)
    {
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            var intent = new Android.Content.Intent(Android.Provider.Settings.ActionApplicationDetailsSettings);
            intent.SetData(Android.Net.Uri.Parse("package:" + packageName));
            intent.AddFlags(Android.Content.ActivityFlags.NewTask | Android.Content.ActivityFlags.ClearTask);
            context.StartActivity(intent);
        }
        catch { }
#endif
    }
}