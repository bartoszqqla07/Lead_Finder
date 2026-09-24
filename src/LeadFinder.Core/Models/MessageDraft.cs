namespace LeadFinder.Models;

/// <summary>Rodzaj szkicu – odpowiada kanałowi i etapowi kontaktu.</summary>
public enum DraftKind
{
    // ----- Pierwszy kontakt (bez zgody) -----

    /// <summary>Sama informacja o niedziałającej stronie, bez oferty (tylko dla "strona nie działa").</summary>
    ProblemNotice,

    /// <summary>Krótka wiadomość na Instagramie/Facebooku z prośbą o zgodę na przesłanie propozycji.</summary>
    DirectMessage,

    /// <summary>E-mail lub formularz kontaktowy z prośbą o zgodę.</summary>
    Email,

    /// <summary>List papierowy – może zawierać ofertę i klauzulę informacyjną RODO.</summary>
    Letter,

    // ----- Po zgodzie -----

    /// <summary>Pierwsza odpowiedź na "możesz podesłać": podgląd (screenshot makiety), bez ceny.</summary>
    Preview,

    /// <summary>Zwięzła oferta na komunikator: co dostają, dlaczego nie szablon, cena, czas.</summary>
    OfferShort,

    /// <summary>Pełna oferta na e-mail: sekcje, proces współpracy, koszt.</summary>
    Proposal,

    /// <summary>Odpowiedź na pytanie "ile to kosztuje?".</summary>
    PriceReply,

    /// <summary>Jedno przypomnienie – po ok. tygodniu od oferty.</summary>
    FollowUp,
}

/// <summary>Szkic wiadomości do ręcznego przejrzenia i wysłania.</summary>
/// <param name="Kind">Rodzaj szkicu.</param>
/// <param name="Title">Krótka nazwa do przełącznika w UI.</param>
/// <param name="Channel">Gdzie wysłać, np. "Instagram / Facebook – wiadomość prywatna".</param>
/// <param name="Guidance">Jak i kiedy wysłać, łącznie z uwagami prawnymi.</param>
/// <param name="Subject">Temat (e-mail, formularz); null, gdy kanał go nie ma.</param>
/// <param name="Body">Treść.</param>
/// <param name="RequiresConsent">Czy wolno wysłać dopiero po zgodzie odbiorcy.</param>
public sealed record MessageDraft(
    DraftKind Kind,
    string Title,
    string Channel,
    string Guidance,
    string? Subject,
    string Body,
    bool RequiresConsent);

/// <summary>Dane nadawcy wstawiane do szkiców. Puste pola zastępowane są placeholderami w nawiasach.</summary>
/// <param name="Name">Imię (w zdaniu "nazywam się …").</param>
/// <param name="Signature">Podpis pod wiadomością (może być wielolinijkowy).</param>
/// <param name="Email">E-mail kontaktowy – w listach i klauzuli RODO.</param>
/// <param name="PostalAddress">Adres nadawcy – w nagłówku listu.</param>
/// <param name="PortfolioUrl">Link do realizacji – w ofertach i listach (nigdy w pierwszej wiadomości elektronicznej).</param>
/// <param name="Price">Cena wykonania strony, np. "od 1500 zł".</param>
/// <param name="CarePlan">Opcjonalna opieka/hosting, np. "100–150 zł miesięcznie".</param>
/// <param name="DeliveryTime">Czas realizacji, np. "2–3 tygodnie".</param>
public sealed record SenderProfile(
    string? Name = null,
    string? Signature = null,
    string? Email = null,
    string? PostalAddress = null,
    string? PortfolioUrl = null,
    string? Price = null,
    string? CarePlan = null,
    string? DeliveryTime = null);
