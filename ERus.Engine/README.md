# ERus.Engine

A `ERus.Engine` é o tempo de execução (runtime) do ecossistema. Todo jogo exportado usará este projeto para gerenciar o loop principal e executar as lógicas do jogo. O núcleo da engine em si (`Core/Engine.cs`) é completamente agnóstico e "cego" às implementações: ele utiliza a biblioteca `Silk.NET.Windowing` para abstrair a janela do SO e atua apenas como um orquestrador de uma esteira de módulos.

## Arquitetura: ECS e Módulos

A Engine é estruturada inteiramente sobre o padrão **Entity Component System (ECS)** focado em Data-Oriented Design. 
Nenhuma entidade possui comportamento ou herança própria (`Entity` é apenas uma *struct* contendo um `int ID`). O estado real fica contido em **Componentes** puros de dados (ex: `TransformComponent`), que são armazenados em arrays genéricos contíguos (`ComponentArray<T>`). Os **Sistemas** invocam métodos de busca (`View<T1, T2>()`) sobre o `Registry` a cada frame para processar entidades que casam com a assinatura de componentes requerida.

Para gerenciar o ciclo de vida, estendemos a Engine via injeção de **Módulos** (`IEngineModule`). A ordem de cadastro dita a ordem de Update/Render. Exemplos:
- `GraphicsModule`: Lida com OpenGL, Buffers, Texturas via extensões do Silk.NET e invoca o `SceneRenderer`.
- `NetworkModule`: Lida com pacotes UDP/TCP. Inclui o `EntityReplicationSystem` que observa alterações no ECS e replica via LiteNetLib.
- `ScriptModule`: Responsável pelo Hot-Reload de lógicas do usuário. Utiliza o pipeline de compilação do **Roslyn (`Microsoft.CodeAnalysis.CSharp`)** para transformar `.cs` brutos em *SyntaxTrees* e emiti-los para a memória. O segredo do reload sem reiniciar a engine é o uso de instâncias de `CollectibleAssemblyLoadContext`, que permitem que Assemblies antigos sejam descarregados pelo Garbage Collector.
- `ECSModule`: O registro central (`Registry`) que executa a simulação física e sistemas internos.

## Problemas Conhecidos e Limitações (Trade-offs)
- **Física Básica**: O suporte a Rigidbodies 3D ainda é embrionário. O sistema de colisão atual é dependente de colisões primitivas customizadas e requer integração com uma engine de física robusta (como JitterPhysics ou BepuPhysics).
- **Alocação de Memória no ECS**: Algumas iterações no ECS ainda não são 100% *cache-friendly*. Precisamos substituir a alocação de classes por `structs` armazenadas de forma contígua na memória (`struct arrays`) para evitar pausas longas do Garbage Collector.
- **Limitações de Culling no Renderizador**: Atualmente renderizamos toda a cena sem Frustum Culling avançado. Em cenas imensas, isso vai gerar lentidão.
