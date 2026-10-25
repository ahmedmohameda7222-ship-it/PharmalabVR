// Source syntax admission only. This is NOT a Unity assembly compilation,
// Editor/PlayMode run, Player build or XR hardware verification.
// It catches malformed Unity C# in sources outside the panel-contract subset.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class Program
{
    private static readonly (string Name, string[] Symbols)[] Modes =
    {
        ("Editor", new[] { "UNITY_EDITOR", "UNITY_STANDALONE", "UNITY_STANDALONE_WIN",
            "ENABLE_INPUT_SYSTEM", "UNITY_6000_3_OR_NEWER" }),
        ("AndroidXR", new[] { "UNITY_ANDROID", "ENABLE_INPUT_SYSTEM",
            "UNITY_6000_3_OR_NEWER" })
    };

    private static IReadOnlyList<Diagnostic> SyntaxErrors(
        string source, string file, IEnumerable<string> symbols)
    {
        var options = new CSharpParseOptions(LanguageVersion.Latest,
            DocumentationMode.Parse, SourceCodeKind.Regular, symbols);
        return CSharpSyntaxTree.ParseText(source, options, file)
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
    }

    private static void SelfTest()
    {
        const string valid = "namespace Example { public sealed class Fine { public int Answer => 42; } }";
        const string bad = "namespace Example { public sealed class Broken { void F( { } }";
        if (SyntaxErrors(valid, "valid.cs", Array.Empty<string>()).Count != 0 ||
            SyntaxErrors(bad, "broken.cs", Array.Empty<string>()).Count == 0)
            throw new InvalidOperationException(
                "Syntax verification failed to distinguish valid and malformed C#.");
        const string conditional = "#if UNITY_EDITOR\nnamespace X { class Broken { void F( { } }\n#else\nnamespace X { class Fine { } }\n#endif\n";
        if (SyntaxErrors(conditional, "platform.cs", new[] { "UNITY_EDITOR" }).Count == 0 ||
            SyntaxErrors(conditional, "platform.cs", new[] { "UNITY_ANDROID" }).Count != 0)
            throw new InvalidOperationException(
                "Syntax verification did not test both Unity compile-time branches.");
    }

    private static int Main()
    {
        SelfTest();
        var basePath = Path.Combine("unity", "Assets", "PharmaLabVR");
        if (!Directory.Exists(basePath))
        {
            Console.Error.WriteLine("Run from repository root; Unity sources missing.");
            return 2;
        }
        var files = Directory.EnumerateFiles(basePath, "*.cs",
            SearchOption.AllDirectories).OrderBy(path => path,
            StringComparer.Ordinal).ToArray();
        if (files.Length < 35)
        {
            Console.Error.WriteLine("Unity source scan is unexpectedly incomplete.");
            return 2;
        }
        var errors = 0;
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            foreach (var mode in Modes)
            {
                foreach (var diagnostic in SyntaxErrors(source, file, mode.Symbols))
                {
                    Console.Error.WriteLine(mode.Name + " " + diagnostic);
                    ++errors;
                }
            }
        }
        if (errors != 0)
        {
            Console.Error.WriteLine("Unity C# syntax violations: " + errors +
                ". Unity compilation and runtime verification remain separately required.");
            return 1;
        }
        Console.WriteLine("Unity C# syntax: " + files.Length +
            " project files parsed in Editor + AndroidXR modes; all valid. " +
            "No Unity assembly, scene, native package or device was executed.");
        return 0;
    }
}
