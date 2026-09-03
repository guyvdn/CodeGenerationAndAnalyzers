using MyApp.Demo;

// Priority.Value comes from the generated partial (typed int).
Console.WriteLine($"Highest priority : {Priority.High} (value {Priority.High.Value})");
Console.WriteLine($"Lowest  priority : {Priority.Low} (value {Priority.Low.Value})");

var customer = new Customer { Name = "Ada Lovelace" };
Console.WriteLine($"Customer         : {customer.Name}");
