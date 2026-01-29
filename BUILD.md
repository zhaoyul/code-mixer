# SharpDeceiver Build and Usage Instructions

## Building the Tool

### Prerequisites
- .NET SDK 8.0 or later (tested with .NET 8.0)
- MSBuild (included with .NET SDK)

### Build Steps

```bash
# Navigate to the project directory
cd SharpDeceiver

# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Or build in Release mode for better performance
dotnet build -c Release
```

The executable will be generated in `bin/Debug/net8.0/SharpDeceiver.dll` (or `bin/Release/net8.0/SharpDeceiver.dll` for Release builds).

## Usage

### Running the Tool

```bash
# Run from project directory
dotnet run --project SharpDeceiver/SharpDeceiver.csproj -- [options]

# Or run the built DLL
dotnet SharpDeceiver/bin/Debug/net10.0/SharpDeceiver.dll [options]
```

### Command-Line Options

| Option         | Short | Required | Description                                                    |
|----------------|-------|----------|----------------------------------------------------------------|
| `--mode`       | `-m`  | Yes      | Operation mode: `obfuscate` or `restore`                       |
| `--path`       | `-p`  | Yes      | Path to C# solution (.sln) or project (.csproj)                |
| `--exclude`    | `-e`  | No       | Comma-separated list of project names to exclude               |
| `--map`        | `-s`  | No       | Path to mapping file (default: `./deceiver_map.json`)          |
| `--dictionary` | `-d`  | No       | Path to custom dictionary file (optional) |
| `--seed`       |       | No       | Random seed for deterministic output (optional) |
| `--help`       | `-h`  | No       | Show help message                                              |

### Examples

#### Obfuscate a Solution

```bash
dotnet run --project SharpDeceiver/SharpDeceiver.csproj -- \
  --mode obfuscate \
  --path MySolution.sln
```

#### Obfuscate with Project Exclusions

```bash
dotnet run --project SharpDeceiver/SharpDeceiver.csproj -- \
  --mode obfuscate \
  --path MySolution.sln \
  --exclude "Tests,CommonLib"
```

#### Restore Obfuscated Code

```bash
dotnet run --project SharpDeceiver/SharpDeceiver.csproj -- \
  --mode restore \
  --path MySolution.sln \
  --map deceiver_map.json
```

**Note:** Restoration is currently experimental and may not work perfectly in all cases. Always keep a backup of your code before obfuscating.

## What Gets Obfuscated

The tool renames the following code elements:
- **Classes**, interfaces, structs, and enums
- **Methods** (except `Main`, overridden methods, and interface implementations)
- **Properties**
- **Fields**

The tool does NOT currently rename:
- Local variables
- Parameters (would break too much code)
- Namespaces
- Interface members or their implementations
- Overridden methods

## Example Output

### Original Code
```csharp
public class UserAuthService
{
    private string secretKey = "mySecretKey123";
    
    public string UserName { get; set; } = "DefaultUser";
    
    public bool Login(string username, string password)
    {
        if (VerifyCredentials(username, password))
        {
            UserName = username;
            return true;
        }
        return false;
    }
    
    private bool VerifyCredentials(string username, string password)
    {
        return password == secretKey;
    }
}
```

### Obfuscated Code
```csharp
public class NotifierStrategy
{
    private string static_type = "mySecretKey123";
    
    public string ContentSocket { get; set; } = "DefaultUser";
    
    public bool insertGenerator(string username, string password)
    {
        if (setWriter(username, password))
        {
            ContentSocket = username;
            return true;
        }
        return false;
    }
    
    private bool setWriter(string username, string password)
    {
        return password == static_type;
    }
}
```

## Important Notes

1. **Always backup your code** or commit to Git before obfuscating
2. **Test the obfuscated code** to ensure it still compiles and runs correctly
3. **Keep the mapping file (`deceiver_map.json`)** safe - you'll need it to restore the code
4. The tool modifies source files **in-place**
5. Restoration is experimental - use Git to revert if needed

## Custom Dictionary Format

Two formats are supported (JSON preferred):

### JSON
```json
{
  "prefixes": ["Core", "Main"],
  "nouns": ["Engine", "Scheduler"],
  "suffixes": ["Service", "Provider"],
  "verbs": ["process", "build"],
  "adjectives": ["current", "final"],
  "technicalNouns": ["buffer", "queue"]
}
```

### Plain text
One word per line. Lines starting with `#` or `//` are ignored. Plain text overrides only `nouns` and `technicalNouns`.

## Troubleshooting

### Build Errors

If you encounter build errors related to MSBuild packages:
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

### MSBuild Not Found

The tool uses `Microsoft.Build.Locator` to find MSBuild. If it fails:
- Ensure .NET SDK is properly installed
- Try running from a Visual Studio Developer Command Prompt (Windows)
- Check that your .NET SDK version is compatible

### Obfuscation Fails

If obfuscation fails partway through:
- Restore your code from backup or Git
- Check that the solution/project file path is correct
- Ensure all projects in the solution can build successfully
- Try excluding problematic projects with `--exclude`

## Testing

No sample test project is included in this repository. To test the tool:

1) Create a small C# solution (or use an internal test solution).
2) Run obfuscation against the solution.
3) Build and run the obfuscated solution to verify behavior.
