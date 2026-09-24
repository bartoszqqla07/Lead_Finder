using System.Globalization;
using LeadFinder.Common;
using LeadFinder.Models;

namespace LeadFinder.Services;

/// <summary>
/// Tworzy SZKICE wiadomości do biznesu – teksty do ręcznego przejrzenia, poprawienia i wysłania.
/// </summary>
/// <remarks>
/// <para>
/// Klasa celowo tylko zwraca tekst. Projekt nie zawiera i nie powinien zawierać automatycznej wysyłki
/// (mail, SMS, komunikatory, API do mass-mailingu).
/// </para>
/// <para>
/// Zestaw szkiców odpowiada procesowi zgodnemu z polskimi przepisami:
/// <list type="number">
///   <item>Pierwszy kontakt elektroniczny (DM, e-mail) to wyłącznie prośba o zgodę – bez opisu oferty i cen.
///         Niezamówiona informacja handlowa drogą elektroniczną jest zakazana (UŚUDE art. 10,
///         Prawo komunikacji elektronicznej art. 398).</item>
///   <item>Informacja o niedziałającej stronie – bez oferty i bez przedstawiania się jako twórca stron.</item>
///   <item>List papierowy – nie jest komunikacją elektroniczną, więc może zawierać ofertę; dostaje klauzulę
///         informacyjną RODO (art. 14), bo dane pochodzą z publicznej wizytówki, a nie od odbiorcy.</item>
///   <item>Propozycja i przypomnienie – dopiero po wyrażeniu zgody.</item>
/// </list>
/// </para>
/// <para>Teksty są w czasie teraźniejszym ("trafiam", "widzę"), żeby pasowały niezależnie od płci nadawcy.</para>
/// </remarks>
public sealed class MessageDrafter
{
    public const string DefaultSenderName = "[Twoje imię]";
    public const string DefaultSignature = "[Imię Nazwisko]\n[telefon] · [link do portfolio]";
    private const string DefaultEmail = "[Twój e-mail]";
    private const string DefaultPostalAddress = "[Imię Nazwisko]\n[ulica i numer]\n[kod pocztowy, miejscowość]";

    /// <summary>Ton wiadomości dla danej branży.</summary>
    /// <param name="Informal">Na "Ty/Wy" (barber, tatuaż, paznokcie) czy na "Państwo".</param>
    /// <param name="NicheInsight">Jedno zdanie pokazujące, że rozumiemy tę branżę.</param>
    /// <param name="Offer">Co konkretnie możemy zrobić (dopełnienie po "Mogę przygotować…").</param>
    private sealed record ToneProfile(bool Informal, string NicheInsight, string Offer);

    private static readonly ToneProfile DefaultTone = new(
        Informal: false,
        NicheInsight: "Coraz więcej klientów sprawdza firmę w internecie, zanim zadzwoni albo się zapisze.",
        Offer: "nowoczesną stronę z ofertą, cennikiem i prostym kontaktem lub rezerwacją");

    /// <summary>Klucze odpowiadają polu "tone" w Config/categories.json.</summary>
    private static readonly Dictionary<string, ToneProfile> Tones = new(StringComparer.OrdinalIgnoreCase)
    {
        ["barber"] = new(
            Informal: true,
            NicheInsight: "Do barbera ludzie zapisują się zwykle z telefonu, na szybko – wygrywa ten, u kogo od razu widać cennik, zdjęcia cięć i wolne terminy.",
            Offer: "prostą, szybką stronę: cennik, galeria cięć, dojazd i przycisk rezerwacji (może być podpięty pod Booksy)"),
        ["hair"] = new(
            Informal: false,
            NicheInsight: "Przed pierwszą wizytą klienci często oglądają zdjęcia metamorfoz i sprawdzają ceny – dobra strona robi tu dużą różnicę.",
            Offer: "przejrzystą stronę z cennikiem, galerią metamorfoz, prezentacją zespołu i rezerwacją online"),
        ["beauty"] = new(
            Informal: false,
            NicheInsight: "Przy zabiegach kosmetycznych klienci chcą przed wizytą przeczytać, na czym polega zabieg, ile trwa i ile kosztuje.",
            Offer: "stronę z opisami zabiegów, cennikiem i rezerwacją online"),
        ["nails"] = new(
            Informal: true,
            NicheInsight: "Przy stylizacji paznokci najlepiej sprzedają zdjęcia prac – galeria na stronie działa jak portfolio, które pracuje 24/7.",
            Offer: "lekką stronę z galerią stylizacji, cennikiem i szybkim zapisem na wizytę"),
        ["spa"] = new(
            Informal: false,
            NicheInsight: "W spa liczy się atmosfera – dobra strona pozwala ją poczuć jeszcze przed wizytą i ułatwia sprzedaż voucherów na prezent.",
            Offer: "elegancką stronę z opisem rytuałów, voucherami podarunkowymi i rezerwacją online"),
        ["tattoo"] = new(
            Informal: true,
            NicheInsight: "Studio tatuażu wybiera się po portfolio i stylu artystów – dobrze ułożona galeria robi połowę roboty.",
            Offer: "stronę-portfolio z pracami podzielonymi na style, profilami artystów i formularzem konsultacji"),
        ["cosmetology"] = new(
            Informal: false,
            NicheInsight: "Przy zabiegach kosmetologicznych liczy się zaufanie: klienci szukają opisu zabiegów, przeciwwskazań i informacji o kwalifikacjach.",
            Offer: "profesjonalną stronę z opisami zabiegów, informacjami o kwalifikacjach i rezerwacją online"),
    };

    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    private readonly string _name;
    private readonly string _signature;
    private readonly string _email;
    private readonly string _postalAddress;

    /// <param name="sender">Dane nadawcy; brakujące pola zastępowane są placeholderami w nawiasach.</param>
    public MessageDrafter(SenderProfile? sender = null)
    {
        _name = OrDefault(sender?.Name, DefaultSenderName);
        _signature = OrDefault(sender?.Signature, DefaultSignature);
        _email = OrDefault(sender?.Email, DefaultEmail);
        _postalAddress = OrDefault(sender?.PostalAddress, DefaultPostalAddress);
    }

    /// <summary>
    /// Szkic do kolumny CSV: rekomendowany pierwszy kontakt (informacja o problemie przy niedziałającej
    /// stronie, w pozostałych przypadkach e-mail z prośbą o zgodę), z tematem w pierwszej linii.
    /// </summary>
    public string Draft(Lead lead)
    {
        var draft = CreateDrafts(lead).First();
        return draft.Subject is null ? draft.Body : $"Temat: {draft.Subject}\n\n{draft.Body}";
    }

    /// <summary>
    /// Wszystkie szkice dla leada: najpierw opcje pierwszego kontaktu (w kolejności rekomendacji),
    /// potem wiadomości wymagające zgody odbiorcy.
    /// </summary>
    public IReadOnlyList<MessageDraft> CreateDrafts(Lead lead)
    {
        var context = new DraftContext(lead, Tones.GetValueOrDefault(lead.Category.Tone) ?? DefaultTone);
        var drafts = new List<MessageDraft>();

        if (lead.Status == LeadStatus.WebsiteDown)
            drafts.Add(ProblemNotice(context));

        drafts.Add(EmailDraft(context));
        drafts.Add(DirectMessage(context));
        drafts.Add(Letter(context));
        drafts.Add(Proposal(context));
        drafts.Add(FollowUp(context));
        return drafts;
    }

    // ---------- Pierwszy kontakt ----------

    private MessageDraft ProblemNotice(DraftContext c)
    {
        var body = c.Informal
            ? $"""
              Cześć!

              Krótka informacja: link do strony w Waszej wizytówce Google ({c.Host}) obecnie się nie otwiera – kto szuka {c.Name} w Google i kliknie „Witryna”, trafia na błąd ({c.TechnicalNote}).

              Może warto sprawdzić domenę albo hosting.

              Pozdrawiam,
              {_name}
              """
            : $"""
              Dzień dobry,

              krótka informacja: link do strony w Państwa wizytówce Google ({c.Host}) obecnie się nie otwiera – osoby, które szukają {c.Name} w Google i klikną „Witryna”, trafiają na błąd ({c.TechnicalNote}).

              Może warto sprawdzić domenę albo hosting.

              Pozdrawiam,
              {_name}
              """;

        return new MessageDraft(
            DraftKind.ProblemNotice,
            Title: "Informacja o problemie",
            Channel: "Instagram / Facebook, formularz kontaktowy lub e-mail",
            Guidance: "Najbezpieczniejsza forma zdalna: czysta uprzejmość, nie reklama. Nie dopisuj oferty, portfolio ani tego, " +
                      "że robisz strony – wtedy staje się informacją handlową. Jeśli odpiszą i zapytają o pomoc, możesz przedstawić ofertę.",
            Subject: "Niedziałająca strona w wizytówce Google",
            Body: body,
            RequiresConsent: false);
    }

    private MessageDraft EmailDraft(DraftContext c)
    {
        var body = c.Informal
            ? $"""
              Cześć!

              tu {_name} – robię strony internetowe dla lokalnych biznesów.

              {StatusHook(c)}

              Czy mogę podesłać krótką propozycję, jak mogłaby wyglądać {c.WebsiteNoun}? Jeśli to nie temat dla Was – żaden problem, nie będę więcej pisać.

              Pozdrawiam,
              {_signature}

              Kontakt do Was pochodzi z publicznej wizytówki Google. Jeśli nie chcecie, żeby był u mnie zapisany, dajcie znać – usunę go.
              """
            : $"""
              Dzień dobry,

              nazywam się {_name} i tworzę strony internetowe dla lokalnych firm usługowych.

              {StatusHook(c)}

              Czy mogę przesłać krótką propozycję, jak mogłaby wyglądać {c.WebsiteNoun}? Jeśli temat Państwa nie interesuje, proszę po prostu zignorować tę wiadomość – nie będę więcej pisać.

              Pozdrawiam serdecznie,
              {_signature}

              Dane kontaktowe pochodzą z publicznej wizytówki Google. Jeśli nie życzą sobie Państwo ich przechowywania, proszę o krótką odpowiedź – usunę je.
              """;

        return new MessageDraft(
            DraftKind.Email,
            Title: "E-mail – prośba o zgodę",
            Channel: "E-mail lub formularz kontaktowy na stronie firmy",
            Guidance: "Tylko prośba o zgodę – bez cen i opisu usług. Wysyłaj ręcznie, pojedynczo, ze swojej skrzynki. " +
                      "Google nie podaje e-maili: szukaj na stronie, Instagramie lub Facebooku firmy. Wyślij raz – brak odpowiedzi traktuj jako „nie”.",
            Subject: c.Lead.Status switch
            {
                LeadStatus.WebsiteDown => $"Niedziałająca strona {c.Name}",
                LeadStatus.NoWebsite => $"Strona internetowa dla {c.Name}?",
                _ => $"Pomysł na stronę {c.Name}",
            },
            Body: body,
            RequiresConsent: false);
    }

    private MessageDraft DirectMessage(DraftContext c)
    {
        var body = c.Informal
            ? $"Cześć! Tu {_name}, robię strony dla lokalnych biznesów. {ShortHook(c)} Mogę podesłać krótką propozycję, jak mogłaby wyglądać {c.WebsiteNoun}? Jeśli nie – żaden problem, nie będę więcej pisać 🙂"
            : $"Dzień dobry, nazywam się {_name} i tworzę strony dla lokalnych firm. {ShortHook(c)} Czy mogę przesłać krótką propozycję, jak mogłaby wyglądać {c.WebsiteNoun}? Jeśli temat Państwa nie interesuje, proszę zignorować wiadomość – nie będę więcej pisać.";

        return new MessageDraft(
            DraftKind.DirectMessage,
            Title: "DM – prośba o zgodę",
            Channel: "Instagram / Facebook – wiadomość prywatna do profilu firmy",
            Guidance: "Salony beauty najszybciej odpowiadają na Instagramie. Pisz do profilu firmy, nie na prywatne konto właściciela. " +
                      "Tylko prośba o zgodę, bez cen i opisu usług. Wyślij raz – brak odpowiedzi traktuj jako „nie”.",
            Subject: null,
            Body: body,
            RequiresConsent: false);
    }

    private MessageDraft Letter(DraftContext c)
    {
        var date = DateTime.Now.ToString("d MMMM yyyy", Polish) + " r.";
        var recipient = string.IsNullOrWhiteSpace(c.Lead.Place.Address)
            ? c.Name
            : $"{c.Name}\n{c.Lead.Place.Address.Replace(", ", "\n")}";

        var body = c.Informal
            ? $"""
              {_postalAddress}

              {date}

              {recipient}

              Cześć!

              tu {_name} – robię strony internetowe dla lokalnych biznesów.

              {StatusHook(c)}

              {c.Tone.NicheInsight}

              Mogę zrobić {c.Tone.Offer}. Przykłady moich realizacji: [link lub kod QR do portfolio].

              Jeśli temat Was zainteresuje, napiszcie na {_email} albo zadzwońcie – chętnie opowiem więcej, bez zobowiązań.

              Pozdrawiam,
              {_signature}

              {PrivacyNotice()}
              """
            : $"""
              {_postalAddress}

              {date}

              {recipient}

              Dzień dobry,

              nazywam się {_name} i tworzę strony internetowe dla lokalnych firm usługowych.

              {StatusHook(c)}

              {c.Tone.NicheInsight}

              Mogę przygotować {c.Tone.Offer}. Przykłady moich realizacji: [link lub kod QR do portfolio].

              Jeśli temat Państwa zainteresuje, zapraszam do kontaktu: {_email} lub telefonicznie – chętnie odpowiem na pytania, bez zobowiązań.

              Pozdrawiam serdecznie,
              {_signature}

              {PrivacyNotice()}
              """;

        return new MessageDraft(
            DraftKind.Letter,
            Title: "List papierowy – z ofertą",
            Channel: "Poczta (list zwykły na adres z wizytówki)",
            Guidance: "Jedyny kanał zdalny, którego nie obejmują zakazy z UŚUDE i Prawa komunikacji elektronicznej (dotyczą komunikacji " +
                      "elektronicznej) – dlatego oferta może być od razu. Klauzula RODO na dole jest wymagana: uzupełnij swoje dane w Ustawieniach. " +
                      "Wyróżnia się, bo prawie nikt już nie wysyła listów.",
            Subject: null,
            Body: body,
            RequiresConsent: false);
    }

    // ---------- Po zgodzie ----------

    private MessageDraft Proposal(DraftContext c)
    {
        var body = c.Informal
            ? $"""
              Cześć, dzięki za odpowiedź!

              Zgodnie z obietnicą podsyłam krótką propozycję dla {c.Name}.

              {c.Tone.NicheInsight}

              Mogę zrobić {c.Tone.Offer}.

              [2–3 zdania: zakres, czas realizacji, cena lub widełki]

              Przykłady moich realizacji: [link do portfolio]

              Pasuje Wam krótka rozmowa (15 min) w tym albo przyszłym tygodniu?

              Pozdrawiam,
              {_signature}
              """
            : $"""
              Dzień dobry,

              dziękuję za odpowiedź! Zgodnie z obietnicą przesyłam krótką propozycję dla {c.Name}.

              {c.Tone.NicheInsight}

              Mogę przygotować {c.Tone.Offer}.

              [2–3 zdania: zakres, czas realizacji, cena lub widełki]

              Przykłady moich realizacji: [link do portfolio]

              Czy pasowałaby Państwu krótka rozmowa (15 minut) w tym lub przyszłym tygodniu?

              Pozdrawiam serdecznie,
              {_signature}
              """;

        return new MessageDraft(
            DraftKind.Proposal,
            Title: "Propozycja",
            Channel: "Tam, gdzie firma odpowiedziała",
            Guidance: "Wysyłaj tylko po wyraźnej zgodzie („tak, proszę przesłać”). Zaznacz zgodę powyżej – data zapisze się jako dowód. " +
                      "Uzupełnij fragmenty w [nawiasach].",
            Subject: $"Propozycja strony dla {c.Name}",
            Body: body,
            RequiresConsent: true);
    }

    private MessageDraft FollowUp(DraftContext c)
    {
        var body = c.Informal
            ? $"""
              Cześć!

              Wracam do propozycji strony dla {c.Name}. Udało się rzucić okiem? Chętnie odpowiem na pytania albo dopasuję zakres.

              Pozdrawiam,
              {_name}
              """
            : $"""
              Dzień dobry,

              wracam do propozycji strony dla {c.Name}. Czy udało się ją przejrzeć? Chętnie odpowiem na pytania albo dopasuję zakres.

              Pozdrawiam serdecznie,
              {_name}
              """;

        return new MessageDraft(
            DraftKind.FollowUp,
            Title: "Przypomnienie",
            Channel: "Tam, gdzie wysłana była propozycja",
            Guidance: "Tylko do firm, które wyraziły zgodę i dostały propozycję. Jedno przypomnienie po około tygodniu – potem odpuść.",
            Subject: $"Re: Propozycja strony dla {c.Name}",
            Body: body,
            RequiresConsent: true);
    }

    // ---------- Wspólne fragmenty ----------

    /// <summary>Otwarcie zależne od sytuacji firmy (e-mail, list).</summary>
    private static string StatusHook(DraftContext c)
    {
        var lead = c.Lead;
        return lead.Status switch
        {
            LeadStatus.NoWebsite when lead.WebsiteCheck?.ProfilePlatform is { } platform => c.Informal
                ? $"{Praise(lead)}W wizytówce Google zamiast strony macie podlinkowany profil na {platform}. To dobre miejsce na zapisy, ale własna strona lepiej działa w wynikach Google i w pełni należy do Was."
                : $"{Praise(lead)}W wizytówce Google zamiast strony jest podlinkowany profil na {platform}. To dobre miejsce na zapisy, ale własna strona lepiej działa w wynikach Google i w pełni należy do Państwa.",

            LeadStatus.NoWebsite => c.Informal
                ? $"{Praise(lead)}Szukając w Google „{lead.Category.Query} {lead.City}”, trafiam na {c.Name}, ale nie widzę Waszej strony internetowej."
                : $"{Praise(lead)}Szukając w Google „{lead.Category.Query} {lead.City}”, trafiam na {c.Name}, ale nie mogę znaleźć Państwa strony internetowej.",

            LeadStatus.WebsiteDown => c.Informal
                ? $"Piszę, bo strona z Waszej wizytówki Google ({c.Host}) nie otwiera się – kto w nią kliknie, zobaczy błąd zamiast oferty."
                : $"Piszę, bo strona podlinkowana w Państwa wizytówce Google ({c.Host}) nie otwiera się – kto w nią kliknie, zobaczy błąd zamiast oferty.",

            LeadStatus.WordPress => c.Informal
                ? $"Oglądam Waszą stronę {c.Host} i mam parę pomysłów, jak ją przyspieszyć na telefonie i ułatwić zapisy."
                : $"Oglądam Państwa stronę {c.Host} i mam kilka pomysłów, jak mogłaby szybciej działać na telefonie i łatwiej prowadzić klientów do rezerwacji.",

            _ => c.Informal
                ? $"Oglądam Waszą stronę {c.Host} i mam parę pomysłów na odświeżenie i rezerwacje online."
                : $"Oglądam Państwa stronę {c.Host} i mam kilka pomysłów na jej odświeżenie i rezerwacje online.",
        };
    }

    /// <summary>Jednozdaniowe otwarcie do krótkiego DM.</summary>
    private static string ShortHook(DraftContext c)
    {
        var lead = c.Lead;
        var goodReviews = HasGoodReviews(lead);
        return lead.Status switch
        {
            LeadStatus.NoWebsite when lead.WebsiteCheck?.ProfilePlatform is { } platform => c.Informal
                ? $"Widzę, że w Google zamiast strony macie podlinkowany profil na {platform}."
                : $"Widzę, że w Google zamiast strony jest podlinkowany Państwa profil na {platform}.",
            LeadStatus.NoWebsite => (c.Informal, goodReviews) switch
            {
                (true, true) => "Widzę Was w Google ze świetnymi opiniami, ale bez strony internetowej.",
                (true, false) => "Widzę Was w Google, ale bez strony internetowej.",
                (false, true) => "Widzę Państwa w Google ze świetnymi opiniami, ale bez strony internetowej.",
                (false, false) => "Widzę Państwa w Google, ale bez strony internetowej.",
            },
            LeadStatus.WebsiteDown => c.Informal
                ? $"Link do strony w Waszej wizytówce Google ({c.Host}) się nie otwiera."
                : $"Link do strony w Państwa wizytówce Google ({c.Host}) się nie otwiera.",
            LeadStatus.WordPress => c.Informal
                ? $"Oglądam Waszą stronę {c.Host} i mam pomysł, jak ją przyspieszyć na telefonie."
                : $"Oglądam Państwa stronę {c.Host} i mam pomysł, jak ją przyspieszyć na telefonie.",
            _ => c.Informal
                ? $"Oglądam Waszą stronę {c.Host} i mam kilka pomysłów na rezerwacje online."
                : $"Oglądam Państwa stronę {c.Host} i mam kilka pomysłów na rezerwacje online.",
        };
    }

    /// <summary>
    /// Klauzula informacyjna RODO (art. 14) do listu: dane pochodzą z publicznego źródła, a nie od odbiorcy,
    /// więc przy pierwszym kontakcie trzeba poinformować, kto je przetwarza, po co i jakie są prawa.
    /// </summary>
    private string PrivacyNotice()
    {
        var controller = _signature.Split('\n')[0].Trim();
        return $"Informacja o danych: nazwa i adres firmy pochodzą z publicznej wizytówki w Mapach Google. Administratorem danych jest {controller} " +
               $"(kontakt: {_email}). Dane przetwarzam wyłącznie w celu jednorazowego przedstawienia oferty, na podstawie prawnie uzasadnionego " +
               "interesu (art. 6 ust. 1 lit. f RODO), maks. przez 12 miesięcy lub do wniesienia sprzeciwu. Przysługuje Państwu prawo dostępu " +
               "do danych, ich sprostowania, usunięcia, sprzeciwu oraz skargi do Prezesa UODO. Dane nie są nikomu przekazywane.";
    }

    /// <summary>Komplement tylko wtedy, gdy jest na czym go oprzeć (wysoka ocena i sensowna liczba opinii).</summary>
    private static string Praise(Lead lead)
    {
        if (!HasGoodReviews(lead))
            return string.Empty;

        var ratingText = lead.Place.Rating!.Value.ToString("0.0", Polish);
        var count = lead.Place.UserRatingCount!.Value;
        return $"Widzę świetne opinie w Google ({ratingText}★, {count} {PolishPlural.Reviews(count)}) – gratulacje! ";
    }

    private static bool HasGoodReviews(Lead lead) =>
        lead.Place is { Rating: >= 4.5, UserRatingCount: >= 10 };

    private static string OrDefault(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    /// <summary>Dane leada potrzebne w wielu szablonach, policzone raz.</summary>
    private sealed class DraftContext(Lead lead, ToneProfile tone)
    {
        public Lead Lead { get; } = lead;
        public ToneProfile Tone { get; } = tone;
        public bool Informal => Tone.Informal;
        public string Name => Lead.Place.Name;

        public string Host { get; } =
            Uri.TryCreate(lead.Place.WebsiteUri, UriKind.Absolute, out var uri) ? uri.Host : lead.Place.WebsiteUri ?? string.Empty;

        /// <summary>Czego dotyczy propozycja – "strona dla X" albo "odświeżona strona".</summary>
        public string WebsiteNoun => Lead.Status switch
        {
            LeadStatus.NoWebsite => $"strona dla {Name}",
            LeadStatus.WebsiteDown => "nowa, działająca strona",
            _ => "odświeżona strona",
        };

        /// <summary>Wynik sprawdzenia strony w nawiasie, np. "domena nie istnieje lub nie ma rekordów DNS".</summary>
        public string TechnicalNote => Lead.WebsiteCheck?.Note ?? "strona nie odpowiada";
    }
}
