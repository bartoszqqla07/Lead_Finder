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
///   <item>Pierwszy kontakt elektroniczny (DM, e-mail) to wyłącznie prośba o zgodę – bez opisu oferty, cen i portfolio.
///         Niezamówiona informacja handlowa drogą elektroniczną jest zakazana (UŚUDE art. 10,
///         Prawo komunikacji elektronicznej art. 398).</item>
///   <item>Informacja o niedziałającej stronie – bez oferty i bez przedstawiania się jako twórca stron.</item>
///   <item>List papierowy – nie jest komunikacją elektroniczną, więc może zawierać ofertę; dostaje klauzulę
///         informacyjną RODO (art. 14), bo dane pochodzą z publicznej wizytówki, a nie od odbiorcy.</item>
///   <item>Po zgodzie: podgląd (makieta), oferta krótka i pełna, odpowiedź o cenę, jedno przypomnienie.</item>
/// </list>
/// </para>
/// <para>
/// Styl: forma "Wy" albo "Państwo" (zależna od branży) trzymana konsekwentnie w całej rozmowie; zamiast oficjalnej
/// nazwy z Google – "Państwa salon", "Wasz barbershop"; czas teraźniejszy ("widzę", "przygotuję"), żeby teksty
/// pasowały niezależnie od płci nadawcy.
/// </para>
/// </remarks>
public sealed class MessageDrafter
{
    public const string DefaultSenderName = "[Twoje imię]";
    public const string DefaultSignature = "[Imię Nazwisko]\n[telefon] · [link do portfolio]";
    private const string DefaultEmail = "[Twój e-mail]";
    private const string DefaultPostalAddress = "[Imię Nazwisko]\n[ulica i numer]\n[kod pocztowy, miejscowość]";
    private const string DefaultPortfolio = "[link do portfolio]";
    private const string DefaultPrice = "[cena, np. od 1500 zł]";
    private const string DefaultCarePlan = "[kwota, np. 100–150 zł miesięcznie]";
    private const string DefaultDeliveryTime = "[czas realizacji, np. 2–3 tygodnie]";

    /// <summary>Ton i treści dopasowane do branży.</summary>
    /// <param name="Informal">Na "Wy" (barber, tatuaż, paznokcie) czy na "Państwo".</param>
    /// <param name="Audience">Dla kogo robisz strony: "robię strony internetowe dla {Audience}".</param>
    /// <param name="NicheInsight">Jedno zdanie pokazujące, że rozumiemy tę branżę.</param>
    /// <param name="Offer">Opis strony w jednym zdaniu (dopełnienie po "Mogę przygotować…").</param>
    /// <param name="Features">Trzy najważniejsze elementy strony dla tej branży – do listy w ofercie.</param>
    private sealed record ToneProfile(bool Informal, string Audience, string NicheInsight, string Offer, string[] Features);

    private static readonly ToneProfile DefaultTone = new(
        Informal: false,
        Audience: "lokalnych firm usługowych",
        NicheInsight: "Coraz więcej klientów sprawdza firmę w internecie, zanim zadzwoni albo się zapisze.",
        Offer: "nowoczesną stronę z ofertą, cennikiem i prostym kontaktem lub rezerwacją",
        Features: ["oferta z cennikiem", "galeria realizacji", "kontakt i rezerwacja jednym kliknięciem"]);

    /// <summary>Klucze odpowiadają polu "tone" w Config/categories.json.</summary>
    private static readonly Dictionary<string, ToneProfile> Tones = new(StringComparer.OrdinalIgnoreCase)
    {
        ["barber"] = new(
            Informal: true,
            Audience: "barbershopów",
            NicheInsight: "Do barbera ludzie zapisują się zwykle z telefonu, na szybko – wygrywa ten, u kogo od razu widać cennik, zdjęcia cięć i wolne terminy.",
            Offer: "prostą, szybką stronę: cennik, galeria cięć, dojazd i przycisk rezerwacji (może być podpięty pod Booksy)",
            Features: ["cennik i galeria Waszych cięć", "przycisk rezerwacji – może być podpięty pod Booksy", "dojazd, godziny otwarcia i telefon jednym kliknięciem"]),
        ["hair"] = new(
            Informal: false,
            Audience: "salonów fryzjerskich",
            NicheInsight: "Przed pierwszą wizytą klienci często oglądają zdjęcia metamorfoz i sprawdzają ceny – dobra strona robi tu dużą różnicę.",
            Offer: "przejrzystą stronę z cennikiem, galerią metamorfoz, prezentacją zespołu i rezerwacją online",
            Features: ["cennik usług i galeria metamorfoz", "prezentacja zespołu", "rezerwacja online albo podpięcie Booksy"]),
        ["beauty"] = new(
            Informal: false,
            Audience: "salonów beauty",
            NicheInsight: "Przy zabiegach kosmetycznych klienci chcą przed wizytą przeczytać, na czym polega zabieg, ile trwa i ile kosztuje.",
            Offer: "stronę z opisami zabiegów, cennikiem i rezerwacją online",
            Features: ["opisy zabiegów z cenami i czasem trwania", "galeria efektów", "rezerwacja online"]),
        ["nails"] = new(
            Informal: true,
            Audience: "salonów paznokci",
            NicheInsight: "Przy stylizacji paznokci najlepiej sprzedają zdjęcia prac – galeria na stronie działa jak portfolio, które pracuje 24/7.",
            Offer: "lekką stronę z galerią stylizacji, cennikiem i szybkim zapisem na wizytę",
            Features: ["galeria Waszych stylizacji", "przejrzysty cennik", "szybki zapis na wizytę"]),
        ["spa"] = new(
            Informal: false,
            Audience: "salonów spa i beauty",
            NicheInsight: "W spa liczy się atmosfera – dobra strona pozwala ją poczuć jeszcze przed wizytą i ułatwia sprzedaż voucherów na prezent.",
            Offer: "elegancką stronę z opisem rytuałów, voucherami podarunkowymi i rezerwacją online",
            Features: ["opisy rytuałów i zabiegów", "vouchery podarunkowe", "rezerwacja online"]),
        ["tattoo"] = new(
            Informal: true,
            Audience: "studiów tatuażu",
            NicheInsight: "Studio tatuażu wybiera się po portfolio i stylu artystów – dobrze ułożona galeria robi połowę roboty.",
            Offer: "stronę-portfolio z pracami podzielonymi na style, profilami artystów i formularzem konsultacji",
            Features: ["portfolio prac podzielone na style", "profile artystów", "formularz konsultacji z możliwością dodania zdjęć"]),
        ["cosmetology"] = new(
            Informal: false,
            Audience: "gabinetów kosmetologicznych",
            NicheInsight: "Przy zabiegach kosmetologicznych liczy się zaufanie: klienci szukają opisu zabiegów, przeciwwskazań i informacji o kwalifikacjach.",
            Offer: "profesjonalną stronę z opisami zabiegów, informacjami o kwalifikacjach i rezerwacją online",
            Features: ["opisy zabiegów z przeciwwskazaniami", "kwalifikacje i certyfikaty", "rezerwacja online"]),
    };

    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    private readonly string _name;
    private readonly string _signature;
    private readonly string _email;
    private readonly string _postalAddress;
    private readonly string _portfolio;
    private readonly string _price;
    private readonly string _carePlan;
    private readonly string _deliveryTime;

    /// <param name="sender">Dane nadawcy; brakujące pola zastępowane są placeholderami w nawiasach.</param>
    public MessageDrafter(SenderProfile? sender = null)
    {
        _name = OrDefault(sender?.Name, DefaultSenderName);
        _signature = OrDefault(sender?.Signature, DefaultSignature);
        _email = OrDefault(sender?.Email, DefaultEmail);
        _postalAddress = OrDefault(sender?.PostalAddress, DefaultPostalAddress);
        _portfolio = OrDefault(sender?.PortfolioUrl, DefaultPortfolio);
        _price = OrDefault(sender?.Price, DefaultPrice);
        _carePlan = OrDefault(sender?.CarePlan, DefaultCarePlan);
        _deliveryTime = OrDefault(sender?.DeliveryTime, DefaultDeliveryTime);
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
    /// Wszystkie szkice dla leada w kolejności użycia: najpierw pierwszy kontakt, potem ścieżka po zgodzie
    /// (podgląd → oferta → odpowiedź o cenę → przypomnienie).
    /// </summary>
    public IReadOnlyList<MessageDraft> CreateDrafts(Lead lead)
    {
        var c = new DraftContext(lead, Tones.GetValueOrDefault(lead.Category.Tone) ?? DefaultTone);
        var drafts = new List<MessageDraft>();

        if (lead.Status == LeadStatus.WebsiteDown)
            drafts.Add(ProblemNotice(c));

        drafts.Add(EmailDraft(c));
        drafts.Add(DirectMessage(c));
        drafts.Add(Letter(c));
        drafts.Add(Preview(c));
        drafts.Add(OfferShort(c));
        drafts.Add(Proposal(c));
        drafts.Add(PriceReply(c));
        drafts.Add(FollowUp(c));
        return drafts;
    }

    // =====================================================================
    //  Pierwszy kontakt – bez oferty, cen i portfolio (tylko prośba o zgodę)
    // =====================================================================

    private MessageDraft ProblemNotice(DraftContext c)
    {
        var body = c.Informal
            ? $"""
              Cześć!

              Krótka informacja: link do strony w Waszej wizytówce Google ({c.Host}) obecnie się nie otwiera – kto szuka Was w Google i kliknie „Witryna”, trafia na błąd ({c.TechnicalNote}).

              Może warto sprawdzić domenę albo hosting.

              Pozdrawiam,
              {_name}
              """
            : $"""
              Dzień dobry,

              krótka informacja: link do strony w Państwa wizytówce Google ({c.Host}) obecnie się nie otwiera – osoby, które szukają Państwa w Google i klikną „Witryna”, trafiają na błąd ({c.TechnicalNote}).

              Może warto sprawdzić domenę albo hosting.

              Pozdrawiam,
              {_name}
              """;

        return new MessageDraft(
            DraftKind.ProblemNotice,
            Title: "Informacja o problemie",
            Channel: "Instagram / Facebook, formularz kontaktowy lub e-mail",
            Guidance: "Najbezpieczniejsza forma zdalna: czysta uprzejmość, nie reklama. Nie dopisuj oferty, portfolio ani tego, " +
                      "że robisz strony – wtedy staje się informacją handlową. Jeśli odpiszą i zapytają o pomoc, przejdź do „Podglądu”.",
            Subject: "Niedziałająca strona w wizytówce Google",
            Body: body,
            RequiresConsent: false);
    }

    private MessageDraft DirectMessage(DraftContext c)
    {
        var body = c.Informal
            ? $"Cześć! Tu {_name}, robię strony internetowe dla {c.Tone.Audience}. {ShortHook(c)} Mogę podesłać krótki podgląd, jak mogłaby wyglądać {c.WebsiteNoun}? Jeśli nie – żaden problem, nie będę więcej pisać 🙂"
            : $"Dzień dobry, nazywam się {_name} i tworzę strony internetowe dla {c.Tone.Audience}. {ShortHook(c)} Czy mogę przesłać krótki podgląd, jak mogłaby wyglądać {c.WebsiteNoun}? Jeśli temat Państwa nie interesuje, proszę zignorować wiadomość – nie będę więcej pisać.";

        return new MessageDraft(
            DraftKind.DirectMessage,
            Title: "DM – prośba o zgodę",
            Channel: "Instagram / Facebook – wiadomość prywatna do profilu firmy",
            Guidance: "Salony najczęściej odpowiadają wieczorem, po pracy – daj im 2–3 dni. Pisz do profilu firmy, nie na prywatne konto. " +
                      "Tylko prośba o zgodę: bez cen, opisu usług i linków. Wyślij raz – brak odpowiedzi traktuj jako „nie”.",
            Subject: null,
            Body: body,
            RequiresConsent: false);
    }

    private MessageDraft EmailDraft(DraftContext c)
    {
        var body = c.Informal
            ? $"""
              Cześć!

              tu {_name} – robię strony internetowe dla {c.Tone.Audience}.

              {StatusHook(c)}

              Mogę podesłać krótki podgląd, jak mogłaby wyglądać {c.WebsiteNoun}? Jeśli to nie temat dla Was – żaden problem, nie będę więcej pisać.

              Pozdrawiam,
              {_signature}

              Kontakt do Was pochodzi z publicznej wizytówki Google. Jeśli nie chcecie, żeby był u mnie zapisany, dajcie znać – usunę go.
              """
            : $"""
              Dzień dobry,

              nazywam się {_name} i tworzę strony internetowe dla {c.Tone.Audience}.

              {StatusHook(c)}

              Czy mogę przesłać krótki podgląd, jak mogłaby wyglądać {c.WebsiteNoun}? Jeśli temat Państwa nie interesuje, proszę po prostu zignorować tę wiadomość – nie będę więcej pisać.

              Pozdrawiam serdecznie,
              {_signature}

              Dane kontaktowe pochodzą z publicznej wizytówki Google. Jeśli nie życzą sobie Państwo ich przechowywania, proszę o krótką odpowiedź – usunę je.
              """;

        return new MessageDraft(
            DraftKind.Email,
            Title: "E-mail – prośba o zgodę",
            Channel: "E-mail lub formularz kontaktowy na stronie firmy",
            Guidance: "Tylko prośba o zgodę – bez cen, opisu usług i linków. Wysyłaj ręcznie, pojedynczo, ze swojej skrzynki. " +
                      "Google nie podaje e-maili: szukaj na stronie, Instagramie lub Facebooku firmy. Wyślij raz – brak odpowiedzi traktuj jako „nie”.",
            Subject: c.Lead.Status switch
            {
                LeadStatus.WebsiteDown => "Niedziałająca strona w wizytówce Google",
                LeadStatus.NoWebsite => $"Strona internetowa dla {c.OfYourPlace}?",
                _ => "Pomysł na odświeżenie strony",
            },
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

              tu {_name} – robię strony internetowe dla {c.Tone.Audience}.

              {StatusHook(c)}

              {c.Tone.NicheInsight}

              Mogę zrobić {c.Tone.Offer}. Każdą stronę projektuję i koduję sam, od zera – bez gotowych szablonów – więc wygląda dokładnie tak, jak chcecie, szybko działa na telefonie i jest Wasza na własność.

              Przykłady moich realizacji: {_portfolio}

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

              nazywam się {_name} i tworzę strony internetowe dla {c.Tone.Audience}.

              {StatusHook(c)}

              {c.Tone.NicheInsight}

              Mogę przygotować {c.Tone.Offer}. Każdą stronę projektuję i koduję sam, od zera – bez gotowych szablonów – więc wygląda dokładnie tak, jak Państwo chcą, szybko działa na telefonie i jest Państwa własnością.

              Przykłady moich realizacji: {_portfolio}

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

    // =====================================================================
    //  Po zgodzie – podgląd, oferta, cena, przypomnienie
    // =====================================================================

    private MessageDraft Preview(DraftContext c)
    {
        var body = c.Informal
            ? $"""
              Hej, dzięki za odpowiedź! 🙌

              Wrzucam szybki podgląd, jak mogłaby wyglądać strona {c.OfYourPlace} 👇

              [wstaw screenshot makiety]

              To wstępny szkic – kolory, zdjęcia i układ dopasuję do Was. Co o tym myślicie?
              """
            : $"""
              Dzień dobry, dziękuję za odpowiedź!

              Przesyłam wstępny podgląd, jak mogłaby wyglądać strona {c.OfYourPlace}:

              [wstaw screenshot makiety]

              To pierwszy szkic – kolory, zdjęcia i układ dopasuję do Państwa. Co Państwo o tym sądzą?

              Pozdrawiam,
              {_name}
              """;

        return new MessageDraft(
            DraftKind.Preview,
            Title: "Podgląd (makieta)",
            Channel: "Tam, gdzie firma odpowiedziała",
            Guidance: "Najskuteczniejsza pierwsza odpowiedź: obraz zamiast opisu. Zrób makietę w kreatorze podglądu " +
                      "(ich nazwa, 3–4 zdjęcia z Instagrama, prawdziwe ceny z Booksy) i wklej obrazek w miejsce " +
                      "[wstaw screenshot makiety]. Bez ceny – podaj ją, gdy zapytają albo gdy podgląd się spodoba " +
                      "(wtedy „Oferta – krótka”).",
            Subject: null,
            Body: body,
            RequiresConsent: true);
    }

    private MessageDraft OfferShort(DraftContext c)
    {
        var features = string.Join("\n", c.Tone.Features.Concat(c.CommonBenefits).Select(f => $"✔️ {f}"));

        var body = c.Informal
            ? $"""
              Jasne, już opowiadam 🙂

              Stronę robię sam, od zera – bez gotowych szablonów – więc będzie wyglądać dokładnie tak, jak chcecie, i szybko działać na telefonie.

              Co dostajecie:
              {features}

              💰 {_price} jednorazowo
              🛠️ opcjonalnie opieka (hosting, aktualizacje, drobne zmiany): {_carePlan}
              ⏱️ czas realizacji: {_deliveryTime}

              Moje realizacje: {_portfolio}

              Pasuje Wam krótka rozmowa (10–15 min), żeby omówić szczegóły?
              """
            : $"""
              Oczywiście, już opowiadam.

              Stronę przygotowuję sam, od zera – bez gotowych szablonów – więc będzie wyglądać dokładnie tak, jak Państwo chcą, i szybko działać na telefonie.

              Co Państwo otrzymują:
              {features}

              💰 {_price} jednorazowo
              🛠️ opcjonalnie opieka (hosting, aktualizacje, drobne zmiany): {_carePlan}
              ⏱️ czas realizacji: {_deliveryTime}

              Moje realizacje: {_portfolio}

              Czy pasowałaby Państwu krótka rozmowa (10–15 minut), żeby omówić szczegóły?

              Pozdrawiam,
              {_name}
              """;

        return new MessageDraft(
            DraftKind.OfferShort,
            Title: "Oferta – krótka",
            Channel: "Instagram / Messenger – gdy chcą poznać szczegóły",
            Guidance: "Zwięzła oferta na komunikator. Opiekę przedstawiaj jako opcję, nie obowiązek – stała opłata na start to częsta obiekcja. " +
                      "Cenę, opiekę, czas i portfolio uzupełnisz raz w Ustawieniach.",
            Subject: null,
            Body: body,
            RequiresConsent: true);
    }

    private MessageDraft Proposal(DraftContext c)
    {
        var features = string.Join("\n", c.Tone.Features.Concat(c.CommonBenefits).Select(f => $"• {f}"));
        var profileNote = c.Lead.WebsiteCheck?.ProfilePlatform is { } platform
            ? c.Informal
                ? $" Profil {OnPlatform(platform)} zostaje – strona go uzupełnia i prowadzi do niego klientów."
                : $" Profil {OnPlatform(platform)} zostaje – strona go uzupełnia i kieruje do niego klientów."
            : string.Empty;

        var body = c.Informal
            ? $"""
              Cześć!

              dzięki za zainteresowanie! Poniżej krótko, co mogę przygotować dla {c.OfYourPlace}.

              DLACZEGO WŁASNA STRONA
              {c.Tone.NicheInsight} Własna strona sprawia też, że pojawiacie się w Google, gdy ktoś wpisze „{c.Lead.Category.Query} {c.Lead.City}” – a nie tylko w social mediach.{profileNote}

              CO PRZYGOTUJĘ
              {features}
              • formularz kontaktowy i przyciski „Zadzwoń” / „Wyznacz trasę”

              DLACZEGO NIE SZABLON
              Każdą stronę projektuję i koduję sam, od zera. Dzięki temu:
              • wygląda tak, jak chcecie – Wasze kolory, zdjęcia i klimat, a nie motyw, który ma sto innych firm,
              • ładuje się szybko, co docenia i klient, i Google,
              • nie płacicie co miesiąc za kreator stron – strona i domena są Wasze.

              JAK WYGLĄDA WSPÓŁPRACA
              1. Krótka rozmowa (15 min) – co ma być na stronie i jaki klimat lubicie.
              2. Projekt – pokazuję wygląd strony, zanim zacznę kodować.
              3. Poprawki – zmieniamy wszystko, co trzeba, aż będzie tak, jak chcecie.
              4. Publikacja – podpinam domenę i uruchamiam stronę.
              5. Opieka (opcjonalnie) – hosting, aktualizacje i drobne zmiany, żebyście niczym nie musieli się martwić.

              KOSZT I CZAS
              • wykonanie strony: {_price} (jednorazowo)
              • opieka i hosting (opcjonalnie): {_carePlan}
              • czas realizacji: {_deliveryTime}

              Przykłady moich realizacji: {_portfolio}

              Pasuje Wam krótka rozmowa w tym albo przyszłym tygodniu? Wystarczy odpisać z dogodnym terminem.

              Pozdrawiam,
              {_signature}
              """
            : $"""
              Dzień dobry,

              dziękuję za zainteresowanie! Poniżej krótko, co mogę przygotować dla {c.OfYourPlace}.

              DLACZEGO WŁASNA STRONA
              {c.Tone.NicheInsight} Dzięki własnej stronie będą Państwo widoczni w Google, gdy ktoś wpisze „{c.Lead.Category.Query} {c.Lead.City}” – a nie tylko w mediach społecznościowych.{profileNote}

              CO PRZYGOTUJĘ
              {features}
              • formularz kontaktowy i przyciski „Zadzwoń” / „Wyznacz trasę”

              DLACZEGO NIE SZABLON
              Każdą stronę projektuję i koduję sam, od zera. Dzięki temu:
              • wygląda tak, jak Państwo chcą – kolory, zdjęcia i klimat {c.OfYourPlace}, a nie motyw, który ma sto innych firm,
              • ładuje się szybko, co docenia i klient, i Google,
              • nie płacą Państwo co miesiąc za kreator stron – strona i domena są Państwa własnością.

              JAK WYGLĄDA WSPÓŁPRACA
              1. Krótka rozmowa (15 min) – co ma być na stronie i jaki klimat Państwo lubią.
              2. Projekt – pokazuję wygląd strony, zanim zacznę kodować.
              3. Poprawki – zmieniamy wszystko, co trzeba, aż będzie tak, jak Państwo chcą.
              4. Publikacja – podpinam domenę i uruchamiam stronę.
              5. Opieka (opcjonalnie) – hosting, aktualizacje i drobne zmiany, żeby niczym nie musieli się Państwo martwić.

              KOSZT I CZAS
              • wykonanie strony: {_price} (jednorazowo)
              • opieka i hosting (opcjonalnie): {_carePlan}
              • czas realizacji: {_deliveryTime}

              Przykłady moich realizacji: {_portfolio}

              Czy pasowałaby Państwu krótka rozmowa w tym lub przyszłym tygodniu? Wystarczy odpisać z dogodnym terminem.

              Pozdrawiam serdecznie,
              {_signature}
              """;

        return new MessageDraft(
            DraftKind.Proposal,
            Title: "Oferta – pełna",
            Channel: "E-mail (albo jako PDF w wiadomości)",
            Guidance: "Pełna prezentacja – gdy firma chce „coś na maila” albo porównuje oferty. Najlepiej po podglądzie. " +
                      "Po oznaczeniu jako wysłane przypomnienie ustawi się za tydzień.",
            Subject: $"Propozycja strony internetowej dla {c.OfYourPlace}",
            Body: body,
            RequiresConsent: true);
    }

    private MessageDraft PriceReply(DraftContext c)
    {
        var body = c.Informal
            ? $"""
              Wykonanie strony to {_price} jednorazowo – w tym projekt, kodowanie i publikacja.

              Jeśli chcecie, mogę potem zająć się hostingiem, aktualizacjami i drobnymi zmianami za {_carePlan} – ale to opcja, nie obowiązek.

              Dokładną kwotę podam po krótkiej rozmowie, bo zależy od tego, ile podstron i funkcji potrzebujecie. Podesłać Wam podgląd, jak to mogłoby wyglądać u Was?
              """
            : $"""
              Wykonanie strony to {_price} jednorazowo – w tym projekt, kodowanie i publikacja.

              Jeśli Państwo zechcą, mogę potem zająć się hostingiem, aktualizacjami i drobnymi zmianami za {_carePlan} – to opcja, nie obowiązek.

              Dokładną kwotę podam po krótkiej rozmowie, bo zależy od liczby podstron i funkcji. Czy przesłać podgląd, jak mogłoby to wyglądać u Państwa?

              Pozdrawiam,
              {_name}
              """;

        return new MessageDraft(
            DraftKind.PriceReply,
            Title: "Odpowiedź: cena",
            Channel: "Gdy ktoś od razu pyta „ile to kosztuje?”",
            Guidance: "Krótko i konkretnie, bez uciekania od pytania – ale z powrotem do podglądu, bo obraz sprzedaje lepiej niż cena.",
            Subject: null,
            Body: body,
            RequiresConsent: true);
    }

    private MessageDraft FollowUp(DraftContext c)
    {
        var body = c.Informal
            ? $"""
              Hej! Wracam do strony dla {c.OfYourPlace} – udało się rzucić okiem na propozycję?

              Jeśli coś jest niejasne albo chcecie coś zmienić, dajcie znać. Mogę też podesłać szybki podgląd, jak to mogłoby wyglądać 🙂
              """
            : $"""
              Dzień dobry,

              wracam do propozycji strony dla {c.OfYourPlace} – czy udało się ją przejrzeć?

              Jeśli coś jest niejasne albo chcieliby Państwo coś zmienić, chętnie dopasuję. Mogę też przesłać podgląd, jak mogłoby to wyglądać.

              Pozdrawiam serdecznie,
              {_name}
              """;

        return new MessageDraft(
            DraftKind.FollowUp,
            Title: "Przypomnienie",
            Channel: "Tam, gdzie wysłana była oferta",
            Guidance: "Jedno przypomnienie po ok. tygodniu – potem odpuść. Najlepiej wysłać wieczorem, gdy salon kończy pracę.",
            Subject: $"Re: Propozycja strony internetowej dla {c.OfYourPlace}",
            Body: body,
            RequiresConsent: true);
    }

    // =====================================================================
    //  Wspólne fragmenty
    // =====================================================================

    /// <summary>Otwarcie zależne od sytuacji firmy (e-mail, list).</summary>
    private static string StatusHook(DraftContext c)
    {
        var lead = c.Lead;
        return lead.Status switch
        {
            LeadStatus.NoWebsite when lead.WebsiteCheck?.ProfilePlatform is { } platform => c.Informal
                ? $"{Praise(lead)}W wizytówce Google zamiast strony macie podlinkowany profil {OnPlatform(platform)}. To dobre miejsce na zapisy, ale własna strona lepiej działa w wynikach Google i w pełni należy do Was."
                : $"{Praise(lead)}W wizytówce Google zamiast strony jest podlinkowany profil {OnPlatform(platform)}. To dobre miejsce na zapisy, ale własna strona lepiej działa w wynikach Google i w pełni należy do Państwa.",

            LeadStatus.NoWebsite => c.Informal
                ? $"{Praise(lead)}Szukając w Google „{lead.Category.Query} {lead.City}”, trafiam na {c.YourPlace}, ale nie widzę strony internetowej."
                : $"{Praise(lead)}Szukając w Google „{lead.Category.Query} {lead.City}”, trafiam na {c.YourPlace}, ale nie mogę znaleźć strony internetowej.",

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
                ? $"Widzę, że w Google zamiast strony macie podlinkowany profil {OnPlatform(platform)}."
                : $"Widzę, że w Google zamiast strony jest podlinkowany Państwa profil {OnPlatform(platform)}.",
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

    /// <summary>"na Facebooku", "na Instagramie" – nazwy platform w miejscowniku.</summary>
    private static string OnPlatform(string platform) => "na " + platform switch
    {
        "Facebook" => "Facebooku",
        "Instagram" => "Instagramie",
        "TikTok" => "TikToku",
        "YouTube" => "YouTubie",
        _ => platform, // Booksy, Linktree – nieodmienne
    };

    /// <summary>Rzeczownik określający firmę w dwóch przypadkach i jego rodzaj (do odmiany zaimka "Wasz").</summary>
    private sealed record BusinessNoun(string Accusative, string Genitive, char Gender);

    /// <summary>
    /// Zamiast oficjalnej nazwy z Google ("Klinika Zdrowego Włosa i Skóry … Katowice"), która w treści brzmi
    /// sztucznie, piszemy "Państwa gabinet", "Wasz barbershop". Rzeczownik wynika z frazy kategorii.
    /// </summary>
    private static BusinessNoun NounFor(Category category) =>
        category.Query.Split(' ', 2)[0].ToLowerInvariant() switch
        {
            "salon" => new("salon", "salonu", 'm'),
            "studio" => new("studio", "studia", 'n'),
            "gabinet" => new("gabinet", "gabinetu", 'm'),
            "barber" => new("barbershop", "barbershopu", 'm'),
            _ => new("firmę", "firmy", 'f'),
        };

    /// <summary>Dane leada potrzebne w wielu szablonach, policzone raz.</summary>
    private sealed class DraftContext(Lead lead, ToneProfile tone)
    {
        private readonly BusinessNoun _noun = NounFor(lead.Category);

        public Lead Lead { get; } = lead;
        public ToneProfile Tone { get; } = tone;
        public bool Informal => Tone.Informal;

        /// <summary>Oficjalna nazwa – tylko tam, gdzie jest potrzebna (adres na liście).</summary>
        public string Name => Lead.Place.Name;

        public string Host { get; } =
            Uri.TryCreate(lead.Place.WebsiteUri, UriKind.Absolute, out var uri) ? uri.Host : lead.Place.WebsiteUri ?? string.Empty;

        /// <summary>Biernik: "Państwa gabinet", "Wasz barbershop", "Wasze studio".</summary>
        public string YourPlace => Informal
            ? (_noun.Gender switch { 'n' => "Wasze", 'f' => "Waszą", _ => "Wasz" }) + " " + _noun.Accusative
            : "Państwa " + _noun.Accusative;

        /// <summary>Dopełniacz: "Państwa gabinetu", "Waszego barbershopu", "Waszej firmy".</summary>
        public string OfYourPlace => Informal
            ? (_noun.Gender == 'f' ? "Waszej" : "Waszego") + " " + _noun.Genitive
            : "Państwa " + _noun.Genitive;

        /// <summary>Czego dotyczy propozycja: "strona Państwa gabinetu" albo "odświeżona strona".</summary>
        public string WebsiteNoun => Lead.Status switch
        {
            LeadStatus.NoWebsite => $"strona {OfYourPlace}",
            LeadStatus.WebsiteDown => "nowa, działająca strona",
            _ => "odświeżona strona",
        };

        /// <summary>Korzyści wspólne dla każdej branży – dopisywane do listy w ofertach.</summary>
        public IEnumerable<string> CommonBenefits =>
        [
            "wersja dopracowana pod telefon – stamtąd wchodzi większość klientów",
            $"widoczność w Google na „{Lead.Category.Query} {Lead.City}”",
            "własna domena, a strona na własność – bez opłat za kreator",
        ];

        /// <summary>Wynik sprawdzenia strony w nawiasie, np. "domena nie istnieje lub nie ma rekordów DNS".</summary>
        public string TechnicalNote => Lead.WebsiteCheck?.Note ?? "strona nie odpowiada";
    }
}
