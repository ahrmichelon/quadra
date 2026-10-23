# Como publicar o Quadra na Mac App Store

O código já está pronto para a App Store. `./build.sh --appstore` gera uma versão própria:

- roda no **sandbox**, o modo isolado que a Apple exige;
- **não tem** o aviso de atualização, porque a App Store atualiza sozinha;
- **não tem** o link "Apoiar o Quadra", porque a Apple não aceita links para pagamentos fora da loja.

Os textos da página estão em [`textos-en.md`](textos-en.md) e [`textos-pt-BR.md`](textos-pt-BR.md),
e as imagens em [`screenshots/`](screenshots). A política de privacidade é o
[`PRIVACY.md`](../../PRIVACY.md) do repositório.

## 0. Conta de desenvolvedor da empresa

O Quadra é publicado pela **Vulpini Contabilidade Ltda** (CNPJ 55.377.852/0001-94,
[vulpini.com.br](https://vulpini.com.br)), inscrita como **organização** no
[Apple Developer Program](https://developer.apple.com/programs/enroll/) (US$ 99 por ano).
A Apple pede o número **D-U-N-S** da empresa ([consulta e pedido gratuitos](https://developer.apple.com/enroll/duns-lookup/)),
um site e um e-mail com o domínio da empresa, e que quem faz a inscrição possa assinar pela empresa.
Na App Store, quem aparece como vendedor é a Vulpini.

## 1. Identificador do app

O identificador da versão da App Store é **`br.com.vulpini.quadra`**. Ele já é o padrão do
`build.sh` e não pode mudar depois que o app estiver na loja. (A versão do GitHub e do Homebrew
continua com o identificador antigo, `io.github.ahrmichelon.quadra`, para quem já usa não precisar
liberar a Acessibilidade de novo. Ninguém vê esse nome.)

## 2. Certificados

No Xcode: **Ajustes → Contas**, escolha a conta da Vulpini, **Gerenciar certificados**, clique em
**+** e crie:

- **Apple Distribution**, que assina o app;
- **Mac Installer Distribution**, que assina o pacote enviado à Apple.

Eles ficam no chaveiro do Mac, e o `build.sh` os encontra sozinho.

## 3. Registrar o app

Em [developer.apple.com/account](https://developer.apple.com/account) → **Certificates, Identifiers & Profiles**
→ **Identifiers** → **+** → **App IDs** → **App**. Descrição "Quadra", **Bundle ID** "Explicit" com o
identificador do passo 1. Não precisa marcar nenhuma capability.

## 4. Perfil de provisionamento

Ainda em **Certificates, Identifiers & Profiles** → **Profiles** → **+** → **Mac App Store Connect**.
Escolha o App ID do passo 3 e o certificado **Apple Distribution**. Baixe o arquivo e salve como
`App/AppStore.provisionprofile` neste projeto. Ele fica só no seu Mac: o git ignora esse arquivo.

## 5. Criar o app no App Store Connect

Em [appstoreconnect.apple.com](https://appstoreconnect.apple.com) → **Apps** → **+** → **Novo app**:

- Plataforma: **macOS**
- Nome: **Quadra – Window Grid** (o nome precisa estar livre na loja; "Macintosh Quadra" foi um
  computador da Apple, por isso o complemento)
- Idioma principal: **Inglês (EUA)**, e depois acrescente **Português (Brasil)**
- Bundle ID: o do passo 1
- SKU: `quadra-mac`

## 6. Preencher a página

- **Textos:** copie de `textos-en.md` e `textos-pt-BR.md`.
- **Imagens:** envie as de `screenshots/` em cada idioma, na ordem 1, 2, 3, 4. Para gerar de novo:
  `./Tools/make-store-screenshots.sh`. Capturas reais do app em uso também são bem-vindas.
- **Privacidade do app:** "Dados não coletados". **URL da política:** a de `textos-en.md`.
- **Categoria:** Produtividade. **Preço:** Grátis. **Classificação etária:** responda "não" a
  tudo, o que dá 4+.

Regras da Apple a lembrar: os textos e as palavras-chave da loja **não podem citar outros apps ou
marcas**, e o app não pode mandar baixar versões de fora da loja.

## 7. Gerar o pacote

Com os certificados e o perfil no lugar:

```bash
./build.sh --appstore
```

O script assina o app e gera o `Quadra.pkg`. Se faltar alguma coisa, ele gera só uma versão de
teste e diz o que falta.

## 8. Enviar à Apple

Instale o app **Transporter** (grátis, na Mac App Store), entre com a conta da Vulpini, arraste o
`Quadra.pkg` e clique em **Entregar**. O pacote aparece no App Store Connect depois de alguns minutos.

## 9. Enviar para revisão

Na página da versão, escolha o build enviado, cole as **notas para a revisão** de `textos-en.md`
(explicam por que o app precisa de Acessibilidade e como testar) e clique em
**Enviar para revisão**. A resposta costuma vir em um a três dias.

## Novas versões

Atualize `CFBundleShortVersionString` e `CFBundleVersion` em `App/Info.plist` (o `CFBundleVersion`
sempre precisa subir), rode `./build.sh --appstore`, envie pelo Transporter e crie a versão nova
no App Store Connect.

## Testar a versão em sandbox antes de enviar

1. Feche o Quadra normal: botão direito no ícone → **Sair do Quadra**.
2. Gere e abra a versão de teste:
   ```bash
   ./build.sh --appstore
   open ~/Library/Caches/Quadra/AppStore/Quadra.app
   ```
   (se o projeto não estiver num disco exFAT, o caminho é `build/AppStore/Quadra.app`)
3. Na tela de boas-vindas, clique em **Abrir Ajustes do Sistema** e ligue o novo **Quadra** em
   Acessibilidade. Ele aparece como um app separado do Quadra normal.
4. Teste: ⌃⌥G e arrastar na grade, ⌃⌥← para a metade esquerda, os Ajustes e layouts.
5. Feche a versão de teste, abra de novo o Quadra normal, e remova a entrada de teste da lista de
   Acessibilidade com **–**.
