using CoachHub.API.Common.Routing;
using CoachHub.API.Auditing;
using CoachHub.API.Auth;
using CoachHub.API.Common.Errors;
using CoachHub.Application;
using CoachHub.Application.Auditing;
using CoachHub.Application.Common.Tenancy;
using CoachHub.Infrastructure;
using CoachHub.Infrastructure.Auth;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(
    builder.Configuration,
    builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"));
builder.Services.AddCoachHubAuthentication(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditActorAccessor, HttpAuditActorAccessor>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
// P14: live chat and notifications over SignalR; the publisher replaces Infrastructure's no-op one.
builder.Services.AddSignalR().AddJsonProtocol(options =>
    options.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, CoachHub.API.Realtime.SubjectUserIdProvider>();
builder.Services.AddSingleton<CoachHub.Application.Notifications.IRealtimePublisher, CoachHub.API.Realtime.SignalRRealtimePublisher>();
// Enums travel as names (each enum also declares this itself, so tests and clients agree).
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("authentication", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    // Every open tab refreshes about every 15 minutes, and each new tab once at start-up, so this
    // is looser than sign-in. Replays are caught by rotation, not by the limit.
    options.AddPolicy("session-refresh", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    // The branded login page loads its brand, logo and favicon on every visit; images are cached.
    options.AddPolicy("branding", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
else
{
    app.MapOpenApi();
}

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.XFrameOptions = "DENY";
        context.Response.Headers.Append("Referrer-Policy", "no-referrer");
        context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
        context.Response.Headers.Append("Permissions-Policy", "camera=(), geolocation=(), microphone=()");

        if (context.Request.Path.StartsWithSegments("/" + ApiRoutes.V1 + "/auth") ||
            context.Request.Path.StartsWithSegments("/" + ApiRoutes.V1 + "/me"))
        {
            context.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
            context.Response.Headers.Pragma = "no-cache";
        }

        return Task.CompletedTask;
    });

    await next();
});

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<CoachHub.API.Realtime.RealtimeHub>(CoachHub.API.Realtime.RealtimeHub.Path);
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("Health")
    .WithTags("Platform");

await using (var scope = app.Services.CreateAsyncScope())
{
    var bootstrapper = scope.ServiceProvider.GetRequiredService<AdminBootstrapper>();
    await bootstrapper.InitializeAsync();
}

app.Run();

public partial class Program;
