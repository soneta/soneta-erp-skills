# ChangeInfos — dziennik zmian / audyt

`session.ChangeInfos` to wbudowany dziennik zdarzeń i zmian rekordów (audyt). Służy do
zapisania śladu operacji: utworzenia/modyfikacji/usunięcia rekordu, logowania, a także
zdarzeń własnych (np. operacji integracyjnych). Wpisy są widoczne m.in. w oknie
konfiguracji „Systemowe/Zmiany rekordów".

## Dopisanie wpisu

```csharp
// w transakcji (Logout) — wpis zapisuje się przy session.Save()
session.ChangeInfos.Add(row, ChangeInfoType.Modified, info, data);
// row może być null, gdy wpis nie dotyczy konkretnego rekordu
```

Najczęstsze przeciążenia:

| Wywołanie | Zastosowanie |
|---|---|
| `Add(GuidedRow row, ChangeInfoType type)` | zdarzenie na rekordzie bez dodatkowego opisu |
| `Add(GuidedRow row, ChangeInfoType type, string info)` | + krótki opis |
| `Add(GuidedRow row, ChangeInfoType type, string info, string data)` | + opis i dane dodatkowe |

Wpis powstaje w bieżącej transakcji (`Session.Logout`) i trafia do bazy przy `session.Save()`.

## Pola wpisu — co gdzie zapisać

| Pole | Typ | Uwagi |
|---|---|---|
| `Operator` | `Operator` | operator zdarzenia |
| `Time` | `DateTime` | data i godzina zdarzenia |
| `Type` | `ChangeInfoType` | rodzaj zdarzenia (enum) |
| `Info` | tekst krótki | **obcinany do 255 znaków** |
| `Data` | tekst długi (memo) | bez limitu — na obszerne treści (payloady, odpowiedzi, JSON) |

> **Pułapka 255 znaków.** Parametr `info` trafia do pola `Info`, które jest **obcinane do
> 255 znaków**. Treść, która ma być zachowana w całości, **musi** iść do `data` (pole `Data`,
> typu memo). Jeśli prezentujesz `Info` na liście, pamiętaj, że to wersja skrócona.

## Typy zdarzeń (`ChangeInfoType`)

Enum `ChangeInfoType` zawiera m.in.: `Created`, `Modified`, `Deleted`, `Accepted`, `Login`,
`Logout`, `Imported`, `Exported`, oraz wartości dziedzinowe/integracyjne. Dla własnego rodzaju
zdarzenia użyj odpowiedniej istniejącej wartości lub zdefiniowanej dla danej funkcji.

## Prezentacja na liście

Listę wpisów buduje się jak każdy widok przez `ViewInfo`/`CreateView` (zob.
[viewinfo.md](viewinfo.md)), filtrując np. po `Type`, `Operator` i zakresie `Time`:

```csharp
View view = args.Session.GetBusiness().ChangeInfos.ByTime.CreateView();
view.AddExpression<ChangeInfo>(ci => ci.Type == ChangeInfoType.Modified);
if (okres != FromTo.All) {
    DateTime od = (DateTime)okres.From, doD = (DateTime)(okres.To + 1);
    view.AddExpression<ChangeInfo>(ci => ci.Time >= od && ci.Time < doD);
}
args.DataSource = view;
```

Start z klucza `ByTime` wykorzystuje indeks czasu (`ChangeInfos` to tabela operacyjna —
ograniczaj zakres czasowy, zob. [safe-code.md](safe-code.md) §6.3).
