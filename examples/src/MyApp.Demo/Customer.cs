using MyApp.Abstractions;

namespace MyApp.Demo;

// Customer is `sealed`, so MarkerTypeAnalyzer (APP3001) stays green.
// (Remove `sealed` during the demo to see APP3001 fire as a bonus.)
public sealed class Customer : EntityBase
{
    public required string Name { get; init; }

    // 👇 LIVE-DEMO BREAK: 'Adress' trips APP1001, promoted to a build ERROR by
    //    <WarningsAsErrors>APP1001</WarningsAsErrors> in MyApp.Demo.csproj.
    //    Fix it live: rename 'Adress' -> 'Address' (or use the light-bulb
    //    "Rename to 'Address'" from MyApp.CodeFixes) and the build goes green.
    public string Adress { get; init; } = "";
}
