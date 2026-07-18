#pragma warning disable CA1416
#if ANDROID
using Android.Content;
using Android.App;
using Android.OS;
using Application = Android.App.Application;
#endif
using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using Microsoft.Maui.Graphics;
using System.Linq;
using Plugin.MauiMtAdmob;

namespace VPN_Gaming;

public partial class MainPage : ContentPage
{
    public static bool IsVpnActive { get; private set; } = false;
    private static bool isBubbleActive = false;
    private static bool isDataLoaded = false;

    private const string AdUnitId = "ca-app-pub-5092092340958197/6441550141";

    private IDispatcherTimer? _ramTimer;
    private IDispatcherTimer? _pingTimer;
    private StorageCircleDrawable _ramDrawable;
    private StorageCircleDrawable _romDrawable;

    public static MainPage? Instance { get; private set; }
    public static ObservableCollection<AppItem> AllAppsList { get; set; } = new();
    public static ObservableCollection<AppItem> SelectedAppsList { get; set; } = new();

    public MainPage()
    {
        InitializeComponent();
        Instance = this;

        _ramDrawable = new StorageCircleDrawable();
        _romDrawable = new StorageCircleDrawable();

        if (RamGraphicsView != null) RamGraphicsView.Drawable = _ramDrawable;
        if (RomGraphicsView != null) RomGraphicsView.Drawable = _romDrawable;

        if (!isDataLoaded)
        {
            LoadApps();
            isDataLoaded = true;
        }

        RestoreUIState();

        _ramTimer = Dispatcher.CreateTimer();
        _ramTimer.Interval = TimeSpan.FromSeconds(2);
        _ramTimer.Tick += (s, e) => UpdateHardwareUsage();

        _pingTimer = Dispatcher.CreateTimer();
        _pingTimer.Interval = TimeSpan.FromSeconds(3);
        _pingTimer.Tick += async (s, e) => await UpdatePingAsync();

        CrossMauiMTAdmob.Current.OnRewardedClosed += (s, e) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ShowCustomAlert("شكراً لك! ❤️", "شكراً لدعمك التطبيق وتمت المشاهدة بنجاح!");
            });
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _ramTimer?.Start();
        _pingTimer?.Start();
        UpdateHardwareUsage();
        await UpdatePingAsync();

        CrossMauiMTAdmob.Current.LoadRewarded(AdUnitId);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _ramTimer?.Stop();
        _pingTimer?.Stop();
    }

    public void ShowCustomAlert(string title, string message)
    {
        AlertTitleLabel.Text = title;
        AlertMessageLabel.Text = message;
        GeneralAlertOverlay.IsVisible = true;
    }
    private void OnCloseAlertClicked(object? sender, EventArgs e) => GeneralAlertOverlay.IsVisible = false;

    private void OnHeartClicked(object? sender, EventArgs e) => AdOverlay.IsVisible = true;
    private void OnCloseAdClicked(object? sender, EventArgs e) => AdOverlay.IsVisible = false;

    private void OnWatchAdClicked(object? sender, EventArgs e)
    {
        AdOverlay.IsVisible = false;

        if (CrossMauiMTAdmob.Current.IsRewardedLoaded())
        {
            CrossMauiMTAdmob.Current.ShowRewarded();
        }
        else
        {
            ShowCustomAlert("عذراً", "جاري تحميل الإعلان، يرجى المحاولة بعد قليل.");
            CrossMauiMTAdmob.Current.LoadRewarded(AdUnitId);
        }
    }

    private void OnGrantAccessibilityClicked(object? sender, EventArgs e)
    {
#if ANDROID
        try
        {
            var intent = new Intent(Android.Provider.Settings.ActionAccessibilitySettings);
            intent.AddFlags(ActivityFlags.NewTask);
            Application.Context.StartActivity(intent);
        }
        catch { }
#endif
        PermissionOverlay.IsVisible = false;
    }
    private void OnClosePermissionClicked(object? sender, EventArgs e) => PermissionOverlay.IsVisible = false;

    public static bool IsAccessibilityEnabled()
    {
#if ANDROID
        var context = Application.Context;
        var am = (global::Android.Views.Accessibility.AccessibilityManager?)context.GetSystemService(Context.AccessibilityService);
        if (am == null || !am.IsEnabled) return false;

        var enabledServices = global::Android.Provider.Settings.Secure.GetString(context.ContentResolver, global::Android.Provider.Settings.Secure.EnabledAccessibilityServices);
        return enabledServices?.Contains(context.PackageName ?? string.Empty) == true;
#else
        return false;
#endif
    }

    public static void ForceVpnUiUpdate(bool isActive)
    {
        IsVpnActive = isActive;
        if (Instance != null)
            MainThread.BeginInvokeOnMainThread(() => Instance.RestoreUIState());
    }

    public static void ForceBubbleUiUpdate(bool isActive) => isBubbleActive = isActive;

    private void RestoreUIState()
    {
        if (IsVpnActive)
        {
            FirewallStatusLabel.Text = "نشط";
            FirewallStatusLabel.TextColor = Color.FromArgb("#5DBB63");
            ToggleVpnMainButton.BackgroundColor = Color.FromArgb("#5DBB63");
            ToggleVpnMainButton.Text = "إيقاف توجيه الإنترنت";
        }
        else
        {
            FirewallStatusLabel.Text = "متوقف";
            FirewallStatusLabel.TextColor = Color.FromArgb("#EA4335");
            ToggleVpnMainButton.BackgroundColor = Color.FromArgb("#3b82f6");
            ToggleVpnMainButton.Text = "توجيه الإنترنت";
        }
    }

    private void UpdateHardwareUsage()
    {
#if ANDROID
        try
        {
            var activityManager = Application.Context.GetSystemService(Context.ActivityService) as Android.App.ActivityManager;
            var memoryInfo = new Android.App.ActivityManager.MemoryInfo();
            activityManager?.GetMemoryInfo(memoryInfo);

            if (memoryInfo.TotalMem > 0)
            {
                double usedRam = memoryInfo.TotalMem - memoryInfo.AvailMem;
                int ramPercent = (int)((usedRam / memoryInfo.TotalMem) * 100);
                ramPercent = Math.Clamp(ramPercent, 0, 100);

                RamStatLabel.Text = ramPercent.ToString();
                _ramDrawable.Progress = ramPercent / 100f;
                RamGraphicsView?.Invalidate();
            }

            var path = Android.OS.Environment.DataDirectory?.Path;
            if (path != null)
            {
                var stat = new Android.OS.StatFs(path);
                long totalRom = stat.BlockCountLong * stat.BlockSizeLong;
                long freeRom = stat.AvailableBlocksLong * stat.BlockSizeLong;
                long usedRom = totalRom - freeRom;

                if (totalRom > 0)
                {
                    int romPercent = (int)(((double)usedRom / totalRom) * 100);
                    romPercent = Math.Clamp(romPercent, 0, 100);

                    RomStatLabel.Text = romPercent.ToString();
                    _romDrawable.Progress = romPercent / 100f;
                    RomGraphicsView?.Invalidate();
                }
            }
        }
        catch { }
#endif
    }

    private async Task UpdatePingAsync()
    {
        try
        {
            using Ping myPing = new Ping();
            PingReply reply = await myPing.SendPingAsync("8.8.8.8", 2000);
            if (reply.Status == IPStatus.Success)
            {
                long pingValue = reply.RoundtripTime;
                PingValueSpan.Text = $"{pingValue} ms";

                if (pingValue < 100)
                    PingTextSpan.TextColor = Color.FromArgb("#5DBB63");
                else if (pingValue < 200)
                    PingTextSpan.TextColor = Color.FromArgb("#F4B400");
                else
                    PingTextSpan.TextColor = Color.FromArgb("#EA4335");
            }
            else
            {
                PingValueSpan.Text = "Error ms";
                PingTextSpan.TextColor = Color.FromArgb("#EA4335");
            }
        }
        catch
        {
            PingValueSpan.Text = "Error ms";
            PingTextSpan.TextColor = Color.FromArgb("#EA4335");
        }
    }

    private void LoadApps()
    {
        var apps = AppManager.GetApps();
        foreach (var app in apps.OrderBy(a => a.AppName))
        {
            AllAppsList.Add(app);
        }
    }

    private async void OnManageVpnAppsClicked(object? sender, EventArgs e) => await Navigation.PushAsync(new AppListPage(isFreezeMode: false));

    private void OnFreezeAppsClicked(object? sender, EventArgs e)
    {
        ShowCustomAlert("قيد التطوير 🚧", "هذه الميزة قيد التطوير حالياً، ستتوفر قريباً لتقديم أفضل أداء لجهازك.");
    }

    private void OnToggleVpnClicked(object? sender, EventArgs e)
    {
        if (!IsVpnActive && AppManager.SelectedVpnApps.Count == 0 && SelectedAppsList.Count == 0)
        {
            ShowCustomAlert("تنبيه", "يجب إضافة تطبيق واحد على الأقل من 'إضافة التطبيقات'.");
            return;
        }
#if ANDROID
        if (!IsVpnActive)
        {
            var intent = Android.Net.VpnService.Prepare(Application.Context);
            if (intent != null)
                Platform.CurrentActivity?.StartActivityForResult(intent, 0);
            else
                StartVpnService();
        }
        else
        {
            StopVpnService();
        }
#endif
    }
#if ANDROID
    private void StartVpnService()
    {
        var serviceIntent = new Intent(Application.Context, typeof(MyVpnService));
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            Application.Context.StartForegroundService(serviceIntent);
        else
            Application.Context.StartService(serviceIntent);
    }

    private void StopVpnService()
    {
        var stopIntent = new Intent(Application.Context, typeof(MyVpnService));
        stopIntent.SetAction("STOP_VPN");
        Application.Context.StartService(stopIntent);
    }
#endif
}

public class StorageCircleDrawable : IDrawable
{
    public float Progress { get; set; } = 0f;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float size = Math.Min(dirtyRect.Width, dirtyRect.Height);
        if (size <= 0) return;

        float stroke = 4f;
        float padding = stroke / 2;
        float drawSize = size - stroke;

        canvas.StrokeColor = Color.FromArgb("#5DBB63");
        canvas.StrokeSize = stroke;
        canvas.DrawEllipse(padding, padding, drawSize, drawSize);

        if (Progress <= 0) return;

        canvas.StrokeColor = Color.FromArgb("#EA4335");
        canvas.StrokeSize = stroke;
        canvas.StrokeLineCap = LineCap.Round;

        float endAngle = 90 - (Progress * 360);
        canvas.DrawArc(padding, padding, drawSize, drawSize, 90, endAngle, true, false);
    }
}
