# ERus Workspace Rules

This file contains coding standards and general rules for working inside the ERus workspace.
Always respect these rules when modifying code.

## C# Coding Standards

1. **Language Version:** ERus uses C# 10.0+ (.NET 10.0). Use modern syntax like File-Scoped Namespaces (`namespace ERus.Engine.ECS;`), global usings (if applicable), and record structs.
2. **Naming Conventions:**
   - Public Properties/Methods: `PascalCase`
   - Private Fields: `_camelCase` with an underscore prefix (e.g., `_myPrivateVar`)
   - Interfaces: Prefix with `I` (e.g., `IComponent`, `ISystem`)
3. **Logging:**
   - NEVER use `Console.WriteLine` inside the Engine or Editor (unless inside `Program.cs` or CLI tools).
   - ALWAYS use `ERus.Engine.Scripting.ConsoleLog.Log()`, `ConsoleLog.Warn()`, or `ConsoleLog.Error()`. This ensures logs appear inside the Editor's Console Window.
   - For networking logs, prefix the message with `[Rede] ` so the console color-codes it automatically (e.g., `ConsoleLog.Log("[Rede] Connected to server.");`).
4. **Performance:**
   - Be careful with boxing/unboxing inside `Update` loops.
   - For ECS components, always use `ref var component = ref registry.GetComponent<T>(entity);` to modify structs by reference without allocating new memory.
5. **UI Structure:**
   - The UI is powered by ImGuiNET. Immediate Mode UI means UI is redrawn every frame. Do NOT put heavy processing inside `ImGui.Button` clicks unless it's sent to a background task or is extremely fast.
