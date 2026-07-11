# PluginRegistration Tool — instrukcja użytkowania

Narzędzie do automatycznej rejestracji pluginów Dataverse na podstawie atrybutów w kodzie źródłowym. Przeznaczone do uruchamiania z pipeline'ów Azure DevOps oraz lokalnie z linii poleceń.

## Wymagania

- .NET SDK **10.0** (patrz `global.json`)
- Dostęp do środowiska Dataverse (Service Principal lub connection string)
- Zbudowany assembly pluginów (`.dll`) — wymagany tylko przy `deploy`, nie przy `sync`

---

## Uruchomienie lokalne — ten projekt (PluginRegistrationTool)

Narzędzie i projekt pluginów **mogą leżeć w osobnych katalogach**. Do `sync` wystarczy wskazać ścieżkę do kodu pluginów parametrem `--path`.

### 1. Zbuduj narzędzie

```bash
cd /ścieżka/do/PluginRegistrationTool
dotnet build -c Release
```

### 2. Uruchamiaj bez globalnej instalacji (najprostsze do pracy lokalnej)

Wszystkie komendy możesz odpalać przez `dotnet run` z katalogu tego repozytorium:

```bash
# Pomoc
dotnet run --project src/PluginRegistration.Tool -- --help

# Test połączenia z Dataverse
dotnet run --project src/PluginRegistration.Tool -- whoami

# Sync stepów w OSOBNYM projekcie pluginów
dotnet run --project src/PluginRegistration.Tool -- sync --path /ścieżka/do/MojProjektPluginow/src/MyPlugins
```

### 3. Opcjonalnie — zainstaluj globalnie jako `pluginreg`

```bash
dotnet pack -c Release -o nupkg
dotnet tool install --global PluginRegistration.Tool --version 1.0.3 --add-source ./nupkg

# Potem z dowolnego katalogu:
pluginreg sync --path /ścieżka/do/MojProjektPluginow/src/MyPlugins
```

> **Uwaga:** Po zmianach w kodzie narzędzia przebuduj je (`dotnet build`) lub przeinstaluj pakiet tool.

---

## Połączenie z Dataverse (konfiguracja lokalna)

Narzędzie łączy się przez `Microsoft.PowerPlatform.Dataverse.Client` (Service Principal / Client Secret).

### Krok 1 — dane, których potrzebujesz

| Wartość | Gdzie znaleźć |
|---------|----------------|
| **URL środowiska** | Power Platform Admin Center → Environments → otwórz środowisko → URL, np. `https://org-dev.crm4.dynamics.com` |
| **Tenant ID** | Azure Portal → Microsoft Entra ID → Overview → Tenant ID |
| **Client ID** | Azure Portal → App registrations → Twoja aplikacja → Application (client) ID |
| **Client Secret** | App registration → Certificates & secrets → New client secret |

### Krok 2 — rejestracja aplikacji w Entra ID (jednorazowo)

1. Azure Portal → **Microsoft Entra ID** → **App registrations** → **New registration**
2. Nazwa np. `pluginreg-local`, typ kont: **Single tenant**
3. Po utworzeniu: skopiuj **Application (client) ID** i **Directory (tenant) ID**
4. **Certificates & secrets** → **New client secret** → skopiuj wartość (widoczna tylko raz)
5. W Power Platform Admin Center → środowisko → **Settings** → **Users + permissions** → **Application users**:
   - Dodaj użytkownika aplikacji (twój Client ID)
   - Przypisz rolę bezpieczeństwa, np. **System Administrator** (DEV) lub dedykowaną rolę do rejestracji pluginów

### Krok 3 — ustaw zmienne środowiskowe (zalecane lokalnie)

**Linux / macOS:**

```bash
export DATAVERSE_URL="https://twoja-org.crm4.dynamics.com"
export DATAVERSE_CLIENT_ID="00000000-0000-0000-0000-000000000000"
export DATAVERSE_CLIENT_SECRET="twoj-sekret"
export DATAVERSE_TENANT_ID="00000000-0000-0000-0000-000000000000"
```

**Windows PowerShell:**

```powershell
$env:DATAVERSE_URL="https://twoja-org.crm4.dynamics.com"
$env:DATAVERSE_CLIENT_ID="00000000-0000-0000-0000-000000000000"
$env:DATAVERSE_CLIENT_SECRET="twoj-sekret"
$env:DATAVERSE_TENANT_ID="00000000-0000-0000-0000-000000000000"
```

Możesz też trzymać je w pliku `.env` i załadować przed uruchomieniem (pliku nie commituj do git).

### Krok 4 — weryfikacja połączenia

```bash
cd /ścieżka/do/PluginRegistrationTool

dotnet run --project src/PluginRegistration.Tool -- whoami
```

Oczekiwany wynik:

```
OrganizationId: ...
BusinessUnitId: ...
UserId: ...
```

### Alternatywa — connection string zamiast zmiennych

```bash
dotnet run --project src/PluginRegistration.Tool -- whoami \
  --connection "AuthType=ClientSecret;Url=https://twoja-org.crm4.dynamics.com;ClientId=<id>;ClientSecret=<secret>;TenantId=<tenant>"
```

---

## Sync stepów w osobnym projekcie pluginów

Typowy scenariusz: **PluginRegistrationTool** w jednym folderze, **kod pluginów** w drugim.

### Wymagania w projekcie pluginów

1. Klasy pluginów istnieją w plikach `.cs` (np. `public class X : BasePlugin`)
2. Pluginy są **już zarejestrowane** w wybranym środowisku Dataverse (assembly + plugintype + stepy)
3. Pełne nazwy typów w kodzie (`namespace` + `class`) muszą **zgadzać się** z `plugintype.typename` w Dataverse

`sync` **nie wymaga** `pluginregistration.json` — operuje tylko na plikach `.cs`.

### Przepływ krok po kroku

```bash
# 1. Ustaw połączenie (patrz sekcja wyżej)
export DATAVERSE_URL="https://org-dev.crm4.dynamics.com"
export DATAVERSE_CLIENT_ID="..."
export DATAVERSE_CLIENT_SECRET="..."
export DATAVERSE_TENANT_ID="..."

# 2. Przejdź do repozytorium NARZĘDZIA
cd /ścieżka/do/PluginRegistrationTool

# 3. Sprawdź połączenie
dotnet run --project src/PluginRegistration.Tool -- whoami

# 4. Zacommituj zmiany w projekcie pluginów (sync nadpisuje pliki .cs)
cd /ścieżka/do/MojProjektPluginow
git status

# 5. Uruchom sync wskazując KATALOG Z KODEM pluginów (absolutna ścieżka zalecana)
cd /ścieżka/do/PluginRegistrationTool
dotnet run --project src/PluginRegistration.Tool -- sync \
  --path /ścieżka/do/MojProjektPluginow/src/MyPlugins
```

### Co robi `sync` w projekcie docelowym

1. Skanuje wszystkie `*.cs` w `--path` (rekurencyjnie, pomija `bin/` i `obj/`)
2. Wykrywa klasy pluginów — także dziedziczące po własnej klasie bazowej (`BasePlugin : IPlugin`)
3. Dla każdego typu pobiera stepy, obrazy i pełną definicję Custom API (wraz z parametrami) z Dataverse
4. Usuwa stare atrybuty rejestracji (`[CrmPluginRegistration]`, `[CrmCustomApiRequestParameter]`, `[CrmCustomApiResponseProperty]`) i wstawia nowe nad klasami
5. Zapisuje zmodyfikowane pliki `.cs`

### Przykład — dwa osobne repozytoria

```
/home/user/PluginRegistrationTool/     ← to repozytorium (narzędzie)
/home/user/MyCrmPlugins/               ← osobny projekt pluginów
    src/
      MyCompany.Plugins/
        BasePlugin.cs
        AccountCreatePlugin.cs
        ContactUpdatePlugin.cs
```

```bash
export DATAVERSE_URL="https://contoso-dev.crm4.dynamics.com"
export DATAVERSE_CLIENT_ID="..."
export DATAVERSE_CLIENT_SECRET="..."
export DATAVERSE_TENANT_ID="..."

dotnet run --project /home/user/PluginRegistrationTool/src/PluginRegistration.Tool -- \
  sync --path /home/user/MyCrmPlugins/src/MyCompany.Plugins
```

### Po sync — konfiguracja deploy (opcjonalnie)

Jeśli chcesz potem używać `deploy` z profilami środowisk, w projekcie pluginów wygeneruj konfigurację:

```bash
dotnet run --project /home/user/PluginRegistrationTool/src/PluginRegistration.Tool -- \
  init --path /home/user/MyCrmPlugins --profiles dev,test,prod --solution MySolution
```

Następnie ręcznie uzupełnij `stepOverrides` w `pluginregistration.json` (URL API per środowisko).

### Pełny scenariusz: sync → init → deploy

```bash
# A. Pobierz stepy z DEV do kodu (osobny projekt)
dotnet run --project src/PluginRegistration.Tool -- \
  sync --path /ścieżka/do/MojProjektPluginow/src/MyPlugins

# B. Wygeneruj pluginregistration.json w projekcie pluginów
dotnet run --project src/PluginRegistration.Tool -- \
  init --path /ścieżka/do/MojProjektPluginow --profiles dev,test,prod

# C. Zbuduj pluginy w projekcie pluginów
cd /ścieżka/do/MojProjektPluginow
dotnet build -c Release

# D. Wdróż na DEV (z katalogu projektu pluginów)
dotnet run --project /ścieżka/do/PluginRegistrationTool/src/PluginRegistration.Tool -- \
  deploy --path /ścieżka/do/MojProjektPluginow --profile dev
```

---

## Pakiety NuGet

| Pakiet                        | Opis                                                                 |
|-------------------------------|----------------------------------------------------------------------|
| `PluginRegistration.Attributes` | Atrybuty `[CrmPluginRegistration]`, `[CrmCustomApiRequestParameter]`, `[CrmCustomApiResponseProperty]` — **dodawaj do projektów pluginów** |
| `PluginRegistration.Core`     | Biblioteka rejestracji (rzadziej używana bezpośrednio)              |
| `PluginRegistration.Tool`     | Globalne narzędzie CLI `pluginreg`                                  |

## Publikowanie pakietów do repozytorium (NuGet feed)

Chcesz używać `pluginreg` i atrybutów w innych repozytoriach/pluginach bez budowania tego narzędzia za każdym razem.

### 1. Przygotowanie wersji

Wersja jest centralnie zarządzana w `Directory.Build.props`:

```xml
<Version>1.0.3</Version>
```

Zmień wersję przed publikacją nowej wersji pakietów.

### 2. Budowanie pakietów

```bash
# Z root tego repozytorium
dotnet pack -c Release -o ./artifacts
```

Spowoduje to wygenerowanie:
- `PluginRegistration.Attributes.1.0.3.nupkg` + `.snupkg`
- `PluginRegistration.Core.1.0.3.nupkg` + `.snupkg`
- `PluginRegistration.Tool.1.0.3.nupkg` + `.snupkg`

### 3. Wrzucanie (push) do repozytorium

#### Przykład — GitHub Packages

```bash
# Potrzebujesz PAT z uprawnieniem `packages:write`
dotnet nuget push ./artifacts/*.nupkg \
  --api-key $GITHUB_TOKEN \
  --source "https://nuget.pkg.github.com/TWOJA_ORG/index.json" \
  --skip-duplicate
```

#### Przykład — Azure Artifacts (Azure DevOps)

```bash
# Użyj Personal Access Token z uprawnieniem Packaging (Read & write)
dotnet nuget push ./artifacts/*.nupkg \
  --api-key $AZURE_ARTIFACTS_PAT \
  --source "https://pkgs.dev.azure.com/TWOJA_ORG/_packaging/TWOJ_FEED/nuget/v3/index.json" \
  --skip-duplicate
```

#### Inne feedy

Użyj odpowiedniego `--source` + `--api-key` (lub uwierzytelnienia przez `dotnet nuget add source`).

**Wskazówka:** zawsze używaj `--skip-duplicate`, żeby nie psuć pipeline'ów przy ponownym uruchomieniu.

### 4. Konfiguracja feedu w innych projektach

#### Opcja A — `NuGet.config` w repozytorium pluginów (zalecane)

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="my-internal-feed" value="https://pkgs.dev.azure.com/.../nuget/v3/index.json" />
    <!-- lub GitHub Packages -->
    <!-- <add key="github" value="https://nuget.pkg.github.com/TWOJA_ORG/index.json" /> -->
  </packageSources>
  <packageSourceCredentials>
    <!-- W razie potrzeby poświadczenia (lub użyj PAT w zmiennych CI) -->
  </packageSourceCredentials>
</configuration>
```

#### Opcja B — `--add-source` przy instalacji narzędzia

```bash
dotnet tool install --global PluginRegistration.Tool --version 1.0.3 \
  --add-source https://pkgs.dev.azure.com/.../nuget/v3/index.json
```

### 5. Korzystanie z pakietów w innych projektach

**Atrybuty w projekcie pluginów:**

```bash
dotnet add package PluginRegistration.Attributes
```

lub bezpośrednio w `.csproj`:

```xml
<PackageReference Include="PluginRegistration.Attributes" Version="1.0.3" />
```

**Instalacja narzędzia w pipeline / lokalnie:**

```bash
dotnet tool install --global PluginRegistration.Tool --version 1.0.3
# lub z prywatnego feedu
dotnet tool install --global PluginRegistration.Tool --version 1.0.3 --add-source <feed-url>
```

Potem normalnie:

```bash
pluginreg deploy --path . --profile dev
```

## Instalacja narzędzia CLI (lokalnie z tego repo)

```bash
dotnet pack -c Release -o ./nupkg
dotnet tool install --global PluginRegistration.Tool --add-source ./nupkg --version 1.0.3
```

### Uruchomienie bez instalacji globalnej

```bash
dotnet run --project src/PluginRegistration.Tool -- deploy --path . --profile dev
```

## Szybki start

### 1. Dodaj atrybuty do projektu pluginów

```bash
dotnet add package PluginRegistration.Attributes
```

Oznacz klasy pluginów atrybutem `[CrmPluginRegistration]`:

```csharp
using Microsoft.Xrm.Sdk;
using PluginRegistration.Attributes;

[CrmPluginRegistration(
    "Create",
    "account",
    StageEnum.PreOperation,
    ExecutionModeEnum.Synchronous,
    "",
    1)]
public class AccountCreatePlugin : IPlugin
{
    public void Execute(IServiceProvider serviceProvider) { }
}
```

### 2. Wygeneruj plik `pluginregistration.json`

**Automatycznie (zalecane):**

```bash
pluginreg init --path . --profiles dev,test,prod --solution MySolution
```

Narzędzie skanuje pliki `.cs`, wykrywa nazwy stepów z atrybutów `[CrmPluginRegistration]` oraz Custom API, a następnie tworzy gotowy szablon z sekcją `profiles` per środowisko.



Opcje:

| Opcja | Domyślnie | Opis |
|-------|-----------|------|
| `--profiles` | `dev,test,prod` | Profile środowisk oddzielone przecinkami |
| `--assembly-path` | `bin/Release` | Ścieżka do DLL |
| `--solution` | — | Nazwa solution w Dataverse |
| `--force` | — | Nadpisz istniejący plik |

**Ręcznie:** skopiuj szablon z `samples/Sample.Plugins/pluginregistration.json` i dostosuj wartości.

Plik umieść w katalogu roboczym narzędzia (root repozytorium pluginów lub folder z artefaktem buildu).

```json
{
  "plugins": [
    {
      "profile": "dev,test,prod",
      "assemblyPath": "bin/Release",
      "solution": "MySolution"
    }
  ],
  "profiles": {
    "dev": {
      "stepOverrides": {
        "MyPlugin.AccountCreate.PreOperation": {
          "unSecureConfiguration": "https://dev-api.contoso.com"
        }
      }
    },
    "prod": {
      "stepOverrides": {
        "MyPlugin.AccountCreate.PreOperation": {
          "unSecureConfiguration": "${PROD_API_URL}"
        }
      }
    }
  }
}
```

### 3. Zbuduj pluginy

```bash
dotnet build -c Release
```

### 4. Wdróż rejestrację

```bash
export DATAVERSE_URL="https://org.crm4.dynamics.com"
export DATAVERSE_CLIENT_ID="<app-id>"
export DATAVERSE_CLIENT_SECRET="<secret>"
export DATAVERSE_TENANT_ID="<tenant-id>"

pluginreg deploy --path . --profile dev
```

---

## Komendy CLI

```bash
pluginreg --help
```

| Komenda | Opis |
|---------|------|
| `pluginreg init` | Generuje `pluginregistration.json` z kodu źródłowego |
| `pluginreg deploy` | Wgrywa assembly i rejestruje/aktualizuje stepy pluginów |
| `pluginreg sync` | Pobiera metadane z Dataverse i zapisuje atrybuty w plikach `.cs` |
| `pluginreg whoami` | Sprawdza połączenie z Dataverse |

### `deploy` — opcje

| Opcja | Skrót | Opis |
|-------|-------|------|
| `--path` | `-p` | Katalog z `pluginregistration.json` i assembly (domyślnie: bieżący) |
| `--profile` | `-pr` | Profil środowiska: `dev`, `test`, `prod` itd. |
| `--connection` | `-c` | Connection string (alternatywa dla zmiennych środowiskowych) |
| `--exclude-steps` | | Tylko aktualizacja assembly, bez rejestracji stepów |
| `--workflow` | | Rejestruj też custom workflow activities |

Przykłady:

```bash
# Wdrożenie na środowisko testowe
pluginreg deploy --path ./src/MyPlugins --profile test

# Tylko aktualizacja assembly (bez stepów)
pluginreg deploy --profile prod --exclude-steps

# Z jawnym connection stringiem
pluginreg deploy -c "AuthType=ClientSecret;Url=https://org.crm4.dynamics.com;..." --profile dev
```

### `sync` — opcje

| Opcja | Opis |
|-------|------|
| `--path` / `-p` | Katalog z plikami `.cs` pluginów (**może być absolutna ścieżka do innego projektu**) |
| `--connection` / `-c` | Connection string |
| `--class-regex` | Własny regex wykrywania klas pluginów (gdy automatyczna analiza dziedziczenia nie wystarcza) |

```bash
# Projekt pluginów w tym samym repo
pluginreg sync --path ./src/MyPlugins

# Projekt pluginów w osobnej lokalizacji
pluginreg sync --path /home/user/MyCrmPlugins/src/MyCompany.Plugins
```

Komenda nadpisuje atrybuty rejestracji w kodzie na podstawie aktualnej konfiguracji w Dataverse (w tym parametry Custom API).

**Wykrywanie klas:** obsługiwane są pluginy dziedziczące po własnej klasie bazowej (`BasePlugin : IPlugin` → `MyPlugin : BasePlugin`). Wymaga, aby pliki `.cs` klas bazowych i pochodnych były w tym samym drzewie katalogów co `--path`.

**Przed sync:** zrób commit / backup — pliki `.cs` są nadpisywane bez tworzenia kopii `.bak`.

### `whoami`

```bash
pluginreg whoami
```

Zwraca `OrganizationId`, `BusinessUnitId` i `UserId` — przydatne do weryfikacji połączenia w pipeline.

---

## Autentykacja

### Zmienne środowiskowe (zalecane w Azure DevOps)

Narzędzie wspiera **trzy metody** autentykacji:

| Metoda                  | Wymagane zmienne                                                                 | Najlepsza dla |
|-------------------------|----------------------------------------------------------------------------------|---------------|
| **Client Secret**       | `DATAVERSE_URL` + ClientId + ClientSecret + TenantId                             | Proste scenariusze |
| **Certificate**         | `DATAVERSE_URL` + `..._CLIENT_CERTIFICATE_PATH` + ClientId + TenantId            | Lepsze bezpieczeństwo |
| **Access Token**        | `DATAVERSE_URL` + `DATAVERSE_ACCESS_TOKEN`                                       | **Workload Identity Federation (WIF)** |

**Wspierane prefiksy zmiennych (w kolejności priorytetu):**

- `DATAVERSE_*`
- `POWERPLATFORM_*`
- `AZURE_*` (dla ClientId/Secret/Tenant/CertificatePath — z service connection)

**Przykłady zmiennych z Azure DevOps Service Connection:**

- `AZURE_CLIENT_ID`
- `AZURE_CLIENT_SECRET`
- `AZURE_TENANT_ID`
- `AZURE_CLIENT_CERTIFICATE_PATH`
- `AZURE_CLIENT_CERTIFICATE_PASSWORD` (opcjonalnie)

**Dla WIF (najlepsza opcja):**

```bash
export DATAVERSE_ACCESS_TOKEN=$(az account get-access-token --resource "$DATAVERSE_URL" --query accessToken -o tsv)
```

### Connection string (klasyczny)

**Najlepsza praktyka (2025+):** Używaj **Workload Identity Federation** (brak sekretów) + pobieraj token w pipeline.

### Connection string

```
AuthType=ClientSecret;Url=https://org.crm4.dynamics.com;ClientId=<id>;ClientSecret=<secret>;TenantId=<tenant>
```

---

## Konfiguracja `pluginregistration.json`

### Sekcja `plugins`

Definiuje, które assembly wdrażać i do jakiej solution dodać komponenty.

| Pole | Opis |
|------|------|
| `profile` | Lista profili oddzielona przecinkami, np. `"dev,test,prod"`. Puste = profil domyślny |
| `assemblyPath` | Ścieżka do folderu z DLL lub wzorzec, np. `bin/Release` lub `bin/Release/*.dll` |
| `solution` | Unikalna nazwa solution, do której dodawane są komponenty (opcjonalnie) |
| `excludePluginSteps` | `true` = tylko assembly, bez stepów |

### Sekcja `profiles`

Nadpisania per środowisko — stosowane przy `deploy --profile <nazwa>`.

#### `stepOverrides`

Klucz: **nazwa stepu** lub **GUID stepu** (`Id` z atrybutu).

| Pole | Opis |
|------|------|
| `unSecureConfiguration` | Konfiguracja niezabezpieczona stepu |
| `secureConfiguration` | Konfiguracja zabezpieczona stepu |
| `description` | Opis stepu |

Wartości mogą zawierać placeholdery `${NAZWA_ZMIENNEJ}` — podstawiane ze zmiennych środowiskowych w momencie deployu:

```json
"unSecureConfiguration": "${PROD_API_URL}"
```

#### `customApis` (opcjonalnie — fallback / nadpisania)

**Źródłem prawdy dla struktury Custom API jest kod** (atrybuty na klasie pluginu). Sekcja `customApis` w profilu służy głównie do:

- definicji API **tylko w JSON** (bez atrybutów w kodzie) — wymaga `createIfMissing: true`
- nadpisania `displayName` / `description` per środowisko

| Pole | Opis |
|------|------|
| `uniqueName` | Unikalna nazwa Custom API (wymagane) |
| `displayName` | Nadpisanie nazwy wyświetlanej (opcjonalne) |
| `description` | Nadpisanie opisu (opcjonalne) |
| `pluginTypeName` | Pełna nazwa typu pluginu, np. `MyNamespace.MyPlugin` |
| `createIfMissing` | `true` = utwórz API z definicji JSON, jeśli nie istnieje w kodzie |
| `isFunction` | Czy API jest funkcją OData (tylko przy definicji w JSON) |
| `isPrivate` | Czy API jest prywatne (tylko przy definicji w JSON) |
| `bindingType` | `0` = Global, `1` = Entity, `2` = EntityCollection |
| `boundEntityLogicalName` | Encja przy binding typu Entity / EntityCollection |
| `allowedCustomProcessingStepType` | `0` = None, `1` = AsyncOnly, `2` = SyncAndAsync |
| `requestParameters` | Lista parametrów wejściowych (tylko definicja w JSON) |
| `responseProperties` | Lista właściwości odpowiedzi (tylko definicja w JSON) |

---

## Rejestracja plugin stepów

Struktura stepów (message, entity, stage, mode, images) jest definiowana **w kodzie** atrybutem. Narzędzie:

1. Wgrywa/aktualizuje `pluginassembly` (zawartość DLL w base64)
2. Rejestruje/aktualizuje `plugintype` dla każdej klasy z atrybutem
3. Tworzy/aktualizuje `sdkmessageprocessingstep` i obrazy (pre/post image)
4. Usuwa stepy, które zniknęły z atrybutów
5. Stosuje `stepOverrides` z profilu środowiska
6. Opcjonalnie dodaje komponenty do solution

### Przykład stepu z obrazem i konfiguracją

```csharp
[CrmPluginRegistration(
    "Update",
    "account",
    StageEnum.PreOperation,
    ExecutionModeEnum.Synchronous,
    "name,telephone1",
    1,
    Id = "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    Description = "Walidacja przed zapisem")]
[CrmPluginStepImage(
    StageEnum.PreOperation,
    "PreImage",
    ImageTypeEnum.PreImage,
    "name,telephone1",
    Message = "Update")]
public class AccountUpdatePlugin : IPlugin { ... }
```

Pole `Id` wiąże step ze stałym GUID w Dataverse — przydatne przy synchronizacji między środowiskami.

### Nazwa stepu i isolation mode

- **Nazwa stepu** (`stepName`) nie jest wymagana w atrybucie — przy `deploy` generowana automatycznie jako:

  `{namespace}.{nazwa_klasy}.{StageEnum}`

  Przykład: klasa `Sample.Plugins.AccountCreatePlugin` ze stage `PreOperation` → `Sample.Plugins.AccountCreatePlugin.PreOperation`

- **Isolation mode** domyślnie `Sandbox` — nie trzeba go podawać w konstruktorze.

- Niestandardową nazwę stepu możesz wymusić właściwością nazwaną `Name` (np. gdy dwa kroki mają ten sam `Stage`):

  ```csharp
  [CrmPluginRegistration("Create", "account", StageEnum.PreOperation, ExecutionModeEnum.Synchronous, "", 1)]
  [CrmPluginRegistration("Update", "account", StageEnum.PreOperation, ExecutionModeEnum.Synchronous, "name", 1,
      Name = "MyNamespace.MyPlugin.UpdatePreOperation")]
  ```

---

## Rejestracja Custom API

Custom API można w pełni zdefiniować w kodzie. Przy `deploy` narzędzie:

1. Tworzy lub aktualizuje rekord `customapi` w Dataverse
2. Rejestruje parametry wejściowe (`customapirequestparameter`) i właściwości odpowiedzi (`customapiresponseproperty`)
3. Wiąże Custom API z typem pluginu (`plugintypeid`)
4. Dodaje komponenty do solution z `pluginregistration.json` (pole `solution` w sekcji `plugins`)

### Definicja w kodzie (zalecane)

```csharp
using Microsoft.Xrm.Sdk;
using PluginRegistration.Attributes;

[CrmPluginRegistration(
    "sample_ProcessAccount",
    FriendlyName = "Process Account",
    Description = "Przetwarza identyfikator konta",
    CustomApiBindingType = CustomApiBindingTypeEnum.Global,
    IsFunction = false,
    IsPrivate = false,
    AllowedCustomProcessingStepType = CustomApiProcessingStepTypeEnum.None)]
[CrmCustomApiRequestParameter(
    "AccountId",
    CustomApiParameterTypeEnum.String,
    IsRequired = true,
    Description = "Identyfikator konta")]
[CrmCustomApiResponseProperty(
    "Success",
    CustomApiParameterTypeEnum.Boolean,
    Description = "Czy operacja się powiodła")]
public sealed class ProcessAccountCustomApiPlugin : IPlugin
{
    public void Execute(IServiceProvider serviceProvider)
    {
        // Odczyt: context.InputParameters["AccountId"]
        // Zapis:  context.OutputParameters["Success"] = true;
    }
}
```

#### Atrybut główny `[CrmPluginRegistration("unique_name")]`

| Właściwość | Opis |
|------------|------|
| `FriendlyName` | Nazwa wyświetlana Custom API |
| `Description` | Opis API |
| `CustomApiBindingType` | `Global` (0), `Entity` (1), `EntityCollection` (2) |
| `BoundEntityLogicalName` | Wymagane przy binding `Entity` / `EntityCollection` |
| `IsFunction` | `true` = OData Function (GET); **nie można zmienić po utworzeniu** |
| `IsPrivate` | `true` = ukryte w $metadata |
| `AllowedCustomProcessingStepType` | `None`, `AsyncOnly`, `SyncAndAsync` |

#### Parametry wejściowe `[CrmCustomApiRequestParameter]`

| Właściwość | Opis |
|------------|------|
| `UniqueName` | Pierwszy argument konstruktora — unikalna nazwa parametru |
| `Type` | Drugi argument — `CustomApiParameterTypeEnum` (np. `String`, `Guid`, `EntityReference`) |
| `DisplayName` | Nazwa wyświetlana (domyślnie = `UniqueName`) |
| `Description` | Opis parametru |
| `IsRequired` | `true` = parametr wymagany |
| `EntityLogicalName` | Wymagane dla typów `Entity`, `EntityReference`, `EntityCollection` |

#### Właściwości odpowiedzi `[CrmCustomApiResponseProperty]`

| Właściwość | Opis |
|------------|------|
| `UniqueName` | Unikalna nazwa właściwości odpowiedzi |
| `Type` | `CustomApiParameterTypeEnum` |
| `DisplayName` | Nazwa wyświetlana |
| `Description` | Opis |
| `EntityLogicalName` | Dla typów encji |

Dozwolone typy parametrów: `Boolean`, `DateTime`, `Decimal`, `Entity`, `EntityCollection`, `EntityReference`, `Float`, `Integer`, `Money`, `Picklist`, `String`, `Guid`, `StringArray`.

### Zachowanie przy `deploy`

| Sytuacja | Działanie narzędzia |
|----------|---------------------|
| Custom API nie istnieje | Tworzy API, parametry, response properties i link do pluginu |
| Zmiana pól **edytowalnych** (display name, description, isprivate, isoptional…) | Aktualizuje istniejące rekordy |
| Dodanie / usunięcie parametru w kodzie | Tworzy nowe lub usuwa zbędne rekordy |
| Zmiana pól **niezmienialnych** po utworzeniu | Usuwa całe Custom API (parametry + response + API) i tworzy od nowa |

Pola uznawane za **niezmienialne** (wymuszają recreate):

- `bindingtype`, `isfunction`, `boundentitylogicalname`
- typ parametru (`type`) lub `logicalentityname` istniejącego parametru
- zmiana `IsRequired` na istniejącym parametrze wejściowym

Przy recreate typ pluginu (`plugintype`) pozostaje — odtwarzana jest tylko definicja Custom API i jej parametry.

### Minimalna forma

Jeśli podasz tylko nazwę API bez parametrów, narzędzie utworzy Custom API i powiąże je z typem pluginu:

```csharp
[CrmPluginRegistration("my_ProcessOrder")]
public class ProcessOrderApiPlugin : IPlugin
{
    public void Execute(IServiceProvider serviceProvider) { }
}
```

Parametry możesz dodać później w kodzie i wdrożyć przez `deploy`, albo pobrać z Dataverse przez `sync`.

### Definicja tylko w profilu JSON (fallback)

Gdy Custom API nie ma atrybutów w kodzie, możesz je zdefiniować w `profiles.customApis` z `createIfMissing: true`:

```json
"customApis": [
  {
    "uniqueName": "my_ProcessOrder",
    "displayName": "Process Order",
    "pluginTypeName": "MyNamespace.ProcessOrderApiPlugin",
    "createIfMissing": true,
    "bindingType": 0,
    "requestParameters": [
      {
        "uniqueName": "OrderId",
        "type": 10,
        "isRequired": true
      }
    ],
    "responseProperties": [
      {
        "uniqueName": "Success",
        "type": 0
      }
    ]
  }
]
```

Kolejność przy `deploy`: najpierw rejestracja assembly i typów pluginów, potem Custom API z kodu, na końcu definicje wyłącznie z profilu JSON.

### `sync` Custom API

`pluginreg sync` pobiera z Dataverse pełną definicję Custom API powiązaną z typem pluginu i zapisuje w kodzie:

- `[CrmPluginRegistration(...)]` z metadanymi API
- `[CrmCustomApiRequestParameter(...)]` dla każdego parametru wejściowego
- `[CrmCustomApiResponseProperty(...)]` dla każdej właściwości odpowiedzi

```bash
pluginreg sync --path ./src/MyPlugins
```

### Przykład deploy Custom API

```bash
cd samples/Sample.Plugins
dotnet build -c Release
pluginreg deploy --path . --profile dev
```

W logach zobaczysz m.in. `Creating Custom API`, `Creating Custom API request parameter`, `Adding Custom API component to solution`.

---

## Azure DevOps

Szablon: `templates/azure-pipelines-plugin-deploy.yml`

### Zalecane podejście: Azure DevOps Service Connections

W Azure DevOps **najlepszą praktyką** jest użycie **Service Connection** typu *Azure Resource Manager* zamiast przechowywania sekretów w Variable Groups.

Korzyści:
- Poświadczenia Service Principal są zarządzane centralnie w Azure DevOps (nie duplikujesz ich w zmiennych).
- Łatwe przełączanie między środowiskami (różne service connections).
- Wsparcie dla Workload Identity Federation (brak długotrwałych sekretów).

**Jak to działa:**
1. Utwórz **Azure Resource Manager** service connection w Project Settings → Service connections (np. `dataverse-dev-spn`).
2. W pipeline użyj zadania `AzureCLI@2` z parametrem `azureSubscription`.
3. Zadanie automatycznie ustawia zmienne `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET` i `AZURE_TENANT_ID`.
4. Narzędzie `pluginreg` automatycznie je wykryje (jako fallback).

### Przykładowy krok z Service Connection (Client Secret lub Certificate)

```yaml
- task: AzureCLI@2
  displayName: Register plugins (DEV)
  inputs:
    azureSubscription: 'dataverse-dev-spn'   # ← Azure RM Service Connection
    scriptType: bash
    scriptLocation: inlineScript
    inlineScript: |
      cd $(Pipeline.Workspace)/plugins
      pluginreg whoami
      pluginreg deploy --path . --profile dev
  env:
    DATAVERSE_URL: $(DATAVERSE_URL)
```

Narzędzie automatycznie użyje:
- `AZURE_CLIENT_SECRET` (jeśli secret-based), lub
- `AZURE_CLIENT_CERTIFICATE_PATH` (jeśli connection używa certyfikatu)

### Najlepszy wzorzec: Workload Identity Federation + Access Token (zalecane)

```yaml
- task: AzureCLI@2
  displayName: Register plugins (WIF - no secrets)
  inputs:
    azureSubscription: 'dataverse-dev-wif'   # Service Connection z Workload Identity Federation
    scriptType: bash
    scriptLocation: inlineScript
    inlineScript: |
      # Pobierz token tylko do tego środowiska Dataverse
      export DATAVERSE_ACCESS_TOKEN=$(az account get-access-token \
        --resource "$DATAVERSE_URL" --query accessToken -o tsv)

      cd $(Pipeline.Workspace)/plugins
      pluginreg whoami
      pluginreg deploy --path . --profile dev
  env:
    DATAVERSE_URL: $(DATAVERSE_URL)
```

To jest obecnie **najbezpieczniejsze** podejście — zero długoterminowych sekretów.

### Alternatywa — ręczne zmienne (Variable Groups)

```yaml
- script: |
    cd $(Pipeline.Workspace)/plugins
    pluginreg deploy --path . --profile dev
  displayName: Register plugins
  env:
    DATAVERSE_URL: $(DATAVERSE_URL)
    DATAVERSE_CLIENT_ID: $(DATAVERSE_CLIENT_ID)
    DATAVERSE_CLIENT_SECRET: $(DATAVERSE_CLIENT_SECRET)
    DATAVERSE_TENANT_ID: $(DATAVERSE_TENANT_ID)
```

### Kroki konfiguracji (ogólne)

1. **Variable groups** per środowisko (przynajmniej `DATAVERSE_URL` + ewentualnie inne wartości).
2. **Service connections** (zalecane) typu Azure Resource Manager.
3. **Environments** w Azure DevOps z approval gates.
4. Opublikuj narzędzie do Azure Artifacts lub używaj wersji z NuGet.

### Pełny szablon

Patrz `templates/azure-pipelines-plugin-deploy.yml`.

---

## Typowy workflow zespołu

```
┌─────────────────┐     ┌──────────────┐     ┌─────────────────┐
│  Kod + atrybuty │────▶│ dotnet build │────▶│ pluginreg deploy│
│  [CrmPlugin...] │     │   Release    │     │  --profile dev  │
└─────────────────┘     └──────────────┘     └─────────────────┘
         ▲                                              │
         │                                              ▼
         │                                    Dataverse (DEV)
         │
         │     pluginreg sync (opcjonalnie — po ręcznych
         └──── zmianach w Plugin Registration Tool)
```

1. Developer pisze plugin i dodaje `[CrmPluginRegistration]` (oraz atrybuty Custom API, jeśli dotyczy)
2. Konfiguracja środowiskowa (URL API, secrets) trafia do `profiles` w `pluginregistration.json`
3. Pipeline buduje DLL i uruchamia `pluginreg deploy --profile <env>` (rejestruje stepy i Custom API)
4. Przy zmianach w PRT — `pluginreg sync` aktualizuje atrybuty w repo (w tym parametry Custom API)

---

## Rozwiązywanie problemów

| Problem | Rozwiązanie |
|---------|-------------|
| `Configuration file not found` | Dotyczy `deploy`/`init` — umieść `pluginregistration.json` w `--path`. `sync` tego pliku nie wymaga |
| `Assembly path not found` | Zbuduj projekt (`dotnet build -c Release`) przed `deploy` |
| `Unable to connect to Dataverse` | Uruchom `whoami`; sprawdź URL, Client ID/Secret, Tenant ID i Application User w środowisku |
| `Could not load file or assembly 'System.ServiceModel'` | Uruchom `dotnet build` po aktualizacji repozytorium — wymagane pakiety WCF są w `PluginRegistration.Core` |
| `Missing environment variables` | Ustaw `DATAVERSE_*` lub podaj `--connection` |
| `Environment variable 'X' is not set` | Ustaw zmienną w pipeline lub lokalnie (używana w `${X}` w `pluginregistration.json`) |
| `sync` nie modyfikuje plików | Pluginy muszą być zarejestrowane w Dataverse; nazwy typów w kodzie muszą = `plugintype.typename` |
| `sync` pomija klasę z BasePlugin | Upewnij się, że `BasePlugin.cs` i klasy pochodne są w tym samym `--path`; użyj `--class-regex` w skrajnych przypadkach |
| `Custom API 'X' not found` | Użyj pełnej definicji w kodzie (atrybuty na klasie) lub `createIfMissing: true` w `profiles.customApis` |
| Custom API odtworzone od zera | Normalne przy zmianie typu parametru lub `IsFunction` — narzędzie usuwa i tworzy API ponownie |
| `Duplicate Custom API request parameter names` | Każdy parametr wejściowy / response na klasie musi mieć unikalną nazwę |
| `Duplicate plugin step names` | Każdy step na klasie musi mieć unikalną nazwę (auto lub `Name = "..."` w atrybucie) |
| Plugin nie wykryty przy `deploy` | Assembly musi implementować `IPlugin` (także przez klasę bazową); sprawdź `assemblyPath` |

---

## Przykład w repozytorium

Pełny przykład: `samples/Sample.Plugins/`

- `AccountCreatePlugin.cs` — plugin step + Custom API z parametrami w kodzie
- `pluginregistration.json` — profile dev/test/prod

```bash
cd samples/Sample.Plugins
dotnet build -c Release
pluginreg deploy --path . --profile dev
```