using ERus.Engine.Core;
using ERus.Engine.Modules;
using ERus.Editor.Modules;

namespace ERus.Editor;

class Program
{
    static void Main(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--project" && i + 1 < args.Length)
            {
                string projectPath = args[i + 1];
                if (System.IO.Directory.Exists(projectPath))
                {
                    System.Environment.CurrentDirectory = projectPath;
                    System.Console.WriteLine($"[Editor] Trabalhando no diretório do projeto: {projectPath}");
                }
            }
        }

        // Instancia a Engine orquestradora
        using var engine = new ERus.Engine.Core.Engine();

        // Módulos da Engine principal
        engine.AddModule(new GraphicsModule());  // 1. Limpa tela (fundo azul)
        engine.AddModule(new ECSModule());       // 2. Lógica local e Física
        engine.AddModule(new InputModule());     // 3. Sistema de Input (Snapshots)
        engine.AddModule(new ScriptModule());    // 4. Scripts do usuário (gameplay)
        engine.AddModule(new NetworkModule());   // 5. Sincronização de Rede
        
        // Módulos específicos do Editor
        engine.AddModule(new EditorUIModule());  // 5. Desenha UI por cima de tudo

        // Trava a Thread no Game Loop do Silk.NET
        engine.Run();
    }
}

