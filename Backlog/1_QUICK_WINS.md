# Nível 1: Vitórias Rápidas (Quick Wins)
*Alto Impacto, Baixo a Médio Esforço*

Essas tarefas trazem grandes benefícios imediatos para o usuário do editor com um esforço de implementação relativamente baixo.

### 1. Primitivas Geométricas e Colliders Básicos
- **Objetivo:** Adicionar maior variedade de formas geométricas e seus respectivos colliders.
- **Tarefas:**
  - Instanciar formas: Cube, Sphere, Capsule, Cylinder, Plane, Quad.
  - Implementar e integrar o `BoxCollider`, `SphereCollider` e componentes de colisão para as outras formas.

### 2. Câmera do Editor e Navegação
- **Focar (Tecla F):** Fazer a câmera centralizar e aproximar a entidade selecionada.
- **Alternar Perspectiva:** Botão para mudar entre câmera Perspectiva e Ortográfica.

### 3. Atalhos e Múltiplas Seleções
- **Duplicação:** Atalho (Ctrl+D) para clonar a entidade selecionada na cena/hierarchy.
- **Seleção Múltipla:** Possibilitar o uso de `Ctrl` ou `Shift` para selecionar, mover e editar várias entidades simultaneamente.

### 4. Snapping de Transformação
- Opção para mover, rotacionar ou escalar objetos na cena travando os valores em uma grade configurável (grid snapping).

### 5. Gizmos e Visualização de Debug
- Implementar a renderização visual no Editor de áreas invisíveis, como as linhas delimitadoras de Box Colliders, Sphere Colliders, caminhos, pontos de respawn, etc.

### 6. Console de Logs Integrado
- Criar uma aba de "Console" no editor para exibir erros, alertas e logs (mensagens impressas pelos scripts), facilitando o debug sem depender do terminal do sistema operacional.

### 7. Otimização de Banda e Network Tick Rate (Rede)
- Separar o envio de pacotes de rede do Framerate (FPS) da engine, implementando uma taxa fixa de envio (ex: 20Hz ou 30Hz) para evitar saturação de banda.
- Aplicar quantização e compressão nos valores numéricos (enviar Posições em Half-floats e Rotações em Quaternions menores em vez de floats inteiros) para a replicação das entidades.
