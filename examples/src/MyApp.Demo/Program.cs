using System.Text.Json;
using MyApp.Demo;

// Priority.Value comes from the generated partial (typed int).
Console.WriteLine($"Highest priority : {Priority.High} (value {Priority.High.Value})");
Console.WriteLine($"Lowest  priority : {Priority.Low} (value {Priority.Low.Value})");
Console.WriteLine($"All members      : {string.Join(", ", Priority.All)}");

// --- Generated converter #1: System.Text.Json -------------------------------
// PriorityJsonConverter is generated and attached with [JsonConverter] on the
// generated partial, so no JsonSerializerOptions wiring is needed here.
var json = JsonSerializer.Serialize(Priority.High);
var fromJson = JsonSerializer.Deserialize<Priority>(json)!;
Console.WriteLine($"JSON             : {json} -> {fromJson} (same instance: {ReferenceEquals(fromJson, Priority.High)})");

// --- Generated converter #2: EF Core value converter ------------------------
// PrioritySqlConverter is only generated because this project references EF Core.
var sql = new PrioritySqlConverter();
var column = sql.ConvertToProvider(Priority.Low);
var entity = sql.ConvertFromProvider(column);
Console.WriteLine($"SQL column       : {column} -> {entity}");

var customer = new Customer { Name = "Ada Lovelace" };
Console.WriteLine($"Customer         : {customer.Name}");
