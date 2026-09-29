using System.Text.RegularExpressions;

namespace LeadFinder.Services;

/// <summary>
/// Zamienia to, co użytkownik wkleił (e-mail, "@salon", "instagram.com/salon", pełny link), na jednolitą postać.
/// Null = pusta wartość (czyści pole); <see cref="FormatException"/> = nie da się tego rozpoznać.
/// </summary>
public static partial class ContactNormalizer
{
    public static string? Email(string? input)
    {
        var value = input?.Trim().Trim('<', '>').Replace("mailto:", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        if (string.IsNullOrEmpty(value))
            return null;
        return EmailRegex().IsMatch(value) ? value : throw new FormatException($"„{input}” nie wygląda na adres e-mail.");
    }

    public static string? Instagram(string? input)
    {
        var value = input?.Trim();
        if (string.IsNullOrEmpty(value))
            return null;

        var handle = InstagramUrlRegex().Match(value) is { Success: true } m ? m.Groups["handle"].Value : value.TrimStart('@');
        return InstagramHandleRegex().IsMatch(handle)
            ? $"https://www.instagram.com/{handle.TrimEnd('/')}/"
            : throw new FormatException($"„{input}” nie wygląda na profil Instagrama (np. @salon albo link do profilu).");
    }

    public static string? Facebook(string? input)
    {
        var value = input?.Trim();
        if (string.IsNullOrEmpty(value))
            return null;
        if (!value.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            value = "https://" + value.TrimStart('/');

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !(uri.Host.EndsWith("facebook.com", StringComparison.OrdinalIgnoreCase)
                 || uri.Host.Equals("fb.com", StringComparison.OrdinalIgnoreCase)
                 || uri.Host.Equals("fb.me", StringComparison.OrdinalIgnoreCase))
            || uri.AbsolutePath.Trim('/').Length == 0)
        {
            throw new FormatException($"„{input}” nie wygląda na stronę na Facebooku (wklej link do profilu salonu).");
        }

        // profile.php?id=… potrzebuje zapytania; zwykłe strony – sama ścieżka, bez śledzących parametrów.
        return uri.AbsolutePath.Contains("profile.php", StringComparison.OrdinalIgnoreCase)
            ? $"https://www.facebook.com{uri.PathAndQuery}"
            : $"https://www.facebook.com/{uri.AbsolutePath.Trim('/')}/";
    }

    [GeneratedRegex("""^[a-z0-9][a-z0-9._%+-]{0,63}@[a-z0-9-]+(\.[a-z0-9-]+)*\.[a-z]{2,24}$""", RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex("""instagram\.com/(?<handle>[A-Za-z0-9._]{1,30})""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InstagramUrlRegex();

    [GeneratedRegex("""^[A-Za-z0-9._]{1,30}/?$""", RegexOptions.CultureInvariant)]
    private static partial Regex InstagramHandleRegex();
}
