using ERus.Engine.Core;
using ERus.Engine.Modules;
using ERus.Editor.Modules;

namespace ERus.Editor;

class Program
{
    static void Main(string[] args)
    {
        string? connectIp = null;
        int connectPort = 27015;
        string? token = null;
        string? remoteProject = null;
        string? baseDirectory = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--project" && i + 1 < args.Length)
            {
                baseDirectory = args[i + 1];
                if (!System.IO.Directory.Exists(baseDirectory))
                {
                    System.IO.Directory.CreateDirectory(baseDirectory);
                }
                System.Environment.CurrentDirectory = baseDirectory;
                System.Console.WriteLine($"[Editor] Trabalhando no diretório do projeto: {baseDirectory}");
            }
            else if (args[i] == "--connect" && i + 1 < args.Length)
            {
                connectIp = args[i + 1];
            }
            else if (args[i] == "--port" && i + 1 < args.Length)
            {
                int.TryParse(args[i + 1], out connectPort);
            }
            else if (args[i] == "--token" && i + 1 < args.Length)
            {
                token = args[i + 1];
            }
            else if (args[i] == "--remote-project" && i + 1 < args.Length)
            {
                remoteProject = args[i + 1];
            }
        }

        // Instancia a Engine orquestradora
        using var engine = new ERus.Engine.Core.Engine(baseDirectory);

        // Módulos da Engine principal
        engine.AddModule(new GraphicsModule());  // 1. Limpa tela (fundo azul)
        engine.AddModule(new PhysicsModule());   // 2. Motor de Física
        engine.AddModule(new ECSModule());       // 3. Lógica local (ECS)
        engine.AddModule(new InputModule());     // 3. Sistema de Input (Snapshots)
        engine.AddModule(new ScriptModule());    // 4. Scripts do usuário (gameplay)
        
        var networkModule = new NetworkModule();
        engine.AddModule(networkModule);   // 5. Sincronização de Rede
        
        // Módulos específicos do Editor
        engine.AddModule(new EditorUIModule());

        // Cliente Remoto
        if (!string.IsNullOrEmpty(connectIp))
        {
            System.Console.WriteLine("[Editor] Iniciando no modo Cliente Remoto...");
            networkModule.SetPendingRemoteConnection(connectIp, connectPort, token ?? "", remoteProject ?? "");
        }

        // Trava a Thread no Game Loop do Silk.NET
        engine.Run();
    }
}

