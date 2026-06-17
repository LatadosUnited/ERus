Aqui está a especificação técnica completa de como essa arquitetura deve ser desenhada sobre o sistema ECS (Entity Component System) da engine:

1. Camada de Sincronização de Estado (Tempo Real / UDP)
Essa camada cuida de tudo o que se move, altera de tamanho, muda de cor ou é deletado na cena. Ela precisa ser instantânea.

O Fluxo baseado em ECS
Como a Stride organiza tudo em Entidades e Componentes, nós não sincronizamos o "objeto", sincronizamos os dados dos componentes.

Identificador Único Global (Network ID): Cada entidade criada na cena recebe um ID de rede único de 4 bytes (ex: Entidade_Mesa = #1024). Esse ID é idêntico nos 3 computadores conectados.

A Captura de Mudanças (Delta Replication): A engine monitora os componentes. Se o Usuário A arrastar uma parede, o TransformComponent (que guarda Posição, Rotação e Escala) daquela entidade muda.

Serialização em Bytes: A engine transforma esses dados brutos em uma array de bytes enxuta. Em vez de enviar o objeto inteiro, ela envia apenas:
[NETWORK_ID: 1024] [COMPONENT_TYPE: TRANSFORM] [POSITION: X, Y, Z] [ROTATION: X, Y, Z, W]

O Protocolo de Transporte: Utiliza UDP (através de bibliotecas C# de alta performance como LiteNetLib ou ENet-CSharp). Se um pacote de posição for perdido no caminho, não importa, porque o próximo pacote que chegar em milissegundos já trará a posição atualizada.

Sistema de Concorrência: Tranca Temporal (Locking)
Para evitar que dois usuários movam o mesmo objeto ao mesmo tempo e gerem "teletransportes" ou bugs visuais:

Quando o Usuário A clica para selecionar um objeto, a engine envia um pacote imediato de alta prioridade: [LOCK_REQUEST] [ENTITY: 1024] [USER: A].

O Host valida e avisa os outros dois usuários. Na tela dos Usuários B e C, o objeto ganha uma borda colorida e fica temporariamente "bloqueado para edição".

Quando o Usuário A solta o objeto, o comando [UNLOCK] é disparado, liberando a entidade para o grupo.

2. Camada de Sincronização de Assets (Arquivos / TCP)
Esta camada cuida de arquivos brutos que não existiam no projeto (modelos .fbx, .obj, texturas .png, .jpg, áudios). Ela prioriza a integridade do arquivo em vez da velocidade.

O Fluxo de Injeção Dinâmica
Importação Local: O Usuário A arrasta uma textura de madeira para o editor. A engine local importa o arquivo e gera um Hash SHA-256 único baseado nos bytes do arquivo (ex: hash: 4f7e...).

Anúncio de Metadados: O Usuário A envia um pacote UDP leve para os outros usuários dizendo: "Criei o Asset de textura com o Hash 4f7e, tamanho 5MB, nome 'Madeira.png'".

Checagem de Existência: As engines dos Usuários B e C verificam suas pastas locais de cache. Se elas não tiverem nenhum arquivo com o hash 4f7e, elas entram na fila de download.

Transferência em Background (TCP Dedicado): Um canal de comunicação TCP em segundo plano (uma thread separada para não travar a renderização do jogo) é aberto entre quem precisa do arquivo e quem o possui (o Usuário A ou o Host). O arquivo é transferido em pedaços (chunks).

Substituição Visual (Placeholder Runtimes): Enquanto o download de 5MB acontece, os Usuários B e C veem o objeto na tela com uma textura padrão cinza quadriculada escrita "Carregando...". Assim que o download via TCP termina e o hash é validado, a Stride injeta a textura real no material em tempo de execução, mudando o visual do objeto instantaneamente para todos.

3. Topologia de Rede: Listen Server (Host-Client)
O projeto funcionará de forma descentralizada comercialmente, rodando direto na máquina dos usuários.

[ Usuário B (Client) ] <--- UDP (Estado) / TCP (Assets) ---> [ Usuário A (Host) ]
                                                                   ^
                                                                   |  UDP / TCP
                                                                   v
                                                             [ Usuário C (Client) ]
O Host é a "Fonte da Verdade": Um dos 3 usuários cria a sala (Host). O computador dele roda a simulação principal do editor e do jogo. Os outros dois se conectam a ele. Se houver qualquer conflito de dados, a posição que estiver no computador do Host é a que vale.

NAT Punching integrado: Para evitar que os usuários tenham que abrir portas de roteador para jogar e editar juntos, a camada de rede implementará técnicas de STUN/ICE para furar o bloqueio de firewalls domésticos automaticamente, permitindo a conexão direta pelo IP público do Host.

4. O Mecanismo de Transição ("Play / Stop")
A sincronização de rede precisa se adaptar instantaneamente quando o grupo decide testar a fase.

Estado de Backup (The Snapshot): No exato momento em que o Host aperta o botão "Play", a engine congela a árvore de cenas do editor e salva um instantâneo (snapshot) do mapa na memória RAM.

Ativação do Gameplay: A engine envia o comando [START_GAMEPLAY] para todos os clientes. A física de gravidade é ligada, os scripts de lógica começam a rodar, e os jogadores ganham o controle de seus personagens. A sincronização de estado continua ativa (agora sincronizando a posição dos avatares dos jogadores e dos inimigos).

O Reset (Botão Stop): Quando o teste acaba, o Host envia o comando [STOP_GAMEPLAY]. A engine limpa as entidades temporárias criadas durante o teste (como marcas de tiros ou objetos destruídos pela física) e recarrega o snapshot salvo na memória RAM. O cenário volta exatamente ao estado em que estava antes do jogo começar, pronto para continuar sendo editado de onde pararam.