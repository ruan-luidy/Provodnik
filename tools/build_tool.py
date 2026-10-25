"""
Provodnik Build Tool - o build_tool.py do Otmetka (que é o do Windfinder e do rnzNegativeObserver), pro Проводник.

    pip install -r tools/requirements.txt
    python tools/build_tool.py                      menu
    python tools/build_tool.py --help               as opções sem menu

O Provodnik é um exe só, que mora na pasta do GAMMA e se atualiza sozinho pelos GitHub Releases (baixa o
Provodnik.exe do último release e troca o próprio arquivo). Menu: subir a versão (patch/minor/major, ou mais uma de
teste; já abrindo a seção no CHANGELOG), build (testes e o exe único), publicar o release no GitHub e copiar o exe
pra uma pasta do GAMMA.

A versão mora no Directory.Build.props e a nota do release sai da seção dela no docs/CHANGELOG.md: sem a seção,
o build não sai. O token do GitHub vem de GITHUB_TOKEN ou, sem ele, do `gh auth token` (o gh já logado).

A nota ganha o SHA-256 do exe e, com a VIRUSTOTAL_API_KEY definida (a chave grátis da conta no virustotal.com),
o link da análise do VirusTotal: o exe vai pra lá antes do release e o resultado aparece no fim.
"""

import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET
from pathlib import Path

from rich.console import Console
from rich.panel import Panel
from rich.prompt import Confirm, Prompt
from rich.table import Table

# o console do Windows em cp1252 quebrava no "✓"
for stream in (sys.stdout, sys.stderr):
    if hasattr(stream, "reconfigure"):
        stream.reconfigure(encoding="utf-8")

console = Console()

# Caminhos
ROOT = Path(__file__).resolve().parent.parent
PROPS_PATH = ROOT / "Directory.Build.props"
CHANGELOG_PATH = ROOT / "docs" / "CHANGELOG.md"
APP_PROJECT = ROOT / "src" / "Provodnik.Wpf"
TESTS_PROJECT = ROOT / "tests" / "Provodnik.Tests"
ARTIFACTS = ROOT / "artifacts"
# O exe único (-p:Distribuir=true) e a versão dele, pro release
EXE_DIR = ARTIFACTS / "exe"
EXE_VERSION = EXE_DIR / "version.txt"

# O nome do arquivo no release tem que bater com o AppUpdateService do app (ele procura "Provodnik.exe")
MAIN_EXE = "Provodnik.exe"
RELEASES_REPO = "https://github.com/ruan-luidy/Provodnik"
VIRUSTOTAL_API = "https://www.virustotal.com/api/v3"


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

def release_exists(version: str) -> bool:
    """O exe único desta versão já está em artifacts/exe."""
    return (EXE_DIR / MAIN_EXE).exists() and EXE_VERSION.exists() and EXE_VERSION.read_text(encoding="utf-8").strip() == version


def clean_release(version: str):
    if EXE_DIR.exists():
        shutil.rmtree(EXE_DIR)
        log_ok(f"Limpou o exe da v{version}")


def build_release(assume_yes: bool = False) -> bool:
    version = get_version()
    console.print()
    console.print(Panel(f"[bold]Provodnik[/bold] v{version}", title="Build", border_style="cyan"))
    console.print()

    if changelog_notes(version) is None:
        log_error(f"O docs/CHANGELOG.md não tem a seção {version} (ou ela está vazia).")
        log_info("Use a opção 'Subir a versão' ou escreva a seção na mão.")
        return False

    if release_exists(version):
        log_warn(f"O exe da v{version} já existe")
        if not assume_yes and not Confirm.ask("Apagar e fazer de novo?", default=True):
            log_info("Cancelado")
            return False
        console.print()

    log_step("Testando...")
    if not run_cmd(["dotnet", "test", str(TESTS_PROJECT), "-c", "Release"], "dotnet test"):
        return False
    log_ok("Testes passaram")

    if not publish_exe():
        return False
    EXE_VERSION.write_text(version, encoding="utf-8")
    return True


# ---------------------------------------------------------------------------------------------------------------
# Exe único (o que vai pro release e pra pasta do GAMMA)
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
    return True


# ---------------------------------------------------------------------------------------------------------------
# SHA-256 e VirusTotal
# ---------------------------------------------------------------------------------------------------------------

def sha256_of(path: Path) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def virustotal_link(sha: str) -> str:
    return f"https://www.virustotal.com/gui/file/{sha}"


def checksum_notes(sha: str, scanned: bool) -> str:
    """O bloco do fim da nota: o hash e, se o exe foi pro VirusTotal, o link da análise."""
    check = f"Check it in PowerShell with `Get-FileHash .\\{MAIN_EXE}`."
    if scanned:
        check += f" [VirusTotal scan]({virustotal_link(sha)})."
    return f"### Checksum\n`{MAIN_EXE}` SHA-256: `{sha}`\n\n{check}"


def virustotal_key() -> str | None:
    return os.environ.get("VIRUSTOTAL_API_KEY") or None


def virustotal_get(path: str, key: str) -> dict | None:
    """GET na API do VirusTotal; None quando ele não conhece (404)."""
    request = urllib.request.Request(VIRUSTOTAL_API + path, headers={"x-apikey": key})
    try:
        with urllib.request.urlopen(request, timeout=60) as response:
            return json.load(response)
    except urllib.error.HTTPError as e:
        if e.code == 404:
            return None
        raise


def virustotal_send(exe: Path, sha: str, key: str) -> bool:
    """Manda o exe pro VirusTotal, se ele ainda não conhece. False só quando não deu pra mandar."""
    log_step("Mandando pro VirusTotal...")
    try:
        if virustotal_get(f"/files/{sha}", key) is not None:
            log_ok("O VirusTotal já conhece esse exe")
            return True
        # mais de 32 MB: precisa do endereço de upload grande
        upload_url = virustotal_get("/files/upload_url", key)["data"]
    except (urllib.error.URLError, KeyError, TypeError, ValueError) as e:
        log_error(f"VirusTotal: {e}")
        return False
    # o curl do Windows monta o multipart sem dependência nova
    if not run_cmd(["curl", "-s", "-S", "--fail", "-H", f"x-apikey: {key}", "-F", f"file=@{exe}", upload_url],
                   "upload pro VirusTotal"):
        return False
    log_ok("Enviado; a análise leva alguns minutos")
    return True


def virustotal_result(sha: str, key: str, minutes: int = 15) -> bool:
    """Espera a análise do exe e mostra quantos antivírus acusaram. False se algum acusou."""
    deadline = time.time() + minutes * 60
    with console.status("[cyan]esperando a análise do VirusTotal[/cyan]", spinner="dots"):
        while True:
            try:
                report = virustotal_get(f"/files/{sha}", key)
            except urllib.error.URLError:
                report = None
            attributes = (report or {}).get("data", {}).get("attributes", {})
            stats = attributes.get("last_analysis_stats") or {}
            # recém-enviado o arquivo aparece antes da análise: só vale com a data dela
            if stats and sum(stats.values()) > 0 and attributes.get("last_analysis_date"):
                break
            if time.time() > deadline:
                log_warn(f"A análise não terminou em {minutes} min; o resultado sai em {virustotal_link(sha)}")
                return True
            time.sleep(20)

    bad = stats.get("malicious", 0) + stats.get("suspicious", 0)
    total = bad + stats.get("undetected", 0) + stats.get("harmless", 0)
    if bad == 0:
        log_ok(f"VirusTotal: 0/{total} acusaram")
        return True
    names = [f"{name} ({r.get('result')})" for name, r in (attributes.get("last_analysis_results") or {}).items()
             if r.get("category") in ("malicious", "suspicious")]
    log_warn(f"VirusTotal: {bad}/{total} acusaram: {', '.join(names)}")
    log_info(virustotal_link(sha))
    return False


def virustotal_release() -> bool:
    """O exe da versão atual pro VirusTotal e o link na nota do release que já está no GitHub."""
    version = get_version()
    key = virustotal_key()
    if not key:
        log_error("Sem VIRUSTOTAL_API_KEY (a chave da conta no virustotal.com, em API key)")
        return False
    if not release_exists(version):
        log_error(f"Nenhum exe da v{version}: faça o build antes")
        return False
    exe = EXE_DIR / MAIN_EXE
    sha = sha256_of(exe)
    if not virustotal_send(exe, sha, key):
        return False

    repo = RELEASES_REPO.removeprefix("https://github.com/")
    tag = f"v{version}"
    token = {"GH_TOKEN": github_token() or ""}
    body = subprocess.run(["gh", "release", "view", tag, "-R", repo, "--json", "body", "-q", ".body"],
                          capture_output=True, text=True, encoding="utf-8", env={**os.environ, **token})
    if body.returncode != 0:
        log_error(f"Sem o release {tag} no GitHub")
        return False
    notes = body.stdout.rstrip()
    if virustotal_link(sha) not in notes:
        old = checksum_notes(sha, scanned=False)
        new = checksum_notes(sha, scanned=True)
        notes = notes.replace(old, new) if old in notes else notes + "\n\n" + new
        notes_file = ARTIFACTS / f"notes-{version}.md"
        notes_file.write_text(notes + "\n", encoding="utf-8")
        if not run_cmd(["gh", "release", "edit", tag, "--notes-file", str(notes_file), "-R", repo], "gh release edit", token):
            return False
        log_ok(f"Link do VirusTotal na nota do {tag}")
    return virustotal_result(sha, key)


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
    """O release v<versão> no GitHub com o Provodnik.exe e a seção do CHANGELOG como nota (de onde o app se atualiza)."""
    version = get_version()
    log_step("Subindo pro GitHub...")
    log_info(f"Repo: {RELEASES_REPO}")

    if not release_exists(version):
        log_error(f"Nenhum exe da v{version}: faça o build antes")
        return False

    token = github_token()
    if not token:
        log_error("Sem token: defina GITHUB_TOKEN ou faça gh auth login")
        return False

    env = {"GH_TOKEN": token}
    repo = RELEASES_REPO.removeprefix("https://github.com/")
    tag = f"v{version}"
    exe = str(EXE_DIR / MAIN_EXE)
    sha = sha256_of(EXE_DIR / MAIN_EXE)
    # o exe vai pro VirusTotal antes: o link já sai na nota e a análise roda enquanto o release sobe
    vt_key = virustotal_key()
    scanned = bool(vt_key) and virustotal_send(EXE_DIR / MAIN_EXE, sha, vt_key)
    if not vt_key:
        log_warn("Sem VIRUSTOTAL_API_KEY: o release sai sem o link do VirusTotal")
    notes_file = ARTIFACTS / f"notes-{version}.md"
    notes = (changelog_notes(version) or "") + "\n\n" + checksum_notes(sha, scanned)
    notes_file.write_text(notes + "\n", encoding="utf-8")

    exists = subprocess.run(["gh", "release", "view", tag, "-R", repo], capture_output=True,
                            env={**os.environ, **env}).returncode == 0
    if exists:
        # o mesmo release de novo: troca o exe e a nota
        if not run_cmd(["gh", "release", "upload", tag, exe, "--clobber", "-R", repo], "gh release upload", env):
            return False
        if not run_cmd(["gh", "release", "edit", tag, "--notes-file", str(notes_file), "-R", repo], "gh release edit", env):
            return False
    else:
        # versão de teste ("0.4.0-teste.2") sobe como pré-release: só quem está numa de teste recebe
        pre = ["--prerelease"] if "-" in version else []
        if not run_cmd(["gh", "release", "create", tag, exe, "--title", f"Provodnik {version}",
                        "--notes-file", str(notes_file), "-R", repo] + pre, "gh release create", env):
            return False

    log_ok(f"Publicado: {RELEASES_REPO}/releases/tag/{tag}")
    log_info("Quem já tem o Provodnik recebe sozinho; quem não tem baixa o Provodnik.exe do release.")
    if scanned:
        console.print()
        virustotal_result(sha, vt_key)
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
        console.print(f"  [green]✓[/green] Exe da v{version} em artifacts/exe")
    else:
        console.print(f"  [dim]○[/dim] Nenhum exe da v{version}")
    if changelog_notes(version) is None:
        console.print(f"  [yellow]![/yellow] docs/CHANGELOG.md sem a seção {version}")
    console.print()

    console.print("[bold]Opções:[/bold]")
    console.print("  [cyan]1[/cyan] - Subir a versão (patch/minor/major)")
    console.print(f"  [cyan]2[/cyan] - {'Refazer o build' if built else 'Build (testes e o exe único)'}")
    console.print("  [cyan]3[/cyan] - Publicar no GitHub" if built else "  [dim]3 - Publicar no GitHub (faça o build antes)[/dim]")
    console.print("  [cyan]4[/cyan] - Exe único (e copiar pra pasta do GAMMA)")
    console.print(f"  [cyan]5[/cyan] - Limpar o exe da v{version}")
    console.print("  [cyan]6[/cyan] - VirusTotal do exe (e o link no release)" if built else "  [dim]6 - VirusTotal do exe (faça o build antes)[/dim]")
    console.print("  [cyan]q[/cyan] - Sair")
    console.print()
    return Prompt.ask("Escolha", choices=["1", "2", "3", "4", "5", "6", "q"], default="2")


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
        log_step(f"Limpando o exe da v{get_version()}...")
        clean_release(get_version())
        console.print()
        return

    if choice == "6":
        console.print()
        if virustotal_release():
            console.print("[green]Pronto![/green]")
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
        console.print("[dim]Ficou só local, em artifacts/exe.[/dim]")
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
               "  python tools/build_tool.py --build               testes e o exe único em artifacts/exe\n"
               "  python tools/build_tool.py --build --github      e publica o release no GitHub\n"
               "  python tools/build_tool.py --virustotal          exe atual pro VirusTotal e o link no release\n"
               "  python tools/build_tool.py --bump patch          0.3.0 -> 0.3.1 (abre a seção no CHANGELOG)\n"
               "  python tools/build_tool.py --gamma D:\\GAMMA       exe único copiado pra D:\\GAMMA\\Provodnik.exe",
    )
    parser.add_argument("--bump", choices=["patch", "minor", "major", "teste"],
                        help="sobe a versão no Directory.Build.props e abre a seção no CHANGELOG")
    parser.add_argument("--build", action="store_true",
                        help="testes e o exe único (um arquivo, .NET dentro) em artifacts/exe")
    parser.add_argument("--github", action="store_true",
                        help="publica o release da versão atual no GitHub com o Provodnik.exe (gh release)")
    parser.add_argument("--virustotal", action="store_true",
                        help="manda o exe da versão atual pro VirusTotal e põe o link na nota do release")
    parser.add_argument("--exe", action="store_true",
                        help="só o exe único (um arquivo, .NET dentro) em artifacts/exe")
    parser.add_argument("--gamma", metavar="PASTA", type=Path,
                        help="o exe único copiado pra pasta do GAMMA (ao lado do ModOrganizer.exe)")
    parser.add_argument("-y", "--yes", action="store_true",
                        help="sem perguntas: refaz o exe da versão se ele já existir")
    return parser.parse_args()


def main():
    args = parse_args()
    if not (args.bump or args.build or args.github or args.exe or args.gamma or args.virustotal):
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
    if ok and args.virustotal and not args.github:
        console.print()
        ok = virustotal_release()
    if ok and (args.exe or args.gamma) and not (args.build and not args.gamma):
        console.print()
        ok = publish_exe(args.gamma)

    console.print()
    if not ok:
        console.print("[red]Falhou![/red]")
        sys.exit(1)
    console.print("[green]Pronto![/green]")


if __name__ == "__main__":
    main()
