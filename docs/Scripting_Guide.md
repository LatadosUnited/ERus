# ERus Engine - Guia de Scripting

Este documento explica como escrever códigos e utilizar a API da ERus Engine para dar vida aos seus jogos.

## A Classe `ERusScript`

Todo script criado pelo usuário deve herdar da classe `ERusScript`. Essa classe fornece acesso ao ciclo de vida da entidade e aos sistemas principais da engine.

### Ciclo de Vida (Callbacks)

Você pode sobrescrever os seguintes métodos para executar lógica em momentos específicos:

*   **`Awake()`**: Chamado uma única vez quando o script é instanciado. Útil para inicializações independentes.
*   **`Start()`**: Chamado no primeiro frame em que o script está ativo (após o Awake). Ideal para buscar referências a outros objetos.
*   **`Update()`**: Chamado a cada frame. Coloque aqui a lógica principal, como movimentação, leitura de input e inteligência artificial.
*   **`OnDestroy()`**: Chamado quando a entidade é destruída ou o modo Play é encerrado. Use para limpar recursos.

### Propriedades Disponíveis

A classe `ERusScript` injeta automaticamente algumas propriedades úteis para uso dentro dos callbacks:

*   **`Entity`**: Retorna a estrutura `Entity` que representa a entidade dona deste script.
*   **`Registry`**: O orquestrador do ECS. Permite manipular outras entidades e componentes.
*   **`DeltaTime`**: O tempo em segundos decorrido desde o último frame (use no `Update()` para manter movimentos consistentes indepedente do framerate).
*   **`Transform`**: Um atalho direto para modificar a Posição, Rotação e Escala da entidade.
*   **`ScreenSize`**: Um atalho (`Vector2`) que retorna a resolução atual do painel do GameView no Editor, útil para cálculos de UI e Raycast.
*   **`MainCamera`**: Um atalho que retorna a entidade (`Entity?`) que possui o `CameraComponent` primário da cena atual. Retorna `null` se nenhuma câmera estiver configurada.

### Logs e Debugging

Use as funções abaixo para imprimir mensagens no Console do Editor:
*   `Log("Mensagem")`
*   `LogWarning("Aviso")`
*   `LogError("Erro grave")`

---

## Modificando o Transform

Você pode acessar e modificar a posição da entidade diretamente pelo atalho `Transform`:

```csharp
public override void Update()
{
    // Move a entidade no eixo X em 5 unidades por segundo
    Transform.Position.X += 5f * (float)DeltaTime;
    
    // Gira a entidade no eixo Y
    Transform.Rotation.Y += 1f * (float)DeltaTime;
}
```

> **Aviso:** Sempre que modificar `Position`, `Rotation` ou `Scale`, a engine marcará o Transform como *sujo* (dirty) e sincronizará a nova posição com a simulação de Física.

---

## Lendo Inputs do Usuário

O sistema de Input da ERus é baseado em "Profiles", "Maps" (Mapas) e "Actions" (Ações). Para ler o input, você deve buscar a `InputAction` desejada através da classe estática `Input`.

### Criando o arquivo `input_profile.json` manualmente

Embora o Editor possua uma interface gráfica para gerenciar os inputs (em `Window > Input Mapping`), você pode criar ou editar o arquivo de mapeamento manualmente criando um arquivo chamado `input_profile.json` na raiz do seu projeto.

Exemplo de estrutura JSON:

```json
{
  "Maps": [
    {
      "Name": "Player",
      "IsActive": true,
      "Actions": [
        {
          "Name": "Jump",
          "Type": "Button",
          "Bindings": [
            {
              "Source": "Keyboard",
              "KeyTarget": "Space",
              "TargetComponent": "Button"
            }
          ]
        },
        {
          "Name": "Move",
          "Type": "Axis2D",
          "Bindings": [
            {
              "Source": "Keyboard",
              "KeyTarget": "W",
              "TargetComponent": "PositiveY"
            },
            {
              "Source": "Keyboard",
              "KeyTarget": "S",
              "TargetComponent": "NegativeY"
            },
            {
              "Source": "Keyboard",
              "KeyTarget": "D",
              "TargetComponent": "PositiveX"
            },
            {
              "Source": "Keyboard",
              "KeyTarget": "A",
              "TargetComponent": "NegativeX"
            }
          ]
        }
      ]
    }
  ]
}
```

> **Dica**: Os valores aceitos em `KeyTarget` são os nomes presentes no enum `Silk.NET.Input.Key`. Para botões do mouse, utilize a propriedade `MouseTarget` com os valores de `Silk.NET.Input.MouseButton`.

---


### 1. Buscando uma Ação

É uma boa prática buscar as referências das ações no método `Start()` e armazená-las em variáveis para evitar buscas repetitivas no `Update()`.

```csharp
using ERus.Engine.Input;

public class PlayerController : ERusScript
{
    private InputAction _jumpAction;
    private InputAction _moveAction;

    public override void Start()
    {
        // Pega a ação "Jump" dentro do mapa "Player"
        _jumpAction = Input.GetAction("Player", "Jump");
        _moveAction = Input.GetAction("Player", "Move");
    }
}
```

### 2. Verificando Botões (Actions do tipo Button)

Para ações que são apenas botões (ex: Pulo, Tiro), utilize os seguintes métodos da `InputAction`:

*   `IsPressed()`: Retorna `true` enquanto o botão estiver sendo segurado.
*   `WasPressedThisFrame()`: Retorna `true` apenas no frame em que o botão foi apertado.
*   `WasReleasedThisFrame()`: Retorna `true` apenas no frame em que o botão foi solto.

```csharp
public override void Update()
{
    if (_jumpAction != null && _jumpAction.WasPressedThisFrame())
    {
        Log("Pulou!");
    }
}
```

### 3. Lendo Eixos (Actions do tipo Axis2D)

Para ações configuradas como `Axis2D` (como WASD ou Analógicos), você pode ler um vetor de direção:

```csharp
public override void Update()
{
    if (_moveAction != null)
    {
        var direction = _moveAction.ReadVector2(); // Retorna um Vector2D<float> (-1 a 1)
        Transform.Position.X += direction.X * 5f * (float)DeltaTime;
        Transform.Position.Z += direction.Y * 5f * (float)DeltaTime;
    }
}
```

### 4. Lendo a Posição do Mouse na Tela

Se você precisar saber onde o cursor está na tela (por exemplo, para interface de usuário, point-and-click ou Raycasting 3D), você pode acessar a propriedade estática `MousePosition` diretamente na classe `Input`. Ela retorna a posição (X, Y) do mouse em relação ao canto superior esquerdo do GameView do Editor (ou da janela do jogo standalone).

```csharp
public override void Update()
{
    var mousePos = ERus.Engine.Input.Input.MousePosition;
    
    // Mostra as coordenadas da tela no console quando a lógica exigir
    if (_jumpAction != null && _jumpAction.WasPressedThisFrame()) 
    {
        Log($"Mouse clicou em X: {mousePos.X}, Y: {mousePos.Y}");
    }
}
```

---

## Acesso a Componentes (ECS Registry)

Você pode acessar qualquer componente atrelado à sua entidade (ou outras entidades) utilizando a propriedade `Registry`.

### Verificando e Obtendo Componentes

```csharp
using ERus.Engine.ECS;

public override void Start()
{
    // Verifica se a entidade tem um RigidBodyComponent
    if (Registry.HasComponent<RigidBodyComponent>(Entity))
    {
        // Obtém o componente como referência (ref)
        ref var rb = ref Registry.GetComponent<RigidBodyComponent>(Entity);
        rb.Mass = 10f;
    }
}
```

> **Importante:** Os métodos `GetComponent<T>` retornam por **referência (`ref`)**. Se você omitir o `ref` na hora de declarar a variável local, fará uma cópia do componente e as alterações não surtirão efeito. Use sempre `ref var meuComponente = ref Registry.GetComponent...`

---

## Interagindo com a Física (RigidBody)

Entidades com `RigidBodyComponent` são simuladas pela engine (Jitter2).

### Lendo Propriedades Físicas

O sistema de física atualiza automaticamente as propriedades do componente. Você pode ler a velocidade linear e angular atual da entidade:

```csharp
public override void Update()
{
    if (Registry.HasComponent<RigidBodyComponent>(Entity))
    {
        ref var rb = ref Registry.GetComponent<RigidBodyComponent>(Entity);
        var velocidadeLinear = rb.LinearVelocity;
        
        if (velocidadeLinear.Length > 10f)
        {
            Log("Estou muito rápido!");
        }
    }
}
```

### Modificando a Física (Aplicando Forças)

Para aplicar forças ou modificar a velocidade diretamente, você precisa acessar o corpo interno (`InternalBody`) da biblioteca Jitter2.

```csharp
using Jitter2.Dynamics; // Necessário para acessar o RigidBody do Jitter2
using Jitter2.LinearMath;

public override void Update()
{
    if (Registry.HasComponent<RigidBodyComponent>(Entity))
    {
        ref var rb = ref Registry.GetComponent<RigidBodyComponent>(Entity);
        
        // Verifica se a física já inicializou o corpo interno e se não é cinemático
        if (rb.InternalBody != null && !rb.IsKinematic)
        {
            var jitterBody = (RigidBody)rb.InternalBody;
            
            // Exemplo 1: Alterando a velocidade diretamente
            if (_jumpAction != null && _jumpAction.WasPressedThisFrame())
            {
                jitterBody.Velocity = new JVector(0, 10f, 0); // Pulo manual
            }
            
            // Exemplo 2: Aplicando uma força contínua
            // jitterBody.AddForce(new JVector(5f, 0, 0));
        }
    }
}
```

## Criando e Modificando Entidades no Código

Você pode criar novas entidades e anexar componentes dinamicamente em tempo de execução através do `Registry`.

```csharp
using ERus.Engine.ECS;
using Silk.NET.Maths;

public override void Start()
{
    // 1. Criar uma Entidade em branco
    Entity novaEntidade = Registry.CreateEntity();
    
    // 2. Adicionar o Transform básico
    ref var transform = ref Registry.AddComponent<TransformComponent>(novaEntidade);
    transform.Position = new Vector3D<float>(0, 5, 0); // Nasce no ar
    
    // 3. Adicionar uma malha primitiva (Cubo)
    ref var mesh = ref Registry.AddComponent<MeshComponent>(novaEntidade);
    mesh.Type = PrimitiveMeshType.Cube;
    
    // 4. Adicionar Física para ela cair (opcional)
    ref var rb = ref Registry.AddComponent<RigidBodyComponent>(novaEntidade);
    rb.Mass = 5f;
    Registry.AddComponent<BoxColliderComponent>(novaEntidade);
}
```

---

## Verificando o Estado da Rede (Multiplayer)

A ERus possui uma arquitetura cliente/servidor nativa. Se o seu jogo for multiplayer, muitas vezes um script só deve rodar lógica (como spawnar inimigos ou receber dano) se estiver rodando no Host (Servidor). 

Você pode acessar o `NetworkModule` através da propriedade `Engine`:

```csharp
using ERus.Engine.Modules;

public override void Update()
{
    var netModule = Engine.GetModule<NetworkModule>();
    
    // Verifica se a engine está rodando como Servidor (Host)
    if (netModule != null && netModule.NetworkManager.IsHost)
    {
        // Lógica de servidor: gerenciar IA, validar danos, etc.
        Transform.Position.X += 2f * (float)DeltaTime;
    }
    else
    {
        // Lógica de cliente puro (ou apenas exibir efeitos visuais)
    }
}
```

---

## Resumo de Dicas
- Guarde as instâncias de `InputAction` no `Start()`.
- Use `ref` ao resgatar componentes do `Registry` para editá-los de verdade e não alterar apenas uma cópia local.
- Para movimentações simples sem física, altere o `Transform.Position`.
- Para personagens complexos simulados por física, modifique a `Velocity` ou adicione forças acessando o `InternalBody` (via `Jitter2.Dynamics.RigidBody`) do `RigidBodyComponent`.
- Use `Engine.GetModule<NetworkModule>().NetworkManager.IsHost` para proteger lógica autoritativa de rede.
