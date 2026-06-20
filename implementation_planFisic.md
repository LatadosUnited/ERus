# Implementação de Sistema de Física (ECS)

O objetivo é integrar um motor de física ao ecossistema da **ERus.Engine**, utilizando a arquitetura Entity Component System (ECS) orientada a dados do projeto. 

## User Review Required

> [!IMPORTANT]
> A implementação de um motor de física afeta profundamente o cálculo de transformações (`TransformComponent`). A física será processada no laço do sistema (`BaseSystem`), e as respostas do motor de física irão sobrescrever/ditar os valores do `TransformComponent` para entidades que contenham `RigidBodyComponent` configurados como dinâmicos (sujeitos a gravidade e colisões).

## Open Questions

> [!WARNING]
> **Qual motor de física devemos usar?**
> A arquitetura permite o acoplamento de bibliotecas de terceiros via pacote NuGet. Como as coordenadas do projeto usam `Vector3D<float>` (Silk.NET), temos as seguintes recomendações para ambiente 3D:
> 1. **(Recomendado) JitterPhysics2**: Integração mais limpa com a estrutura atual de ECS, excelente desempenho e API simples de acoplar aos nossos componentes sem exigir gerenciamento extremo de memória. 
> 2. **BepuPhysics v2**: Focado em altíssima performance, porém requer alocações especiais de memória e ponteiros, o que conflita com algumas premissas do nosso atual `ComponentArray<T>`.
> 3. **Motor customizado**: Expandir as colisões primitivas atuais para detecção de colisão manual sem suporte avançado a corpos rígidos interativos.
>
> Você está de acordo em usarmos o pacote **Jitter2** para a base inicial da física?

## Proposed Changes

A arquitetura seguirá a mesma estrutura de módulos existente (`NetworkModule`, `GraphicsModule`, etc).

### ERus.Engine (Módulo e Dependências)

#### [MODIFY] [ERus.Engine.csproj](file:///e:/Projetos/ERus/ERus.Engine/ERus.Engine.csproj)
- Adição do `PackageReference` da biblioteca de física escolhida.

#### [NEW] [PhysicsModule.cs](file:///e:/Projetos/ERus/ERus.Engine/Modules/PhysicsModule.cs)
- O orquestrador de física que implementa `IEngineModule`. Inicializa o mundo da física (gravity, config) e encerra instâncias no `Dispose()`.
- **(NOVO) Raycasting:** Exporá métodos utilitários globais, como `bool Raycast(Vector3D origin, Vector3D direction, out RaycastHit hitInfo, float maxDistance)`, delegando para a consulta da biblioteca de física subjacente.

### ERus.Engine (ECS - Componentes e Sistemas)

#### [NEW] [RigidBodyComponent.cs](file:///e:/Projetos/ERus/ERus.Engine/ECS/RigidBodyComponent.cs)
- Um componente de dados puro (`IComponent`) contendo as propriedades base de física: massa, flag `IsKinematic` (estático/imóvel), travamento de eixos (constraints de posição/rotação) e rastreador da biblioteca de física.

#### [NEW] [ColliderComponent.cs](file:///e:/Projetos/ERus/ERus.Engine/ECS/ColliderComponent.cs)
- Componente para armazenar o formato de colisão (caixa, esfera, cápsula), atrito e elasticidade (restitution).
- **(NOVO) IsTrigger:** Flag que indica se este colisor é apenas um gatilho fantasma (não causa repulsão física, apenas dispara um evento quando interceptado).

#### [NEW] [JointComponent.cs](file:///e:/Projetos/ERus/ERus.Engine/ECS/JointComponent.cs)
- **(NOVO)** Componente para criar amarras entre duas entidades (ex: HingeJoint para portas, FixedJoint, SpringJoint). Conecta o RigidBody atual ao RigidBody de outra entidade.

#### [NEW] [CollisionEvent.cs](file:///e:/Projetos/ERus/ERus.Engine/ECS/CollisionEvent.cs)
- **(NOVO)** Uma estrutura de dados para representar um evento de colisão (`EntityA`, `EntityB`, `ImpactPoint`, `Normal`). Ideal para ser processado por um sistema de Eventos ou despachado diretamente para o `ScriptModule`.

#### [NEW] [PhysicsSystem.cs](file:///e:/Projetos/ERus/ERus.Engine/ECS/PhysicsSystem.cs)
- Um sistema que herda de `BaseSystem`. A cada frame executará:
  1. Adição/Atualização de corpos recém criados na engine para o "mundo" da física.
  2. Execução do passo de simulação via `World.Step(deltaTime)`.
  3. Sincronização dos resultados da simulação de volta para os `TransformComponent`.
  4. **(NOVO) Captura de Callbacks:** Registrará os *callbacks* de colisão (e triggers) gerados no passo atual do motor e irá despachá-los para a camada de Scripts da ERus na forma de `CollisionEvent`.

### ERus.Editor (Injeção do Sistema)

#### [MODIFY] [Program.cs](file:///e:/Projetos/ERus/ERus.Editor/Program.cs)
- Adicionar o registro do módulo (`engine.AddModule(new PhysicsModule())`) no setup da esteira do loop do Editor, logo no começo do ciclo do ECS.

## Verification Plan

### Manual Verification
- Ao aplicarmos a mudança, instanciar via Editor:
  1. Um "Chão" estático e um "Cubo" dinâmico com massa para validar queda e repulsão (modificando o `TransformComponent` automaticamente).
  2. **(NOVO)** Um colisor marcado como `IsTrigger` para validar que o evento de colisão é disparado na console sem impedir o movimento do corpo rígido que passou por ele.
  3. **(NOVO)** Disparar um *Raycast* da câmera até o chão do Editor usando o mouse para pintar o ponto de contato (validar o utilitário de *Mouse Picking* do `PhysicsModule`).
