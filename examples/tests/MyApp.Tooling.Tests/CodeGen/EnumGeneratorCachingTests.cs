using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MyApp.CodeGen;
using MyApp.Tooling.Tests.Infrastructure;

namespace MyApp.Tooling.Tests.CodeGen;

/// <summary>
/// The incremental half of <see cref="EnumGenerator"/>. A generator that
/// re-generates on every keystroke is a correctness pass and a performance
/// regression at the same time, and nothing about the emitted text reveals which
/// one you shipped — only the step reasons do.
/// </summary>
public sealed class EnumGeneratorCachingTests
{
    private const string PrioritySource = """
        using MyApp.CodeGen;

        namespace MyApp.Demo;

        [GeneratedEnum(EnumType.Int)]
        public sealed partial class Priority
        {
            public required string Name { get; init; }
        }
        """;

    /// <summary>
    /// The reason the pipeline can cache at all: the model is a
    /// <c>readonly record struct</c>, so two runs that see the same declaration
    /// produce two *equal* models. Swap it for a class without value equality and
    /// this test — and every cached run — goes red.
    /// </summary>
    [Fact]
    public void Model_HasValueEquality()
    {
        var model = new EnumToGenerate("MyApp.Demo", EnumType.Int, "Priority");

        Assert.Equal(model, new EnumToGenerate("MyApp.Demo", EnumType.Int, "Priority"));
        Assert.NotEqual(model, new EnumToGenerate("MyApp.Other", EnumType.Int, "Priority"));
        Assert.NotEqual(model, new EnumToGenerate("MyApp.Demo", EnumType.String, "Priority"));
        Assert.NotEqual(model, new EnumToGenerate("MyApp.Demo", EnumType.Int, "Channel"));
    }

    [Fact]
    public void EditingAnUnrelatedFile_DoesNotRegenerate()
    {
        var compilation = TestCompilation.Create(PrioritySource);
        var driver = GeneratorRunner.CreateDriver(trackSteps: true)
            .RunGenerators(compilation, TestContext.Current.CancellationToken);

        // A file that has nothing to do with any [GeneratedEnum] — the everyday
        // case while someone types somewhere else in the solution.
        var edited = compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText(
                "namespace MyApp.Demo; public sealed class Unrelated;",
                TestCompilation.ParseOptions,
                cancellationToken: TestContext.Current.CancellationToken));

        driver = driver.RunGenerators(edited, TestContext.Current.CancellationToken);

        Assert.All(OutputReasons(driver), reason => Assert.Contains(
            reason,
            (IncrementalStepRunReason[])[IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged]));
    }

    [Fact]
    public void ChangingTheBackingType_Regenerates()
    {
        var compilation = TestCompilation.Create(PrioritySource);
        var driver = GeneratorRunner.CreateDriver(trackSteps: true)
            .RunGenerators(compilation, TestContext.Current.CancellationToken);

        var before = Generated(driver);

        var edited = compilation.ReplaceSyntaxTree(
            compilation.SyntaxTrees.Single(),
            CSharpSyntaxTree.ParseText(
                PrioritySource.Replace("EnumType.Int", "EnumType.String"),
                TestCompilation.ParseOptions,
                cancellationToken: TestContext.Current.CancellationToken));

        driver = driver.RunGenerators(edited, TestContext.Current.CancellationToken);

        Assert.Contains("IntEnum<Priority>", before);
        Assert.Contains("StringEnum<Priority>", Generated(driver));
        Assert.DoesNotContain(IncrementalStepRunReason.Cached, OutputReasons(driver));
    }

    /// <summary>Two runs over the same compilation must produce byte-identical text.</summary>
    [Fact]
    public void Generator_IsDeterministic()
    {
        var compilation = TestCompilation.Create(PrioritySource);

        Assert.Equal(
            GeneratorRunner.Run(compilation).Source("Priority.g.cs"),
            GeneratorRunner.Run(compilation).Source("Priority.g.cs"));
    }

    private static string Generated(GeneratorDriver driver) =>
        driver.GetRunResult().Results.Single().Source("Priority.g.cs");

    private static IncrementalStepRunReason[] OutputReasons(GeneratorDriver driver) =>
    [
        .. driver.GetRunResult().Results.Single()
            .TrackedOutputSteps
            .SelectMany(step => step.Value)
            .SelectMany(step => step.Outputs)
            .Select(output => output.Reason),
    ];
}
