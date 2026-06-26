# Nível 4: Polimento e Usabilidade (Quality of Life)
*Médio Esforço, Médio/Alto Impacto (Conforto)*

Melhorias para deixar o fluxo de trabalho dos desenvolvedores mais agradável e parecido com motores profissionais.

### 1. Sistema de Materiais (PBR)
- Suporte claro e interface gráfica para propriedades de Materiais baseados em física: Albedo, Normal Map, Metallic, Roughness e Emission.

### 2. Edição de Referências no Inspector
- Permitir arrastar e soltar (Drag & Drop) uma Entidade da janela Hierarchy diretamente para um campo público do tipo referencial de um Script/Componente aberto no Inspector.

### 3. Salvar/Carregar Layout de Janelas (Docking)
- Garantir que a posição, abas ativas e tamanhos das janelas da interface (ImGui) sejam persistidos entre as sessões, para que o usuário não perca seu layout customizado.

### 4. Melhorias na Documentação
- Melhorar e expandir a documentação geral do projeto.

### 5. Áudio Espacial e Componentes
- Criar o `AudioSource` (emissor de som) e `AudioListener` (receptor de som na câmera) para suportar áudio localizado 3D, com variação de volume baseada em distância.

### 6. Interface de Download de Assets Sincronizados (Rede)
- Adicionar ao Editor uma barra ou pop-up de progresso visual monitorando a transferência assíncrona de Assets gigantescos entre o Servidor e o Cliente (via `AssetSyncManager`).
- Fornecer feedback visual em vez de travamentos e modelos "Placeholder".
