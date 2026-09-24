namespace LeadFinder.Common;

/// <summary>Polska odmiana rzeczowników po liczebnikach: 1 opinia, 2–4 opinie, 5–21 opinii, 22–24 opinie…</summary>
public static class PolishPlural
{
    public static string Choose(int count, string one, string few, string many)
    {
        if (count == 1)
            return one;

        var lastDigit = count % 10;
        var lastTwoDigits = count % 100;
        return lastDigit is >= 2 and <= 4 && lastTwoDigits is < 12 or > 14 ? few : many;
    }

    /// <summary>"opinia" / "opinie" / "opinii".</summary>
    public static string Reviews(int count) => Choose(count, "opinia", "opinie", "opinii");
}
