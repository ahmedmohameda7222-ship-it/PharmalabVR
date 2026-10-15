using System;

namespace PharmaLabVR.UI
{
    // Presentation locale is session-safe and distinct from chemistry and
    // ergonomic preferences. Arabic shaping must be performed by RTLTMPro.
    public static class LanguageSelection
    {
        public static string Current { get; private set; } = "en";
        public static bool IsRightToLeft => Current == "ar";
        public static event Action<string> Changed;

        public static void Select(string locale)
        {
            if (locale != "en" && locale != "ar")
                throw new ArgumentException("Language must be a pinned en/ar locale.", nameof(locale));
            if (Current == locale) return;
            Current = locale;
            Changed?.Invoke(locale);
        }

        public static void Toggle() => Select(Current == "en" ? "ar" : "en");
    }
}
