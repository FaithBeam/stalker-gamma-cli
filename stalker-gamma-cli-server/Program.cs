using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Playwright;
using Scalar.AspNetCore;
using stalker_gamma_cli_server;

// Load appsettings.json from next to the executable rather than the working directory, so the
// server works when launched from anywhere (e.g. the Homebrew cask's symlink on PATH).
var builder = WebApplication.CreateBuilder(
    new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory }
);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    // The web default also accepts numbers as strings, which documents them as integer|string.
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});

// Show Docker image build output, which Testcontainers logs at Debug.
builder.Logging.AddFilter(CamoufoxBrowser.TestcontainersLogCategory, LogLevel.Debug);

builder.Services.AddOpenApi(options =>
    options.AddDocumentTransformer(
        (document, _, _) =>
        {
            document.Info.Title = "Camoufox API";
            document.Info.Description =
                "Loads pages through a Camoufox browser, solving Cloudflare Turnstile challenges.";
            return Task.CompletedTask;
        }
    )
);

builder
    .Services.AddOptions<CamoufoxOptions>()
    .Bind(builder.Configuration.GetSection(CamoufoxOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<CamoufoxBrowser>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<CamoufoxBrowser>());
builder.Services.AddHealthChecks().AddCheck<CamoufoxBrowser>("camoufox", tags: ["ready"]);

var app = builder.Build();

// OpenAPI document at /openapi/v1.json, browsable docs at /scalar.
app.MapOpenApi();
app.MapScalarApiReference();

// Health endpoints are regular endpoints (not MapHealthChecks) so they appear in the docs.
var health = app.MapGroup("").WithTags("Health");

health
    .MapGet("/livez", () => TypedResults.Text("Healthy"))
    .WithSummary("Liveness")
    .WithDescription("The process is up and serving requests. No dependency checks.")
    .Produces<string>(StatusCodes.Status200OK, "text/plain");

health
    .MapGet(
        "/readyz",
        async (HealthCheckService healthChecks) =>
        {
            var report = await healthChecks.CheckHealthAsync(check => check.Tags.Contains("ready"));
            return report.Status == HealthStatus.Healthy
                ? TypedResults.Text("Healthy")
                : TypedResults.Text(
                    report.Status.ToString(),
                    statusCode: StatusCodes.Status503ServiceUnavailable
                );
        }
    )
    .WithSummary("Readiness")
    .WithDescription("The Camoufox container is running and the browser is connected.")
    .Produces<string>(StatusCodes.Status200OK, "text/plain")
    .Produces<string>(StatusCodes.Status503ServiceUnavailable, "text/plain");

app.MapPost(
        "/navigate",
        async Task<Results<Ok<NavigateResponseDto>, ValidationProblem, ProblemHttpResult>> (
            NavigateRequestDto request,
            CamoufoxBrowser browser
        ) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (
                !Uri.TryCreate(request.Url, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https")
            )
                errors["url"] = ["Must be an absolute http or https URL."];
            if (request.TimeoutSeconds is < 1 or > 300)
                errors["timeout_seconds"] = ["Must be between 1 and 300."];
            if (errors.Count > 0)
                return TypedResults.ValidationProblem(errors);

            try
            {
                return TypedResults.Ok(
                    await browser.NavigateAsync(
                        request.Url,
                        request.FollowRedirects,
                        TimeSpan.FromSeconds(request.TimeoutSeconds)
                    )
                );
            }
            catch (BrowserUnavailableException e)
            {
                return TypedResults.Problem(
                    e.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable
                );
            }
            catch (TimeoutException e)
            {
                return TypedResults.Problem(
                    e.Message,
                    statusCode: StatusCodes.Status504GatewayTimeout
                );
            }
            catch (PlaywrightException e)
            {
                return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status502BadGateway);
            }
        }
    )
    .WithTags("Browser")
    .WithSummary("Navigate to a URL")
    .WithDescription(
        "Loads the URL in the browser, solving a Cloudflare Turnstile challenge if one appears. "
            + "With follow_redirects off, a 3xx response is returned as-is with empty content."
    )
    .ProducesProblem(StatusCodes.Status502BadGateway)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
    .ProducesProblem(StatusCodes.Status504GatewayTimeout);

app.Run();
