using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;
using ModelContextProtocol.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddAppConfigurationSources<Program>();
builder.Services.AddHttpContextAccessor();
builder.AddNpgsqlDbContext<AppDbContext>("database");
builder.Services.AddHostedService<DatabaseSeeder>();

builder.Services.AddProblemDetails();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// Configure AzureAd options
builder
    .Services.AddOptions<AzureAdOptions>()
    .BindConfiguration(AzureAdOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

var azureAdOptions =
    builder.Configuration.GetSection(AzureAdOptions.SectionName).Get<AzureAdOptions>()
    ?? throw new InvalidOperationException("AzureAd configuration is required.");

builder
    .Services.AddAuthentication(options =>
    {
        options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddMcp(options =>
    {
        options.ResourceMetadata = new()
        {
            AuthorizationServers =
            {
                new Uri($"{azureAdOptions.Instance}{azureAdOptions.TenantId}"),
            },
            ScopesSupported = [.. azureAdOptions.ScopesArray],
        };
    })
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddInMemoryTokenCaches();
builder.Services.AddAuthorization(options =>
{
    // Default policy requires authenticated user
    options.FallbackPolicy = options.DefaultPolicy;
});

builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

builder
    .Services.AddMcpServer(options =>
    {
        options.ServerInfo = new() { Name = "My MCP Server (HTTP)", Version = "1.0.0" };
    })
    .AddAuthorizationFilters()
    .WithHttpTransport(options =>
    {
        options.Stateless = true;
    })
    .WithToolsFromAssembly();

builder.Services.AddOpenApi();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

var app = builder.Build();

app.UseExceptionHandler();
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapMcp("/mcp").RequireAuthorization();

// Health check endpoint
app.MapGet(
        "/health",
        () =>
            Results.Ok(
                new
                {
                    status = "healthy",
                    server = "My MCP Server (HTTP)",
                    version = "1.0.0",
                    timestamp = DateTime.UtcNow,
                }
            )
    )
    .WithName("HealthCheck")
    .WithTags("Health");

app.Run();
