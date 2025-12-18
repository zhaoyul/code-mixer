using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Rename;

namespace SharpDeceiver;

/// <summary>
/// Main obfuscation engine that handles the transformation of C# code.
/// </summary>
public class Obfuscator
{
    private readonly Dictionary<string, string> _symbolMap = new();
    private readonly HashSet<string> _excludedProjects = new();
    private MSBuildWorkspace? _workspace;

    public Obfuscator(IEnumerable<string>? excludedProjects = null)
    {
        if (excludedProjects != null)
        {
            foreach (var project in excludedProjects)
            {
                _excludedProjects.Add(project.Trim());
            }
        }
    }

    /// <summary>
    /// Obfuscates the solution or project at the given path.
    /// </summary>
    public async Task<bool> ObfuscateAsync(string solutionPath, string mapOutputPath)
    {
        try
        {
            Console.WriteLine($"Starting obfuscation for: {solutionPath}");
            
            // Initialize MSBuild
            if (!MSBuildLocator.IsRegistered)
            {
                var instances = MSBuildLocator.QueryVisualStudioInstances().ToArray();
                if (instances.Length > 0)
                {
                    MSBuildLocator.RegisterInstance(instances[0]);
                }
                else
                {
                    Console.WriteLine("No MSBuild instance found. Using default .NET SDK.");
                    MSBuildLocator.RegisterDefaults();
                }
            }

            _workspace = MSBuildWorkspace.Create();
            _workspace.RegisterWorkspaceFailedHandler(e =>
            {
                if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                {
                    Console.WriteLine($"Workspace error: {e.Diagnostic.Message}");
                }
            });

            Solution solution;
            if (solutionPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Loading solution...");
                solution = await _workspace.OpenSolutionAsync(solutionPath);
            }
            else if (solutionPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Loading project...");
                var project = await _workspace.OpenProjectAsync(solutionPath);
                solution = project.Solution;
            }
            else
            {
                Console.WriteLine("Error: Path must be a .sln or .csproj file");
                return false;
            }

            Console.WriteLine($"Loaded solution with {solution.Projects.Count()} projects");

            // Reset the dictionary to start fresh
            DeceiverDictionary.Reset();

            // Process each project
            foreach (var project in solution.Projects)
            {
                if (_excludedProjects.Contains(project.Name))
                {
                    Console.WriteLine($"Skipping excluded project: {project.Name}");
                    continue;
                }

                Console.WriteLine($"Processing project: {project.Name}");
                solution = await ObfuscateProjectAsync(solution, project.Id);
            }

            // Apply all changes to disk
            Console.WriteLine("Applying changes to disk...");
            if (!_workspace.TryApplyChanges(solution))
            {
                Console.WriteLine("Warning: Some changes could not be applied");
            }

            // Save the mapping file
            Console.WriteLine($"Saving mapping to: {mapOutputPath}");
            await SaveMappingAsync(mapOutputPath);

            Console.WriteLine($"Obfuscation complete! Renamed {_symbolMap.Count} symbols.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during obfuscation: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return false;
        }
        finally
        {
            _workspace?.Dispose();
        }
    }

    /// <summary>
    /// Restores the solution or project using the mapping file.
    /// </summary>
    public async Task<bool> RestoreAsync(string solutionPath, string mapFilePath)
    {
        try
        {
            Console.WriteLine($"Starting restoration for: {solutionPath}");
            Console.WriteLine($"Loading mapping from: {mapFilePath}");

            // Load the mapping
            if (!await LoadMappingAsync(mapFilePath))
            {
                Console.WriteLine("Error: Could not load mapping file");
                return false;
            }

            Console.WriteLine($"Loaded {_symbolMap.Count} symbol mappings");

            // Initialize MSBuild
            if (!MSBuildLocator.IsRegistered)
            {
                var instances = MSBuildLocator.QueryVisualStudioInstances().ToArray();
                if (instances.Length > 0)
                {
                    MSBuildLocator.RegisterInstance(instances[0]);
                }
                else
                {
                    MSBuildLocator.RegisterDefaults();
                }
            }

            _workspace = MSBuildWorkspace.Create();
            _workspace.RegisterWorkspaceFailedHandler(e =>
            {
                if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                {
                    Console.WriteLine($"Workspace error: {e.Diagnostic.Message}");
                }
            });

            Solution solution;
            if (solutionPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                solution = await _workspace.OpenSolutionAsync(solutionPath);
            }
            else if (solutionPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                var project = await _workspace.OpenProjectAsync(solutionPath);
                solution = project.Solution;
            }
            else
            {
                Console.WriteLine("Error: Path must be a .sln or .csproj file");
                return false;
            }

            // Create reverse mapping (obfuscated -> original)
            var reverseMap = _symbolMap.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

            // Process each project
            foreach (var project in solution.Projects)
            {
                if (_excludedProjects.Contains(project.Name))
                {
                    Console.WriteLine($"Skipping excluded project: {project.Name}");
                    continue;
                }

                Console.WriteLine($"Restoring project: {project.Name}");
                solution = await RestoreProjectAsync(solution, project.Id, reverseMap);
            }

            // Apply all changes
            Console.WriteLine("Applying changes to disk...");
            if (!_workspace.TryApplyChanges(solution))
            {
                Console.WriteLine("Warning: Some changes could not be applied");
            }

            Console.WriteLine("Restoration complete!");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during restoration: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return false;
        }
        finally
        {
            _workspace?.Dispose();
        }
    }

    private async Task<Solution> ObfuscateProjectAsync(Solution solution, ProjectId projectId)
    {
        var project = solution.GetProject(projectId);
        if (project == null) return solution;

        var compilation = await project.GetCompilationAsync();
        if (compilation == null) return solution;

        // Collect all symbols to rename
        var symbolsToRename = new List<ISymbol>();

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = await syntaxTree.GetRootAsync();

            // Find all named type declarations (classes, interfaces, structs, enums)
            var typeDeclarations = root.DescendantNodes()
                .Where(node => node is ClassDeclarationSyntax || 
                               node is InterfaceDeclarationSyntax || 
                               node is StructDeclarationSyntax ||
                               node is EnumDeclarationSyntax);

            foreach (var typeDecl in typeDeclarations)
            {
                var symbol = semanticModel.GetDeclaredSymbol(typeDecl);
                if (symbol != null && ShouldRenameSymbol(symbol))
                {
                    symbolsToRename.Add(symbol);
                }
            }

            // Find all method declarations
            var methodDeclarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var methodDecl in methodDeclarations)
            {
                var symbol = semanticModel.GetDeclaredSymbol(methodDecl);
                if (symbol != null && ShouldRenameSymbol(symbol))
                {
                    symbolsToRename.Add(symbol);
                }
            }

            // Find all property declarations
            var propertyDeclarations = root.DescendantNodes().OfType<PropertyDeclarationSyntax>();
            foreach (var propDecl in propertyDeclarations)
            {
                var symbol = semanticModel.GetDeclaredSymbol(propDecl);
                if (symbol != null && ShouldRenameSymbol(symbol))
                {
                    symbolsToRename.Add(symbol);
                }
            }

            // Find all field declarations
            var fieldDeclarations = root.DescendantNodes().OfType<FieldDeclarationSyntax>();
            foreach (var fieldDecl in fieldDeclarations)
            {
                foreach (var variable in fieldDecl.Declaration.Variables)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(variable);
                    if (symbol != null && ShouldRenameSymbol(symbol))
                    {
                        symbolsToRename.Add(symbol);
                    }
                }
            }

            // Find all parameter declarations
            var parameterDeclarations = root.DescendantNodes().OfType<ParameterSyntax>();
            foreach (var paramDecl in parameterDeclarations)
            {
                var symbol = semanticModel.GetDeclaredSymbol(paramDecl);
                if (symbol != null && ShouldRenameSymbol(symbol))
                {
                    symbolsToRename.Add(symbol);
                }
            }
        }

        // Rename symbols
        foreach (var symbol in symbolsToRename)
        {
            var originalName = symbol.Name;
            var newName = GenerateNewName(symbol);

            if (!string.IsNullOrEmpty(newName) && originalName != newName)
            {
                Console.WriteLine($"  Renaming {symbol.Kind}: {originalName} -> {newName}");
                _symbolMap[GetSymbolKey(symbol)] = newName;

                solution = await Renamer.RenameSymbolAsync(
                    solution,
                    symbol,
                    default(SymbolRenameOptions),
                    newName);
            }
        }

        return solution;
    }

    private async Task<Solution> RestoreProjectAsync(Solution solution, ProjectId projectId, Dictionary<string, string> reverseMap)
    {
        var project = solution.GetProject(projectId);
        if (project == null) return solution;

        var compilation = await project.GetCompilationAsync();
        if (compilation == null) return solution;

        // Collect all symbols to restore
        var symbolsToRestore = new List<(ISymbol symbol, string originalName)>();

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = await syntaxTree.GetRootAsync();

            // Find all declarations
            var declarations = root.DescendantNodes()
                .Where(node => node is BaseTypeDeclarationSyntax || 
                               node is MethodDeclarationSyntax ||
                               node is PropertyDeclarationSyntax ||
                               node is FieldDeclarationSyntax ||
                               node is ParameterSyntax);

            foreach (var decl in declarations)
            {
                ISymbol? symbol = null;
                
                if (decl is FieldDeclarationSyntax fieldDecl)
                {
                    foreach (var variable in fieldDecl.Declaration.Variables)
                    {
                        symbol = semanticModel.GetDeclaredSymbol(variable);
                        if (symbol != null)
                        {
                            var key = GetSymbolKey(symbol);
                            if (reverseMap.TryGetValue(key, out var originalName))
                            {
                                symbolsToRestore.Add((symbol, originalName));
                            }
                        }
                    }
                }
                else
                {
                    symbol = semanticModel.GetDeclaredSymbol(decl);
                    if (symbol != null)
                    {
                        var key = GetSymbolKey(symbol);
                        if (reverseMap.TryGetValue(key, out var originalName))
                        {
                            symbolsToRestore.Add((symbol, originalName));
                        }
                    }
                }
            }
        }

        // Restore symbols
        foreach (var (symbol, originalName) in symbolsToRestore)
        {
            Console.WriteLine($"  Restoring {symbol.Kind}: {symbol.Name} -> {originalName}");

            solution = await Renamer.RenameSymbolAsync(
                solution,
                symbol,
                default(SymbolRenameOptions),
                originalName);
        }

        return solution;
    }

    private bool ShouldRenameSymbol(ISymbol symbol)
    {
        // Don't rename:
        // - Main method (entry point)
        // - Overridden methods
        // - Interface implementations
        // - Compiler-generated symbols
        // - Symbols from external assemblies

        if (symbol.Name == "Main")
            return false;

        if (symbol.IsOverride)
            return false;

        if (symbol.ContainingAssembly?.Name == null)
            return false;

        // Check if it's implementing an interface member
        if (symbol is IMethodSymbol method)
        {
            if (method.ExplicitInterfaceImplementations.Any())
                return false;
            
            // Check if overriding interface
            var interfaces = method.ContainingType.AllInterfaces;
            foreach (var iface in interfaces)
            {
                if (iface.GetMembers().Any(m => m.Name == symbol.Name))
                    return false;
            }
        }

        if (symbol is IPropertySymbol property)
        {
            if (property.ExplicitInterfaceImplementations.Any())
                return false;
        }

        return true;
    }

    private string GenerateNewName(ISymbol symbol)
    {
        return symbol.Kind switch
        {
            SymbolKind.NamedType => DeceiverDictionary.GenerateClassName(),
            SymbolKind.Method => DeceiverDictionary.GenerateMethodName(),
            SymbolKind.Property => DeceiverDictionary.GeneratePropertyName(),
            SymbolKind.Field => DeceiverDictionary.GenerateVariableName(),
            SymbolKind.Parameter => DeceiverDictionary.GenerateVariableName(),
            _ => DeceiverDictionary.GenerateVariableName()
        };
    }

    private string GetSymbolKey(ISymbol symbol)
    {
        // Create a unique key for the symbol
        return $"{symbol.ContainingType?.ToDisplayString() ?? ""}.{symbol.Name}:{symbol.Kind}";
    }

    private async Task SaveMappingAsync(string path)
    {
        var json = JsonSerializer.Serialize(_symbolMap, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        await File.WriteAllTextAsync(path, json);
    }

    private async Task<bool> LoadMappingAsync(string path)
    {
        try
        {
            if (!File.Exists(path))
                return false;

            var json = await File.ReadAllTextAsync(path);
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            
            if (map != null)
            {
                _symbolMap.Clear();
                foreach (var kvp in map)
                {
                    _symbolMap[kvp.Key] = kvp.Value;
                }
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
