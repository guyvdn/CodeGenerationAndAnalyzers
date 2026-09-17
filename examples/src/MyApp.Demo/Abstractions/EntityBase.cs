namespace MyApp.Demo.Abstractions;

// Marker base type that MarkerTypeAnalyzer (APP3001) keys on. Any non-sealed,
// non-abstract class deriving from this triggers APP3001.
public abstract class EntityBase
{
    public int Id { get; init; }
}
