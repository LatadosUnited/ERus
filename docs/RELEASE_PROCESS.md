# Guia de Compilação e Lançamento (Release)

Este guia explica como compilar e empacotar o **ERus Hub** e a **ERus Engine**, e como disponibilizar essas versões compactadas para os usuários através do GitHub Releases.

## 1. Pré-requisitos

Certifique-se de que você possui o **.NET 10.0 SDK** (ou a versão mais recente exigida pelo projeto) instalado na sua máquina.
As compilações e o empacotamento utilizam scripts em PowerShell disponíveis na pasta `scripts/`.

## 2. Compilando e Empacotando

Existem scripts dedicados para compilar e gerar os arquivos `.zip` prontos para distribuição. Eles automaticamente realizam o *build* (ou *publish*) em modo `Release` e criam as pastas e arquivos compactados no diretório `Builds/` na raiz do projeto.

### Compilando a ERus Engine

Para compilar a Engine, abra um terminal PowerShell na raiz do projeto e execute:

```powershell
.\scripts\build_engine_release.ps1 [versão]
```

**Exemplo:**
```powershell
.\scripts\build_engine_release.ps1 v0.3.7
```
*(Se você não passar a versão, ele usará a versão padrão definida no script, por exemplo, `v0.2.6`).*

Este comando irá gerar um arquivo `ERus.Engine-v0.3.7.zip` na pasta `Builds/`.

### Compilando o ERus Hub

O Hub é publicado como um executável único (*Single File*) contido (com tudo embutido) para Windows de 64 bits, facilitando a distribuição.

Para compilar o Hub, execute o seguinte comando:

```powershell
.\scripts\build_hub_release.ps1 [versão]
```

**Exemplo:**
```powershell
.\scripts\build_hub_release.ps1 v0.1.0
```
*(Se não passar a versão, o script usará a padrão, por exemplo, `v1.0.0`).*

Este comando irá gerar um arquivo `ERus.Hub-v0.1.0.zip` na pasta `Builds/`.

## 3. Subindo as versões para o GitHub (Releases)

Com os arquivos `.zip` gerados na pasta `Builds/`, o próximo passo é disponibilizá-los para os usuários utilizando o recurso de **Releases** do GitHub. Você pode fazer isso via Terminal ou pela Interface Web.

### Opção A: Usando o Terminal (GitHub CLI - Recomendado)

Se você possui o [GitHub CLI (`gh`)](https://cli.github.com/) instalado e autenticado (`gh auth login`), você pode criar o release e subir o `.zip` com um único comando.

**Para a Engine:**
```powershell
gh release create v0.3.7 .\Builds\ERus.Engine-v0.3.7.zip --title "ERus Engine v0.3.7" --notes "Notas de atualização da Engine."
```

**Para o Hub:**
```powershell
gh release create v0.1.0 .\Builds\ERus.Hub-v0.1.0.zip --title "ERus Hub v0.1.0" --notes "Notas de atualização do Hub." --repo "LatadosUnited/ERusHub"
```

### Opção B: Passo a passo pela Interface Web

1. Acesse o repositório do seu projeto no **GitHub**.
2. Na barra lateral direita da página inicial, clique em **Releases**.
3. Clique no botão **"Draft a new release"**.
4. **Choose a tag:**
   - Digite a versão (ex: `v0.3.1` para a Engine).
   - Selecione a opção para criar a tag, se ela não existir.
5. **Release title:**
   - Exemplo: `ERus Engine v0.3.1` ou `ERus Hub v0.1.0`.
6. **Describe this release:**
   - Descreva as novidades, correções de bugs, etc.
7. **Attach binaries:**
   - Arraste os arquivos **`.zip`** gerados na pasta `Builds/` (`ERus.Engine-v0.3.1.zip` ou `ERus.Hub-v0.1.0.zip`) para a caixa de upload no final da página.
8. (Opcional) Marque *Set as the latest release*.
9. Clique no botão verde **"Publish release"**.

Pronto! Agora qualquer usuário poderá baixar diretamente os arquivos `.zip` contendo as versões compiladas.
