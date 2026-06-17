# Interface do Editor e Janelas (Editor & UI)

O projeto `ERus.Editor` é a aplicação de interface gráfica que envelopa a `ERus.Engine`. Todo o sistema de UI foi construído de forma modular para que janelas pudessem ser injetadas, ocultas e renderizadas usando **Immediate Mode GUI (ImGui)** através da biblioteca `ImGui.NET`.

## O Controlador: `EditorUIController`

O `EditorUIController` é a peça central da interface. Ele herda das engrenagens principais e tem acesso direto à referência global do `Engine`. Ele coordena o gerenciador de janelas (`EditorWindowManager`) e processa a fila de chamadas de desenho ImGui (como o `EditorToolbar` e `EditorNetworkMenu`) no pipeline visual (logo após a Engine finalizar a renderização dos componentes gráficos).

## Gerenciamento de Janelas: `EditorWindowManager`

A interface não é estática. Janelas são classes concretas que herdam da classe base `EditorWindow` e são registradas no gerenciador de janelas.
Algumas das principais janelas construídas na arquitetura são:

- **`HierarchyWindow`**: Lê o `Registry` da engine e lista todas as Entidades vivas (com seus IDs/Nomes). Permite selecionar entidades para inspeção.
- **`InspectorWindow`**: Janela reativa. Ao receber uma `Entity` selecionada, ela itera sobre o `Registry` buscando os componentes (ex: `TransformComponent`, `MeshComponent`). Usando ImGui, desenha os campos de edição (Vector3 Inputs, Color Pickers) para os componentes, disparando eventos que serão capturados pela rede caso modificados.
- **`SceneViewWindow` / `GameViewWindow`**: Onde o mundo 3D ganha vida na UI. Usam uma técnica de *Offscreen Rendering*, desenhando a cena não direto na tela, mas num `GLFramebuffer` (textura interna) da engine, que é então re-injetado dentro do layout do ImGui como um *Image Widget*.
- **`ConsoleWindow`**: Captura `ConsoleLog` e eventos do sistema para *tracing* de pacotes ou erros (como compilação de scripts).
- **`ProjectWindow`**: Um file-browser visual que gerencia Assets carregados e interage com o `AssetSyncManager`.

## Câmera do Editor e Interação (`EditorCamera` & Gizmos)

O usuário não está num avatar enquanto edita, mas numa **Câmera Livre** especial.

- A `EditorCamera` calcula matrizes de View/Projection desconectada do ECS de física ou do `PlayerController`, permitindo voo livre na cena (WASD + Mouse).
- **Gizmos (`GizmoInteraction`, `GizmoMath`, `GizmoRenderer`)**: Um dos sistemas matemáticos mais complexos da interface. Permite clicar em eixos coloridos sobre objetos na *SceneView* para manipular visualmente o `TransformComponent`. Toda interação (arrastar um eixo) invoca a engine, que empurra a alteração local via pacotes (`TransformPacket`), ativando também o sistema de travamento local (`LockPacket`) para que múltiplos usuários não movam a mesma aba ao mesmo tempo.
