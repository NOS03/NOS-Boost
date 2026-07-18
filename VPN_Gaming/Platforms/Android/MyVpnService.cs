#pragma warning disable CA1416
using Android.App;
using Android.Content;
using Android.Net;
using Android.OS;

namespace VPN_Gaming;

[Service(Permission = "android.permission.BIND_VPN_SERVICE", Exported = false)]
[IntentFilter(new[] { "android.net.VpnService" })]
public class MyVpnService : VpnService
{
    private ParcelFileDescriptor? vpnInterface;
    private const string ChannelId = "VpnServiceChannel";
    private const int NotificationId = 1;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        // استقبال أمر الإيقاف الإجباري
        if (intent?.Action == "STOP_VPN")
        {
            CleanupService();
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        // إغلاق الواجهة القديمة إن وجدت
        CloseVpnInterface();

        CreateNotificationChannel();

        var notification = new Notification.Builder(this, ChannelId)
            .SetContentTitle("الجدار الناري نشط")
            .SetContentText("تم عزل الإنترنت عن الجهاز باستثناء التطبيقات المحددة")
            .SetSmallIcon(Android.Resource.Drawable.IcDialogInfo)
            .SetOngoing(true)
            .Build();

        StartForeground(NotificationId, notification);

        // إنشاء واجهة VPN الوهمية (الثقب الأسود)
        var builder = new Builder(this)
            .AddAddress("10.0.0.2", 32)
            .AddRoute("0.0.0.0", 0) // حجب IPv4
            .AddRoute("::", 0)      // حجب IPv6
            .SetSession("NOS Firewall");

        // 1. استثناء التطبيق نفسه حتى لا ينعزل عن النظام
        if (!string.IsNullOrEmpty(PackageName))
        {
            try
            {
                builder.AddDisallowedApplication(PackageName);
            }
            catch (Exception ex)
            {
                Android.Util.Log.Warn("MyVpnService", $"فشل استثناء التطبيق نفسه: {ex.Message}");
            }
        }

        // 2. استثناء التطبيقات المختارة من قبل المستخدم
        var selectedApps = MainPage.SelectedAppsList;
        if (selectedApps != null && selectedApps.Count > 0)
        {
            foreach (var app in selectedApps)
            {
                if (app?.PackageName == null) continue;

                try
                {
                    // استبعاد التطبيق من الـ VPN ليستخدم الإنترنت العادي مباشرة
                    builder.AddDisallowedApplication(app.PackageName);
                }
                catch (Exception ex)
                {
                    Android.Util.Log.Warn("MyVpnService", $"فشل استثناء {app.PackageName}: {ex.Message}");
                }
            }
        }

        vpnInterface = builder.Establish();

        if (vpnInterface == null)
        {
            Android.Util.Log.Error("MyVpnService", "فشل إنشاء واجهة VPN");
            CleanupService();
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        ToggleDnd(true);
        MainPage.ForceVpnUiUpdate(true);

        return StartCommandResult.Sticky;
    }

    public override void OnTaskRemoved(Intent? rootIntent)
    {
        // تنظيف الخدمة عند إزالة التطبيق من المهام الأخيرة
        CleanupService();
        StopSelf();
        base.OnTaskRemoved(rootIntent);
    }

    public override void OnRevoke()
    {
        CleanupService();
        StopSelf();
        base.OnRevoke();
    }

    public override void OnDestroy()
    {
        CleanupService();
        base.OnDestroy();
    }

    private void CleanupService()
    {
        CloseVpnInterface();
        StopForeground(StopForegroundFlags.Remove);
        ToggleDnd(false);
        MainPage.ForceVpnUiUpdate(false);
    }

    private void CloseVpnInterface()
    {
        if (vpnInterface != null)
        {
            try
            {
                vpnInterface.Close();
            }
            catch (Exception ex)
            {
                Android.Util.Log.Warn("MyVpnService", $"خطأ عند إغلاق الواجهة: {ex.Message}");
            }
            finally
            {
                vpnInterface = null;
            }
        }
    }

    private void ToggleDnd(bool enable)
    {
        try
        {
            var notificationManager = GetSystemService(NotificationService) as NotificationManager;
            if (notificationManager != null && notificationManager.IsNotificationPolicyAccessGranted)
            {
                notificationManager.SetInterruptionFilter(
                    enable ? InterruptionFilter.Alarms : InterruptionFilter.All);
            }
        }
        catch (Exception ex)
        {
            Android.Util.Log.Warn("MyVpnService", $"خطأ في تبديل DND: {ex.Message}");
        }
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            try
            {
                var channel = new NotificationChannel(
                    ChannelId,
                    "خدمة الجدار الناري",
                    NotificationImportance.Low);

                var manager = GetSystemService(NotificationService) as NotificationManager;
                manager?.CreateNotificationChannel(channel);
            }
            catch (Exception ex)
            {
                Android.Util.Log.Warn("MyVpnService", $"خطأ في إنشاء قناة الإشعارات: {ex.Message}");
            }
        }
    }
}