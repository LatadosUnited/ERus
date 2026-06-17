# Core e Entity Component System (ECS)

O coração da **ERus.Engine** foi construído sob o paradigma de **Data-Oriented Design**, o que a torna diferente da maioria das engines orientadas a objetos.

## Entity Component System (ECS)

Em vez de classes pesadas herdando de "MonoBehaviour" ou "Actor", a arquitetura é perfeitamente dividida em três pilares:

### 1. Entidades (`Entity`)
Uma `Entity` não é um objeto. É estritamente um **Identificador (ID) numérico**. Na nossa implementação, uma entidade geralmente carrega um ID local e um `Network ID` para que a mesma entidade possa ser rastreada entre computadores durante a replicação de rede.

### 2. Componentes (`Registry` & `ComponentArray`)
Componentes são dados puros (Structs ou Classes simples de dados, como `TransformComponent`, `MeshComponent`). Eles não possuem lógica.
O sistema armazena todos os componentes do mesmo tipo juntos na memória em um `ComponentArray` contido dentro do gerenciador central chamado `Registry`.
- A implementação do `ComponentArray` utiliza o padrão **Sparse Set**: internamente, os dados (`T[]`) são mantidos em um **array contíguo/denso** para garantir a contiguidade de memória. Um `Dictionary` é utilizado apenas como índice secundário para mapear a `Entity ID` para o índice do array. Quando um componente é removido, o último elemento do array é movido para o espaço vago (swap-and-pop), mantendo os dados sem buracos na memória.
- Isso garante real **Cache Locality**, permitindo que a CPU processe transformações, físicas ou simulações em massa quase que instantaneamente.

### 3. Sistemas (`BaseSystem` & `ScriptExecutionSystem`)
Toda a lógica reside nos Sistemas. Um sistema pede ao `Registry` todas as entidades que possuem um determinado grupo de componentes (ex: "Me dê todas as entidades que têm Transform e Mesh") e as processa sequencialmente em um loop `.Update()`.
- **`BaseSystem`**: A classe base da qual os sistemas do motor herdam. Cada sistema é executado em uma ordem específica determinada pela inicialização da engine.
- **`ScriptExecutionSystem`**: Um sistema especializado que roda dentro do motor principal para invocar o método `Update()` dos scripts definidos em C# pelo usuário via `ERusScript`.

## Pipeline de Módulos (Module Pipeline)

Para manter a base de código do `Engine.cs` pequena e permitir expansibilidade total, a inicialização e o ciclo de vida são separados em **Módulos**.

Um módulo deve implementar a interface `IEngineModule`, que geralmente provê métodos como `Initialize()`, `Update()`, e `Dispose()`.

Exemplos de módulos injetados na Engine:
- `ECSModule`: Onde o `Registry` é criado.
- `NetworkModule`: Carrega o `NetworkPacketDispatcher` e gerencia a conexão com o `NetworkTransport`.
- `GraphicsModule`: Instancia a janela do OpenGL (`Silk.NET`), carrega os shaders e chama a renderização de malhas (`Mesh Component`).
- `EditorUIModule` (apenas injetado se estiver rodando via Editor): Assume o controle da tela de UI após o frame gráfico principal, usando o `EditorUIController`.
- `ScriptModule`: O coração de carregamento do código de usuário (Roslyn / `AssemblyLoadContext`).

O ciclo da engine (o "Game Loop") em um frame chama o `Update()` sequencialmente de cada módulo registrado, garantindo uma execução em blocos lógicos rígidos.

## Sincronização e Estado

Um dos focos desta arquitetura ECS é tornar fácil a replicação de dados em rede. Sendo componentes apenas structs de dados (sem métodos ocultos ou loops escondidos), serializar um `TransformComponent` para enviá-lo via rede pelo `EntityReplicationSystem` é tão simples quanto convertê-lo diretamente numa `byte array`. Isso se integra perfeitamente com a *State Synchronization (UDP Layer)* descrita no módulo de Rede.
