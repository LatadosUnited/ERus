using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ERus.Engine.Scripting;

/// <summary>
/// Erro individual de compilação, com localização no arquivo fonte.
/// </summary>
public record CompilationError(string File, int Line, int Column, string Message, bool IsWarning);

/// <summary>
/// Resultado de uma compilação: Assembly carregado com sucesso ou lista de erros.
/// </summary>
public record CompilationResult(
    Assembly? Assembly, 
    AssemblyLoadContext? LoadContext,
    List<CompilationError> Errors,
    List<Type> ScriptTypes)
{
    public bool Success => Assembly != null && Errors.Count == 0;
}

/// <summary>
/// Compila scripts C# do usuário em assemblies na memória via Roslyn.
/// 
/// Usa AssemblyLoadContext isolado para permitir unload/reload (hot-reload):
/// ao recompilar, o contexto antigo é descarregado e um novo é criado.
/// 
/// Referências automáticas incluídas:
///   - ERus.Engine.dll (para ERusScript, Entity, TransformComponent, etc.)
///   - Silk.NET.Maths (para Vector3D)
///   - System.Runtime e dependências do .NET
/// </summary>
public static class ScriptCompiler
{
    /// <summary>
    /// Compila todos os arquivos .cs encontrados nos caminhos fornecidos.
    /// </summary>
    /// <param name="sourceFiles">Caminhos absolutos dos arquivos .cs</param>
    /// <returns>Resultado da compilação com Assembly ou erros</returns>
    public static CompilationResult Compile(string[] sourceFiles)
    {
        var errors = new List<CompilationError>();

        if (sourceFiles.Length == 0)
        {
            return new CompilationResult(null, null, errors, new List<Type>());
        }

        // 1. Parsear cada arquivo em uma SyntaxTree
        var syntaxTrees = new List<SyntaxTree>();
        foreach (var file in sourceFiles)
        {
            try
            {
                var sourceText = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(
                    sourceText,
                    CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                    path: file);
                syntaxTrees.Add(tree);
            }
            catch (Exception ex)
            {
                errors.Add(new CompilationError(file, 0, 0, $"Erro ao ler arquivo: {ex.Message}", false));
            }
        }

        if (syntaxTrees.Count == 0)
        {
            return new CompilationResult(null, null, errors, new List<Type>());
        }

        // 2. Coletar referências de metadados
        var references = CollectReferences();

        // 3. Criar a compilação
        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            optimizationLevel: OptimizationLevel.Debug,
            allowUnsafe: true);

        var compilation = CSharpCompilation.Create(
            assemblyName: $"ERus_UserScripts_{Guid.NewGuid():N}",
            syntaxTrees: syntaxTrees,
            references: references,
            options: compilationOptions);

        // 4. Emitir para memória
        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);

        if (!emitResult.Success)
        {
            foreach (var diagnostic in emitResult.Diagnostics)
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error || 
                    diagnostic.Severity == DiagnosticSeverity.Warning)
                {
                    var location = diagnostic.Location.GetMappedLineSpan();
                    var filePath = location.Path ?? "unknown";
                    var line = location.StartLinePosition.Line + 1;
                    var column = location.StartLinePosition.Character + 1;
                    
                    errors.Add(new CompilationError(
                        Path.GetFileName(filePath),
                        line,
                        column,
                        diagnostic.GetMessage(),
                        diagnostic.Severity == DiagnosticSeverity.Warning));
                }
            }

            return new CompilationResult(null, null, errors, new List<Type>());
        }

        // 5. Carregar o assembly em um contexto isolado
        ms.Seek(0, SeekOrigin.Begin);
        var loadContext = new CollectibleAssemblyLoadContext();
        var assembly = loadContext.LoadFromStream(ms);

        // 6. Encontrar todos os tipos que herdam de ERusScript
        var scriptTypes = new List<Type>();
        try
        {
            foreach (var type in assembly.GetExportedTypes())
            {
                if (type.IsClass && !type.IsAbstract && typeof(ERusScript).IsAssignableFrom(type))
                {
                    scriptTypes.Add(type);
                }
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            foreach (var loaderException in ex.LoaderExceptions)
            {
                if (loaderException != null)
                    errors.Add(new CompilationError("Assembly", 0, 0, loaderException.Message, true));
            }
            // Usar os tipos que conseguimos carregar
            if (ex.Types != null)
            {
                foreach (var type in ex.Types)
                {
                    if (type != null && type.IsClass && !type.IsAbstract && typeof(ERusScript).IsAssignableFrom(type))
                    {
                        scriptTypes.Add(type);
                    }
                }
            }
        }

        ConsoleLog.Log($"Compilação OK: {sourceFiles.Length} arquivo(s), {scriptTypes.Count} script(s) encontrado(s)");
        foreach (var t in scriptTypes)
        {
            ConsoleLog.Log($"  → {t.Name}");
        }

        return new CompilationResult(assembly, loadContext, errors, scriptTypes);
    }

    /// <summary>
    /// Coleta todas as referências de metadados necessárias para compilar scripts do usuário.
    /// Inclui assemblies do runtime .NET, do Silk.NET e do ERus.Engine.
    /// </summary>
    private static List<MetadataReference> CollectReferences()
    {
        var references = new List<MetadataReference>();
        var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Referências do runtime .NET (System.Runtime, System.Console, etc.)
        var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (trustedAssemblies != null)
        {
            foreach (var path in trustedAssemblies.Split(Path.PathSeparator))
            {
                if (File.Exists(path) && addedPaths.Add(path))
                {
                    try
                    {
                        references.Add(MetadataReference.CreateFromFile(path));
                    }
                    catch
                    {
                        // Ignorar assemblies que não podem ser lidos
                    }
                }
            }
        }

        // Referências dos assemblies carregados no processo atual
        // (inclui ERus.Engine.dll, Silk.NET.Maths.dll, etc.)
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.IsDynamic) continue;
            
            try
            {
                var location = asm.Location;
                if (!string.IsNullOrEmpty(location) && File.Exists(location) && addedPaths.Add(location))
                {
                    references.Add(MetadataReference.CreateFromFile(location));
                }
            }
            catch
            {
                // Ignorar assemblies sem localização
            }
        }

        return references;
    }
}

/// <summary>
/// AssemblyLoadContext que pode ser coletado pelo GC (unloadable).
/// Necessário para hot-reload: permite descarregar o assembly antigo ao recompilar.
/// </summary>
internal class CollectibleAssemblyLoadContext : AssemblyLoadContext
{
    public CollectibleAssemblyLoadContext() : base(isCollectible: true) { }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Delega ao contexto padrão (não carrega nada extra)
        return null;
    }
}
