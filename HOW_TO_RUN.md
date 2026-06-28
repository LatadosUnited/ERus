# Como Iniciar o Servidor e o Hub

Este guia rápido explica como rodar os componentes do ecossistema ERus localmente pelo terminal para fins de teste e desenvolvimento.

## 1. Iniciando o Servidor Dedicado (Master Server / Headless)
O ERus possui um servidor dedicado (`ERus.Server`) que roda a Engine em modo Headless (sem interface gráfica), lidando com a física, lógica de rede e provendo uma API HTTP (porta 8080) para autenticação.

Abra um terminal na pasta raiz do projeto (`e:\Projetos\ERus`) e rode o comando:
```powershell
dotnet run --project ERus.Server/ERus.Server.csproj
```

## 2. Iniciando o ERus Hub (Launcher)
O Hub é o gerenciador central de projetos, versões e conexões. É por aqui que os clientes vão baixar a Engine e se conectar ao servidor.

Abra um **segundo terminal** na mesma pasta raiz e rode o comando:
```powershell
dotnet run --project ERus.Hub/ERus.Hub.csproj
```
No Hub, você poderá selecionar o IP do Servidor (ex: `127.0.0.1:7777`) e se conectar à sessão de jogo/edição.

## 3. Iniciando um Cliente de Teste Adicional
Se você não quiser abrir o Hub (ou se quiser testar dois jogadores localmente contornando o Launcher), você pode iniciar a Engine diretamente no modo Cliente para se conectar automaticamente:

Abra um **terceiro terminal** e rode:
```powershell
dotnet run --project ERus.Editor/ERus.Editor.csproj -- --connect 127.0.0.1 --port 7777
```
