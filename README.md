# Проводник

Launcher próprio para o **S.T.A.L.K.E.R. G.A.M.M.A.**, no lugar do `G.A.M.M.A. Launcher.exe`. Instala e atualiza a mesma lista de mods, nas mesmas pastas e com o mesmo perfil do MO2, mas sem os problemas do original: mod que fica vazio, download que não volta, tudo em sequência e reextraído a cada atualização.

> *Проводник* (pra-va-DNIK) = guia.

## O que faz

- **Instala e atualiza** o GAMMA: repositórios, definição, os ~470 addons, arquivos grandes, perfil do MO2 e patches do Anomaly.
- **Downloads em paralelo** (ModDB limitado pra não acordar o Cloudflare), continuando de onde pararam, com novas tentativas e md5 conferido antes de extrair.
- **Só reextrai o que mudou.** Pasta de mod nunca fica pela metade; arquivo quebrado deixa a versão anterior no lugar.
- **Para e continua.** Fechou no meio, caiu a internet, saiu atualização: rodar de novo continua de onde parou.
- **Confere a instalação:** todo mod ligado no `modlist.txt` do GAMMA (a "regra dos 5xx mods ativos") precisa ter pasta com conteúdo. O que faltar vira falha com "tentar de novo".
- **Jogar** direto no executável escolhido (DX11-AVX, DX11, DX9...) pelo MO2.
- **Console** das ferramentas (como o cmd do launcher original), com cada erro conhecido explicado em português embaixo dele. Na tela de instalação ou numa janela separada.
- **Checagem antes de instalar** (permissão, Anomaly, MO2 fechado, espaço, antivírus, caminhos longos), com o conserto num botão. Administrador só quando a pasta não deixa gravar; o MO2 e o jogo sempre abrem sem.
- Tela de **Mods** com o arquivo e a pasta de cada addon, e **Registro** com o passo a passo (e um log por instalação em `.provodnik\logs`).

## E o Mod Organizer?

Continua sendo ele. O jogo só enxerga os mods pela pasta virtual do MO2, e ligar, desligar, ordenar, buscar e ver conflitos é trabalho dele. O Jogar do Проводник abre o MO2 direto no executável escolhido, e a tela de Mods daqui só diz se cada addon foi baixado e extraído.

## Usando direto na pasta do GAMMA

O jeito pensado: publicar um exe só e deixar ele ao lado do `ModOrganizer.exe`.

```powershell
.\tools\publicar.ps1 -Gamma D:\GAMMA
```

Isso gera `D:\GAMMA\Provodnik.exe`, com o .NET dentro (não precisa instalar nada). Ele acha a pasta sozinho e usa as ferramentas que o GAMMA já traz em `.Grok's Modpack Installer\resources` (7-Zip, curl-impersonate, stalker-gamma-dl-cli e Git). O launcher original pode continuar lá: os dois leem e escrevem os mesmos arquivos.

Pra rodar do código:

```powershell
dotnet run --project src/Provodnik.Wpf
dotnet test
```

## Estrutura

| Projeto | O que é |
|---|---|
| `Provodnik.Core` | Modelos e contratos (mods.txt, meta.ini, ModOrganizer.ini, modlist.txt, estado). Zero dependências. |
| `Provodnik.Features` | O pipeline de instalação, downloads, verificação, perfil, patches e o Jogar. |
| `Provodnik.Infrastructure` | HttpClient e as ferramentas do GAMMA rodadas como processo. |
| `Provodnik.Wpf` | A interface (HandyControl + CommunityToolkit.Mvvm), em fatias por `Features/`. |

