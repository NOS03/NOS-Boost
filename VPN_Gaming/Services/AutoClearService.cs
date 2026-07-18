#if ANDROID
using Android.AccessibilityServices;
using Android.App;
using Android.Content;
using Android.Views.Accessibility;
using System.Threading.Tasks;

namespace VPN_Gaming.Services;

[Service(Label = "NOS Boost Cleaner Service", Permission = "android.permission.BIND_ACCESSIBILITY_SERVICE", Exported = true)]
[IntentFilter(new[] { "android.accessibilityservice.AccessibilityService" })]
public class AutoClearService : AccessibilityService
{
    protected override void OnServiceConnected()
    {
        base.OnServiceConnected();
        var info = new AccessibilityServiceInfo
        {
            EventTypes = EventTypes.WindowContentChanged | EventTypes.WindowStateChanged,
            FeedbackType = FeedbackFlags.Generic,
            Flags = AccessibilityServiceFlags.RetrieveInteractiveWindows | AccessibilityServiceFlags.IncludeNotImportantViews
        };
        SetServiceInfo(info);
    }

    public override void OnAccessibilityEvent(AccessibilityEvent? e)
    {
        if (e?.Source == null) return;

        // 1. الدخول إلى إعدادات التخزين (كل المرادفات الممكنة لأنظمة أندرويد المختلفة)
        string[] storageKeywords = { "استخدام سعة التخزين", "مكان التخزين", "التخزين وذاكرة التخزين المؤقت", "مساحة التخزين", "التخزين", "Storage", "Storage & cache" };
        ClickNodeIfFound(e.Source, storageKeywords);

        // 2. الضغط على زر مسح الكاش
        string[] cacheKeywords = { "مسح التخزين المؤقت", "مسح ذاكرة التخزين المؤقت", "مسح الكاش", "Clear cache" };
        bool isCacheCleared = ClickNodeIfFound(e.Source, cacheKeywords);

        // 3. الرجوع للخلف بعد نجاح المسح للانتقال للتطبيق التالي
        if (isCacheCleared)
        {
            Task.Delay(400).ContinueWith(_ => PerformGlobalAction(GlobalAction.Back));
            Task.Delay(800).ContinueWith(_ => PerformGlobalAction(GlobalAction.Back));
        }
    }

    // دالة مساعدة ذكية للبحث والضغط
    private bool ClickNodeIfFound(AccessibilityNodeInfo source, string[] targetTexts)
    {
        foreach (var text in targetTexts)
        {
            var nodes = source.FindAccessibilityNodeInfosByText(text);
            if (nodes != null && nodes.Count > 0)
            {
                foreach (var node in nodes)
                {
                    if (node.Enabled && node.Clickable)
                    {
                        node.PerformAction(global::Android.Views.Accessibility.Action.Click);
                        return true;
                    }
                }
            }
        }
        return false;
    }

    public override void OnInterrupt() { }
}
#endif