#pragma warning disable CA1416
#if ANDROID
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Microsoft.Maui.Controls.Platform;
using Application = Android.App.Application;
using Color = Android.Graphics.Color;

namespace VPN_Gaming;

[Service(Exported = false)]
public class FloatingWidgetService : Service
{
    private IWindowManager? windowManager;
    private Android.Views.View? floatingView;
    private TextView? firewallButton;
    private WindowManagerLayoutParams? layoutParams;

    private int initialX, initialY;
    private float initialTouchX, initialTouchY;
    private bool isDragging = false;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        MainPage.ForceBubbleUiUpdate(true);
        return StartCommandResult.Sticky;
    }

    public override void OnCreate()
    {
        base.OnCreate();
        windowManager = GetSystemService(WindowService).JavaCast<IWindowManager>();

        layoutParams = new WindowManagerLayoutParams(
            ViewGroup.LayoutParams.WrapContent,
            ViewGroup.LayoutParams.WrapContent,
            Build.VERSION.SdkInt >= BuildVersionCodes.O ? WindowManagerTypes.ApplicationOverlay : WindowManagerTypes.Phone,
            WindowManagerFlags.NotFocusable | WindowManagerFlags.LayoutNoLimits,
            Format.Translucent)
        {
            Gravity = GravityFlags.Top | GravityFlags.Start,
            X = 50,
            Y = 200
        };

        var background = new GradientDrawable();
        background.SetColor(Color.ParseColor("#EE121212")); // لون داكن شفاف قليلاً
        background.SetStroke(2, Color.ParseColor("#3b82f6")); // إطار أزرق أنيق
        background.SetCornerRadius(30f);

        var layout = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
            LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
        };

        layout.SetBackground(background);
        layout.SetPadding(30, 15, 30, 25);
        layout.Elevation = 15f;

        // --- زر الإغلاق ✕ ---
        var closeButton = new TextView(this)
        {
            Text = "✕",
            TextSize = 12f
        };
        closeButton.SetTextColor(Color.ParseColor("#888888"));
        closeButton.Gravity = GravityFlags.Right;
        closeButton.SetPadding(0, 0, 0, 10);
        closeButton.Click += (sender, e) => StopSelf();

        // --- زر التحكم بالجدار الناري (يعتمد على الحالة الحقيقية من الواجهة) ---
        bool isActive = MainPage.IsVpnActive;
        firewallButton = new TextView(this)
        {
            Text = isActive ? "🛡️ نشط" : "🛡️ متوقف",
            TextSize = 14f
        };
        firewallButton.SetTextColor(Color.ParseColor(isActive ? "#34A853" : "#EA4335"));
        firewallButton.SetTypeface(null, TypefaceStyle.Bold);
        firewallButton.Gravity = GravityFlags.Center;

        layout.AddView(closeButton);
        layout.AddView(firewallButton);
        floatingView = layout;

        // --- التمييز بين السحب والضغط السريع ---
        firewallButton.Touch += (sender, e) =>
        {
            if (e.Event == null || layoutParams == null || windowManager == null) return;

            switch (e.Event.Action)
            {
                case MotionEventActions.Down:
                    initialX = layoutParams.X;
                    initialY = layoutParams.Y;
                    initialTouchX = e.Event.RawX;
                    initialTouchY = e.Event.RawY;
                    isDragging = false;
                    break;

                case MotionEventActions.Move:
                    float deltaX = e.Event.RawX - initialTouchX;
                    float deltaY = e.Event.RawY - initialTouchY;

                    // إذا تحركت الإصبع أكثر من 10 بكسل، نعتبرها سحباً وليس ضغطة
                    if (Math.Abs(deltaX) > 10 || Math.Abs(deltaY) > 10)
                    {
                        isDragging = true;
                        layoutParams.X = initialX + (int)deltaX;
                        layoutParams.Y = initialY + (int)deltaY;
                        windowManager.UpdateViewLayout(floatingView, layoutParams);
                    }
                    break;

                case MotionEventActions.Up:
                    // إذا لم تكن سحباً، إذن هي ضغطة لتشغيل/إيقاف الجدار
                    if (!isDragging)
                    {
                        ToggleVpn();
                    }
                    break;
            }
        };

        windowManager?.AddView(floatingView, layoutParams);
    }

    private void ToggleVpn()
    {
        if (MainPage.IsVpnActive)
        {
            // إرسال أمر الإيقاف الإجباري
            var stopIntent = new Intent(Application.Context, typeof(MyVpnService));
            stopIntent.SetAction("STOP_VPN");
            Application.Context.StartService(stopIntent);

            firewallButton!.Text = "🛡️ متوقف";
            firewallButton.SetTextColor(Color.ParseColor("#EA4335"));
        }
        else
        {
            // منع التشغيل إذا كانت قائمة التطبيقات فارغة
            if (MainPage.SelectedAppsList.Count == 0)
            {
                Toast.MakeText(Application.Context, "يجب إضافة تطبيق واحد على الأقل من الواجهة!", ToastLength.Long)?.Show();
                return;
            }

            AppManager.AllApps = MainPage.AllAppsList.ToList();
            var vpnIntent = new Intent(Application.Context, typeof(MyVpnService));
            Application.Context.StartService(vpnIntent);

            firewallButton!.Text = "🛡️ نشط";
            firewallButton.SetTextColor(Color.ParseColor("#34A853"));
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (floatingView != null) windowManager?.RemoveView(floatingView);
        MainPage.ForceBubbleUiUpdate(false);
    }
}
#endif