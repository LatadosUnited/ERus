# ERus.Hub

O `ERus.Hub` é a porta de entrada (Launcher) de todo o ecossistema. Ele gerencia as versões instaladas da Engine e os Projetos ativos do usuário.

## Funcionalidades Core
- **Versionamento Dinâmico**: Conecta-se à API pública do GitHub (`/releases`) de forma assíncrona (`Task`) para listar, baixar e instalar versões novas do Motor através do `GitHubReleaseManager`. O progresso de download (`_downloadProgress`) é injetado diretamente no ImGui, evitando travamentos na renderização.
- **ConfigManager e Isolamento de Versão**: Persiste metainformações (`ProjectData` e `EngineInstall`) no arquivo `config.json` guardado em `%AppData%/ERusHub/`. Cada projeto é atrelado firmemente a uma versão específica da Engine, prevenindo que atualizações globais quebrem a lógica do projeto.
- **Bootstrapper (Launch Arguments)**: Ao clicar em "Open", o Hub delega a execução instanciando um processo da Engine selecionada via `Process.Start`, injetando o caminho raiz através do argumento `--project "[Caminho]"`.
- **Paths Customizáveis**: Permite ao usuário escolher HDs alternativos (ex: `D:\Engines`) para instalação, usando seletores de pastas nativos do Windows (`NativeFileDialogSharp`).

### Tecnologia da UI
Diferente do `ERus.Editor`, o `Hub` **não carrega** as lógicas pesadas da `ERus.Engine` (ECS, Rede, etc). Ele utiliza apenas os wrappers base do `Silk.NET.Windowing` e `ImGuiNET` (`Program.cs`) para renderizar uma janela levíssima contendo um sistema de abas (`Projects` e `Installs`).

## Problemas Conhecidos e Limitações (Trade-offs)
- **Tratamento de Downloads Interrompidos**: Atualmente não existe suporte a "Pausar/Retomar" o download do arquivo ZIP do GitHub. Se a internet cair, o arquivo parcial precisa ser apagado manualmente ou será deletado no re-download.
- **Falta de Hash Verification (SHA256)**: Os `.zip` não estão sendo validados por checksum após o download, então zips corrompidos podem ocasionalmente travar a extração.
- **Blocking Dialogs**: Os `Dialog.FolderPicker()` do Windows chamados durante o ImGui Loop geram um bloqueio de renderização (UI Freezes) até o usuário fechar a janela.
