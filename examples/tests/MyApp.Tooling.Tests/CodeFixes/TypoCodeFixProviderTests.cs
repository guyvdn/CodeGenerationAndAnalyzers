using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using MyApp.Analyzers;
using MyApp.CodeFixes;
using MyApp.Tooling.Tests.Infrastructure;

namespace MyApp.Tooling.Tests.CodeFixes;

/// <summary>
/// The light bulb behind APP1001. The fix IS the correct form, which is the whole
/// argument for shipping one: an agent applying it cannot guess the spelling wrong.
/// So these tests assert on the code that comes out, not just that an action was
/// offered.
/// </summary>
public sealed class TypoCodeFixProviderTests
{
    [Fact]
    public void Provider_FixesExactlyApp1001()
    {
        var provider = new TypoCodeFixProvider();

        Assert.Equal(["APP1001"], provider.FixableDiagnosticIds);
    }

    /// <summary>
    /// The batch fixer is what gives "Fix all in document / project / solution" for
    /// free — and what makes the rule cheap to adopt on an existing codebase.
    /// </summary>
    [Fact]
    public void Provider_SupportsFixAll() =>
        Assert.Same(WellKnownFixAllProviders.BatchFixer, new TypoCodeFixProvider().GetFixAllProvider());

    [Fact]
    public async Task Fix_IsOfferedOnceWithTheCorrectedNameInItsTitle()
    {
        var actions = await OfferedFixesAsync("""
            public class Customer
            {
                public string Adress { get; set; } = "";
            }
            """);

        var action = Assert.Single(actions);
        Assert.Equal("Rename to 'Address'", action.Title);
    }

    [Fact]
    public async Task Fix_RenamesThePropertyAndEveryUseOfIt()
    {
        var fixedSource = await ApplyAsync("""
            public class Customer
            {
                public string Adress { get; set; } = "";

                public string Label() => Adress.Trim();
            }
            """);

        Assert.Contains("public string Address { get; set; }", fixedSource);
        Assert.Contains("Address.Trim()", fixedSource);
        Assert.DoesNotContain("Adress", fixedSource);
    }

    [Theory]
    [InlineData("public class C { public string ShippingAdress { get; set; } = \"\"; }", "ShippingAddress")]
    [InlineData("public class C { private string _adress = \"\"; }", "_address")]
    [InlineData("public class C { public void SaveAdress() { } }", "SaveAddress")]
    [InlineData("public class AdressBook { }", "AddressBook")]
    public async Task Fix_CorrectsBothCasingsAndLeavesTheRestOfTheNameAlone(string source, string expected)
    {
        var fixedSource = await ApplyAsync(source);

        Assert.Contains(expected, fixedSource);
    }

    /// <summary>
    /// The fix returns a changed <c>Solution</c>, not a changed document, because
    /// Renamer reaches every reference — including the ones in other files.
    /// </summary>
    [Fact]
    public async Task Fix_ReachesReferencesInOtherDocuments()
    {
        var declaration = """
            public class Customer
            {
                public string Adress { get; set; } = "";
            }
            """;

        var usage = """
            public static class Shipping
            {
                public static string Label(Customer customer) => customer.Adress;
            }
            """;

        var project = CodeFixRunner.CreateProject(declaration, usage);
        var diagnostic = Assert.Single(await CodeFixRunner.AnalyzeAsync(new TypoAnalyzer(), project));
        var action = Assert.Single(await CodeFixRunner.GetFixesAsync(new TypoCodeFixProvider(), project, diagnostic));

        var documents = await CodeFixRunner.DocumentTextsAsync(await CodeFixRunner.ApplyAsync(action));

        Assert.All(documents, document => Assert.DoesNotContain("Adress", document));
        Assert.Contains("public string Address { get; set; }", documents[0]);
        Assert.Contains("customer.Address", documents[1]);
    }

    /// <summary>After the fix, the analyzer that triggered it has nothing left to say.</summary>
    [Fact]
    public async Task FixedCode_NoLongerReportsApp1001()
    {
        var fixedSource = await ApplyAsync("""
            public class Customer
            {
                public string Adress { get; set; } = "";
            }
            """);

        var diagnostics = await AnalyzerRunner.RunAsync(new TypoAnalyzer(), fixedSource);

        Assert.Empty(diagnostics);
    }

    private static async Task<string> ApplyAsync(string source)
    {
        var actions = await OfferedFixesAsync(source);
        var solution = await CodeFixRunner.ApplyAsync(Assert.Single(actions));

        return Assert.Single(await CodeFixRunner.DocumentTextsAsync(solution));
    }

    private static async Task<IReadOnlyList<CodeAction>> OfferedFixesAsync(string source)
    {
        var project = CodeFixRunner.CreateProject(source);
        var diagnostic = Assert.Single(await CodeFixRunner.AnalyzeAsync(new TypoAnalyzer(), project));

        return await CodeFixRunner.GetFixesAsync(new TypoCodeFixProvider(), project, diagnostic);
    }
}
