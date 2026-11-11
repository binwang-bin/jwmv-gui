# jwmv — Java Version Manager for Windows

[中文文档](./README.zh-CN.md)

A fast, lightweight tool to install, manage, and switch between Java/JDK and JVM toolchain versions on Windows. Built with .NET 8, inspired by [SDKMAN!](https://sdkman.io/) and [nvm](https://github.com/nvm-sh/nvm).

This repository is a **secondary development** based on the open-source project [stescobedo92/jwmv](https://github.com/stescobedo92/jwmv). It keeps the original CLI capabilities and adds a Windows desktop GUI, a unified `jwmv.exe` host, Chinese/English UI, local JDK import, and an Inno Setup installer.

---

## What's new in this fork

| Area | Description |
|------|-------------|
| **Desktop GUI** | WPF app for Overview, Catalog, Installed, Doctor, and Settings |
| **Unified host** | One `jwmv.exe` for both GUI (double-click) and CLI (arguments) |
| **Installer** | `jwmv-setup-*-win-x64.exe` via Inno Setup (`pack.ps1`) |
| **Localization** | Chinese / English in the GUI (language in Settings) |
| **Local import** | Register an existing JDK/SDK without copying; adopt existing `JAVA_HOME` / `*_HOME` as default when paths match |
| **Safe uninstall** | App uninstall removes only program files / optional PATH entry; leaves `JAVA_HOME` / SDK homes / `~/.jwmv` data alone |

Upstream CLI features (catalog install, checksums, `.jwmvrc`, PowerShell integration, `doctor`, etc.) remain available.

---

## Features

- **Install JDKs and JVM SDKs** — Java via [Foojay Disco API](https://api.foojay.io/), plus Gradle, Maven, and Kotlin from official release feeds
- **GUI + CLI** — manage versions visually or from the terminal
- **Switch versions instantly** — per-session, per-project (`.jwmvrc`), or persistent defaults per candidate
- **Import local installations** — point at an existing JDK tree; no re-download required
- **Verified downloads** — archives must match the catalog checksum before extraction (`--allow-unverified` to opt out)
- **Shell integration** — PowerShell profile bootstrap for automatic version switching
- **Diagnostics** — `doctor` (CLI and GUI) detects PATH conflicts and misconfigurations
- **Single-file executable** — no separate .NET runtime install for the published payload
- **Windows x64** installer packaging (ARM64 CLI build patterns from upstream still apply if you publish that RID yourself)

---

## Installation

### Setup.exe (recommended for this fork)

1. Build or obtain `artifacts\jwmv-setup-0.1.0-win-x64.exe`
2. Run the installer (default location: `%LOCALAPPDATA%\Programs\jwmv`)
3. Optionally add jwmv to the user `PATH` and create a desktop shortcut
4. Launch **jwmv** from the Start menu, or run `jwmv` in a new terminal

```powershell
# From a clone of this repo (requires .NET 8 SDK + Inno Setup 6)
.\pack.ps1
# Output: artifacts\jwmv-setup-0.1.0-win-x64.exe
```

### Portable payload

After `.\pack.ps1` (or publish only), you can also run:

```text
artifacts\payload\win-x64\jwmv.exe
```

### Upstream package channels

The original project also publishes via winget / npm / .NET tool / GitHub Releases. Those packages track **upstream** builds and may not include this fork's GUI. Prefer the setup.exe from this repository when you need the desktop UI.

---

## Quick Start

### GUI

1. Open **jwmv**
2. **Browse & Install** — search the catalog and install a JDK / Gradle / Maven / Kotlin
3. **Installed** — import a local JDK, set default, uninstall, or copy home path
4. **Doctor** — check `JAVA_HOME`, PATH, and shell integration
5. **Settings** — switch UI language (中文 / English)

### CLI

```powershell
# See what's available
jwmv candidates
jwmv list

# Install Java 21 (Temurin) and set it as default
jwmv install 21-tem --default

# Import an existing JDK (registers path; adopts JAVA_HOME when it already matches)
jwmv import "C:\Program Files\Java\jdk-21" --candidate java

# Verify
jwmv current
java -version

# Install build tooling
jwmv install gradle 9.5.1
jwmv install maven 3.9.15
jwmv install kotlin 2.3.21

# Switch for this session
jwmv use 17-zulu
jwmv use gradle 9.5.1
```

---

## GUI overview

| Page | Purpose |
|------|---------|
| **Overview** | Current SDKs and quick status |
| **Browse & Install** | Catalog search / install / set default after install |
| **Installed** | Local list, import folder, set default, uninstall, export `use` script |
| **Doctor** | Environment diagnostics |
| **Settings** | Language and related options |

Busy operations disable conflicting actions; empty lists show clear empty states. Importing a home that already matches `JAVA_HOME` (or the candidate `*_HOME`) is treated as the configured default so the list badge appears without a manual re-set.

---

## Commands

### `jwmv candidates [filter]`

List supported SDK installation candidates.

```powershell
jwmv candidates
jwmv candidates java
```

| Candidate | Source |
|-----------|--------|
| `java`   | Foojay Disco API |
| `gradle` | Gradle services version feed |
| `maven`  | Maven Central metadata |
| `kotlin` | JetBrains Kotlin GitHub Releases |

---

### `jwmv list [candidate] [filter]`

List available SDK versions. If the first argument is not a known candidate, it is treated as a legacy Java filter.

```powershell
jwmv list java
jwmv list 21
jwmv list gradle
jwmv list maven 3.9
jwmv list kotlin --refresh
```

**Aliases:** `ls`

| Option          | Description                        |
|-----------------|------------------------------------|
| `[candidate]`   | Optional candidate (`java`, `gradle`, `maven`, `kotlin`) |
| `[filter]`      | Optional version/distribution filter |
| `-r, --refresh` | Force refresh the catalog cache    |

---

### `jwmv install [candidate] [version]`

Install an SDK version. Java keeps the previous shorthand syntax for compatibility.

```powershell
jwmv install
jwmv install 21-tem
jwmv install java 21-tem
jwmv install gradle 9.5.1
jwmv install 21.0.4-tem --default
jwmv install 17-zulu --refresh
jwmv install kotlin 1.9.0 --allow-unverified
```

| Option               | Description                                |
|----------------------|--------------------------------------------|
| `[candidate]`        | Candidate name, optional for Java shorthand |
| `[version]`          | Candidate version or Java identifier |
| `-d, --default`      | Set as default SDK after install     |
| `-r, --refresh`      | Force refresh the catalog before install   |
| `--allow-unverified` | Install even when the catalog publishes no checksum |

**Identifier format:** `<version>-<distribution>`

| Alias     | Distribution        |
|-----------|---------------------|
| `tem`     | Eclipse Temurin     |
| `zulu`    | Azul Zulu           |
| `ms`      | Microsoft OpenJDK   |
| `graalvm` | GraalVM Community   |
| `cor`     | Amazon Corretto     |
| `lib`     | Liberica            |
| `sap`     | SAP Machine         |
| `ojdk`    | Oracle OpenJDK      |
| `oracle`  | Oracle JDK          |

---

### `jwmv import <path>`

Register an existing local SDK home without copying files.

```powershell
jwmv import "D:\jdks\jdk-21" --candidate java
jwmv import "D:\jdks" --candidate java --all
jwmv import "D:\jdks\jdk-21" --default
jwmv import "D:\jre-17" --allow-jre
```

| Option            | Description |
|-------------------|-------------|
| `<path>`          | JDK/SDK home or parent directory |
| `--candidate`     | Candidate name (default: `java`) |
| `--alias`         | Custom alias |
| `-d, --default`   | Set as jwmv default after import |
| `--all`           | Import every home found under the path |
| `--allow-jre`     | Allow Java runtime without `javac` |

If `JAVA_HOME` / `*_HOME` already points at the imported home, jwmv adopts it as the configured default even without `-d`.

---

### `jwmv uninstall [candidate] [version]`

Remove an installed SDK version (external imports remove the manifest only; the original directory stays).

```powershell
jwmv uninstall
jwmv uninstall 17-zulu
jwmv uninstall gradle 9.5.1
```

**Aliases:** `remove`, `delete`, `rm`

---

### `jwmv installed [candidate]`

List locally installed SDK versions.

```powershell
jwmv installed
jwmv installed java
```

**Aliases:** `local`

---

### `jwmv use [candidate] <version>`

Activate an SDK for the current shell session. Does **not** modify the persistent default.

```powershell
jwmv use 17-zulu
jwmv use gradle 9.5.1
jwmv use 21-tem --shell powershell
```

| Option            | Description |
|-------------------|-------------|
| `[candidate]`     | Optional for Java shorthand |
| `<version>`       | Version to activate |
| `--shell <SHELL>` | Target shell (default: powershell) |

---

### `jwmv default [candidate] <version>`

Set the persistent default for new shells (updates user `JAVA_HOME` / `*_HOME` and `PATH`).

```powershell
jwmv default 21-tem
jwmv default maven 3.9.15
```

---

### `jwmv current [candidate]`

Show active SDK versions and resolution source (`Default` / `Session` / `Project` / `Environment`).

```powershell
jwmv current
jwmv current java
```

---

### `jwmv home [candidate] [version]`

Print the SDK home path (useful for scripting).

```powershell
jwmv home
jwmv home 17-zulu
jwmv home gradle 9.5.1
$env:JAVA_HOME = $(jwmv home 21-tem)
```

---

### `jwmv upgrade [identifier]`

Upgrade installed JDK(s) to the latest patch in the same major/vendor track.

```powershell
jwmv upgrade 21-tem
jwmv upgrade
```

---

### `jwmv update`

Refresh the local catalog cache from all SDK providers.

```powershell
jwmv update
```

---

### `jwmv doctor`

Run diagnostics (`JAVA_HOME`, PATH conflicts, PowerShell integration, `where.exe` resolution).

```powershell
jwmv doctor
```

---

### `jwmv config`

Display the current jwmv configuration.

```powershell
jwmv config
```

---

### `jwmv integrate`

Install the PowerShell profile hook for automatic version switching.

```powershell
jwmv integrate
jwmv integrate --shell powershell
jwmv integrate --profile "C:\Users\me\Documents\PowerShell\Microsoft.PowerShell_profile.ps1"
```

| Option             | Description |
|--------------------|-------------|
| `--shell <SHELL>`  | Target shell (default: powershell) |
| `--profile <PATH>` | Custom profile file path |

---

### `jwmv env`

Print environment activation scripts or manage project configuration.

```powershell
jwmv env
jwmv env --init
jwmv env init
jwmv env install
jwmv env --cwd ./my-project
```

| Option            | Description |
|-------------------|-------------|
| `--shell <SHELL>` | Target shell |
| `--cwd <PATH>`    | Working directory for `.jwmvrc` |
| `--init`          | Emit shell initialization script |
| `init`            | Create `.jwmvrc` from active SDKs |
| `install`         | Install missing SDKs from `.jwmvrc` |

---

### `jwmv flush`

Clear cached files selectively.

```powershell
jwmv flush --archives
jwmv flush --temp
jwmv flush --catalog
jwmv flush --archives --temp --catalog
```

---

### `jwmv selfupdate`

Update jwmv itself from GitHub Releases (upstream-oriented; signed assets). Prefer reinstalling this fork's setup.exe when you are on the GUI build.

```powershell
jwmv selfupdate --check
jwmv selfupdate
```

**Aliases:** `self-update`

---

## Shell Integration

```powershell
jwmv integrate
```

Adds a managed block to your PowerShell profile (`$PROFILE`) that:

1. Reads the current or project-specific SDK versions  
2. Sets `JAVA_HOME`, `GRADLE_HOME`, `MAVEN_HOME`, `KOTLIN_HOME`, and `PATH`  
3. Switches versions as you navigate between projects  

### Manual integration

```powershell
# >>> jwmv initialize >>>
$jwmvInit = & jwmv env --init --shell powershell 2>$null
if ($jwmvInit) { $jwmvInit | Invoke-Expression }
# <<< jwmv initialize <<<
```

---

## Per-Project SDK Versions

Create a `.jwmvrc` in the project root:

```
java=21-tem
gradle=9.5.1
maven=3.9.15
kotlin=2.3.21
```

Legacy Java-only files containing just `21-tem` are accepted as `java=21-tem`.

Resolution order per candidate:

1. **Session** — `jwmv use`  
2. **Project** — `.jwmvrc` (walks up the tree)  
3. **Default** — `jwmv default`  
4. **Environment** — existing `JAVA_HOME` / `*_HOME` when no jwmv default is set  

---

## Configuration

Data root: `~/.jwmv/`

```
~/.jwmv/
├── config.json
├── candidates/
│   ├── java/
│   ├── gradle/
│   ├── maven/
│   └── kotlin/
├── archives/
├── tmp/
└── var/
    ├── catalog-v2.json
    └── manifests/
```

### `config.json` (example)

```json
{
  "preferredDistributionAlias": "tem",
  "catalogRefreshHours": 6,
  "autoEnvEnabled": true,
  "defaultShell": "powershell",
  "defaultJavaAlias": "21-tem",
  "defaultVersions": {
    "java": "21-tem",
    "gradle": "9.5.1"
  },
  "selfUpdateRepository": "stescobedo92/jwmv"
}
```

| Key | Default | Description |
|-----|---------|-------------|
| `preferredDistributionAlias` | `"tem"` | Default distribution when not specified |
| `catalogRefreshHours` | `6` | Hours before catalog auto-refresh |
| `autoEnvEnabled` | `true` | Enable `.jwmvrc` auto-switching |
| `defaultShell` | `"powershell"` | Shell for script generation |
| `defaultJavaAlias` | — | Legacy persistent default Java |
| `defaultVersions` | `{}` | Persistent defaults by candidate |
| `selfUpdateRepository` | — | GitHub `owner/repo` for self-update |

---

## Build from source

Requirements:

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) (for the installer)

```powershell
# Run tests
dotnet test

# Publish + build setup.exe
.\pack.ps1
.\pack.ps1 -Version 0.1.0 -Configuration Release
```

Artifacts:

| Path | Description |
|------|-------------|
| `artifacts\payload\win-x64\jwmv.exe` | Unified GUI + CLI host |
| `artifacts\jwmv-setup-0.1.0-win-x64.exe` | Windows installer |

---

## Usage example

```powershell
cd ~\examples\legacy-app
@"
java=17-cor
maven=3.9.15
"@ > .jwmvrc

cd ~\examples\modern-api
@"
java=21-tem
gradle=9.5.1
"@ > .jwmvrc

jwmv install 17-cor
jwmv install 21-tem --default
jwmv install maven 3.9.15
jwmv install gradle 9.5.1

# With shell integration, SDKs switch automatically
cd ~\examples\legacy-app
java -version   # → Corretto 17

cd ~\examples\modern-api
java -version   # → Temurin 21
```

---

## Security

- **Verified downloads** — checksum verification is fail-closed unless `--allow-unverified` is used.
- **Environment changes** — `jwmv default` edits the user `Path` while preserving `%VAR%` entries, and only removes `JAVA_HOME` / `*_HOME` values that jwmv itself set.
- **App uninstall** — Inno uninstaller does **not** clear SDK home variables or `~/.jwmv` data.
- **Shell integration** — the profile hook only applies provider-defined variable names and escaped values.
- Upstream also documents Sigstore-signed release assets and CodeQL; see the [original project](https://github.com/stescobedo92/jwmv) and [SECURITY.md](./SECURITY.md) when applicable.

---

## Architecture

```
Jwmv.Gui             → WPF desktop UI (views / view-models / localization)
Jwmv.Cli             → Spectre.Console CLI commands and host entry
Jwmv.Core            → Interfaces, models, utilities (no dependencies)
Jwmv.Infrastructure  → Catalog providers, storage, Windows environment integration
Jwmv.Tests           → xUnit tests
installer/           → Inno Setup script
pack.ps1             → Publish + compile setup.exe
```

Clean architecture with dependency inversion; services are interface-based and injected via `Microsoft.Extensions.DependencyInjection`.

---

## Upstream & attribution

- Upstream project: [stescobedo92/jwmv](https://github.com/stescobedo92/jwmv)
- This fork extends that work with GUI packaging and Windows desktop workflows while preserving CLI compatibility where practical.

---

## License

MIT License — see [LICENSE](./LICENSE). Upstream copyright notices apply to original portions of the Software.
