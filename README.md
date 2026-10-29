<p align="center">
  <img src="src/Provodnik.Wpf/Assets/provodnik.png" width="96" alt="Provodnik">
</p>

<h1 align="center">Provodnik</h1>

<p align="center">
  A faster, sturdier installer and launcher for <b>S.T.A.L.K.E.R. G.A.M.M.A.</b>
</p>

<p align="center">
  <a href="https://github.com/ruan-luidy/Provodnik/releases/latest"><img src="https://img.shields.io/github/v/release/ruan-luidy/Provodnik?label=download&color=5cc41c" alt="Latest release"></a>
  <a href="https://github.com/ruan-luidy/Provodnik/releases"><img src="https://img.shields.io/github/downloads/ruan-luidy/Provodnik/total?color=5cc41c" alt="Downloads"></a>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-3778bf" alt="Windows 10 and 11">
  <img src="https://img.shields.io/badge/.NET-10-512bd4" alt=".NET 10">
</p>

<p align="center">
  English · <a href="docs/README.pt-BR.md">Português</a> · the app also speaks Русский
</p>

![Installing G.A.M.M.A. with Provodnik](docs/images/install.png)

Provodnik (*Проводник*, "guide" in Russian) installs and updates the exact same G.A.M.M.A. you get from the official
launcher: same mod list, same folders, same MO2 profile. It just does it faster, and it doesn't leave you guessing when
something goes wrong.

> **Not an official G.A.M.M.A. tool.** The modpack, the mod list and the original launcher belong to Grok and the GAMMA
> team. Provodnik reads and writes the same files, so you can switch back and forth between the two.

## Why

|  | Official launcher | Provodnik |
|---|---|---|
| Downloads | one at a time | in parallel, resumed where they stopped |
| ModDB | the downloader now fails with "Failed to generate download link" | goes through a real browser engine (WebView2), paced so Cloudflare stays calm |
| Updates | re-extracts every addon | only re-extracts what changed |
| Interrupted install | you find out later | run it again and it picks up where it stopped |
| Empty mod folders | silent | detected, with one click to fix them |
| Your own mods | come back disabled and mixed in with GAMMA's after every update | stay on, in your order, under a "My mods" separator |
| Errors | raw tool output in a console | the same console, with each known error explained and what to do about it |
| Before installing | — | checks permissions, disk space, antivirus, long paths, MO2 and the game being closed |
| First time | create folders, download Anomaly and RC3, extract, antivirus exclusions, "First Install Initialization" by hand | one click: Provodnik does every wiki step itself |

It also checks the result the way the community does by eye (every mod GAMMA enables in MO2 must actually have files),
and fixes a typo in GAMMA's own mod list that leaves one mod disabled for everybody.

![Status and numbers at a glance](docs/images/home.png)

## Install

No installer. One file: download **`Provodnik.exe`** from the [latest release](https://github.com/ruan-luidy/Provodnik/releases/latest).

**Already have GAMMA:** drop the exe in your GAMMA folder, next to `ModOrganizer.exe`, run it and press **Install /
update**. Right-click → *Send to → Desktop* if you want a shortcut.

**From scratch:** run the exe from wherever it is (your Downloads folder is fine). With no GAMMA on the computer, the
Home screen becomes the from-scratch install: pick a drive and press **Install everything**. It does every step of the
[wiki](https://github.com/Grokitach/Stalker_GAMMA/wiki/Installing-GAMMA):

- creates `GAMMA` and `Anomaly` side by side on the chosen drive, and rules out what the wiki forbids (different
  drives, the user folder, OneDrive, Program Files, USB drives, not enough space);
- adds both folders to the Windows Defender exclusions and installs any missing Visual C++ and DirectX, behind a single
  administrator prompt (Microsoft's own installers, checked by their signature);
- downloads GAMMA RC3 from Mediafire and Anomaly 1.5.3 from ModDB, checks the SHA-256 of each and extracts them;
- does the "First Install Initialization": MO2 comes out already pointing at Anomaly;
- copies itself into the GAMMA folder and starts the regular install.

If it stops halfway, running it again picks up where it left off. If you already downloaded `GAMMA RC3.7z` or
`Anomaly-1.5.3-Full.2.7z`, leave them in your Windows Downloads folder and it uses them instead of downloading again.
A clean Anomaly 1.5.3 you already have works too (**I already have Anomaly**). Provodnik can't configure Norton, Avast,
AVG or Bitdefender: it warns you, and you add the folders to their exclusions.

It finds everything on its own, uses the tools GAMMA already ships, and keeps itself up to date: new versions download
in the background and swap in when you close it (or right away with **Restart now**).

Windows 10/11 x64, everything bundled. A first install downloads around 45 GB and needs about 200 GB of free space
(the extracted mods alone are about 150 GB); after that, updates only fetch what changed. A full install from
scratch took about 35 minutes here.

## Mods screen

![Every addon, its archive and its folder](docs/images/mods.png)

Every addon with its archive and folder, a filter, "only empty" and one click to redo a single mod. Enabling,
disabling and ordering mods is still MO2's job: **Play** opens MO2 straight into the executable you pick
(DX11-AVX, DX11, DX9...), and into the MO2 profile you pick when you have more than one.

## FAQ

**Can I use it on a GAMMA installed with the official launcher?** Yes. The first run checks everything and only fixes
what's wrong.

**Will it touch my own MO2 profiles or extra mods?** Other profiles, never. In the `G.A.M.M.A` profile an update
follows GAMMA's new mod list but keeps what you did:

- mods you added stay together, in your order and on/off state, under a "My mods" separator at the bottom of MO2's
  list (the highest priority, so they win over GAMMA's);
- GAMMA mods you turned on or off stay that way, unless GAMMA changed that same mod;
- a GAMMA mod you replaced with your own version through MO2 keeps your version (**Reinstall this mod** on the Mods
  screen brings GAMMA's back).

A copy of the previous `modlist.txt` goes to `.Grok's Modpack Installer\.provodnik\profile-backups` (the last 10), and
the clock button next to the MO2 profile on Home puts it back. Your saves are zipped to `.provodnik\save-backups`
before every install or update (the last 5).

**GAMMA takes too much space.** Every GAMMA update leaves the old version of the archives it changed in `downloads`.
**Free up space** on the Mods screen lists the ones nothing uses anymore and deletes them. Archives MO2 downloaded for
you are never touched.

**Something failed.** Press **Retry failed** on the Install screen first; most ModDB hiccups go away. If not, the
**Log** screen has every install, and `.Grok's Modpack Installer\.provodnik\logs` has the files to attach to an
[issue](https://github.com/ruan-luidy/Provodnik/issues). Please report Provodnik problems here, not to the GAMMA
helpers: their support is for the official launcher, so check the problem there before asking on the GAMMA Discord.

## Build

```powershell
dotnet run --project src/Provodnik.Wpf -- --demo   # simulation: a fake GAMMA in %TEMP%, nothing is downloaded
dotnet test
python tools\build_tool.py --build --github        # tests, the single exe and the GitHub release
```

The code is split into `Provodnik.Core` (models and contracts, no dependencies), `Provodnik.Features` (the install
pipeline), `Provodnik.Infrastructure` (HTTP and the GAMMA tools) and `Provodnik.Wpf` (the UI). Translations live in
`Localization/*.{pt,en,ru}.json`; fixes and new languages are welcome.

## Credits

G.A.M.M.A. by Grok and the GAMMA team. Atom icon by [Icons8](https://icons8.com). Built with
[HandyControl](https://github.com/HandyOrg/HandyControl) and [Phosphor icons](https://phosphoricons.com).

## License

[MIT](LICENSE). G.A.M.M.A. itself and every addon it installs keep their own licenses.
