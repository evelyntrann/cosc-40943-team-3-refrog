using ReFrog.Server.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddReFrogCore();

var app = builder.Build();

app.MapGet("/", () => Results.Text("ReFrog"));

app.Run();

public partial class Program;
