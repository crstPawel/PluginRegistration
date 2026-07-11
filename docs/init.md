# Init — generowanie `pluginregistration.json`

Ten dokument opisuje, jak działa komenda `pluginreg init`: od wywołania CLI do pliku konfiguracyjnego deploy.

**Ważne:** `init` **nie łączy się z Dataverse** i **nie modyfikuje kodu źródłowego**. Tworzy wyłącznie plik `pluginregistration.json` w katalogu roboczym.

---

## Przegląd przepływu

```mermaid
flowchart TD
    A["pluginreg init"] --> B{"pluginregistration.json istnieje?"}
    B -->|tak, bez --force| C["Błąd: użyj --force"]
    B -->|nie lub --force| D["CreateFromSource"]
    D --> E["Skan plików .cs"]
    E --> F["Wykryj nazwy stepów"]
    E --> G["Wykryj Custom API"]
    F --> H["Utwórz profiles + stepOverrides"]
    G --> I["Utwórz profiles.customApis"]
    H --> J["Zapis pluginregistration.json"]
    I --> J
```

---

## Krok 1 — Uruchomienie CLI

Komenda `init` w `Program.cs` wywołuje `ConfigScaffoldService.Generate()`.

```bash
pluginreg init --path samples/Sample.Plugins --profiles dev,test,prod --assembly-path bin/Release --solution SampleSolution
```

| Parametr | Domyślnie | Opis |
|----------|-----------|------|
| `--path`, `-p` | bieżący katalog | Katalog projektu pluginów |
| `--profiles` | `dev,test,prod` | Lista profili oddzielona przecinkami |
| `--assembly-path` | `bin/Release` | Ścieżka DLL zapisywana w `plugins[].assemblyPath` |
| `--solution` | — | Nazwa solution dodawana do wpisu deploy |
| `--force` | `false` | Nadpisuje istniejący `pluginregistration.json` |

---

## Krok 2 — Walidacja pliku wyjściowego

`ConfigScaffoldService.Generate()` sprawdza, czy `pluginregistration.json` już istnieje.

- Plik istnieje i **brak** `--force` → wyjątek z komunikatem o użyciu `--force`.
- W przeciwnym razie kontynuuje generowanie.

---

## Krok 3 — Generowanie z kodu źródłowego

`init` zawsze skanuje pliki źródłowe `.cs` w podanym katalogu (`CreateFromSource`).

---

## Krok 4 — Skan plików źródłowych

`EnumerateSourceFiles()` przeszukuje rekurencyjnie katalog `--path`:

- uwzględnia pliki `*.cs`;
- pomija katalogi `obj/` i `bin/`.

Dla każdego pliku odczytywana jest zawartość tekstowa (regex, bez kompilacji).

---

## Krok 5 — Wykrywanie nazw stepów

`DiscoverStepNames()` szuka bloków `[CrmPluginRegistration(...)]` powiązanych z klasą pluginu (`PluginStepBlockRegex`).

Dla każdego dopasowania:

1. Wyciąga `StageEnum` z atrybutu.
2. Wyciąga nazwę klasy z deklaracji `class`.
3. Łączy z `namespace` z pliku.
4. Generuje nazwę stepu: `{namespace}.{class}.{Stage}` przez `PluginStepNameResolver`.

Przykład: `Sample.Plugins.AccountCreatePlugin.PreOperation`.

**Uwaga:** `init` nie czyta DLL ani Dataverse — wykrywa tylko to, co jest już zapisane w atrybutach w kodzie. Jeśli atrybutów jeszcze nie ma, `stepOverrides` będą puste.

---

## Krok 6 — Wykrywanie Custom API

`DiscoverCustomApis()` szuka wzorca:

```csharp
[CrmPluginRegistration("api_unique_name")]
public class MyPlugin : IPlugin
```

(`PluginTypeRegex`)

Dla każdego API zapisywane są:

- `uniqueName` — z atrybutu;
- `displayName` — domyślnie taki sam jak `uniqueName`;
- `pluginTypeName` — pełna nazwa klasy (`namespace.class`).

Duplikaty `uniqueName` są deduplikowane.

---

## Krok 7 — Budowa struktury JSON

### Sekcja `plugins`

Jeden wpis deploy dla wszystkich profili:

```json
{
  "profile": "dev,test,prod",
  "assemblyPath": "bin/Release",
  "solution": "SampleSolution"
}
```

Wartości pochodzą z parametrów CLI.

### Sekcja `profiles`

Dla **każdego** profilu z `--profiles` tworzony jest `ProfileSettings`:

#### `stepOverrides`

Dla każdej wykrytej nazwy stepu:

```json
"Sample.Plugins.AccountCreatePlugin.PreOperation": {
  "unSecureConfiguration": ""
}
```

Puste `unSecureConfiguration` to placeholder — uzupełniasz go ręcznie lub przez zmienne `${ENV_VAR}` przed deployem.

#### `customApis`

Dla każdego wykrytego Custom API:

```json
{
  "uniqueName": "sample_ProcessAccount",
  "displayName": "sample_ProcessAccount",
  "pluginTypeName": "Sample.Plugins.ProcessAccountCustomApiPlugin",
  "createIfMissing": true,
  "bindingType": 0
}
```

`createIfMissing: true` ustawiane jest **tylko dla pierwszego profilu** z listy `--profiles`. Pozostałe profile dostają `false`.

---

## Krok 8 — Serializacja i zapis

Konfiguracja jest serializowana do JSON (camelCase, pomijane wartości `null`) i zapisywana jako:

```
{workingDirectory}/pluginregistration.json
```

Narzędzie loguje ścieżkę utworzonego pliku.

---

## Przykład użycia

```bash
# Pierwsze uruchomienie w nowym projekcie
pluginreg init --path samples/Sample.Plugins

# Z własnymi profilami i solution
pluginreg init \
  --path samples/Sample.Plugins \
  --profiles dev,test,prod \
  --assembly-path bin/Release \
  --solution SampleSolution

# Nadpisanie istniejącego pliku
pluginreg init --path samples/Sample.Plugins --force
```

---

## Relacja `init` → `deploy`

| Element | `init` | `deploy` |
|---------|--------|----------|
| `plugins[].assemblyPath` | zapisuje | używa do wyszukania DLL |
| `plugins[].solution` | zapisuje | dodaje komponenty do solution |
| `profiles.*.stepOverrides` | tworzy szkielet | nadpisuje config stepów per środowisko |
| `profiles.*.customApis` | rejestruje wykryte API | `createIfMissing` tworzy API z JSON |

Typowy workflow:

```bash
pluginreg init --path ./MyPlugins
# uzupełnij stepOverrides w pluginregistration.json
dotnet build -c Release
pluginreg deploy --path ./MyPlugins --profile dev
```

---

## Ograniczenia

- **Bez atrybutów w kodzie** `init` nie wygeneruje `stepOverrides` ani `customApis`.
- **Nie waliduje** połączenia z Dataverse — to robi `whoami` / `deploy`.
- **Regex, nie kompilator** — wykrywanie opiera się na wzorcu tekstowym w plikach `.cs`; nietypowy format atrybutów może nie zostać rozpoznany.
- **Nie generuje atrybutów** — do synchronizacji metadanych z Dataverse do kodu służy `sync` (patrz [sync.md](sync.md)).

---

## W skrócie

`init` to **generator szkieletu konfiguracji deploy**: tworzy `pluginregistration.json` z profilami środowisk, kluczami `stepOverrides` i wpisami Custom API na podstawie istniejących atrybutów w kodzie.