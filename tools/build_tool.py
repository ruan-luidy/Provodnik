"""
Provodnik Build Tool - o build_tool.py do Otmetka (que é o do Windfinder e do rnzNegativeObserver), pro Проводник.

    pip install -r tools/requirements.txt
    python tools/build_tool.py                      menu
    python tools/build_tool.py --help               as opções sem menu

Menu: subir a versão (patch/minor/major, ou mais uma de teste; já abrindo a seção no CHANGELOG), build completo
(testes, publish e pacote do Velopack), publicar no GitHub Releases (de onde a versão instalada se atualiza) e o
exe único pra morar na pasta do GAMMA (o que o antigo tools/publicar.ps1 fazia).

A versão mora no Directory.Build.props e a nota do release sai da seção dela no docs/CHANGELOG.md: sem a seção,
o build não sai. O token do GitHub vem de GITHUB_TOKEN ou, sem ele, do `gh auth token` (o gh já logado).
"""

import argparse
import os
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

from rich.console import Console
from rich.panel import Panel
from rich.prompt import Confirm, Prompt
from rich.table import Table

console = Console()

# Caminhos
ROOT = Path(__file__).resolve().parent.parent
# O ícone do instalador (Setup.exe), o mesmo do .exe
APP_ICON = ROOT / "src" / "Provodnik.Wpf" / "Assets" / "provodnik.ico"
PROPS_PATH = ROOT / "Directory.Build.props"
CHANGELOG_PATH = ROOT / "docs" / "CHANGELOG.md"
APP_PROJECT = ROOT / "src" / "Provodnik.Wpf"
TESTS_PROJECT = ROOT / "tests" / "Provodnik.Tests"
ARTIFACTS = ROOT / "artifacts"
PUBLISH_DIR = ARTIFACTS / "publish"
RELEASES_DIR = ARTIFACTS / "releases"
# O exe único (-p:Distribuir=true), separado do publish do Velopack
EXE_DIR = ARTIFACTS / "exe"

# Velopack: tem que bater com o AppUpdateService do app (canal fixo "win")
PACK_ID = "Provodnik"
MAIN_EXE = "Provodnik.exe"
CHANNEL = "win"
RUNTIME = "win-x64"

RELEASES_REPO = "https://github.com/ruan-luidy/Provodnik"


# ---------------------------------------------------------------------------------------------------------------
# Saída
# ---------------------------------------------------------------------------------------------------------------

def log_step(msg: str):
    console.print(f"[cyan]>[/cyan] {msg}")


def log_ok(msg: str):
    console.print(f"  [green]✓[/green] {msg}")


def log_warn(msg: str):
    console.print(f"  [yellow]![/yellow] {msg}")


def log_error(msg: str):
    console.print(f"  [red]✗[/red] {msg}")


def log_info(msg: str):
    console.print(f"  [dim]{msg}[/dim]")


def run_cmd(cmd: list[str], desc: str, env: dict | None = None) -> bool:
    """Roda um comando com spinner; na falha mostra o fim da saída (o erro do dotnet vem no stdout)."""
    with console.status(f"[cyan]{desc}[/cyan]", spinner="dots"):
        result = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            cwd=str(ROOT),
            encoding="utf-8",
            errors="replace",
            env={**os.environ, **(env or {})},
        )

    if result.returncode != 0:
        log_error(f"{desc} falhou")
        output = (result.stdout + "\n" + result.stderr).strip().splitlines()
        for line in [l for l in output if l.strip()][-8:]:
            log_info(line)
        return False

    return True


# ---------------------------------------------------------------------------------------------------------------
# Versão e CHANGELOG
# ---------------------------------------------------------------------------------------------------------------

def get_version() -> str:
    version = ET.parse(PROPS_PATH).getroot().find(".//Version")
    return version.text.strip() if version is not None and version.text else "0.0.0"


def set_version(version: str):
    # Troca só o número: reescrever o XML inteiro com o ElementTree perderia os comentários do arquivo
    text = PROPS_PATH.read_text(encoding="utf-8")
    text = re.sub(r"<Version>[^<]*</Version>", f"<Version>{version}</Version>", text, count=1)
    PROPS_PATH.write_text(text, encoding="utf-8")


def bumped(version: str, part: str) -> str:
    # "0.4.0-teste.1": teste sobe o número do teste; as outras partem do número sem o "-teste"
    base, _, pre = version.partition("-")
    if part == "teste":
        number = int(pre.split(".")[-1]) + 1 if pre.startswith("teste.") else 1
        return f"{base}-teste.{number}"
    major, minor, patch = (int(p) for p in base.split("."))
    if pre and part == "patch":
        return base
    if part == "major":
        return f"{major + 1}.0.0"
    if part == "minor":
        return f"{major}.{minor + 1}.0"
    return f"{major}.{minor}.{patch + 1}"


def changelog_section(version: str) -> re.Match | None:
    # As seções do Проводник são "## 0.3.0" (o colchete e a data do Otmetka também valem)
    text = CHANGELOG_PATH.read_text(encoding="utf-8")
    return re.search(rf"(?ms)^## \[?{re.escape(version)}\]?(?:[ \t][^\n]*)?\n(.*?)(?=^## |\Z)", text)


def changelog_notes(version: str) -> str | None:
    """O texto da seção ## versão do CHANGELOG, ou None quando ela não existe ou está vazia."""
    match = changelog_section(version)
    if not match:
        return None
    notes = match.group(1).strip()
    return notes if notes and notes != "-" else None


def add_changelog_section(version: str):
    """Abre a seção da versão nova no topo do CHANGELOG, pra escrever o que mudou."""
    text = CHANGELOG_PATH.read_text(encoding="utf-8")
    section = f"## {version}\n\n- \n\n"
    first = text.find("## ")
    text = text + "\n" + section if first < 0 else text[:first] + section + text[first:]
    CHANGELOG_PATH.write_text(text, encoding="utf-8")


def bump_version(part: str | None = None) -> bool:
    current = get_version()
    if part is None:
        console.print()
        console.print("[bold]Subir de qual jeito?[/bold]")
        console.print(f"  [cyan]1[/cyan] - patch  [dim]{current} → {bumped(current, 'patch')}  (correção)[/dim]")
        console.print(f"  [cyan]2[/cyan] - minor  [dim]{current} → {bumped(current, 'minor')}  (função nova)[/dim]")
        console.print(f"  [cyan]3[/cyan] - major  [dim]{current} → {bumped(current, 'major')}  (mudança grande)[/dim]")
        console.print(f"  [cyan]4[/cyan] - teste  [dim]{current} → {bumped(current, 'teste')}  (versão de teste)[/dim]")
        choice = Prompt.ask("Escolha", choices=["1", "2", "3", "4"], default="1")
        part = {"1": "patch", "2": "minor", "3": "major", "4": "teste"}[choice]
    version = bumped(current, part)

    set_version(version)
    log_ok(f"Directory.Build.props: {current} → {version}")
    if changelog_section(version) is None:
        add_changelog_section(version)
        log_ok(f"docs/CHANGELOG.md: seção {version} aberta")
    log_info("Escreva no CHANGELOG o que mudou antes do build.")
    return True


# ---------------------------------------------------------------------------------------------------------------
# Build
# ---------------------------------------------------------------------------------------------------------------

def release_files(version: str) -> list[Path]:
    if not RELEASES_DIR.exists():
        return []
    return sorted(f for f in RELEASES_DIR.iterdir() if f.is_file() and version in f.name)


def release_exists(version: str) -> bool:
    return (RELEASES_DIR / f"{PACK_ID}-{version}-full.nupkg").exists()


def clean_release(version: str):
    """Tira os arquivos desta versão e os índices (o vpk gera de novo); os de versões antigas ficam pro delta."""
    removed = 0
    for f in release_files(version):
        f.unlink()
        removed += 1
    for name in ["RELEASES", f"releases.{CHANNEL}.json", f"assets.{CHANNEL}.json", f"{PACK_ID}-{CHANNEL}-Setup.exe",
                 f"{PACK_ID}-{CHANNEL}-Portable.zip"]:
        path = RELEASES_DIR / name
        if path.exists():
            path.unlink()
            removed += 1
    if removed:
        log_ok(f"Limpou {removed} arquivos")


def build_release(assume_yes: bool = False) -> bool:
    version = get_version()
    console.print()
    console.print(Panel(f"[bold]Provodnik[/bold] v{version}", title="Build", border_style="cyan"))
    console.print()

    notes = changelog_notes(version)
    if notes is None:
        log_error(f"O docs/CHANGELOG.md não tem a seção {version} (ou ela está vazia).")
        log_info("Use a opção 'Subir a versão' ou escreva a seção na mão.")
        return False

    if release_exists(version):
        log_warn(f"O release v{version} já existe")
        if not assume_yes and not Confirm.ask("Apagar e fazer de novo?", default=True):
            log_info("Cancelado")
            return False
        clean_release(version)
        console.print()

    log_step("Testando...")
    if not run_cmd(["dotnet", "test", str(TESTS_PROJECT), "-c", "Release"], "dotnet test"):
        return False
    log_ok("Testes passaram")

    log_step("Publicando...")
    if PUBLISH_DIR.exists():
        shutil.rmtree(PUBLISH_DIR)
    if not run_cmd(["dotnet", "publish", str(APP_PROJECT), "-c", "Release", "-r", RUNTIME, "--self-contained",
                    "-o", str(PUBLISH_DIR), "--verbosity", "quiet"], "dotnet publish"):
        return False
    log_ok("Publish pronto")

    log_step("Empacotando...")
    RELEASES_DIR.mkdir(parents=True, exist_ok=True)
    notes_file = ARTIFACTS / f"notes-{version}.md"
    notes_file.write_text(notes, encoding="utf-8")
    # vpk local do repositório (.config/dotnet-tools.json), na mesma versão do pacote Velopack do app
    if not run_cmd(["dotnet", "tool", "restore"], "dotnet tool restore"):
        return False
    if not run_cmd(["dotnet", "vpk", "pack", "--packId", PACK_ID, "--packVersion", version, "--packDir", str(PUBLISH_DIR),
                    "--mainExe", MAIN_EXE, "--packTitle", PACK_ID, "--runtime", RUNTIME, "--channel", CHANNEL,
                    "--packAuthors", "Ruan Melo", "--releaseNotes", str(notes_file), "--outputDir", str(RELEASES_DIR),
                    "--icon", str(APP_ICON)],
                   "vpk pack"):
        return False
    log_ok("Velopack pronto")

    console.print()
    table = Table(title="Arquivos", border_style="green")
    table.add_column("Arquivo", style="cyan")
    table.add_column("Tamanho", justify="right")
    for f in release_files(version) + [RELEASES_DIR / f"{PACK_ID}-{CHANNEL}-Setup.exe",
                                       RELEASES_DIR / f"{PACK_ID}-{CHANNEL}-Portable.zip"]:
        if f.exists():
            table.add_row(f.name, f"{f.stat().st_size / (1024 * 1024):.1f} MB")
    console.print(table)
    return True


# ---------------------------------------------------------------------------------------------------------------
# Exe único (a pasta do GAMMA)
# ---------------------------------------------------------------------------------------------------------------

def publish_exe(gamma: Path | None = None) -> bool:
    """Um exe só, com o .NET dentro, e se pedir a cópia pra pasta do GAMMA (ao lado do ModOrganizer.exe)."""
    if gamma is not None and not (gamma / "ModOrganizer.exe").exists():
        log_error(f"{gamma} não tem o ModOrganizer.exe: não parece a pasta do GAMMA")
        return False

    log_step("Publicando o exe único...")
    if EXE_DIR.exists():
        shutil.rmtree(EXE_DIR)
    if not run_cmd(["dotnet", "publish", str(APP_PROJECT), "-c", "Release", "-p:Distribuir=true", "-o", str(EXE_DIR),
                    "--verbosity", "quiet"], "dotnet publish (exe único)"):
        return False
    exe = EXE_DIR / MAIN_EXE
    log_ok(f"{exe.relative_to(ROOT)} ({exe.stat().st_size / (1024 * 1024):.1f} MB)")

    if gamma is None:
        return True

    target = gamma / MAIN_EXE
    try:
        shutil.copy2(exe, target)
    except PermissionError:
        # O Windows não deixa sobrescrever o exe que está rodando
        log_error(f"Feche o Provodnik antes de trocar o {target}")
        return False
    log_ok(f"Copiado pra {target}")
    log_info("Esse exe não se atualiza sozinho: é só rodar de novo com a pasta do GAMMA.")
    return True


# ---------------------------------------------------------------------------------------------------------------
# Publicar
# ---------------------------------------------------------------------------------------------------------------

def github_token() -> str | None:
    """GITHUB_TOKEN, ou o token do gh já logado (gh auth login)."""
    if os.environ.get("GITHUB_TOKEN"):
        return os.environ["GITHUB_TOKEN"]
    try:
        result = subprocess.run(["gh", "auth", "token"], capture_output=True, text=True)
        return result.stdout.strip() or None if result.returncode == 0 else None
    except FileNotFoundError:
        return None


def upload_release() -> bool:
    version = get_version()
    log_step("Subindo pro GitHub...")
    log_info(f"Repo: {RELEASES_REPO}")

    if not release_exists(version):
        log_error(f"Nenhum release da v{version}: faça o build antes")
        return False

    token = github_token()
    if not token:
        log_error("Sem token: defina GITHUB_TOKEN ou faça gh auth login")
        return False

    # vpk lê o token do VPK_TOKEN; o pacote anterior do GitHub entra como base do delta (na primeira vez não tem)
    env = {"VPK_TOKEN": token}
    if not run_cmd(["dotnet", "tool", "restore"], "dotnet tool restore"):
        return False
    run_cmd(["dotnet", "vpk", "download", "github", "--repoUrl", RELEASES_REPO, "--outputDir", str(RELEASES_DIR)],
            "vpk download (release anterior)", env)
    # Versão de teste ("0.4.0-teste.2") sobe como pré-release
    pre = ["--pre"] if "-" in version else []
    if not run_cmd(["dotnet", "vpk", "upload", "github", "--repoUrl", RELEASES_REPO, "--tag", f"v{version}",
                    "--releaseName", f"Provodnik {version}", "--outputDir", str(RELEASES_DIR), "--publish"] + pre,
                   "vpk upload", env):
        return False

    log_ok(f"Publicado: {RELEASES_REPO}/releases/tag/v{version}")
    log_info(f"Pra instalar: {RELEASES_REPO}/releases/latest ({PACK_ID}-{CHANNEL}-Setup.exe)")
    return True


# ---------------------------------------------------------------------------------------------------------------
# Menu
# ---------------------------------------------------------------------------------------------------------------

def show_menu() -> str:
    version = get_version()
    built = release_exists(version)

    console.print()
    console.print(f"[bold cyan]Provodnik[/bold cyan] [dim]v{version}[/dim]")
    console.print()
    if built:
        console.print(f"  [green]✓[/green] Release v{version} em artifacts/releases")
    else:
        console.print(f"  [dim]○[/dim] Nenhum release da v{version}")
    if changelog_notes(version) is None:
        console.print(f"  [yellow]![/yellow] docs/CHANGELOG.md sem a seção {version}")
    console.print()

    console.print("[bold]Opções:[/bold]")
    console.print("  [cyan]1[/cyan] - Subir a versão (patch/minor/major)")
    console.print(f"  [cyan]2[/cyan] - {'Refazer o build (apaga o release atual)' if built else 'Build completo (testes, publish, pacote)'}")
    console.print("  [cyan]3[/cyan] - Publicar no GitHub" if built else "  [dim]3 - Publicar no GitHub (faça o build antes)[/dim]")
    console.print("  [cyan]4[/cyan] - Exe único (e copiar pra pasta do GAMMA)")
    console.print(f"  [cyan]5[/cyan] - Limpar o release da v{version}")
    console.print("  [cyan]q[/cyan] - Sair")
    console.print()
    return Prompt.ask("Escolha", choices=["1", "2", "3", "4", "5", "q"], default="2")


def menu():
    console.clear()
    choice = show_menu()

    if choice == "q":
        console.print("[dim]Até mais![/dim]")
        return

    if choice == "1":
        bump_version()
        console.print()
        return

    if choice == "5":
        console.print()
        log_step(f"Limpando o release da v{get_version()}...")
        clean_release(get_version())
        console.print()
        return

    if choice == "3":
        console.print()
        if upload_release():
            console.print("[green]Pronto![/green]")
        console.print()
        return

    if choice == "4":
        console.print()
        folder = Prompt.ask("Pasta do GAMMA [dim](Enter: só gerar o exe)[/dim]", default="", show_default=False).strip()
        console.print()
        if publish_exe(Path(folder) if folder else None):
            console.print("[green]Pronto![/green]")
        console.print()
        return

    if not build_release():
        console.print()
        console.print("[red]O build falhou![/red]")
        sys.exit(1)

    console.print()
    console.print("[green]Build pronto![/green]")
    console.print()
    if Confirm.ask("Publicar no GitHub agora?", default=False):
        console.print()
        upload_release()
    else:
        console.print("[dim]Ficou só local, em artifacts/releases.[/dim]")
    console.print()


# ---------------------------------------------------------------------------------------------------------------
# Linha de comando
# ---------------------------------------------------------------------------------------------------------------

def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        prog="build_tool.py",
        description="Build e release do Provodnik. Sem opções abre o menu.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="exemplos:\n"
               "  python tools/build_tool.py --build               testes, publish e pacote em artifacts/releases\n"
               "  python tools/build_tool.py --build --github      e sobe pro GitHub Releases\n"
               "  python tools/build_tool.py --bump patch          0.3.0 -> 0.3.1 (abre a seção no CHANGELOG)\n"
               "  python tools/build_tool.py --gamma D:\\GAMMA       exe único copiado pra D:\\GAMMA\\Provodnik.exe",
    )
    parser.add_argument("--bump", choices=["patch", "minor", "major", "teste"],
                        help="sobe a versão no Directory.Build.props e abre a seção no CHANGELOG")
    parser.add_argument("--build", action="store_true",
                        help="testes, publish self-contained e pacote do Velopack (vpk pack) em artifacts/releases")
    parser.add_argument("--github", action="store_true",
                        help="sobe o pacote da versão atual pro GitHub Releases (vpk upload github)")
    parser.add_argument("--exe", action="store_true",
                        help="só o exe único (um arquivo, .NET dentro) em artifacts/exe")
    parser.add_argument("--gamma", metavar="PASTA", type=Path,
                        help="o exe único copiado pra pasta do GAMMA (ao lado do ModOrganizer.exe)")
    parser.add_argument("-y", "--yes", action="store_true",
                        help="sem perguntas: refaz o release da versão se ele já existir")
    return parser.parse_args()


def main():
    args = parse_args()
    if not (args.bump or args.build or args.github or args.exe or args.gamma):
        menu()
        return

    if args.bump:
        bump_version(args.bump)
        # Com a seção recém-aberta e vazia, o build não sai: escreve o CHANGELOG e roda de novo
        if (args.build or args.github) and changelog_notes(get_version()) is None:
            log_warn("Escreva a seção nova do CHANGELOG e rode o build de novo.")
            sys.exit(1)

    ok = True
    if args.build:
        ok = build_release(assume_yes=args.yes)
    if ok and args.github:
        console.print()
        ok = upload_release()
    if ok and (args.exe or args.gamma):
        console.print()
        ok = publish_exe(args.gamma)

    console.print()
    if not ok:
        console.print("[red]Falhou![/red]")
        sys.exit(1)
    console.print("[green]Pronto![/green]")


if __name__ == "__main__":
    main()
