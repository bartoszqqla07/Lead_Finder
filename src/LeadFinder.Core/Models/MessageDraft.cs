namespace LeadFinder.Models;

/// <summary>Rodzaj szkicu – odpowiada kanałowi i etapowi kontaktu.</summary>
public enum DraftKind
{
    /// <summary>Sama informacja o niedziałającej stronie, bez oferty (tylko dla "strona nie działa").</summary>
    ProblemNotice,

    /// <summary>Krótka wiadomość na Instagramie/Facebooku z prośbą o zgodę na przesłanie propozycji.</summary>
    DirectMessage,

    /// <summary>E-mail lub formularz kontaktowy z prośbą o zgodę.</summary>
    Email,

    /// <summary>List papierowy – może zawierać ofertę i klauzulę informacyjną RODO.</summary>
    Letter,

    /// <summary>Właściwa propozycja – dopiero po wyrażeniu zgody.</summary>
    Proposal,

    /// <summary>Jedno przypomnienie – tylko po wyrażeniu zgody.</summary>
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
public sealed record SenderProfile(
    string? Name = null,
    string? Signature = null,
    string? Email = null,
    string? PostalAddress = null);
