using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpDeceiver;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Parse command line arguments manually
        string? mode = null;
        string? path = null;
        string? exclude = null;
        string map = "./deceiver_map.json";
        string? dictionary = null; // Optional: custom dictionary file
        int? seed = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--mode" || args[i] == "-m")
            {
                if (i + 1 < args.Length)
                    mode = args[++i];
            }
            else if (args[i] == "--path" || args[i] == "-p")
            {
                if (i + 1 < args.Length)
                    path = args[++i];
            }
            else if (args[i] == "--exclude" || args[i] == "-e")
            {
                if (i + 1 < args.Length)
                    exclude = args[++i];
            }
            else if (args[i] == "--map" || args[i] == "-s")
            {
                if (i + 1 < args.Length)
                    map = args[++i];
            }
            else if (args[i] == "--dictionary" || args[i] == "-d")
            {
                // Optional: allow users to provide custom word dictionaries
                if (i + 1 < args.Length)
                    dictionary = args[++i];
            }
            else if (args[i] == "--seed")
            {
                if (i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedSeed))
                {
                    seed = parsedSeed;
                    i++;
                }
                else
                {
                    Console.WriteLine("Error: --seed requires an integer value.");
                    return 1;
                }
            }
            else if (args[i] == "--help" || args[i] == "-h")
            {
                PrintHelp();
                return 0;
            }
        }

        // Validate required parameters
        if (mode == null || path == null)
        {
            Console.WriteLine("Error: --mode and --path are required.");
            Console.WriteLine();
            PrintHelp();
            return 1;
        }

        try
        {
            // Validate mode
            if (mode != "obfuscate" && mode != "restore")
            {
                Console.WriteLine("Error: mode must be 'obfuscate' or 'restore'");
                return 1;
            }

            // Validate path
            if (!File.Exists(path))
            {
                Console.WriteLine($"Error: File not found: {path}");
                return 1;
            }

            if (!string.IsNullOrWhiteSpace(dictionary))
            {
                if (!File.Exists(dictionary))
                {
                    Console.WriteLine($"Error: Dictionary file not found: {dictionary}");
                    return 1;
                }

                if (!ScannerGroup.LoadCustomDictionary(dictionary))
                {
                    Console.WriteLine($"Error: Failed to load dictionary file: {dictionary}");
                    return 1;
                }
            }

            if (seed.HasValue)
            {
                ScannerGroup.SetSeed(seed.Value);
            }

            // Parse excluded projects
            var excludedProjects = exclude?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .ToArray();

            // Create obfuscator
            var obfuscator = new Obfuscator(excludedProjects);

            bool success;
            if (mode == "obfuscate")
            {
                Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║           SharpDeceiver - Obfuscation Mode                     ║");
                Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
                Console.WriteLine();
                Console.WriteLine("⚠️  WARNING: This will modify your source code in-place!");
                Console.WriteLine("   Make sure you have committed your code to Git or have a backup.");
                Console.WriteLine();

                success = await obfuscator.ObfuscateAsync(path, map);
            }
            else // restore
            {
                Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║           SharpDeceiver - Restoration Mode                     ║");
                Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
                Console.WriteLine();

                if (!File.Exists(map))
                {
                    Console.WriteLine($"Error: Mapping file not found: {map}");
                    return 1;
                }

                success = await obfuscator.RestoreAsync(path, map);
            }

            if (success)
            {
                Console.WriteLine();
                Console.WriteLine("✓ Operation completed successfully!");
                return 0;
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("✗ Operation failed.");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    static void PrintHelp()
    {
        Console.WriteLine("SharpDeceiver: C# Semantic Camouflage Obfuscator");
        Console.WriteLine();
        Console.WriteLine("Usage: SharpDeceiver --mode <mode> --path <path> [options]");
        Console.WriteLine();
        Console.WriteLine("Required Options:");
        Console.WriteLine("  --mode, -m <mode>       Operation mode: 'obfuscate' or 'restore'");
        Console.WriteLine("  --path, -p <path>       Path to C# solution (.sln) or project (.csproj)");
        Console.WriteLine();
        Console.WriteLine("Optional Options:");
        Console.WriteLine("  --exclude, -e <list>    Comma-separated list of project names to exclude");
        Console.WriteLine("  --map, -s <path>        Path to mapping file (default: ./deceiver_map.json)");
        Console.WriteLine("  --dictionary, -d <path> Path to custom dictionary file (optional)");
        Console.WriteLine("  --seed <int>            Random seed for deterministic output (optional)");
        Console.WriteLine("  --help, -h              Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  # Obfuscate a solution");
        Console.WriteLine("  SharpDeceiver --mode obfuscate --path MySolution.sln");
        Console.WriteLine();
        Console.WriteLine("  # Obfuscate with exclusions");
        Console.WriteLine("  SharpDeceiver --mode obfuscate --path MySolution.sln --exclude \"Tests,Common\"");
        Console.WriteLine();
        Console.WriteLine("  # Restore obfuscated code");
        Console.WriteLine("  SharpDeceiver --mode restore --path MySolution.sln --map deceiver_map.json");
    }
}
