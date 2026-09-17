using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace MyApp.Tooling.Tests.Infrastructure;

/// <summary>
/// A code fix needs a Workspace, not just a Compilation: the fix hands back a
/// changed <see cref="Solution"/>, and TypoCodeFixProvider's rename reaches across
/// every document in it. An <see cref="AdhocWorkspace"/> is the smallest thing
/// that gives us one.
/// </summary>
internal static class CodeFixRunner
{
    public static Project CreateProject(params string[] sources)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();

        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                name: "TestProject",
                assemblyName: "TestProject",
                LanguageNames.CSharp)
                .WithMetadataReferences(TestCompilation.MetadataReferences())
                .WithParseOptions(TestCompilation.ParseOptions)
                .WithCompilationOptions(new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContextOptions.Enable)));

        for (var i = 0; i < sources.Length; i++)
        {
            solution = solution.AddDocument(
                DocumentId.CreateNewId(projectId),
                name: $"Test{i}.cs",
                text: SourceText.From(sources[i]));
        }

        return solution.GetProject(projectId)!;
    }

    public static async Task<Diagnostic[]> AnalyzeAsync(DiagnosticAnalyzer analyzer, Project project)
    {
        var compilation = await project.GetCompilationAsync(TestContext.Current.CancellationToken);

        return await AnalyzerRunner.RunAsync(analyzer, (CSharpCompilation)compilation!);
    }

    /// <summary>Everything the light bulb would offer for one diagnostic.</summary>
    public static async Task<ImmutableArray<CodeAction>> GetFixesAsync(
        CodeFixProvider provider, Project project, Diagnostic diagnostic)
    {
        var document = project.GetDocument(diagnostic.Location.SourceTree)!;
        var actions = ImmutableArray.CreateBuilder<CodeAction>();

        await provider.RegisterCodeFixesAsync(new CodeFixContext(
            document,
            diagnostic,
            registerCodeFix: (action, _) => actions.Add(action),
            cancellationToken: TestContext.Current.CancellationToken));

        return actions.ToImmutable();
    }

    /// <summary>Applies a code action and returns the solution it produced.</summary>
    public static async Task<Solution> ApplyAsync(CodeAction action)
    {
        var operations = await action.GetOperationsAsync(TestContext.Current.CancellationToken);

        return operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
    }

    /// <summary>The documents of the single test project, in the order they were added.</summary>
    public static async Task<string[]> DocumentTextsAsync(Solution solution)
    {
        var documents = solution.Projects.Single().Documents.OrderBy(d => d.Name, StringComparer.Ordinal);
        var texts = new List<string>();

        foreach (var document in documents)
        {
            var text = await document.GetTextAsync(TestContext.Current.CancellationToken);
            texts.Add(text.ToString());
        }

        return [.. texts];
    }
}
