# ERus - Engine, Editor & Hub

A **ERus** é um ecossistema completo de desenvolvimento de jogos em C# (focado no .NET 10). Ela foi desenhada usando uma arquitetura modular moderna e focada em alto desempenho, separando responsabilidades em três pilares principais:

## 1. O Ecossistema

- **[ERus.Engine](./ERus.Engine/)**: O coração (runtime). Usa um sistema de **ECS** (Entity Component System) customizado e lida com toda a lógica gráfica (OpenGL via Silk.NET), física, scripts e sincronização de redes. É o que o jogador final roda.
- **[ERus.Editor](./ERus.Editor/)**: A ferramenta de criação de jogos. Uma UI construída sobre a própria engine (usando ImGui) que permite manipular cenas, entidades, sistemas e exportar os pacotes de assets.
- **[ERus.Hub](./ERus.Hub/)**: O gerenciador de versões e projetos. É o ponto de entrada para qualquer desenvolvedor, capaz de se conectar ao GitHub para baixar versões atualizadas da Engine/Editor e criar novos projetos de jogo a partir de templates.

## 2. Visão Geral da Arquitetura
O ecossistema adota a separação total entre **Dados e Comportamento** (pilar do ECS). Toda a lógica pesada é quebrada em Módulos (GraphicsModule, ECSModule, NetworkModule), e o Editor nada mais é do que uma fina camada visual em cima desses mesmos módulos.

Leia o documento detalhado de arquitetura: [Architecture.md](docs/Architecture.md).

## 3. Como Compilar e Rodar

1. **Rodar o Hub**:  
   O Hub é a forma recomendada de gerenciar seus projetos.
   ```bash
   dotnet run --project ERus.Hub/ERus.Hub.csproj
   ```

2. **Rodar a Engine/Editor (Sem o Hub)**:  
   Para focar apenas no desenvolvimento do core ou testar o editor diretamente:
   ```bash
   dotnet run --project ERus.Editor/ERus.Editor.csproj
   ```

3. **Gerar Nova Versão da Engine**:  
   Use o nosso script de automação para gerar um zip limpo da versão atual.
   ```bash
   .\scripts\build_engine_release.ps1 v0.2.6
   ```

## 4. Documentação e Limitações
Cada projeto dentro da ERus tem sua própria complexidade. Para entender os problemas atuais, gargalos ou as regras de negócio de cada parte, acesse a documentação local de cada sub-projeto:
- 📖 [Documentação da Engine (ECS & Core)](ERus.Engine/README.md)
- 📖 [Documentação do Editor (ImGui & Render)](ERus.Editor/README.md)
- 📖 [Documentação do Hub (Versionamento)](ERus.Hub/README.md)

Para registros de porquê uma arquitetura foi escolhida, acesse a nossa pasta de **ADRs** (Architecture Decision Records): [docs/ADR](docs/ADR/).
