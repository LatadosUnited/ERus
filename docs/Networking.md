# Rede, Sincronização e Colaboração (Networking)

A premissa fundamental da arquitetura do ERus é o suporte multiusuário desde a camada mais baixa. Ao invés de construir o editor para ser local e tentar adaptar a rede por cima, a rede é um cidadão de primeira classe do sistema ECS.

A rede opera primariamente em duas camadas distintas: Estado e Assets.

## 1. Camada de Sincronização de Estado (State Synchronization - UDP)

Esta camada cuida de dados mutáveis em tempo real (posição de objetos, rotações, destruição de instâncias).

- **O Protocolo:** Utiliza `LiteNetLib` rodando sobre **UDP**. Pacotes perdidos são ignorados, priorizando sempre que o último dado chegue o mais rápido possível (ótimo para atualizações constantes como de `TransformComponent`).
- **Topologia (Listen Server P2P):** Um usuário cria a sala e se torna o *Host* (a fonte da verdade). Os demais entram como *Clients*. A comunicação ocorre via `NetPeer` dentro do gerenciador principal `NetworkTransport`.
- **Entity Replication System:** O sistema de replicação da Engine. Ele converte dados de componentes para bytes (Byte Serialization Protocol). Os pacotes são definidos em classes com identificadores, como:
  - `SpawnEntityPacket` / `DestroyEntityPacket`: Ciclo de vida de entidades.
  - `TransformPacket`: Enviado a cada clique e arraste para replicar posição.
  - `EngineStatePacket` / `RenameEntityPacket`.

### 1.1 Temporal Locking (Concurrency Control)
Como múltiplas pessoas podem tentar editar a mesma entidade no mesmo momento, existe um mecanismo de bloqueio (`LockPacket` e `UnlockPacket`).
Quando um usuário "segura" um objeto para movê-lo, ele envia um Request Lock para o Host. Se deferido, o objeto fica bloqueado para os outros usuários até que ele o solte.

## 2. Camada de Sincronização de Assets (Asset Sync - TCP)

Diferente do estado que são apenas matrizes matemáticas rápidas, os Assets (Modelos 3D `.fbx`, texturas `.png`) são arquivos pesados brutos. A sincronização de estado não deve engasgar pelo download de arquivos.

- **O Protocolo:** Rodando via **TCP**, garante integridade e entrega total do arquivo pesado (usando pedaços / chunks).
- **Asset Sync Manager:** Componente central que gerencia os clientes (`AssetTcpClient`) e o servidor (`AssetTcpServer`).
- **Anúncio de Metadados (AssetAnnouncePacket):** Um Host que insere uma imagem envia apenas um "Aviso" contendo o Hash SHA-256 da imagem, nome e tamanho (via pacote UDP rápido). Os clientes verificam o Cache local; caso não exista o arquivo correspondente com o exato Hash, iniciam uma requisição de download paralelo no TCP.
- Dessa forma o editor mostra "Placeholders" e não congela as edições de Posição de outros usuários enquanto baixa as texturas no fundo.

## 3. O Mecanismo Play/Stop (Scene Snapshot)

Uma das transições mais complexas da rede do ERus é o momento em que a equipe para de "Editar" e entra no "Gameplay".
- **Snapshot Backup:** O Host captura o estado atual do `Registry` da ECS em memória (Scene Snapshot Backup).
- Comandos disparam a física (gravidade e colisões) e executam a camada de scripts locais e de lógica dos jogadores. A replicação UDP permanece ativa para sincronizar inimigos e avatares.
- **Stop (Reset):** Ao finalizar o teste, o Host restaura o *Snapshot* de memória apagando "sujeira do gameplay" (marcas de tiros, lixo de física), restaurando o ambiente do editor em perfeita sincronia com todos os peers num instante.

## 4. Limitações Arquiteturais Conhecidas

Para um ambiente de produção em escala ou internet aberta, o sistema atual possui limitações projetuais que demandam evolução:

- **Ausência de Host Migration:** A topologia P2P é puramente *Host-Authoritative* fixa. Não existe mecanismo de transferência de estado se o Host cair. Se a conexão do Host é perdida, a sessão de todos os peers é encerrada imediatamente.
- **Fricção do Temporal Locking:** O uso de *Pessimistic Locking* (Lock/Unlock) previne colisões de edição facilmente, mas cria entraves. Atualmente, se um usuário sofre queda de rede (Timeout) enquanto segura o *Lock* de uma entidade, o destravamento não ocorre (pois o evento de desconexão não limpa locks retidos), causando o travamento definitivo (deadlock) da entidade até que o pacote manual de *Unlock* seja forjado ou a sala reiniciada. Para escala, algoritmos de *Optimistic Concurrency* ou *CRDTs* (Conflict-free Replicated Data Types) seriam mais sofisticados.
- **Autenticação Inexistente:** Não há validação robusta de handshake. A conexão via `LiteNetLib` aceita requests baseados numa chave de conexão global em texto pleno (`"ERusKeys"`). Qualquer peer conhecendo o IP e a porta tem confiança total para emitir `DestroyEntityPacket`, o que restringe o uso a redes VPN ou times em total confiança (Trust-Based Environment).
