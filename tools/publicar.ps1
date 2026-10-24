# Publica o Проводник como um exe só (com o .NET dentro) e, se pedir, coloca na pasta do GAMMA.
#   .\tools\publicar.ps1                    -> artifacts\publish\Provodnik.exe
#   .\tools\publicar.ps1 -Gamma D:\GAMMA    -> e copia pra D:\GAMMA\Provodnik.exe
param(
  [string]$Gamma
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'artifacts\publish'

dotnet publish (Join-Path $root 'src\Provodnik.Wpf\Provodnik.Wpf.csproj') -c Release -p:Distribuir=true -o $out
if ($LASTEXITCODE -ne 0) { throw 'publish falhou' }

$exe = Join-Path $out 'Provodnik.exe'
"Publicado: $exe ($([math]::Round((Get-Item $exe).Length / 1MB, 1)) MB)"

if ($Gamma) {
  if (-not (Test-Path (Join-Path $Gamma 'ModOrganizer.exe'))) { throw "$Gamma não tem o ModOrganizer.exe: não parece a pasta do GAMMA" }
  if (Get-Process Provodnik -ErrorAction SilentlyContinue) { throw 'Feche o Проводник antes de trocar o exe' }
  Copy-Item $exe (Join-Path $Gamma 'Provodnik.exe') -Force
  "Copiado pra $(Join-Path $Gamma 'Provodnik.exe')"
}
