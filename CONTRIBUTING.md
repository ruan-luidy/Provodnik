# Contributing

Thanks for wanting to help! Bug reports, translations and code are all welcome. English or Portuguese are both fine
in issues and pull requests.

## Reporting a problem

Open an [issue](https://github.com/ruan-luidy/Provodnik/issues/new/choose) and attach the files from
`.Grok's Modpack Installer\.provodnik\logs`. If you can, check whether the official launcher has the same problem:
it tells us right away if the issue is in Provodnik or in GAMMA itself.

## Building

You need Windows 10/11 and the .NET 10 SDK.

```powershell
dotnet run --project src/Provodnik.Wpf -- --demo   # simulation: a fake GAMMA in %TEMP%, nothing is downloaded
dotnet test
```

The simulation runs next to a real install without touching it (separate settings and folders), so you can try UI
changes without downloading 45 GB.

## Code

- `Provodnik.Core`: models and contracts, no dependencies.
- `Provodnik.Features`: the install pipeline.
- `Provodnik.Infrastructure`: HTTP and the GAMMA tools (git, curl, 7-Zip, dl-cli).
- `Provodnik.Wpf`: the UI.

Follow the existing style (`.editorconfig`). Provodnik must stay 100% compatible with the official launcher: same
folders, same `meta.ini`, same MO2 profile, so people can switch back and forth.

## Translations

Every text lives in `Localization/*.{pt,en,ru}.json` next to the code that uses it. Fixes are welcome, and so are new
languages: copy the `en` files with your language code and translate the values.
