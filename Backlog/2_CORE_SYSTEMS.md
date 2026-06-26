# Nível 2: Sistemas Centrais (Core Systems)
*Alto Impacto, Alto Esforço*

Funcionalidades fundamentais que exigem uma arquitetura mais complexa, mas são obrigatórias para considerar a engine "completa".

### 1. Rigidbody & Physics Materials
- Implementar controle completo sobre propriedades físicas: gravidade, massa, arrasto (drag), atrito (friction) e ressalto (bounciness).
- Adicionar Constraints Físicas (travar eixos de rotação ou posição).

### 2. Mesh Collider
- **Objetivo:** Suportar colisões complexas baseadas na malha 3D do objeto.
- **Tarefas:**
  - Adicionar o componente `MeshCollider` para possibilitar colisões que seguem a geometria exata de modelos 3D customizados.

### 3. Raycasting Avançado
- Possibilitar disparos de "raios" a partir do mouse (para seleção de objetos na tela) ou através de código (para mecânicas de jogo, linha de visão, etc).

### 4. Triggers (Gatilhos)
- Criar a funcionalidade para que Colliders atuem como gatilhos (disparando eventos como `OnTriggerEnter` ou `OnTriggerExit` sem bloquear o movimento físico de outros objetos).

### 5. Sistema de UI in-game
- **Objetivo:** Permitir a criação de interfaces de usuário (menus, HUDs) diretamente pela engine.
- **Tarefas:**
  - Iniciar a implementação de sistemas e componentes de UI para os jogos.

### 6. Sistema de Prefabs
- Permitir transformar qualquer entidade configurada da Hierarchy em um arquivo reutilizável (`.prefab`) salvo no Project View para instanciação fácil.

### 7. Sistema de Undo/Redo (Desfazer/Refazer)
- Implementar histórico de ações (Ctrl+Z / Ctrl+Y) para reverter transformações de entidades, exclusões ou alterações de valores no Inspector.

### 8. Sistema de RPCs e SyncVars (Rede)
- Abstrair a comunicação cliente-servidor para os Scripts C# customizados do usuário.
- Adicionar marcações (como `[SyncVar]` para variáveis e `[ServerRpc]`/`[ClientRpc]` para métodos) facilitando a criação e sincronização da lógica nativa de jogo (ex: dano, inputs, pontuação).

### 9. Anti-Jitter Avançado e Snapshot Interpolation (Rede)
- Substituir o `Lerp` básico da replicação por buffers circulares baseados no tempo do servidor e interpolação de snapshots.
- Aplicar dead reckoning (extrapolação de velocidade) para garantir que o movimento visual das entidades seja fluído e suave, mesmo sob perdas de pacote ou variações de ping.
