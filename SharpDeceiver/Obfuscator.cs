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
    private readonly List<SymbolMapping> _symbolMappings = new();
    private readonly HashSet<string> _excludedProjects = new();
    private MSBuildWorkspace? _workspace;
    private Dictionary<string, string>? _legacyObfuscatedToOriginalName;

    private sealed record SymbolMapping(string OriginalName, string OriginalKey, string ObfuscatedKey);

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
            ScannerGroup.Reset();

            // Process each project in dependency order
            var projectOrder = solution.GetProjectDependencyGraph()
                .GetTopologicallySortedProjects()
                .ToList();

            foreach (var projectId in projectOrder)
            {
                var project = solution.GetProject(projectId);
                if (project == null)
                    continue;

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

            Console.WriteLine($"Obfuscation complete! Renamed {_symbolMappings.Count} symbols.");
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

            Console.WriteLine($"Loaded {_symbolMappings.Count} symbol mappings");

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

            // Create reverse mapping (obfuscated -> original) using symbol keys
            Dictionary<string, string>? obfuscatedKeyToOriginalName = null;
            if (_symbolMappings.Count > 0)
            {
                obfuscatedKeyToOriginalName = new Dictionary<string, string>();
                foreach (var mapping in _symbolMappings)
                {
                    if (string.IsNullOrWhiteSpace(mapping.ObfuscatedKey))
                        continue;
                    if (!obfuscatedKeyToOriginalName.ContainsKey(mapping.ObfuscatedKey))
                    {
                        obfuscatedKeyToOriginalName[mapping.ObfuscatedKey] = mapping.OriginalName;
                    }
                }
            }

            // Process each project in dependency order
            var projectOrder = solution.GetProjectDependencyGraph()
                .GetTopologicallySortedProjects()
                .ToList();

            foreach (var projectId in projectOrder)
            {
                var project = solution.GetProject(projectId);
                if (project == null)
                    continue;

                if (_excludedProjects.Contains(project.Name))
                {
                    Console.WriteLine($"Skipping excluded project: {project.Name}");
                    continue;
                }

                Console.WriteLine($"Restoring project: {project.Name}");
                if (obfuscatedKeyToOriginalName != null && obfuscatedKeyToOriginalName.Count > 0)
                {
                    solution = await RestoreProjectAsync(solution, project.Id, obfuscatedKeyToOriginalName);
                }
                else if (_legacyObfuscatedToOriginalName != null && _legacyObfuscatedToOriginalName.Count > 0)
                {
                    solution = await RestoreProjectLegacyAsync(solution, project.Id, _legacyObfuscatedToOriginalName);
                }
                else
                {
                    Console.WriteLine("No valid mappings found; skipping restoration.");
                }
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

        // Collect all symbols to rename with their original names and metadata
        var symbolsToRename = new List<(ISymbol symbol, string originalName, string key)>();
        var processedKeys = new HashSet<string>();

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
                    var key = GetSymbolKey(symbol);
                    if (!processedKeys.Contains(key))
                    {
                        symbolsToRename.Add((symbol, symbol.Name, key));
                        processedKeys.Add(key);
                    }
                }
            }

            // Find all method declarations
            var methodDeclarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var methodDecl in methodDeclarations)
            {
                var symbol = semanticModel.GetDeclaredSymbol(methodDecl);
                if (symbol != null && ShouldRenameSymbol(symbol))
                {
                    var key = GetSymbolKey(symbol);
                    if (!processedKeys.Contains(key))
                    {
                        symbolsToRename.Add((symbol, symbol.Name, key));
                        processedKeys.Add(key);
                    }
                }
            }

            // Find all property declarations
            var propertyDeclarations = root.DescendantNodes().OfType<PropertyDeclarationSyntax>();
            foreach (var propDecl in propertyDeclarations)
            {
                var symbol = semanticModel.GetDeclaredSymbol(propDecl);
                if (symbol != null && ShouldRenameSymbol(symbol))
                {
                    var key = GetSymbolKey(symbol);
                    if (!processedKeys.Contains(key))
                    {
                        symbolsToRename.Add((symbol, symbol.Name, key));
                        processedKeys.Add(key);
                    }
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
                        var key = GetSymbolKey(symbol);
                        if (!processedKeys.Contains(key))
                        {
                            symbolsToRename.Add((symbol, symbol.Name, key));
                            processedKeys.Add(key);
                        }
                    }
                }
            }
        }

        // Now rename all symbols one by one
        foreach (var (symbol, originalName, key) in symbolsToRename)
        {
            var newName = GenerateUniqueName(symbol);

            if (!string.IsNullOrEmpty(newName) && originalName != newName)
            {
                Console.WriteLine($"  Renaming {symbol.Kind}: {originalName} -> {newName}");

                try
                {
                    var currentSymbol = await FindSymbolInSolutionAsync(solution, symbol);
                    if (currentSymbol != null)
                    {
                        solution = await Renamer.RenameSymbolAsync(
                            solution,
                            currentSymbol,
                            default(SymbolRenameOptions),
                            newName);

                        var updatedSymbol = await FindRenamedSymbolAsync(solution, symbol, newName);
                        if (updatedSymbol != null)
                        {
                            var obfuscatedKey = GetSymbolKey(updatedSymbol);
                            _symbolMappings.Add(new SymbolMapping(originalName, key, obfuscatedKey));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    Warning: Failed to rename {originalName}: {ex.Message}");
                }
            }
        }

        return solution;
    }

    // Helper method to find the current symbol in the updated solution
    private async Task<ISymbol?> FindSymbolInSolutionAsync(Solution solution, ISymbol originalSymbol)
    {
        var location = originalSymbol.Locations.FirstOrDefault();
        if (location == null || location.SourceTree == null)
            return null;

        var document = solution.GetDocument(location.SourceTree);
        if (document == null)
            return null;

        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null)
            return null;

        var root = await document.GetSyntaxRootAsync();
        if (root == null)
            return null;

        var node = root.FindNode(location.SourceSpan);
        return semanticModel.GetDeclaredSymbol(node);
    }

    private async Task<ISymbol?> FindRenamedSymbolAsync(Solution solution, ISymbol originalSymbol, string newName)
    {
        var location = originalSymbol.Locations.FirstOrDefault();
        var filePath = location?.SourceTree?.FilePath;
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        var lineNumber = location?.GetLineSpan().StartLinePosition.Line ?? -1;
        var document = solution.Projects
            .SelectMany(p => p.Documents)
            .FirstOrDefault(d => string.Equals(d.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (document == null)
            return null;

        var semanticModel = await document.GetSemanticModelAsync();
        var root = await document.GetSyntaxRootAsync();
        if (semanticModel == null || root == null)
            return null;

        IEnumerable<SyntaxNode> candidates = originalSymbol.Kind switch
        {
            SymbolKind.NamedType => root.DescendantNodes()
                .Where(node => node is ClassDeclarationSyntax ||
                               node is InterfaceDeclarationSyntax ||
                               node is StructDeclarationSyntax ||
                               node is EnumDeclarationSyntax),
            SymbolKind.Method => root.DescendantNodes().OfType<MethodDeclarationSyntax>(),
            SymbolKind.Property => root.DescendantNodes().OfType<PropertyDeclarationSyntax>(),
            SymbolKind.Field => root.DescendantNodes().OfType<VariableDeclaratorSyntax>(),
            _ => Enumerable.Empty<SyntaxNode>()
        };

        var namedCandidates = candidates
            .Where(node => GetNodeIdentifier(node) == newName)
            .ToList();

        if (namedCandidates.Count == 0)
            return null;

        var lineMatched = namedCandidates
            .FirstOrDefault(node => node.GetLocation().GetLineSpan().StartLinePosition.Line == lineNumber);

        var targetNode = lineMatched ?? namedCandidates[0];
        return semanticModel.GetDeclaredSymbol(targetNode);
    }

    private static string? GetNodeIdentifier(SyntaxNode node)
    {
        return node switch
        {
            ClassDeclarationSyntax cds => cds.Identifier.Text,
            InterfaceDeclarationSyntax ids => ids.Identifier.Text,
            StructDeclarationSyntax sds => sds.Identifier.Text,
            EnumDeclarationSyntax eds => eds.Identifier.Text,
            MethodDeclarationSyntax mds => mds.Identifier.Text,
            PropertyDeclarationSyntax pds => pds.Identifier.Text,
            VariableDeclaratorSyntax vds => vds.Identifier.Text,
            _ => null
        };
    }

    private async Task<Solution> RestoreProjectAsync(Solution solution, ProjectId projectId, Dictionary<string, string> obfuscatedKeyToOriginalName)
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

            // Find all named type declarations (classes, interfaces, structs, enums)
            var typeDeclarations = root.DescendantNodes()
                .Where(node => node is ClassDeclarationSyntax ||
                               node is InterfaceDeclarationSyntax ||
                               node is StructDeclarationSyntax ||
                               node is EnumDeclarationSyntax);

            foreach (var typeDecl in typeDeclarations)
            {
                var symbol = semanticModel.GetDeclaredSymbol(typeDecl);
                if (symbol != null)
                {
                    // Check if this symbol's current (obfuscated) name is in our reverse map
                    var obfuscatedKey = GetSymbolKey(symbol);
                    if (obfuscatedKeyToOriginalName.TryGetValue(obfuscatedKey, out var originalName))
                    {
                        symbolsToRestore.Add((symbol, originalName));
                    }
                }
            }

            // Find all method declarations
            var methodDeclarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var methodDecl in methodDeclarations)
            {
                var symbol = semanticModel.GetDeclaredSymbol(methodDecl);
                if (symbol != null)
                {
                    // Check if this symbol's current (obfuscated) name is in our reverse map
                    var obfuscatedKey = GetSymbolKey(symbol);
                    if (obfuscatedKeyToOriginalName.TryGetValue(obfuscatedKey, out var originalName))
                    {
                        symbolsToRestore.Add((symbol, originalName));
                    }
                }
            }

            // Find all property declarations
            var propertyDeclarations = root.DescendantNodes().OfType<PropertyDeclarationSyntax>();
            foreach (var propDecl in propertyDeclarations)
            {
                var symbol = semanticModel.GetDeclaredSymbol(propDecl);
                if (symbol != null)
                {
                    // Check if this symbol's current (obfuscated) name is in our reverse map
                    var obfuscatedKey = GetSymbolKey(symbol);
                    if (obfuscatedKeyToOriginalName.TryGetValue(obfuscatedKey, out var originalName))
                    {
                        symbolsToRestore.Add((symbol, originalName));
                    }
                }
            }

            // Find all field declarations
            var fieldDeclarations = root.DescendantNodes().OfType<FieldDeclarationSyntax>();
            foreach (var fieldDecl in fieldDeclarations)
            {
                foreach (var variable in fieldDecl.Declaration.Variables)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(variable);
                    if (symbol != null)
                    {
                        // Check if this symbol's current (obfuscated) name is in our reverse map
                        var obfuscatedKey = GetSymbolKey(symbol);
                        if (obfuscatedKeyToOriginalName.TryGetValue(obfuscatedKey, out var originalName))
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

            var currentSymbol = await FindSymbolInSolutionAsync(solution, symbol);
            if (currentSymbol == null)
                continue;

            solution = await Renamer.RenameSymbolAsync(
                solution,
                currentSymbol,
                default(SymbolRenameOptions),
                originalName);
        }

        return solution;
    }

    private async Task<Solution> RestoreProjectLegacyAsync(Solution solution, ProjectId projectId, Dictionary<string, string> obfuscatedNameToOriginalName)
    {
        var project = solution.GetProject(projectId);
        if (project == null) return solution;

        var compilation = await project.GetCompilationAsync();
        if (compilation == null) return solution;

        var symbolsToRestore = new List<(ISymbol symbol, string originalName)>();

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = await syntaxTree.GetRootAsync();

            var typeDeclarations = root.DescendantNodes()
                .Where(node => node is ClassDeclarationSyntax ||
                               node is InterfaceDeclarationSyntax ||
                               node is StructDeclarationSyntax ||
                               node is EnumDeclarationSyntax);

            foreach (var typeDecl in typeDeclarations)
            {
                var symbol = semanticModel.GetDeclaredSymbol(typeDecl);
                if (symbol != null && obfuscatedNameToOriginalName.TryGetValue(symbol.Name, out var originalName))
                {
                    symbolsToRestore.Add((symbol, originalName));
                }
            }

            var methodDeclarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var methodDecl in methodDeclarations)
            {
                var symbol = semanticModel.GetDeclaredSymbol(methodDecl);
                if (symbol != null && obfuscatedNameToOriginalName.TryGetValue(symbol.Name, out var originalName))
                {
                    symbolsToRestore.Add((symbol, originalName));
                }
            }

            var propertyDeclarations = root.DescendantNodes().OfType<PropertyDeclarationSyntax>();
            foreach (var propDecl in propertyDeclarations)
            {
                var symbol = semanticModel.GetDeclaredSymbol(propDecl);
                if (symbol != null && obfuscatedNameToOriginalName.TryGetValue(symbol.Name, out var originalName))
                {
                    symbolsToRestore.Add((symbol, originalName));
                }
            }

            var fieldDeclarations = root.DescendantNodes().OfType<FieldDeclarationSyntax>();
            foreach (var fieldDecl in fieldDeclarations)
            {
                foreach (var variable in fieldDecl.Declaration.Variables)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(variable);
                    if (symbol != null && obfuscatedNameToOriginalName.TryGetValue(symbol.Name, out var originalName))
                    {
                        symbolsToRestore.Add((symbol, originalName));
                    }
                }
            }
        }

        foreach (var (symbol, originalName) in symbolsToRestore)
        {
            Console.WriteLine($"  Restoring {symbol.Kind}: {symbol.Name} -> {originalName}");

            var currentSymbol = await FindSymbolInSolutionAsync(solution, symbol);
            if (currentSymbol == null)
                continue;

            solution = await Renamer.RenameSymbolAsync(
                solution,
                currentSymbol,
                default(SymbolRenameOptions),
                originalName);
        }

        return solution;
    }

    // Helper method to extract the original name from the symbol key
    private string ExtractOriginalName(string key)
    {
        // Key format: [containingType]::[symbolName]:[Kind]:[filePath]:[lineNumber]
        // Example: <global namespace>::Program:NamedType:/path/to/Program.cs:6
        // We need to split on "::" first, then the rest
        var doubleColonIndex = key.IndexOf("::");
        if (doubleColonIndex != -1)
        {
            var afterDoubleColon = key.Substring(doubleColonIndex + 2);
            var colonIndex = afterDoubleColon.IndexOf(':');
            if (colonIndex != -1)
            {
                return afterDoubleColon.Substring(0, colonIndex);
            }
            return afterDoubleColon; // if no colon after "::", return the rest
        }

        // If parse fails, return the whole key as fallback
        return key;
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

            var interfaces = method.ContainingType.AllInterfaces;
            foreach (var iface in interfaces)
            {
                foreach (var member in iface.GetMembers())
                {
                    var impl = method.ContainingType.FindImplementationForInterfaceMember(member);
                    if (SymbolEqualityComparer.Default.Equals(impl, method))
                        return false;
                }
            }
        }

        if (symbol is IPropertySymbol property)
        {
            if (property.ExplicitInterfaceImplementations.Any())
                return false;

            var interfaces = property.ContainingType.AllInterfaces;
            foreach (var iface in interfaces)
            {
                foreach (var member in iface.GetMembers())
                {
                    var impl = property.ContainingType.FindImplementationForInterfaceMember(member);
                    if (SymbolEqualityComparer.Default.Equals(impl, property))
                        return false;
                }
            }
        }

        return true;
    }

    private string GenerateNewName(ISymbol symbol)
    {
        return symbol.Kind switch
        {
            SymbolKind.NamedType => ScannerGroup.GenerateClassName(),
            SymbolKind.Method => ScannerGroup.GenerateMethodName(),
            SymbolKind.Property => ScannerGroup.GeneratePropertyName(),
            SymbolKind.Field => ScannerGroup.GenerateVariableName(),
            SymbolKind.Parameter => ScannerGroup.GenerateVariableName(),
            _ => ScannerGroup.GenerateVariableName()
        };
    }

    private string GenerateUniqueName(ISymbol symbol)
    {
        const int maxAttempts = 200;
        for (var i = 0; i < maxAttempts; i++)
        {
            var candidate = GenerateNewName(symbol);
            if (string.IsNullOrWhiteSpace(candidate))
                continue;
            if (!SyntaxFacts.IsValidIdentifier(candidate))
                continue;
            if (!HasNameConflict(symbol, candidate))
                return candidate;
        }

        return GenerateNewName(symbol);
    }

    private static bool HasNameConflict(ISymbol symbol, string candidate)
    {
        if (symbol is INamedTypeSymbol)
        {
            if (symbol.ContainingType != null)
            {
                return symbol.ContainingType.GetMembers(candidate)
                    .Any(m => !SymbolEqualityComparer.Default.Equals(m, symbol));
            }

            return symbol.ContainingNamespace.GetMembers(candidate)
                .Any(m => !SymbolEqualityComparer.Default.Equals(m, symbol));
        }

        var containingType = symbol.ContainingType;
        if (containingType == null)
            return false;

        if (symbol is IMethodSymbol method)
        {
            foreach (var member in containingType.GetMembers(candidate))
            {
                if (member is IMethodSymbol other)
                {
                    if (SymbolEqualityComparer.Default.Equals(other, method))
                        continue;
                    if (MethodSignaturesMatch(method, other))
                        return true;
                }
                else
                {
                    return true;
                }
            }

            return false;
        }

        return containingType.GetMembers(candidate)
            .Any(m => !SymbolEqualityComparer.Default.Equals(m, symbol));
    }

    private static bool MethodSignaturesMatch(IMethodSymbol a, IMethodSymbol b)
    {
        if (a.Parameters.Length != b.Parameters.Length)
            return false;
        if (a.TypeParameters.Length != b.TypeParameters.Length)
            return false;

        for (var i = 0; i < a.Parameters.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(a.Parameters[i].Type, b.Parameters[i].Type))
                return false;
        }

        return true;
    }

    private string GetSymbolKey(ISymbol symbol)
    {
        var docId = symbol.GetDocumentationCommentId();
        if (!string.IsNullOrWhiteSpace(docId))
        {
            return docId;
        }

        var container = symbol.ContainingType?.ToDisplayString() ??
                       symbol.ContainingNamespace?.ToDisplayString() ?? "";
        return $"{container}::{symbol.MetadataName}:{symbol.Kind}";
    }

    private async Task SaveMappingAsync(string path)
    {
        var json = JsonSerializer.Serialize(_symbolMappings, new JsonSerializerOptions
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
            var mappings = JsonSerializer.Deserialize<List<SymbolMapping>>(json);
            if (mappings != null && mappings.Count > 0)
            {
                _symbolMappings.Clear();
                _symbolMappings.AddRange(mappings);
                _legacyObfuscatedToOriginalName = null;
                return true;
            }

            var legacyMap = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (legacyMap != null && legacyMap.Count > 0)
            {
                // Legacy format: originalKey -> obfuscatedName
                _legacyObfuscatedToOriginalName = new Dictionary<string, string>();
                foreach (var kvp in legacyMap)
                {
                    var originalName = ExtractOriginalName(kvp.Key);
                    _legacyObfuscatedToOriginalName[kvp.Value] = originalName;
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
