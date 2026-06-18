# ERus.Editor

O `ERus.Editor` é a aplicação desktop (frontend de autoria) utilizada pelo desenvolvedor para construir projetos e cenas. Ele importa o núcleo principal `ERus.Engine` como biblioteca e roda acoplado a ele, servindo como uma camada de abstração visual para manipulação da Engine em tempo real.

## Arquitetura da Interface Gráfica

A interface não é estática, mas baseada em um sistema de janelas gerenciado modularmente. O núcleo do controle visual se divide em:

- **`EditorUIController`**: O controlador central que coordena o ciclo de vida da UI. Ele intercepta as chamadas de renderização e injeta o pipeline da interface logo após a Engine finalizar o desenho gráfico da cena.
- **`EditorWindowManager`**: O gerenciador responsável por registrar e desenhar todas as sub-janelas ativas na sessão.
- **Sistema de Undo/Redo (`UndoSystem`)**: Implementado através do padrão *Command* (`IUndoCommand`), permite reverter ações (ex: `TransformCommand`) via `Ctrl+Z` / `Ctrl+Shift+Z`. Ao desfazer/refazer manipulações no ECS, o sistema também injeta pacotes na rede via `NetworkModule` para garantir o sincronismo com outros clientes da sessão.
- **Multi-Seleção e Pickers**: O `EditorUIController` rastreia um `HashSet<Entity>`, suportando seleção múltipla com `Ctrl+Click`. Suporta exclusão em lote (tecla `Delete`) com replicação nativa.

### Janelas Principais

O Editor é composto por várias janelas utilitárias:

- **HierarchyWindow**: Lista todas as entidades instanciadas na cena lendo o `Registry` (ECS). Permite selecionar objetos para edição.
- **InspectorWindow**: Uma interface reativa que exibe e edita os componentes (`TransformComponent`, `MeshComponent`, etc) da entidade selecionada, refletindo as alterações instantaneamente (localmente e na rede).
- **SceneView / GameView**: Painéis de visualização. Em vez de renderizar direto na tela, a Engine desenha em um *Framebuffer Offscreen*, convertendo a cena em uma textura que é apresentada dentro destas abas de ImGui.
- **ConsoleWindow**: Terminal embutido para monitoramento do sistema de logs da engine, pacotes de rede e rastreamento de erros (ex: compilação de scripts).
- **ProjectWindow**: Navegador visual de arquivos que gerencia Assets carregados e interage diretamente com o `AssetSyncManager`.

## Câmera do Editor e Gizmos

A navegação no modo de edição é desvinculada do ECS principal através de uma câmera de voo livre (`EditorCamera`). 
A interação física com objetos na cena 3D é feita através de **Gizmos** (Eixos interativos). Componentes como `GizmoInteraction` e `GizmoRenderer` cuidam da matemática complexa de raycasting (usando intersecções *Ray-OBB* via `GizmoMath`), permitindo arrastar, rotacionar ou escalar entidades. 

As mudanças feitas por Gizmos emitem automaticamente comandos em rede:
- Ação de arrastar injeta um travamento temporal (`LockPacket`) na entidade, proibindo que outros usuários a modifiquem simultaneamente.
- Alterações ativas enviam continuamente `TransformPacket`s.
- Ao finalizar o *Drag*, a ação é encapsulada em um `TransformCommand` e salva na pilha de histórico do `UndoSystem`. Adicionalmente, há suporte robusto para ferramentas de *Snapping* (configuráveis via `EditorToolbar`) em espaços *World* ou *Local*.

## Tecnologia Visual (ImGui) e Inicialização

Toda a nossa interface é gerada via **Immediate Mode GUI** usando a biblioteca `Dear ImGui` (`ImGui.NET`) acoplada ao OpenGL através da ponte de extensões do Silk.NET (`Silk.NET.OpenGL.Extensions.ImGui`). O uso do padrão imediato facilita muito a criação reativa do *InspectorWindow*, já que a interface se reconstrói a cada frame baseada puramente no estado atual da memória.

### Docking e Layout

A UI tem as flags `ImGuiConfigFlags.DockingEnable` e `ViewportsEnable` ativas, suportando que janelas sejam arrastadas para fora da janela principal do SO. Se o usuário não possuir um `imgui.ini` salvo, o `EditorUIController` tem uma configuração *hardcoded* capaz de montar automaticamente um **Layout estilo Unity**, encaixando a Scene no centro, Hierarchy à esquerda, Inspector à direita e Console/Project na base.

### O Módulo de UI (`EditorUIModule`)

Toda essa interface roda conectada à Engine principal. Durante a execução (`Program.cs`), o `EditorUIModule` é o último módulo injetado no loop da engine (`engine.AddModule(new EditorUIModule())`). Isso garante que os eventos de redes e os cálculos de simulação (`ECSModule`) sejam finalizados antes do frame ser apresentado, desenhando a UI perfeitamente por cima do viewport OpenGL.

## Problemas Conhecidos e Limitações (Trade-offs)

- **Framebuffer Resize**: O redimensionamento da janela `SceneView` às vezes distorce o Aspect Ratio se a lógica do Framebuffer não for reconstruída a cada evento de Resize.
- **Captura de Inputs (Teclado/Mouse)**: Como o ImGui lida com os eventos de mouse, há momentos em que a Engine "vaza" e processa inputs que deveriam ter sido engolidos pela interface do Editor. Precisamos de um sistema global rígido de bloqueio de Raycasts para resolver isso.
- **Hot-Reload de Scripts**: Atualmente, compilar novos scripts do usuário dentro do Editor exige reiniciar a aplicação, ou pode causar lentidão se usarmos `AssemblyLoadContext` dinamicamente sem descarregar as dependências corretamente.
