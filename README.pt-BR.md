<p align="center">
  <img src="Branding/quadra-icon-1024.png" width="128" alt="Ícone do Quadra">
</p>

<h1 align="center">Quadra</h1>

<p align="center">
  Organize as janelas do Mac numa grade. Grátis e de código aberto.<br>
  <a href="README.md">Read in English</a>
</p>

O Quadra é um app de barra de menus inspirado no [Divvy](https://mizage.com/divvy/). Aperte um
atalho ou clique no ícone, arraste na grade, e a janela vai para aquela área da tela.

## O que ele faz

- **Arraste numa grade** para escolher qualquer área, com 1 a 20 colunas e linhas.
- **Painel translúcido** no meio da tela, com uma prévia de onde a janela vai parar.
- **Clique em outra janela** com a grade aberta para mover essa no lugar.
- **Layouts com atalho próprio**: salve áreas como "metade esquerda" ou "centro" e aplique com
  um atalho. Já vem com seis layouts prontos.
- **Vários monitores**: escolha a tela no painel ou mande a janela para a próxima tela.
- **Pensado para monitores ultrawide**, e funciona com monitores em pé e telas de notebook.
- Espaço entre janelas, formato, tamanho e opacidade da grade ajustáveis.
- Em inglês e português do Brasil.

## Instalar

1. Baixe o `Quadra.zip` na [versão mais recente](https://github.com/ahrmichelon/quadra/releases/latest),
   descompacte e mova o **Quadra.app** para **Aplicativos**.
2. Abra o app. O Quadra ainda não tem a verificação da Apple, então na primeira vez o macOS bloqueia.
   Abra **Ajustes do Sistema → Privacidade e Segurança**, role até o fim e clique em **Abrir Mesmo Assim**.
3. Quando pedir, permita a **Acessibilidade** (Ajustes do Sistema → Privacidade e Segurança →
   Acessibilidade → ligue o Quadra). O Quadra precisa disso para mover as janelas dos outros apps.

Precisa do macOS 14 Sonoma ou mais novo, em Mac com Apple Silicon ou Intel.

> Depois de atualizar o Quadra, o macOS pode deixar de reconhecer a permissão. Se os atalhos
> pararem de mover as janelas, remova o Quadra da lista de Acessibilidade com **–** e adicione de
> novo com **+**.

## Como usar

| Atalho | O que faz |
|---|---|
| **⌃⌥G** ou clique no ícone da barra de menus | Abre a grade |
| Arrastar na grade | Leva a janela para aquela área |
| **← → ↑ ↓** / **↩** (com a grade aberta) | Metades / tela cheia |
| **esc** | Fecha a grade |
| **⌃⌥← → ↑ ↓**, **⌃⌥↩**, **⌃⌥C** | Layouts padrão: metades, tela cheia, centro |
| **⌃⌥⌘→** / **⌃⌥⌘←** | Leva para a próxima tela / tela anterior |

O menu abre com o botão direito no ícone ou na engrenagem do painel. Todos os atalhos podem ser
trocados em **Ajustes e layouts**.

## Compilar

Precisa do macOS 14 ou mais novo e do Xcode 15.3 ou mais novo.

```bash
git clone https://github.com/ahrmichelon/quadra.git
cd quadra
./build.sh --install   # gera o build/Quadra.app, copia para Aplicativos e abre
swift test             # roda os testes
```

Para mexer no código, abra o `Package.swift` no Xcode.

## Contribuir

Relatos de problemas, ideias e pull requests são bem-vindos, em português ou inglês.
Veja o [CONTRIBUTING.md](CONTRIBUTING.md).

## Licença

[MIT](LICENSE)
