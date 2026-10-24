# Novidades

## 0.3.0

- Links do ModDB pelo navegador embutido (WebView2): o ModDB passou a exigir navegador por causa do Cloudflare, e o stalker-gamma-dl-cli do GAMMA responde "Failed to generate download link" pra todo mundo. Lê o HTML assim que chega e vai direto pro espelho, ~1 s por link; desafio com clique abre a janela pra pessoa resolver, e os cookies ficam salvos.
- Instrução com barra no começo ("\Black_Market-main") ou com "/" entrava errado; os mods montados assim são refeitos sozinhos.
- Pasta somente-leitura dentro do arquivo (zip/rar) não trava mais a extração.
- Quem só espera a vez na fila do ModDB não aparece mais como "baixando".
- Todos os mods entram na fila de uma vez: os do GitHub não esperam os do ModDB.
- Simulação (`Provodnik.exe --demo`, `--demo-novo` pra começar do zero): GAMMA de mentira em %TEMP%, a lista de mods de verdade, ferramentas falsas com bloqueio do ModDB, quedas, 404 e arquivo corrompido, tempo acelerado. Roda junto com uma instalação de verdade sem encostar nela.
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
- Ícone: o átomo do GAMMA (três órbitas, elétrons e núcleo) redesenhado em vetor no verde dele, nítido de 16 a 256 px (tools/icon/atom.cs).
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
