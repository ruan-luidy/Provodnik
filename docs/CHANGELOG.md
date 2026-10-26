# Novidades

## 0.3.4

- Liberar espaço (tela Mods): apaga de downloads\ as versões velhas que as atualizações do GAMMA deixam e nada mais usa (nem a lista atual, nem o meta.ini de alguma pasta em mods\), depois de mostrar quais e quanto. O que o MO2 baixou pra pessoa (tem .meta do lado) nunca sai.
- Voltar o perfil de antes (Início, na linha do perfil do MO2): põe de volta a cópia do modlist.txt de antes da última instalação. A lista de agora vira cópia também, então voltar de novo desfaz. Pede pra fechar o MO2 antes, que ele grava a lista dele por cima quando fecha.
- Backup dos saves antes de instalar ou atualizar, num zip em .provodnik\save-backups (os 5 últimos); desliga nas Configurações. Pega o savedgames do Anomaly, ou o saves\ do perfil quando ele usa saves próprios. O "tentar de novo" e o reinstalar de um mod não fazem cópia.

## 0.3.3

- A atualização não estraga mais o perfil do GAMMA. Em vez de trocar o modlist.txt inteiro pelo da definição (o que o launcher original faz), mescla a lista nova com o perfil como está e com a lista do GAMMA da última instalação:
  - os mods da pessoa vão juntos, na ordem e no estado em que estavam, pro fim da lista do MO2, embaixo do separador "Meus mods" (separador da própria pessoa no fim do grupo dispensa o "Meus mods");
  - mod do GAMMA que a pessoa ligou ou desligou fica como ela deixou, a não ser que o GAMMA também tenha mudado ele;
  - addon opcional do GAMMA (modpack_addons, gamma_large_files_v2) que a pessoa ligou conta como dela; as sobras do GAMMA fora da lista ganham linha desligada no começo do MO2, senão o MO2 jogava elas no meio dos mods da pessoa;
  - uma cópia do modlist.txt de antes fica em .provodnik\profile-backups (as 10 últimas).
- Mod do GAMMA que a pessoa trocou pela versão dela pelo MO2 (o meta.ini aponta pra outro arquivo) não é sobrescrito quando o GAMMA atualiza: fica a dela, com um aviso no relatório. O Reinstalar da tela Mods volta pra do GAMMA.
- Escolha do perfil do MO2 no Início, do lado do executável (aparece com mais de um perfil): o Jogar e o Abrir no MO2 abrem nele (`-p` do MO2).
- Início: o Instalar / atualizar subiu pro topo, junto do Abrir no MO2 (novo ali), do Conferir e da Pasta do GAMMA, e fica azul quando tem o que instalar; no cartão ficam o perfil, o executável e o Jogar.
- Abrir no MO2 que não abre mostra o motivo em vez de derrubar o app.
- Fechar depois de baixar uma versão nova dava "FileNotFoundException: Microsoft.Web.WebView2.Wpf": o exe era renomeado pra .old antes de o navegador do ModDB ser fechado, e o exe único não achava mais a biblioteca dentro dele. A troca agora é a última coisa ao fechar, e o Reiniciar agora passa pelo mesmo caminho.

## 0.3.2

- O atalho da área de trabalho volta pro nome latino, "Provodnik (G.A.M.M.A.)"; o de nome cirílico da 0.3.1 é apagado.

## 0.3.1

- O atalho da área de trabalho volta a se chamar "Проводник (G.A.M.M.A.)": agora é criado pelo IShellLinkW do Windows, em Unicode, e o cirílico não vira mais "?????????". O atalho de nome latino das versões anteriores é apagado.
- README com o tamanho certo: a primeira instalação baixa uns 45 GB e precisa de uns 200 GB livres (os 150 GB eram dos mods extraídos).
- Licença MIT, modelos de issue e PR, guia de contribuição, código de conduta e política de segurança.

## 0.3.0

- Sem instalador: o Проводник é um exe só, que mora na pasta do GAMMA e se atualiza sozinho. Procura o último release no GitHub, baixa o Provodnik.exe novo do lado do atual (conferido pelo tamanho e pelo SHA-256) e troca quando a pessoa reinicia ou o app fecha. O `tools/build_tool.py` faz o build, o exe único e o release no GitHub (no lugar do `tools/publicar.ps1`).
- Links do ModDB pelo navegador embutido (WebView2): o ModDB passou a exigir navegador por causa do Cloudflare, e o stalker-gamma-dl-cli do GAMMA responde "Failed to generate download link" pra todo mundo. Lê o HTML assim que chega e vai direto pro espelho, ~1 s por link; desafio com clique abre a janela pra pessoa resolver, e os cookies ficam salvos.
- Instrução com barra no começo ("\Black_Market-main") ou com "/" entrava errado; os mods montados assim são refeitos sozinhos.
- Pasta somente-leitura dentro do arquivo (zip/rar) não trava mais a extração.
- Quem só espera a vez na fila do ModDB não aparece mais como "baixando".
- Todos os mods entram na fila de uma vez: os do GitHub não esperam os do ModDB.
- Simulação (`Provodnik.exe --demo`, `--demo-novo` pra começar do zero): GAMMA de mentira em %TEMP%, a lista de mods de verdade, ferramentas falsas com bloqueio do ModDB, quedas, 404 e arquivo corrompido, tempo acelerado. Roda junto com uma instalação de verdade sem encostar nela. Com `--shots pasta` tira as capturas do README sozinha (2x, sem o selo DEMO, cantos e sombra do Windows 11).
- Início: a legenda de "Última instalação" segue o estado ("a última teve falha", "rode Instalar / atualizar") em vez de dizer sempre "completa, sem falha".
- Tabela no estilo do rnzNegative (StandardDataGrid) com o degradê embaixo.
- Registro legível: texto na cor normal, o nível só na bolinha e numa etiqueta.
- Barra de título sem a pílula: carregando, andamento e barra fina.
- Camadas da Связь, sem linha dura: o painel fica direto no Mica, a tela é uma camada com o canto de cima arredondado e os cartões são brancos translúcidos; o cartão Pastas é o de informação da Связь, com divisória fina.
- Início com cara própria: o cartão do estado ganha o ícone grande e um degradê suave da cor do estado; os números ganham ícone e, nas proporções, a barrinha que fica verde quando completa.
- Diálogo aberto escurece a barra de título junto; o carregando e o erro respeitam o canto arredondado da tela.
- Fundo Mica do Windows 11, como na Связь (desliga nas Configurações; no Windows 10 fica o cinza sólido).
- Pasta de instrução renomeada pelo autor (o Defended Lab X10: a lista diz "Spawn Options\No Bloodsuckers", o arquivo tem "Spawn - No Bloodsuckers"): usa a única pasta que termina igual, em vez de deixar o mod vazio como o original.
- Conferência: nome do modlist com TAB no fim conta certo, e nome que o próprio modlist do GAMMA escreveu errado vira aviso, não falha.
- Atalho "Provodnik (G.A.M.M.A.)" (o WScript.Shell não grava nome em cirílico).
- Ícone: o átomo do Icons8 no verde do GAMMA, em vetor, nítido de 16 a 256 px.
- Dicas com a lâmpada do Phosphor no lugar da seta; estado "Tentando de novo" enquanto espera a próxima tentativa; depois de cancelar nada fica girando.

## 0.2.0

- Console das ferramentas, como o cmd do launcher original, mas legível: cada erro conhecido do git, curl, 7-Zip e stalker-gamma-dl-cli ganha embaixo o que quer dizer e o que fazer. Fica na tela de instalação ou numa janela separada, e vai pra .provodnik\logs\console-*.log.
- Checagem antes de instalar: permissão nas pastas, Anomaly, MO2 e jogo fechados, espaço, ferramentas do GAMMA (antivírus) e caminhos longos, com o conserto num botão.
- Administrador só quando precisa: oferece reabrir elevado se a pasta não deixa gravar. Elevado, o MO2 e o jogo abrem sem administrador.
- Falhas da instalação mostram o que fazer.
- Tela de Mods com o atalho pro MO2 (ligar, desligar e ordenar continua sendo lá).
- Ritmo com o ModDB: os pedidos de link (o que ele limita) vão um por vez, e quando ele bloqueia a fila inteira espera junto em vez de cada mod insistir; os downloads no CDN continuam em paralelo.

## 0.1.0

- Primeira versão: instala e atualiza o G.A.M.M.A. no lugar do `G.A.M.M.A. Launcher.exe`, com a mesma lista de mods, as mesmas pastas e o mesmo perfil do MO2.
- Downloads em paralelo, com retomada, novas tentativas e md5 conferido antes de extrair.
- Só reextrai o mod cujo arquivo mudou; pasta de mod nunca fica pela metade.
- Relatório no fim com os mods que falharam e "tentar de novo" só neles.
- Jogar direto num executável do MO2 (DX11, DX11-AVX, DX9...).
