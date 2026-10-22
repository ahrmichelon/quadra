<p align="center">
  <img src="../Branding/quadra-icon-1024.png" width="128" alt="Ícone do Quadra">
</p>

<h1 align="center">Quadra para Windows</h1>

<p align="center">
  Organize suas janelas numa grade. Grátis e de código aberto.<br>
  <a href="README.md">Read in English</a> · <a href="../README.pt-BR.md">Quadra para Mac</a>
</p>

O Quadra é um app da bandeja do sistema inspirado no [Divvy](https://mizage.com/divvy/). Aperte
um atalho ou clique no ícone ao lado do relógio, arraste na grade, e a janela vai para aquela área
da tela. É a versão para Windows do [Quadra para Mac](../README.pt-BR.md), com a mesma grade, os
mesmos layouts e atalhos.

O Windows tem o Snap Layouts, e o Microsoft PowerToys tem o FancyZones. O Quadra funciona de outro
jeito: não é preciso montar zonas antes. Você desenha qualquer área numa grade, em qualquer tela,
na hora em que precisa, e vê onde a janela vai parar antes de soltar o mouse.

## O que ele faz

- **Arraste numa grade** para escolher qualquer área, com 1 a 20 colunas e linhas.
- **Painel translúcido** no meio da tela, com uma prévia de onde a janela vai parar.
- **Clique em outra janela** com a grade aberta para mover essa no lugar.
- **Layouts com atalho próprio**: salve áreas como "metade esquerda" ou "centro" e aplique com
  um atalho. Já vem com seis layouts prontos.
- **Vários monitores**: escolha a tela no painel ou mande a janela para a próxima tela, mesmo
  quando cada tela tem uma escala diferente.
- **Pensado para monitores ultrawide**, e funciona com monitores em pé e telas de notebook.
- As janelas encostam exatamente nas bordas da tela (o Quadra desconta as bordas invisíveis do
  Windows).
- Espaço entre janelas, formato, tamanho e opacidade da grade ajustáveis. Segue o modo claro ou
  escuro e a cor de destaque do Windows.
- Em inglês e português do Brasil, conforme o idioma do Windows.
- Um arquivo só: nada para instalar e sem precisar de administrador.

## Instalar

1. Baixe o Quadra na [página de versões](https://github.com/vulpini-br/quadra/releases?q=windows&expanded=true)
   (procure **Quadra for Windows**):
   - **Quadra-x64.exe** para a maioria dos PCs (Intel e AMD).
   - **Quadra-arm64.exe** para PCs com processador ARM (Snapdragon, Surface Pro X).
2. Abra o arquivo baixado. O Quadra ainda não tem um certificado pago, então o Windows pode mostrar
   **"O Windows protegeu o computador"**. Clique em **Mais informações** e depois em
   **Executar assim mesmo**.
3. O Quadra se instala sozinho e abre. Ele fica na sua pasta de usuário
   (`%LocalAppData%\Programs\Quadra`, sem precisar de administrador), aparece no menu Iniciar e em
   **Configurações → Aplicativos**, e abre sozinho toda vez que o Windows inicia (dá para desligar
   nas configurações dele). Depois, pode apagar o arquivo baixado.
4. Uma janela avisa que o Quadra foi instalado. O ícone fica ao lado do relógio; se não aparecer,
   clique na setinha **^**. Para deixá-lo sempre à vista, arraste o ícone para a barra de tarefas.

Precisa do Windows 10 (versão 1809 ou mais nova) ou do Windows 11, 64 bits. O .NET já vem dentro
do Quadra.exe, então não há mais nada para instalar.

## Como usar

| Atalho | O que faz |
|---|---|
| **Ctrl+Alt+G** ou clique no ícone ao lado do relógio | Abre a grade |
| Arrastar na grade | Leva a janela para aquela área |
| **← → ↑ ↓** / **Enter** (com a grade aberta) | Metades / tela cheia |
| **Esc** | Fecha a grade |
| **Ctrl+Alt+← → ↑ ↓**, **Ctrl+Alt+Enter**, **Ctrl+Alt+C** | Layouts prontos: metades, tela cheia, centro |
| **Ctrl+Alt+Shift+→** / **Ctrl+Alt+Shift+←** | Leva para a próxima tela / tela anterior |

Clicar no ícone abre a grade para a última janela que você usou (ou a que está na frente); sem
nenhuma janela aberta, abre as configurações. Clique com o botão direito no ícone, ou na engrenagem
do painel, para ver o menu. Todos os atalhos
podem ser trocados em **Configurações e layouts**; quando outro app já usa um atalho, o Quadra
mostra um aviso ao lado dele.

### Atualizações

Uma vez por dia o Quadra pergunta ao GitHub se saiu versão nova e avisa com uma notificação e um
item no topo do menu (dá para desligar nas Configurações, onde **Procurar agora** verifica na
hora). Clique em **Sim** para atualizar: o Quadra baixa a versão nova, confere com o arquivo
publicado no GitHub, fecha e abre de novo sozinho, mantendo suas configurações e layouts. Abrir um
Quadra.exe mais novo que você mesmo baixou atualiza do mesmo jeito. A verificação e o download são
as únicas vezes que o Quadra usa a internet, e não enviam nada sobre você ou o seu PC.

Bom saber:

- Abrir o Quadra.exe de novo com ele já aberto mostra as configurações.
- Janelas de apps abertos como administrador só podem ser movidas se o Quadra também for aberto
  como administrador.
- As configurações ficam em `%AppData%\Quadra\settings.json`. O arquivo `quadra.log`, na mesma
  pasta, ajuda quando for relatar um problema.
- Para remover o Quadra, abra **Configurações → Aplicativos → Aplicativos instalados**, procure
  **Quadra** e escolha **Desinstalar**. Isso remove o Quadra, o atalho e as configurações dele.

## Compilar

Você precisa do [.NET 10 SDK](https://dotnet.microsoft.com/download). O app só roda no Windows,
mas compila, e os testes rodam, também no macOS e no Linux.

```bash
cd Windows
dotnet test tests/Quadra.Core.Tests/Quadra.Core.Tests.csproj
dotnet publish src/Quadra/Quadra.csproj -c Release -r win-x64 -o publish/x64
```

O segundo comando gera `publish/x64/Quadra.exe` com o .NET dentro. Use `-r win-arm64` para ARM.
Aberto normalmente, o Quadra.exe se instala; acrescente `--portable` para rodá-lo de onde está
enquanto mexe no código.
No Windows, `Quadra.exe --self-test resultados` testa as partes que conversam com o Windows (mover
janelas, atalhos, o painel, as configurações) e grava um relatório e capturas de tela na pasta
`resultados`. O GitHub roda os testes e esse autoteste a cada mudança.

Todo texto que aparece para as pessoas está em `src/Quadra.Core/Strings.resx`, com a tradução em
`Strings.pt-BR.resx`; um teste confere se os dois têm os mesmos textos.

## Apoie o Quadra

O Quadra é grátis. Se ele te ajuda, veja [como apoiar](../README.pt-BR.md#apoie-o-quadra).

## Licença

[MIT](../LICENSE)

O Quadra é feito pela [Vulpini](https://vulpini.com.br).

O Quadra é um projeto independente, sem ligação com a Mizage LLC nem com o Divvy, e não é endossado por elas.
Divvy é uma marca da Mizage LLC.
