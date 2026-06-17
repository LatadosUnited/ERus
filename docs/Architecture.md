# Arquitetura do ERus Engine

O ERus é uma engine e editor colaborativo em tempo real baseado em **Entity Component System (ECS)**. A arquitetura foi desenhada em torno de modularidade e replicação de estado em rede para permitir que múltiplos usuários editem a mesma cena simultaneamente de forma distribuída (P2P).

## Visão Geral dos Projetos

A solução está dividida em 3 partes principais:

1. **`ERus.Engine` (O Núcleo):** Biblioteca base que contém toda a lógica de simulação. Aqui residem o sistema ECS (Registry), gráficos via OpenGL (Silk.NET), simulação física, módulos de scripting (Roslyn) e o principal: o código de rede responsável por espelhar o estado das entidades (via LiteNetLib e UDP) e transferir arquivos brutos (TCP).
2. **`ERus.Editor` (A Interface de Edição):** Aplicativo construído sobre a `ERus.Engine` que provê a interface para o criador usando `ImGui.NET`. Ele expõe ferramentas visuais (Inspector, Hierarchy, Scene View) permitindo manipular a Engine em tempo real.
3. **`ERus.Hub`:** Ponto de entrada (Launcher) do projeto, usado para gerenciar e abrir diferentes projetos ERus, configurar perfis e gerenciar conexões de rede iniciais antes de carregar o editor propriamente dito.

## Os "God Nodes" (Pilares da Arquitetura)

De acordo com o mapeamento estrutural (Graphify), a base do projeto se sustenta nestes cinco pilares abstratos (as classes mais conectadas de toda a engine):

1. **`EntityReplicationSystem`:** O coração colaborativo. Responsável por capturar mudanças nos componentes (Delta Replication) usando o sistema ECS e transmiti-las instantaneamente via UDP para os pares conectados. Usa um sistema de "Temporal Locking" para que duas pessoas não editem o mesmo objeto concorrentemente.
2. **`EditorUIController`:** O gerente da interface. Ele coordena o ciclo de vida de todas as janelas (Panels) construídas em ImGui, intercepta interações do usuário e invoca comandos sobre a cena da engine.
3. **`ScriptModule` (e `ScriptExecutionSystem`):** O pipeline de lógica do usuário. Este módulo usa `AssemblyLoadContext` e compiladores C# para carregar scripts em tempo real, anexá-los a entidades e invocar métodos como `.Update()` sem precisar reiniciar a engine.
4. **`AssetSyncManager`:** A ponte de recursos pesados. Ao contrário do estado que viaja via UDP, modelos 3D, texturas e sons viajam por este gerenciador via TCP garantindo que arquivos importados por um host sejam baixados pelos clientes, inclusive lidando com validações de Hash (SHA-256).
5. **`Registry`:** O banco de dados em memória. A implementação pura do conceito ECS. O `Registry` armazena as entidades e garante que todos os dados de componentes fiquem alinhados de forma eficiente (Data-Oriented Design).

## Fluxo Principal de Execução

1. **Inicialização (`Program.cs` do Editor):** A aplicação inicializa o ambiente gráfico (Silk.NET) e cria uma instância principal do `Engine`.
2. **Registro de Módulos:** Os módulos (`IEngineModule`) são cadastrados. Esses módulos definem extensões ao comportamento básico do `Engine` (ex: `EditorUIModule`, `ScriptModule`, `NetworkModule`, `GraphicsModule`).
3. **Loop Principal (Update & Render):** 
   - A engine processa os eventos de rede (Network Packets).
   - O ECS atualiza os sistemas lógicos (`BaseSystem.Update`).
   - Os módulos processam suas lógicas locais.
   - O estado final dos componentes é enviado para renderização.
   - A UI (ImGui) é desenhada por cima do framebuffer e apresentada na tela.
   - Se houver mudanças locais a serem replicadas, o `EntityReplicationSystem` e o `AssetSyncManager` empurram essas mudanças para a rede.

---
Para entender melhor partes específicas, navegue:
- [Núcleo e ECS (Core_and_ECS.md)](Core_and_ECS.md)
- [Rede e Colaboração (Networking.md)](Networking.md)
- [Interface do Editor (Editor_and_UI.md)](Editor_and_UI.md)
- [Gráficos e Assets (Graphics_and_Assets.md)](Graphics_and_Assets.md)
