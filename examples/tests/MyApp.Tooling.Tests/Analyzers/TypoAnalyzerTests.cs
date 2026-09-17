using Microsoft.CodeAnalysis;
using MyApp.Analyzers;
using MyApp.Tooling.Tests.Infrastructure;

namespace MyApp.Tooling.Tests.Analyzers;

/// <summary>
/// APP1001 — the naming rule that becomes a build error in the demo project.
/// It is the diagnostic the live code fix hangs off, so both what it reports and
/// exactly where it reports it are part of the contract.
/// </summary>
public sealed class TypoAnalyzerTests
{
    /// <summary>
    /// Asserting on the reported span, not just the line, catches a diagnostic that
    /// lands on the right member but the wrong token — which is what a code fix
    /// then tries to rename.
    /// </summary>
    [Theory]
    [InlineData("public class C { public string Adress { get; set; } = \"\"; }", "Adress")]
    [InlineData("public class C { private string _adress = \"\"; }", "_adress")]
    [InlineData("public class C { public void SaveAdress() { } }", "SaveAdress")]
    [InlineData("public class AdressBook { }", "AdressBook")]
    [InlineData("public class C { public string BillingADRESS { get; set; } = \"\"; }", "BillingADRESS")]
    public async Task MisspelledIdentifier_IsReportedOnTheIdentifierToken(string source, string identifier)
    {
        var diagnostics = await AnalyzerRunner.RunAsync(new TypoAnalyzer(), source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("APP1001", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(identifier, diagnostic.SpanText(source));
        Assert.Contains($"Identifier '{identifier}'", diagnostic.GetMessage());
        Assert.Contains("did you mean 'Address'", diagnostic.GetMessage());
    }

    [Theory]
    [InlineData("public class C { public string Address { get; set; } = \"\"; }")]
    [InlineData("public class C { public void SaveAddress() { } }")]
    [InlineData("public class AddressBook { }")]
    [InlineData("public class C { public string Addressee { get; set; } = \"\"; }")]
    public async Task CorrectSpelling_IsNotReported(string source)
    {
        var diagnostics = await AnalyzerRunner.RunAsync(new TypoAnalyzer(), source);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// A property is three symbols to Roslyn — the property plus get_/set_. Without
    /// the AssociatedSymbol guard in the analyzer this reports three times, and the
    /// light bulb offers the same rename three times over.
    /// </summary>
    [Fact]
    public async Task Property_IsReportedOnceNotOncePerAccessor()
    {
        var source = """
            public class Customer
            {
                public string Adress { get; set; } = "";
            }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(new TypoAnalyzer(), source);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task EveryMisspelledMember_IsReportedSeparately()
    {
        var source = """
            public class Customer
            {
                public string Adress { get; set; } = "";
                public string ShippingAdress { get; set; } = "";
                public void CopyAdress() { }
            }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(new TypoAnalyzer(), source);

        Assert.Equal(3, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("APP1001", d.Id));
    }

    /// <summary>
    /// Documents a real gap rather than hiding it: the analyzer registers for
    /// properties, fields, methods and types, so locals and parameters are out of
    /// scope. Widen the SymbolKind list (or add a syntax-node action) if that ever
    /// needs to change.
    /// </summary>
    [Fact]
    public async Task LocalsAndParameters_AreOutOfScope()
    {
        var source = """
            public class C
            {
                public void Ship(string adress)
                {
                    var adressLine = adress;
                }
            }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(new TypoAnalyzer(), source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void SupportedDiagnostics_ExposesOnlyApp1001()
    {
        var rule = Assert.Single(new TypoAnalyzer().SupportedDiagnostics);

        Assert.Equal("APP1001", rule.Id);
        Assert.Equal("Naming", rule.Category);
        Assert.True(rule.IsEnabledByDefault);
        Assert.Equal(DiagnosticSeverity.Warning, rule.DefaultSeverity);
    }
}
