using CoachHub.API.Common.Errors;
using CoachHub.API.Settings;
using CoachHub.Application;
using CoachHub.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<CoachHubOptions>()
    .BindConfiguration(CoachHubOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("Health")
    .WithTags("Platform");

app.Run();

public partial class Program;
