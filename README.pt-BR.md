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

> **Usa Windows?** O Quadra também roda no Windows 10 e 11: veja o [Quadra para Windows](Windows/README.pt-BR.md).

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

**Com o [Homebrew](https://brew.sh):**

```bash
brew install --cask vulpini-br/tap/quadra
```

Depois siga os passos 2 e 3 abaixo. Para atualizar no futuro: `brew upgrade --cask quadra`.

**Ou baixe direto:**

1. Baixe o `Quadra.zip` na [versão mais recente](https://github.com/vulpini-br/quadra/releases/latest),
   descompacte e mova o **Quadra.app** para **Aplicativos**.
2. Abra o app. O Quadra ainda não tem a verificação da Apple, então na primeira vez o macOS bloqueia.
   Abra **Ajustes do Sistema → Privacidade e Segurança**, role até o fim e clique em **Abrir Mesmo Assim**.
3. Quando pedir, permita a **Acessibilidade** (Ajustes do Sistema → Privacidade e Segurança →
   Acessibilidade → ligue o Quadra). O Quadra precisa disso para mover as janelas dos outros apps.

Precisa do macOS 14 Sonoma ou mais novo, em Mac com Apple Silicon ou Intel.

### Atualizações

Uma vez por dia o Quadra pergunta ao GitHub se saiu uma versão nova e te avisa quando houver
(dá para desligar nos Ajustes). É a única vez que o Quadra usa a internet.

A partir da versão 1.1.0, o macOS mantém a permissão de Acessibilidade quando o Quadra atualiza.
Vindo da 1.0.x, remova o Quadra da lista de Acessibilidade com **–** e adicione de novo com **+**
uma vez.

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
git clone https://github.com/vulpini-br/quadra.git
cd quadra
./build.sh --install   # gera o build/Quadra.app, copia para Aplicativos e abre
swift test             # roda os testes
```

Para mexer no código, abra o `Package.swift` no Xcode.

## Contribuir

Relatos de problemas, ideias e pull requests são bem-vindos, em português ou inglês.
Veja o [CONTRIBUTING.md](CONTRIBUTING.md).

## Apoie o Quadra

O Quadra é grátis. Se ele te ajuda, você pode apoiar o projeto com qualquer valor. A primeira
meta é pagar o Developer ID da Apple (US$ 99 por ano), para o macOS parar de bloquear o download.

**Pix:** aponte a câmera do app do banco para o QR code, ou use a chave aleatória
`8483959a-77da-4767-ba1b-dde7eb4e1bd1`.

<img src="docs/images/pix-qr.png" width="200" alt="QR code do Pix para apoiar o Quadra">

Pix copia e cola:

```
00020126580014br.gov.bcb.pix01368483959a-77da-4767-ba1b-dde7eb4e1bd15204000053039865802BR5914ALANHRMICHELON6009Sao Paulo62070503***63049B10
```

## Licença

[MIT](LICENSE)

O Quadra é feito pela [Vulpini](https://vulpini.com.br).

O Quadra é um projeto independente, sem ligação com a Mizage LLC nem com o Divvy, e não é endossado por elas.
Divvy é uma marca da Mizage LLC.
