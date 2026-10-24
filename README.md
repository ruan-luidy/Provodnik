# Provodnik

A community launcher and installer for **S.T.A.L.K.E.R. G.A.M.M.A.**, a drop-in alternative to `G.A.M.M.A. Launcher.exe`.
It installs and updates the exact same mod list, into the same folders, with the same MO2 profile, but without the
usual pain: empty mod folders, downloads that never come back, everything running one after another, and every addon
re-extracted on each update.

> *Проводник* (pra-vad-NEEK) means "guide" in Russian.

**Not an official G.A.M.M.A. tool.** The modpack, the mod list and the original launcher belong to Grok and the GAMMA
team. Provodnik reads the same files and produces the same install; you can keep using both.

Available in English, Português and Русский. [Leia em português](docs/README.pt-BR.md).

## Why

- **Parallel downloads** that resume where they stopped, retry on their own and check the md5 before extracting.
  ModDB is paced so Cloudflare doesn't start blocking you, and it goes through a real browser engine (WebView2), which
  ModDB now requires.
- **Only re-extracts what changed.** A mod folder is never left half-done; a broken archive keeps the previous version.
- **Stop and continue.** Closed it halfway, lost internet, a new GAMMA update came out: run it again and it picks up
  where it left off.
- **Checks the result.** Every mod enabled in GAMMA's `modlist.txt` (the "5xx active mods" rule) must have a folder
  with content. Anything missing becomes a failure with a **Retry** button. It also fixes a typo in GAMMA's own mod list
  that leaves one mod disabled in MO2.
- **Readable tool console.** The git, curl, 7-Zip and ModDB output, like the original's command window, with every
  known error explained right below it (disk full, antivirus, Cloudflare, corrupted archive, path too long...).
- **Pre-install checks** for folder permissions, Anomaly, MO2/game closed, disk space, antivirus and long paths, with
  the fix one click away. Admin rights only when the folder really needs them; MO2 and the game always start without.
- **Mods screen** with the archive and folder of every addon (and one click to redo a single one), and a **Log** of
  this session and of every past install.
- **Play** straight into the executable you pick (DX11-AVX, DX11, DX9...) through MO2.

## What about Mod Organizer?

It's still MO2. The game only sees mods through MO2's virtual folder, and enabling, disabling, ordering, searching and
checking conflicts is MO2's job. Provodnik's **Play** opens MO2 straight into your chosen executable, and its Mods
screen only tells you whether each addon was downloaded and extracted.

## Install

**Option 1, installer (updates itself):** download `Provodnik-win-Setup.exe` from the
[latest release](https://github.com/ruan-luidy/Provodnik/releases/latest). New versions download in the background and
apply when you close the app (or right away with **Restart now**).

**Option 2, inside the GAMMA folder:** download `Provodnik.exe` from the release and put it next to `ModOrganizer.exe`
(for example `C:\GAMMA\Provodnik.exe`). It finds everything on its own and uses the tools GAMMA already ships in
`.Grok's Modpack Installer\resources` (7-Zip, curl-impersonate, Git). This copy doesn't update itself: **About →
Check** tells you when a new version is out.

Requirements: Windows 10/11 x64. .NET is bundled, nothing else to install. WebView2 ships with Windows 11 and recent
Windows 10.

Then open it, check the **Install** screen and press **Install / update**. A first install takes a while (around 150 GB
of downloads); after that, updates only fetch what changed.

## FAQ

**Can I use it on an install made with the official launcher?** Yes. It reads the same folders and state; the first
run checks everything and only fixes what's wrong.

**Does it change my MO2 profile?** Like the official launcher, an install rewrites the `G.A.M.M.A` profile's mod list
from GAMMA's definition. Your own profiles (for example a copy named "GAMMA Custom") are never touched.

**Where are the logs?** `.Grok's Modpack Installer\.provodnik\logs` (one file per install, plus the tool console), also
browsable in the **Log** screen.

**Something failed.** Press **Retry** on the Install screen first; most ModDB hiccups go away. If not, open the log and
bring it to the GAMMA Discord, or open an issue here.

## Build from source

```powershell
dotnet run --project src/Provodnik.Wpf
dotnet run --project src/Provodnik.Wpf -- --demo        # simulation: fake GAMMA in %TEMP%, nothing is downloaded
dotnet run --project src/Provodnik.Wpf -- --demo-novo   # simulation from scratch
dotnet test
```

Releases are built with `tools/build_tool.py` (Velopack):

```powershell
pip install -r tools\requirements.txt
python tools\build_tool.py --bump patch        # bump the version and open a CHANGELOG section
python tools\build_tool.py --build --github    # tests, publish, Velopack package and GitHub release
python tools\build_tool.py --gamma C:\GAMMA    # the single exe straight into a GAMMA folder
```

| Project | What it is |
|---|---|
| `Provodnik.Core` | Models and contracts (mods.txt, meta.ini, ModOrganizer.ini, modlist.txt, install state, texts). No dependencies. |
| `Provodnik.Features` | The install pipeline, downloads, verification, MO2 profile, patches and Play. |
| `Provodnik.Infrastructure` | HttpClient and the GAMMA tools run as processes. |
| `Provodnik.Wpf` | The UI (HandyControl + CommunityToolkit.Mvvm), sliced by `Features/`. |

Translations live in `Localization/*.{pt,en,ru}.json` inside each project. Pull requests with fixes or new languages
are welcome.

Atom icon by [Icons8](https://icons8.com).
