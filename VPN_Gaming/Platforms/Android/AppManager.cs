#if ANDROID
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Application = Android.App.Application;
#endif
using Microsoft.Maui.Controls;

namespace VPN_Gaming;

public static class AppManager
{
    public static List<AppItem> AllApps = new();

    // قوائم منفصلة تماماً
    public static List<AppItem> SelectedVpnApps = new();
    public static List<AppItem> SelectedFreezeApps = new();

    public static List<AppItem> GetApps()
    {
        var apps = new List<AppItem>();
#if ANDROID
        var pm = Application.Context.PackageManager;
        if (pm == null) return apps;

        var packages = pm.GetInstalledPackages(PackageInfoFlags.MatchUninstalledPackages);
        if (packages == null) return apps;

        foreach (var pkg in packages)
        {
            if (string.IsNullOrEmpty(pkg.PackageName)) continue;

            if (pm.GetLaunchIntentForPackage(pkg.PackageName) != null)
            {
                string appName = pkg.ApplicationInfo?.LoadLabel(pm)?.ToString() ?? "بدون اسم";
                var drawable = pkg.ApplicationInfo?.LoadIcon(pm);

                apps.Add(new AppItem
                {
                    AppName = appName,
                    PackageName = pkg.PackageName,
                    IsVpnSelected = false,
                    IsFreezeSelected = false,
                    IsUISelected = false,
                    AppIcon = GetLightweightIcon(drawable)
                });
            }
        }
#endif
        return apps;
    }

#if ANDROID
    private static ImageSource? GetLightweightIcon(Drawable? drawable)
    {
        if (drawable == null) return null;

        try
        {
            Bitmap bitmap = Bitmap.CreateBitmap(96, 96, Bitmap.Config.Argb8888!);
            Canvas canvas = new Canvas(bitmap);
            drawable.SetBounds(0, 0, canvas.Width, canvas.Height);
            drawable.Draw(canvas);

            MemoryStream ms = new MemoryStream();
            bitmap.Compress(Bitmap.CompressFormat.Png!, 100, ms);
            ms.Position = 0;
            return ImageSource.FromStream(() => new MemoryStream(ms.ToArray()));
        }
        catch
        {
            return null;
        }
    }
#endif
}