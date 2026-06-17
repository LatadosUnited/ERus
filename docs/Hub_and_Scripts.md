# Hub de Projetos e Sistema de Scripting

O sistema foi desenhado de forma a separar a criação de lógica pura (Scripts em C#) do ambiente de execução nativo da Engine, e gerenciar toda a entrada na aplicação de modo amigável pelo Hub.

## `ERus.Hub` (Gerenciador de Projetos)

Antes do editor pesado (`ERus.Editor`) inicializar, o usuário passa pelo Hub.

- **HubUI (`ImGuiController`):** Assim como o Editor, utiliza OpenGL e ImGui nativos (Silk.NET) porém como uma aplicação muito mais leve e simplificada, dedicada apenas a gerenciamento de Janelas e UI bidimensional.
- **Project Configuration (`ConfigManager`):** Lê e grava estruturas de `ProjectData` em arquivos como `input_profile.json` e serializações contendo: IP de amigos, portas UDP/TCP, nome do projeto em disco e diretório root.
- **Transição ao Editor:** Uma vez que um perfil é selecionado e a opção "Host" ou "Join" é ativada, o Hub invoca a camada principal do framework (`ERus.Editor`), injetando os dados de configuração (IPs) via argumentos de memória (Memory Pointers) ou serializados, para que o motor carregue diretamente conectado à rede.

## O Sistema de Scripting em C# (`ERusScript` & Roslyn)

Uma característica avançada da Engine é que o usuário não programa lógica em C++ ou Lua, ele escreve códigos C# padrão que rodam com a mesma performance e alocação de memória do motor base.

### Como a Engine entende o código

1. **`ERusScript` (A Classe Base):** Scripts como o `PlayerController` ou `RotateScript` (no diretório `Assets/Scripts/`) devem obrigatoriamente herdar da classe abstrata `ERusScript`. Ela expõe eventos chaves do loop do ECS, como:
   - `OnStart()`
   - `OnUpdate(float deltaTime)`
2. **`ScriptModule` & Monitoramento:** O gerenciador de scripts fica analisando o diretório com um *FileWatcher*. Se o usuário modificar e salvar o código fonte `PlayerController.cs` no VSCode, um evento de *File Changed* é disparado.
3. **`ScriptCompiler` (Roslyn C# Compiler):** A Engine converte instantaneamente o arquivo fonte `.cs` num assembly binário (Dll na RAM) usando a API de compilação da Microsoft em background.

### Hot-Reloading & AssemblyLoadContext

Para garantir que o jogo não precisa ser reiniciado:
- **`CollectibleAssemblyLoadContext`:** Uma abstração do .NET Core que permite que DLLs compiladas sejam "descarregadas" da memória RAM sem reiniciar o executável principal. 
- Quando o arquivo muda, o `ScriptModule` pede ao `ScriptExecutionSystem` para pausar os `Update()` de scripts atuais. O compilador gera uma nova Dll. O sistema antigo é destruído (`UnloadCurrentAssembly`), a nova versão da lógica do script é instanciada e injetada na Entidade (que não perdeu seu estado do ECS, apenas o "cérebro" de lógica). O ECS retoma a chamada ao `Update()`, refletindo o novo código instantaneamente na tela, sem recarregar nenhum componente gráfico ou rede.
