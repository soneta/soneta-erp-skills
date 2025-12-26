# Przykłady kodu - Podstawowe klasy ORM

Praktyczne przykłady użycia podstawowych klas logiki biznesowej enova365/Soneta.

## Ważne zasady

### Thread-safety

**Obiekty single-threaded** - nie współdziel między wątkami:
- `Session`, `Module`, `Table`, `Row`, `Context`

**Obiekty multi-threaded** - można współdzielić:
- `BusApplication`, `Database`, `Login`

Każdy wątek powinien tworzyć własną sesję (Login można współdzielić).

### Extension methods dla modułów

Dostęp do modułów przez extension methods:

```csharp
var tm = session.GetTowary();
var hm = session.GetHandel();
var crm = session.GetCRM();
var kadry = session.GetKadry();
var bm = session.GetBusiness();
```

### Transakcje biznesowe

**Każda zmiana obiektu MUSI być w transakcji** `Session.Logout(editMode: true)`:

```csharp
using (var transaction = session.Logout(editMode: true))
{
    // Zmiany: dodawanie, modyfikacja, kasowanie
    obiekt.Wlasciwosc = nowaWartosc;
    transaction.Commit();  // Zatwierdza zmiany
}
// Brak Commit() = automatyczny rollback

session.Save();  // Zapis do bazy danych
```

## Dostęp do danych

### Odczyt listy towarów

```csharp
using Soneta.Business;
using Soneta.Towary;

public void WyswietlTowary(Login login)
{
    // Sesja tylko do odczytu
    using (var session = login.CreateSession(true, false, "OdczytTowarow"))
    {
        var tm = session.GetTowary();  // Extension method
        
        // Iteracja po kluczu podstawowym (WgKodu)
        foreach (Towar t in tm.Towary.WgKodu)
        {
            Console.WriteLine($"{t.Kod}: {t.Nazwa}");
        }
    }
}
```

### Wyszukiwanie po kluczu

```csharp
public Towar ZnajdzTowar(Session session, string kod)
{
    var tm = session.GetTowary();
    
    // Wyszukiwanie po kluczu unikalnym
    return tm.Towary.WgKodu[kod];
}
```

### Iteracja z filtrowaniem

```csharp
public void WyswietlAktywneTowary(Session session)
{
    var tm = session.GetTowary();
    
    foreach (Towar t in tm.Towary.WgKodu)
    {
        // Filtrowanie w kodzie
        if (t.Typ == TypTowaru.Towar)
        {
            Console.WriteLine(t.Nazwa);
        }
    }
}
```

## Tworzenie obiektów

### Dodawanie nowego towaru

```csharp
public void DodajTowar(Login login)
{
    // Sesja edycyjna
    using (var session = login.CreateSession(false, false, "DodawanieTowaru"))
    {
        var tm = session.GetTowary();
        
        // Transakcja biznesowa - wymagana!
        using (var transaction = session.Logout(editMode: true))
        {
            // Utworzenie nowego obiektu
            var towar = new Towar();
            
            // Dodanie do tabeli (zmiana stanu na Added)
            tm.Towary.AddRow(towar);
            
            // Ustawienie właściwości
            towar.Kod = "NOWY001";
            towar.Nazwa = "Nowy towar";
            towar.Typ = TypTowaru.Towar;
            
            transaction.Commit();
        }
        
        // Zapisanie do bazy
        session.Save();
    }
}
```

### Dodawanie dokumentu z pozycjami

```csharp
public void UtworzFakture(Login login, Kontrahent kontrahentZInnejSesji, 
                          List<(Towar towar, int ilosc)> pozycjeZInnejSesji)
{
    using (var session = login.CreateSession(false, false, "TworzenieFaktury"))
    {
        var hm = session.GetHandel();
        
        // WAŻNE: Obiekty z innej sesji trzeba doczytać w bieżącej sesji!
        var kontrahent = session.Get(kontrahentZInnejSesji);
        
        // Cała operacja w jednej transakcji
        using (var transaction = session.Logout(editMode: true))
        {
            // Utworzenie nagłówka dokumentu
            var faktura = new DokumentHandlowy();
            hm.DokHandlowe.AddRow(faktura);
            
            faktura.Definicja = hm.DefDokHandlowe.WgSymbolu["FV"];
            faktura.Kontrahent = kontrahent;
            faktura.Data = Date.Today;
            
            // Dodanie pozycji
            int lp = 1;
            foreach (var (towarZInnejSesji, ilosc) in pozycjeZInnejSesji)
            {
                // Doczytaj towar w bieżącej sesji
                var towar = session.Get(towarZInnejSesji);
                
                var poz = new PozycjaDokHandlowego(faktura);
                faktura.Pozycje.AddRow(poz);
                
                poz.Towar = towar;
                poz.Ilosc = new Quantity(ilosc, towar.Jednostka.Kod);
                poz.Lp = lp++;
            }
            
            transaction.Commit();
        }
        
        session.Save();
    }
}
```

**WAŻNE:** W jednej sesji nie można mieszać obiektów z różnych sesji. Użyj `session.Get(obiekt)` aby doczytać obiekt w bieżącej sesji.

## Modyfikacja obiektów

### Aktualizacja pojedynczego obiektu

```csharp
public void ZmienNazweTowaru(Login login, string kod, string nowaNazwa)
{
    using (var session = login.CreateSession(false, false, "EdycjaTowaru"))
    {
        var tm = session.GetTowary();
        var towar = tm.Towary.WgKodu[kod];
        
        if (towar != null)
        {
            using (var transaction = session.Logout(editMode: true))
            {
                towar.Nazwa = nowaNazwa;
                transaction.Commit();
            }
            session.Save();
        }
    }
}
```

### Aktualizacja z transakcją biznesową

```csharp
public void AktualizujCeny(Login login, string nazwaCeny, decimal procentPodwyzki)
{
    using (var session = login.CreateSession(false, false, "AktualizacjaCen"))
    {
        var tm = session.GetTowary();
        
        foreach (Towar t in tm.Towary.WgKodu)
        {
            // Transakcja biznesowa - WYMAGANA dla każdej zmiany!
            using (var transaction = session.Logout(editMode: true))
            {
                var cena = t.Ceny[nazwaCeny];
                if (cena != null)
                {
                    cena.Netto = new DoubleCy(cena.Netto.Value * (1 + procentPodwyzki / 100));
                }
                transaction.Commit();
            }
        }
        
        session.Save();  // Zapisuje wszystkie zmiany do bazy
    }
}
```

## Usuwanie obiektów

### Usuwanie obiektu

```csharp
public void UsunTowar(Login login, string kod)
{
    using (var session = login.CreateSession(false, false, "UsuwanieTowaru"))
    {
        var tm = session.GetTowary();
        var towar = tm.Towary.WgKodu[kod];
        
        if (towar != null)
        {
            using (var transaction = session.Logout(editMode: true))
            {
                towar.Delete();  // Zmiana stanu na Deleted
                transaction.Commit();
            }
            session.Save();  // Fizyczne usunięcie z bazy
        }
    }
}
```

## Praca z kontekstem

### Worker wyliczający właściwość

```csharp
// Rejestracja workera na poziomie assembly
[assembly: Worker<TowarWorker, Towar>]

public class TowarWorker
{
    [Context]
    public Magazyn MagazynFiltra { get; set; }
    
    [Context]
    public Towar Towar { get; set; }
    
    public decimal StanMagazynowy
    {
        get
        {
            if (MagazynFiltra != null)
            {
                return PoliczStan(Towar, MagazynFiltra);
            }
            return PoliczStanCalkowity(Towar);
        }
    }
    
    private decimal PoliczStan(Towar towar, Magazyn magazyn)
    {
        // Implementacja...
        return 0;
    }
    
    private decimal PoliczStanCalkowity(Towar towar)
    {
        // Implementacja...
        return 0;
    }
}
```

### Akcja workera w menu Czynności

```csharp
// Rejestracja workera na poziomie assembly
[assembly: Worker<WyslijEmailWorker, Kontrahent>]

public class WyslijEmailWorker
{
    [Context]
    public Kontrahent[] Kontrahenci { get; set; }
    
    [Context]
    public Context Context { get; set; }
    
    [Action("Wyślij email")]
    public void Execute()
    {
        foreach (var k in Kontrahenci)
        {
            if (!string.IsNullOrEmpty(k.Email))
            {
                WyslijEmail(k.Email);
            }
        }
    }
    
    private void WyslijEmail(string email)
    {
        // Implementacja...
    }
}
```

### Klasa parametrów (ContextBase)

Klasa `ContextBase` jest przeznaczona do budowania klas parametrów, nie workerów:

```csharp
public class FiltryTowarow(Context context) : ContextBase(context)
{
    public Magazyn Magazyn { get; set; }
    
    [Caption("Typ towaru")]  // Etykieta w UI, gdy inna niż nazwa property
    public TypTowaru? Typ { get; set; }
    
    public bool TylkoAktywne { get; set; } = true;
}
```

**Uwaga:** W klasach parametrów atrybut `[Context]` nie jest wymagany.

**Współdzielenie wartości przez Context** - wartości parametrów można przechowywać w obiekcie Context, co pozwala na współdzielenie między różnymi klasami parametrów:

```csharp
public class FiltryTowarow(Context context) : ContextBase(context)
{
    public Magazyn Magazyn
    {
        get => Context.GetOrDefault<Magazyn>();
        set => Context.Set(value);
    }
    
    [Caption("Typ towaru")]
    public TypTowaru? Typ
    {
        get => Context.GetOrDefault<TypTowaru?>();
        set => Context.Set(value);
    }
}
```

## Praca z GuidedRow

### Dostęp do historii zmian

```csharp
public void PokazHistorie(Session session, Kontrahent kontrahent)
{
    var bm = session.GetBusiness();
    
    Console.WriteLine($"Historia zmian dla: {kontrahent.Nazwa}");
    
    foreach (ChangeInfo ci in bm.ChangeInfos[kontrahent])
    {
        Console.WriteLine($"  {ci.Time}: {ci.Operator}");
    }
    
    // Skróty
    Console.WriteLine($"Utworzono: {kontrahent.FirstChangeInfo?.Time}");
    Console.WriteLine($"Ostatnia zmiana: {kontrahent.LastChangeInfo?.Time}");
}
```

### Praca z załącznikami

```csharp
public void DodajZalacznik(Login login, Towar towar, byte[] plik, string nazwa)
{
    using (var session = login.CreateSession(false, false, "DodawanieZalacznika"))
    {
        // Doczytaj towar w bieżącej sesji
        var towarInSession = session.Get(towar);
        
        using (var transaction = session.Logout(editMode: true))
        {
            var attachment = new Attachment(towarInSession, AttachmentType.Attachments);
            towarInSession.Module.Business.Attachments.AddRow(attachment);

            attachment.Name = nazwa;
            attachment.RawData = plik;
            
            transaction.Commit();
        }
        session.Save();
    }
}

public void WyswietlZalaczniki(Towar towar)
{
    foreach (Attachment att in towar.Attachments)
    {
        Console.WriteLine($"- {att.Name} ({att.RawData.Length} bajtów)");
    }

    // Domyślne zdjęcie
    using var defaultImage = towar.DefaultImage;
    if (defaultImage != null)
    {
        Console.WriteLine($"Zdjęcie główne: {defaultImage.FileName}");
    }
}
```

## Dane konfiguracyjne vs operacyjne

### Odczyt danych konfiguracyjnych

```csharp
// Odczyt pojedynczej wartości
var opisDlaSzt = login.ExecuteConfig(configSession =>
    configSession.GetTowary().Jednostki.WgKodu["szt"]?.Opis);

// Odczyt listy wartości prostych
public string[] PobierzKodyJednostek(Login login)
{
    return login.ExecuteConfig(configSession => 
    {
        var tm = configSession.GetTowary();
        return tm.Jednostki.WgKodu.Select(j => j.Kod).ToArray();
    });
}
```

**WAŻNE:** Używając `ExecuteConfig()` nie można zwracać obiektów sesyjnych z sesji konfiguracyjnej, ponieważ może być używana w innym wątku do innych celów. Zwracaj tylko wartości proste lub kopie danych.

```csharp
public void OdczytKonfiguracji(Login login)
{
    // Własna sesja konfiguracyjna - gdy potrzebny dostęp do obiektów
    using (var session = login.CreateSession(true, true, "Konfiguracja"))
    {
        var tm = session.GetTowary();
        
        foreach (Jednostka j in tm.Jednostki.WgKodu)
        {
            Console.WriteLine($"{j.Kod}: {j.Opis}");
        }
    }
}
```

### Modyfikacja danych konfiguracyjnych

```csharp
public void DodajJednostke(Login login, string kod, string opis)
{
    // Sesja edycyjna konfiguracyjna
    using (var session = login.CreateSession(false, true, "DodawanieJednostki"))
    {
        var tm = session.GetTowary();
        
        using (var transaction = session.Logout(editMode: true))
        {
            var jednostka = new Jednostka();
            tm.Jednostki.AddRow(jednostka);
            
            jednostka.Kod = kod;
            jednostka.Opis = opis;
            
            transaction.Commit();
        }
        
        session.Save();
    }
}
```

## Pełny przykład - import towarów

```csharp
public class ImportTowarow
{
    public void Importuj(Login login, string sciezkaPliku)
    {
        var dane = WczytajZPliku(sciezkaPliku);
        
        using (var session = login.CreateSession(false, false, "ImportTowarow"))
        {
            var tm = session.GetTowary();
            int dodano = 0;
            int zaktualizowano = 0;
            
            // Cały import w jednej transakcji
            using (var transaction = session.Logout(editMode: true))
            {
                foreach (var wiersz in dane)
                {
                    // Sprawdź czy towar istnieje
                    var towar = tm.Towary.WgKodu[wiersz.Kod];
                    
                    if (towar == null)
                    {
                        // Dodaj nowy
                        towar = new Towar();
                        tm.Towary.AddRow(towar);
                        towar.Kod = wiersz.Kod;
                        dodano++;
                    }
                    else
                    {
                        zaktualizowano++;
                    }
                    
                    // Ustaw/aktualizuj właściwości
                    towar.Nazwa = wiersz.Nazwa;
                    var cena = towar.Ceny["Hurtowa"];
                    cena.Netto = new DoubleCy(wiersz.Cena);
                }
                
                transaction.Commit();
            }
            
            session.Save();
            
            Console.WriteLine($"Import zakończony:");
            Console.WriteLine($"  Dodano: {dodano}");
            Console.WriteLine($"  Zaktualizowano: {zaktualizowano}");
        }
    }
    
    private List<DaneImportu> WczytajZPliku(string sciezka)
    {
        // Implementacja wczytywania z CSV/Excel...
        return new List<DaneImportu>();
    }
    
    private class DaneImportu
    {
        public string Kod { get; set; }
        public string Nazwa { get; set; }
        public decimal Cena { get; set; }
    }
}
```

## Obsługa błędów

### Wzorzec try-catch z sesją

```csharp
public void BezpiecznaOperacja(Login login)
{
    using (var session = login.CreateSession(false, false, "Operacja"))
    {
        try
        {
            var tm = session.GetTowary();
            
            using (var transaction = session.Logout(editMode: true))
            {
                // Operacje na danych...
                var towar = new Towar();
                tm.Towary.AddRow(towar);
                towar.Kod = "TEST";
                
                transaction.Commit();
            }
            
            session.Save();
        }
        catch (Exception ex)
        {
            // Logowanie błędu
            Console.WriteLine($"Błąd: {ex.Message}");
            // Zmiany nie zostały zatwierdzone (brak Commit lub Save)
            // Sesja zostanie automatycznie zwolniona przez using
        }
    }
}
```

### Wzorzec z wieloma operacjami

```csharp
public void WieleOperacji(Login login, List<string> kody)
{
    using (var session = login.CreateSession(false, false, "WieleOperacji"))
    {
        var tm = session.GetTowary();
        var bledy = new List<string>();
        
        foreach (var kod in kody)
        {
            try
            {
                using (var transaction = session.Logout(editMode: true))
                {
                    var towar = tm.Towary.WgKodu[kod];
                    if (towar != null)
                    {
                        towar.Delete();
                        transaction.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                bledy.Add($"{kod}: {ex.Message}");
                // Kontynuuj z następnym elementem
            }
        }
        
        session.Save();  // Zapisz udane operacje
        
        if (bledy.Any())
        {
            Console.WriteLine("Błędy: " + string.Join(", ", bledy));
        }
    }
}
```
