using System;
using ERus.Engine.Core;
using ERus.Engine.Modules;
using ERus.Server.Data;

namespace ERus.Server;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Iniciando ERus Dedicated Server...");
        ServerDatabase.Initialize();

        using var engine = new ERus.Engine.Core.Engine();
        
        // Headless mode only needs logic and networking
        engine.AddModule(new PhysicsModule());
        engine.AddModule(new ECSModule());
        engine.AddModule(new ScriptModule());
        
        var networkModule = new NetworkModule();
        engine.AddModule(networkModule);

        var httpServer = new HttpServer();
        httpServer.Start(8080);

        System.Threading.Tasks.Task.Run(async () => {
            await System.Threading.Tasks.Task.Delay(500); 

            // Configurar autenticação
            networkModule.NetworkManager.Dispatcher.SubscribeReusable<ERus.Engine.Network.Packets.Auth.AuthRequestPacket>((packet, peer) => 
            {
                Console.WriteLine($"[Auth] Recebido pedido de autenticação de {peer.Id}. Token: {packet.Token}, Projeto: {packet.ProjectId}");
                bool isValid = ServerDatabase.ValidateProjectAccess(packet.Token, packet.ProjectId);
                
                var response = new ERus.Engine.Network.Packets.Auth.AuthResponsePacket { Success = isValid };
                if (!isValid)
                {
                    response.ErrorMessage = "Acesso negado. Token inválido ou projeto não pertence a este usuário.";
                    Console.WriteLine($"[Auth] Acesso NEGADO para {peer.Id}");
                }
                else
                {
                    Console.WriteLine($"[Auth] Acesso PERMITIDO para {peer.Id}. Sessão iniciada no projeto {packet.ProjectId}.");
                }

                networkModule.NetworkManager.Dispatcher.SendToPeer(peer, response, LiteNetLib.DeliveryMethod.ReliableOrdered);
                
                if (!isValid)
                {
                    System.Threading.Tasks.Task.Delay(500).ContinueWith(_ => {
                        if (peer.ConnectionState == LiteNetLib.ConnectionState.Connected)
                            peer.Disconnect();
                    });
                }
            });

            int port = 27015;
            networkModule.StartServer(port);
        });

        Console.WriteLine("Servidor rodando. Pressione Ctrl+C para encerrar.");
        
        engine.RunHeadless(60); 
    }
}
