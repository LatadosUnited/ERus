using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using ERus.Engine.Core;
using ERus.Engine.ECS;
using ERus.Engine.Modules;
using ERus.Engine.Scripting;

namespace ERus.Editor.EditorUI.Project;

public enum AssetCreationType
{
    Folder,
    Script,
    Scene,
    InputProfile
}

public static class ProjectFileOperations
{
    public static bool MoveItem(string sourcePath, string targetDirectory, Engine.Core.Engine engine)
    {
        try
        {
            if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
                return false;

            string fileName = Path.GetFileName(sourcePath);
            string destPath = Path.Combine(targetDirectory, fileName);

            if (sourcePath == destPath) return false;

            if (File.Exists(destPath) || Directory.Exists(destPath))
            {
                ConsoleLog.Error($"[Project] Erro ao mover: Já existe um item com o nome {fileName} no diretório de destino.");
                return false;
            }

            if (File.Exists(sourcePath))
            {
                File.Move(sourcePath, destPath);
                string sourceMeta = sourcePath + ".meta";
                if (File.Exists(sourceMeta))
                {
                    File.Move(sourceMeta, destPath + ".meta");
                }
            }
            else if (Directory.Exists(sourcePath))
            {
                Directory.Move(sourcePath, destPath);
            }

            engine.AssetDatabase.Scan();
            ConsoleLog.Log($"[Project] Item movido: {fileName} -> {Path.GetFileName(targetDirectory)}");
            return true;
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"[Project] Erro ao mover arquivo: {ex.Message}");
            return false;
        }
    }

    public static bool RenameItem(string currentPath, string newName, Engine.Core.Engine engine, out string newPath)
    {
        newPath = currentPath;
        try
        {
            if (string.IsNullOrWhiteSpace(newName)) return false;

            string cleanName = string.Join("_", newName.Trim().Split(Path.GetInvalidFileNameChars()));
            string dir = Path.GetDirectoryName(currentPath) ?? Environment.CurrentDirectory;
            newPath = Path.Combine(dir, cleanName);

            if (currentPath == newPath) return false;

            if (Directory.Exists(currentPath))
            {
                Directory.Move(currentPath, newPath);
            }
            else if (File.Exists(currentPath))
            {
                File.Move(currentPath, newPath);
                string oldMeta = currentPath + ".meta";
                if (File.Exists(oldMeta))
                {
                    File.Move(oldMeta, newPath + ".meta");
                }
            }
            else
            {
                return false;
            }

            engine.AssetDatabase.Scan();
            ConsoleLog.Log($"[Project] Item renomeado de {Path.GetFileName(currentPath)} para {cleanName}");
            _ = engine.GetModule<NetworkModule>()?.NetworkManager?.AssetSync?.AnnounceAssetAsync(newPath);
            return true;
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"[Project] Erro ao renomear: {ex.Message}");
            return false;
        }
    }

    public static bool DeleteItem(string path, Engine.Core.Engine engine)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
                ConsoleLog.Log($"[Project] Pasta deletada: {path}");
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
                string metaFile = path + ".meta";
                if (File.Exists(metaFile)) File.Delete(metaFile);
                ConsoleLog.Log($"[Project] Arquivo deletado: {path}");
            }
            else
            {
                return false;
            }

            engine.AssetDatabase.Scan();
            return true;
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"[Project] Erro ao deletar: {ex.Message}");
            return false;
        }
    }

    public static bool CreateAsset(AssetCreationType type, string currentDirectory, string enteredName, Engine.Core.Engine engine)
    {
        try
        {
            string cleanName = enteredName.Trim();
            if (string.IsNullOrWhiteSpace(cleanName)) return false;

            string safeName = string.Join("_", cleanName.Split(Path.GetInvalidFileNameChars()));
            string fullPath = Path.Combine(currentDirectory, safeName);

            switch (type)
            {
                case AssetCreationType.Folder:
                    Directory.CreateDirectory(fullPath);
                    ConsoleLog.Log($"[Project] Pasta criada: {fullPath}");
                    break;

                case AssetCreationType.Script:
                    if (!fullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) fullPath += ".cs";
                    string className = Path.GetFileNameWithoutExtension(fullPath);
                    string scriptTemplate = $@"using ERus.Engine.ECS;
using ERus.Engine.Core;
using ERus.Engine.Scripting;
using System;

public class {className} : EntityScript
{{
    public override void OnUpdate(float deltaTime)
    {{
        // Seu código aqui
    }}
}}";
                    File.WriteAllText(fullPath, scriptTemplate);
                    ConsoleLog.Log($"[Project] Script criado: {fullPath}");
                    _ = engine.GetModule<NetworkModule>()?.NetworkManager?.AssetSync?.AnnounceAssetAsync(fullPath);
                    break;

                case AssetCreationType.Scene:
                    if (!fullPath.EndsWith(".scene", StringComparison.OrdinalIgnoreCase)) fullPath += ".scene";
                    string sceneTemplate = "{ \"Entities\": [] }";
                    File.WriteAllText(fullPath, sceneTemplate);
                    ConsoleLog.Log($"[Project] Cena criada: {fullPath}");
                    _ = engine.GetModule<NetworkModule>()?.NetworkManager?.AssetSync?.AnnounceAssetAsync(fullPath);
                    break;

                case AssetCreationType.InputProfile:
                    if (!fullPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) fullPath += ".json";
                    string inputTemplate = @"{
  ""Maps"": [
    {
      ""Name"": ""Player"",
      ""IsActive"": true,
      ""Actions"": []
    }
  ]
}";
                    File.WriteAllText(fullPath, inputTemplate);
                    ConsoleLog.Log($"[Project] Input Map criado: {fullPath}");
                    _ = engine.GetModule<NetworkModule>()?.NetworkManager?.AssetSync?.AnnounceAssetAsync(fullPath);
                    break;
            }

            engine.AssetDatabase.Scan();
            return true;
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"[Project] Erro ao criar item: {ex.Message}");
            return false;
        }
    }

    public static void SaveEntityAsPrefab(string targetDirectory, int entityId, Engine.Core.Engine engine)
    {
        try
        {
            var ecsModule = engine.GetModule<ECSModule>();
            if (ecsModule == null) return;

            var entity = ecsModule.ActiveScene.Registry.GetLivingEntities().FirstOrDefault(e => e.Id == entityId);
            if (!ecsModule.ActiveScene.Registry.IsAlive(entity)) return;

            string tagName = "Prefab";
            if (ecsModule.ActiveScene.Registry.HasComponent<TagComponent>(entity))
                tagName = ecsModule.ActiveScene.Registry.GetComponent<TagComponent>(entity).Name;

            string safeName = string.Join("_", tagName.Split(Path.GetInvalidFileNameChars()));
            string destFile = Path.Combine(targetDirectory, safeName + ".prefab");

            SceneSerializer.SavePrefab(destFile, ecsModule.ActiveScene, entity);
            engine.AssetDatabase.Scan();
            ConsoleLog.Log($"[Project] Prefab salvo: {destFile}");
            _ = engine.GetModule<NetworkModule>()?.NetworkManager?.AssetSync?.AnnounceAssetAsync(destFile);
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"[Project] Erro ao salvar Prefab: {ex.Message}");
        }
    }

    public static void ImportFiles(string[] files, string targetDirectory, Engine.Core.Engine engine)
    {
        Task.Run(() =>
        {
            try
            {
                var importedFiles = new List<string>();

                foreach (var file in files)
                {
                    if (File.Exists(file))
                    {
                        string fileName = Path.GetFileName(file);
                        string destFile = Path.Combine(targetDirectory, fileName);

                        if (file != destFile)
                        {
                            File.Copy(file, destFile, true);
                            importedFiles.Add(destFile);
                            ConsoleLog.Log($"[Project] Arquivo importado: {fileName} -> {targetDirectory}");
                        }
                    }
                    else if (Directory.Exists(file))
                    {
                        CopyFilesRecursively(file, targetDirectory, importedFiles);
                    }
                }

                if (importedFiles.Count > 0)
                {
                    engine.AssetDatabase.Scan();
                    var networkModule = engine.GetModule<NetworkModule>();
                    if (networkModule?.NetworkManager?.AssetSync != null)
                    {
                        foreach (var imported in importedFiles)
                        {
                            _ = networkModule.NetworkManager.AssetSync.AnnounceAssetAsync(imported);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleLog.Error($"[Project] Erro ao importar arquivos: {ex.Message}");
            }
        });
    }

    private static void CopyFilesRecursively(string sourcePath, string targetPath, List<string> importedFiles)
    {
        var dirInfo = new DirectoryInfo(sourcePath);
        if (!dirInfo.Exists) return;

        string[] ignoredDirs = { ".git", ".vs", "bin", "obj", "node_modules" };
        if (Array.Exists(ignoredDirs, d => d.Equals(dirInfo.Name, StringComparison.OrdinalIgnoreCase)) || dirInfo.Name.StartsWith("."))
        {
            return;
        }

        string newTargetDir = Path.Combine(targetPath, dirInfo.Name);
        if (!Directory.Exists(newTargetDir))
        {
            Directory.CreateDirectory(newTargetDir);
            ConsoleLog.Log($"[Project] Pasta criada na importação: {newTargetDir}");
        }

        foreach (var fileInfo in dirInfo.GetFiles())
        {
            if (fileInfo.Name.StartsWith(".") || fileInfo.Name.EndsWith(".meta")) continue;

            string destFile = Path.Combine(newTargetDir, fileInfo.Name);
            fileInfo.CopyTo(destFile, true);
            importedFiles.Add(destFile);
            ConsoleLog.Log($"[Project] Arquivo importado: {fileInfo.Name} -> {newTargetDir}");
        }

        foreach (var subdirInfo in dirInfo.GetDirectories())
        {
            CopyFilesRecursively(subdirInfo.FullName, newTargetDir, importedFiles);
        }
    }
}
