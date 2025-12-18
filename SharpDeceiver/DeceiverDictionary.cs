using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpDeceiver;

/// <summary>
/// Provides a dictionary of deceptive but realistic-looking software engineering terms
/// for semantic camouflage obfuscation.
/// </summary>
public static class DeceiverDictionary
{
    private static readonly Random _random = new Random();
    
    // Prefixes for class/method names
    private static readonly string[] _prefixes = new[]
    {
        "Base", "Abstract", "Generic", "Default", "Common", "Core", "Main",
        "System", "Application", "Service", "Business", "Data", "Entity",
        "Model", "View", "Controller", "Manager", "Handler", "Provider",
        "Factory", "Builder", "Helper", "Utility", "Processor", "Executor",
        "Coordinator", "Orchestrator", "Dispatcher", "Resolver", "Converter",
        "Adapter", "Wrapper", "Proxy", "Interceptor", "Validator", "Filter",
        "Formatter", "Parser", "Serializer", "Deserializer", "Encoder", "Decoder",
        "Compressor", "Decompressor", "Analyzer", "Optimizer", "Scheduler",
        "Initializer", "Configurator", "Registry", "Repository", "Context",
        "Session", "Transaction", "Connection", "Channel", "Stream", "Buffer",
        "Cache", "Pool", "Queue", "Stack", "Heap", "Tree", "Graph", "Map"
    };

    // Core nouns for class/method names
    private static readonly string[] _nouns = new[]
    {
        "Monitor", "Observer", "Watcher", "Tracker", "Logger", "Recorder",
        "Reader", "Writer", "Scanner", "Generator", "Creator", "Destroyer",
        "Loader", "Saver", "Fetcher", "Pusher", "Puller", "Sender", "Receiver",
        "Listener", "Notifier", "Publisher", "Subscriber", "Consumer", "Producer",
        "Worker", "Agent", "Client", "Server", "Host", "Guest", "Peer",
        "Node", "Cluster", "Network", "Protocol", "Message", "Event", "Signal",
        "Command", "Query", "Request", "Response", "Callback", "Hook", "Trigger",
        "Action", "Operation", "Task", "Job", "Process", "Thread", "Routine",
        "Function", "Method", "Procedure", "Algorithm", "Strategy", "Policy",
        "Rule", "Constraint", "Condition", "State", "Status", "Flag", "Token",
        "Key", "Value", "Pair", "Entry", "Item", "Element", "Component", "Module",
        "Package", "Bundle", "Container", "Wrapper", "Envelope", "Header", "Footer",
        "Body", "Content", "Payload", "Data", "Metadata", "Info", "Config",
        "Settings", "Options", "Parameters", "Arguments", "Attributes", "Properties",
        "Fields", "Members", "Variables", "Constants", "Literals", "References",
        "Pointers", "Handles", "Descriptors", "Identifiers", "Names", "Labels",
        "Tags", "Markers", "Indicators", "Counters", "Indexes", "Offsets"
    };

    // Suffixes for class/method names  
    private static readonly string[] _suffixes = new[]
    {
        "Engine", "Core", "System", "Framework", "Platform", "Infrastructure",
        "Service", "Manager", "Controller", "Handler", "Provider", "Factory",
        "Builder", "Adapter", "Wrapper", "Helper", "Utility", "Tool", "Kit",
        "Suite", "Set", "Collection", "Group", "Batch", "Bundle", "Package"
    };

    // Action verbs for method names
    private static readonly string[] _verbs = new[]
    {
        "process", "handle", "manage", "execute", "perform", "run", "invoke",
        "call", "trigger", "fire", "dispatch", "route", "forward", "redirect",
        "transform", "convert", "parse", "format", "serialize", "deserialize",
        "encode", "decode", "encrypt", "decrypt", "compress", "decompress",
        "validate", "verify", "check", "test", "assert", "ensure", "confirm",
        "initialize", "setup", "configure", "prepare", "build", "create", "construct",
        "destroy", "dispose", "cleanup", "finalize", "terminate", "shutdown",
        "start", "stop", "pause", "resume", "restart", "reset", "refresh", "reload",
        "update", "modify", "change", "alter", "adjust", "tune", "optimize",
        "save", "load", "store", "retrieve", "fetch", "get", "set", "put",
        "add", "remove", "delete", "insert", "append", "prepend", "push", "pop",
        "enqueue", "dequeue", "peek", "poll", "offer", "take", "drain", "fill",
        "read", "write", "scan", "search", "find", "lookup", "query", "filter",
        "sort", "order", "arrange", "organize", "group", "aggregate", "collect",
        "map", "reduce", "fold", "flatten", "expand", "collapse", "merge", "split",
        "join", "combine", "unite", "separate", "divide", "partition", "segment",
        "send", "receive", "transmit", "broadcast", "publish", "subscribe", "notify",
        "listen", "watch", "observe", "monitor", "track", "record", "log", "trace",
        "sync", "async", "await", "yield", "return", "complete", "finish", "end"
    };

    // Adjectives for variable/parameter names
    private static readonly string[] _adjectives = new[]
    {
        "current", "previous", "next", "first", "last", "initial", "final",
        "primary", "secondary", "temporary", "permanent", "local", "global",
        "internal", "external", "public", "private", "protected", "static",
        "dynamic", "virtual", "abstract", "concrete", "generic", "specific",
        "default", "custom", "standard", "extended", "advanced", "basic",
        "simple", "complex", "composite", "atomic", "shared", "exclusive",
        "cached", "buffered", "pooled", "queued", "stacked", "mapped",
        "sorted", "filtered", "validated", "verified", "encoded", "decoded"
    };

    // Technical nouns for variable/parameter names
    private static readonly string[] _technicalNouns = new[]
    {
        "buffer", "cache", "pool", "queue", "stack", "heap", "list", "array",
        "vector", "matrix", "table", "map", "set", "tree", "graph", "node",
        "edge", "vertex", "path", "route", "link", "chain", "sequence", "stream",
        "channel", "socket", "port", "endpoint", "address", "uri", "url",
        "connection", "session", "context", "scope", "namespace", "domain",
        "range", "interval", "boundary", "limit", "threshold", "offset", "index",
        "position", "location", "coordinate", "dimension", "size", "length",
        "width", "height", "depth", "capacity", "count", "total", "sum",
        "average", "minimum", "maximum", "value", "result", "output", "input",
        "source", "target", "destination", "origin", "reference", "instance",
        "object", "entity", "record", "row", "column", "field", "property",
        "attribute", "member", "element", "item", "entry", "key", "token",
        "identifier", "name", "label", "tag", "marker", "flag", "state",
        "status", "code", "type", "kind", "category", "class", "group",
        "metadata", "info", "data", "content", "payload", "message", "event"
    };

    private static readonly HashSet<string> _usedNames = new HashSet<string>();

    /// <summary>
    /// Generates a deceptive class name that looks professional but is semantically misleading.
    /// </summary>
    public static string GenerateClassName()
    {
        string name;
        int attempts = 0;
        do
        {
            var pattern = _random.Next(4);
            name = pattern switch
            {
                0 => $"{GetRandom(_prefixes)}{GetRandom(_nouns)}",
                1 => $"{GetRandom(_nouns)}{GetRandom(_suffixes)}",
                2 => $"{GetRandom(_prefixes)}{GetRandom(_nouns)}{GetRandom(_suffixes)}",
                _ => $"{GetRandom(_nouns)}{GetRandom(_nouns)}"
            };
            attempts++;
        } while (_usedNames.Contains(name) && attempts < 100);

        _usedNames.Add(name);
        return name;
    }

    /// <summary>
    /// Generates a deceptive method name that looks like a standard software operation.
    /// </summary>
    public static string GenerateMethodName()
    {
        string name;
        int attempts = 0;
        do
        {
            var pattern = _random.Next(3);
            name = pattern switch
            {
                0 => $"{GetRandom(_verbs)}_{GetRandom(_technicalNouns)}",
                1 => $"{GetRandom(_verbs)}{Capitalize(GetRandom(_nouns))}",
                _ => $"{GetRandom(_verbs)}{Capitalize(GetRandom(_adjectives))}{Capitalize(GetRandom(_technicalNouns))}"
            };
            attempts++;
        } while (_usedNames.Contains(name) && attempts < 100);

        _usedNames.Add(name);
        return name;
    }

    /// <summary>
    /// Generates a deceptive parameter/variable name that looks like a common technical term.
    /// </summary>
    public static string GenerateVariableName()
    {
        string name;
        int attempts = 0;
        do
        {
            var pattern = _random.Next(3);
            name = pattern switch
            {
                0 => $"{GetRandom(_adjectives)}_{GetRandom(_technicalNouns)}",
                1 => $"{GetRandom(_technicalNouns)}_{GetRandom(_adjectives)}",
                _ => $"temp_{GetRandom(_technicalNouns)}"
            };
            attempts++;
        } while (_usedNames.Contains(name) && attempts < 100);

        _usedNames.Add(name);
        return name;
    }

    /// <summary>
    /// Generates a deceptive property name.
    /// </summary>
    public static string GeneratePropertyName()
    {
        string name;
        int attempts = 0;
        do
        {
            var pattern = _random.Next(2);
            name = pattern switch
            {
                0 => $"{Capitalize(GetRandom(_adjectives))}{Capitalize(GetRandom(_technicalNouns))}",
                _ => $"{Capitalize(GetRandom(_nouns))}{Capitalize(GetRandom(_technicalNouns))}"
            };
            attempts++;
        } while (_usedNames.Contains(name) && attempts < 100);

        _usedNames.Add(name);
        return name;
    }

    /// <summary>
    /// Clears the used names cache. Should be called when starting a new obfuscation session.
    /// </summary>
    public static void Reset()
    {
        _usedNames.Clear();
    }

    private static string GetRandom(string[] array)
    {
        return array[_random.Next(array.Length)];
    }

    private static string Capitalize(string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;
        return char.ToUpper(str[0]) + str.Substring(1);
    }
}
