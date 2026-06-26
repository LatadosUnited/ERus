# Nível 3: Ferramentas Avançadas (Advanced Features)
*Alto Esforço, Médio/Alto Impacto*

Sistemas que elevam o patamar da engine para projetos mais sofisticados.

### 1. Ferramenta de Build Standalone
- Criar a funcionalidade para empacotar o jogo feito no ERus Editor, gerando um executável (`.exe`) acompanhado de todos os recursos (assets) necessários para rodar fora da engine.

### 2. IA como Modificadora de Cenas
- Verificar a possibilidade e implementar uma forma de permitir que a IA altere uma cena via comandos/scripts (ex: manipular coordenadas de um objeto, alterar componentes e parâmetros, etc).

### 3. Efeitos de Tela (Post-Processing)
- Implementar stack de efeitos de pós-processamento, como Bloom, Ambient Occlusion (SSAO), Anti-aliasing e Color Grading para polimento visual.

### 4. Sistema Avançado de Luzes e Sombras
- Refinar e expandir os tipos de iluminação com: Point Lights (pontuais), Spot Lights (focais) e renderização de sombras dinâmicas mais avançadas.

### 5. Sistema de Partículas (VFX)
- Criação de um editor/componente de emissão de partículas para efeitos visuais contínuos ou explosivos (fogo, fumaça, brilhos).

### 6. Profiler Embutido
- Implementar ferramentas dentro do editor para exibir gráficos de performance, medindo o uso de CPU, GPU, consumo de memória (RAM) e variação de quadros por segundo (FPS) para detecção de gargalos.

### 7. Hot-Reloading de Scripts C#
- Capacidade de recompilar e atualizar os scripts do jogo sem a necessidade de fechar e reabrir o motor.

### 8. Gerenciamento de Visibilidade e Distância - Interest Management (Rede)
- Otimizar o envio de atualizações transform/estado, calculando zonas de interesse no servidor.
- O servidor enviará pacotes apenas para jogadores e conexões que estejam próximos ou tenham linha de visão das entidades afetadas, poupando tráfego enorme de rede.
