# Gráficos, Renderização e Assets (Graphics & Assets)

O sistema gráfico e o pipeline de assets (recursos) da ERus Engine foram construídos para serem de alta performance, desacoplados e prontos para sincronização.

## O Módulo Gráfico (`GraphicsModule`)

O `GraphicsModule` é inicializado no momento em que a `Engine` sobre, conectando-se ao back-end da API gráfica. 

- **Back-end e API:** Utiliza o framework **Silk.NET**, provendo acesso direto a chamadas base do **OpenGL** (GL) de forma otimizada usando bindings para C#.
- **SceneRenderer:** Este é o coração do loop de renderização (pipeline de vetorização de vértices/malhas). Em vez de renderizar entidades num grande loop orientado a objetos, o SceneRenderer funciona quase como um Sistema (BaseSystem) que busca entidades contendo `MeshComponent` e componentes ligados a `Material` ou `Texture`.
- **Renderização Offscreen (`GLFramebuffer`):** O projeto emprega uma técnica avançada de renderização offscreen. O mundo do jogo/cena não é renderizado na tela (swap chain principal) diretamente, mas sim dentro de um `GLFramebuffer` oculto (uma textura contendo cor e profundidade/depth buffer). Isso permite que a Janela de Visualização (`SceneViewWindow` ou `GameViewWindow`) do ImGui desenhe esta textura em qualquer lugar do Editor, simulando painéis destacáveis, sem travar o motor principal.

## Componentes Gráficos (ECS)

No mundo de **Data-Oriented Design**, o que dá forma ao mundo 3D é o **`MeshComponent`**.
- Ele referencia um objeto `Mesh` estático, e através das matrizes mantidas no `TransformComponent` (Matrix4x4) da mesma entidade, o `SceneRenderer` monta as matrizes de mundo, câmera (`EditorCamera` na edição) e projeção (MVP) repassando para o Shader do OpenGL desenhar na tela.

## Asset Pipeline (`AssetManager` & `Assimp`)

Diferente do estado leve do ECS, meshes e texturas são pesados. O pipeline atua carregando arquivos raw em estruturas otimizadas em memória:

- **Assimp (Open Asset Import Library):** Uma biblioteca canivete suíço usada pela engine para carregar modelos tridimensionais complexos de formatos como `.obj` ou `.fbx`.
- **`AssetManager`:** É o dicionário central de assets (`Dictionary<string, object>`). Quando a engine solicita a textura "wood_floor", ela não carrega do disco de novo; ela busca no cache em RAM do `AssetManager`.
- **A Relação com a Rede (`AssetSyncManager`):** Se a engine requisita um recurso ao `AssetManager` que não foi encontrado no disco do cliente (porque outro usuário acabou de importá-lo), um sistema de *Placeholder* visual temporário assume, enquanto o componente `AssetSyncManager` abre portas TCP dedicadas em background para iniciar o download direto do host via protocolo de arquivos. Isso está bem documentado na seção [Networking](Networking.md).
