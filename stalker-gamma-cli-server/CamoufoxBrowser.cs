using System.ComponentModel.DataAnnotations;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace stalker_gamma_cli_server;

// Bound from the "Camoufox" section of configuration (appsettings.json).
public sealed class CamoufoxOptions
{
    public const string Section = "Camoufox";

    // Docker image of the Camoufox server, e.g. "faithbeam/stalker-gamma-camoufox-server:v1.1.1".
    [Required(AllowEmptyStrings = false)]
    public string Image { get; set; } = "";

    // The watchdog probes the browser every ProbeInterval by opening a page and running a
    // script in it. The container is replaced after MaxProbeFailures probes in a row fail
    // or take longer than ProbeTimeout.
    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan ProbeInterval { get; set; } = TimeSpan.FromSeconds(30);

    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan ProbeTimeout { get; set; } = TimeSpan.FromSeconds(10);

    [Range(1, 100)]
    public int MaxProbeFailures { get; set; } = 3;

    // Wait between a failed start (e.g. Docker unavailable) and the next attempt.
    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan RestartDelay { get; set; } = TimeSpan.FromSeconds(10);

    // Bounds teardown of a browser or container that has stopped responding.
    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan TeardownTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

// Thrown when there's no usable browser, e.g. before startup finishes or while the
// watchdog is restarting the container.
public sealed class BrowserUnavailableException(string message) : Exception(message);

// Owns the Camoufox container and a single browser connection to it. Started in the
// background so the app can answer /livez and /readyz while the image builds and the
// container boots; reports healthy once the browser is connected. A watchdog replaces the
// container when the connection drops or the browser stops answering probes.
public sealed class CamoufoxBrowser(
    IOptions<CamoufoxOptions> options,
    ILogger<CamoufoxBrowser> logger,
    ILoggerFactory loggerFactory
) : BackgroundService, IHealthCheck
{
    // Category for Testcontainers' output. Image build steps are logged at Debug, so
    // Program.cs enables Debug for this category to show build progress.
    public const string TestcontainersLogCategory = "Testcontainers";

    private readonly CamoufoxOptions _options = options.Value;

    private IPlaywright? _playwright;
    private IContainer? _container;
    private IBrowser? _browser;
    private IBrowserContext? _context;

    public bool IsReady => _context is not null && _browser?.IsConnected == true;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _playwright = await Playwright.CreateAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var (browser, context) = await StartBrowserAsync(_playwright, stoppingToken);
                await WatchAsync(browser, context, stoppingToken);
                logger.LogWarning("Restarting the Camoufox container");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                logger.LogError(
                    e,
                    "Camoufox failed to start; retrying in {Delay}",
                    _options.RestartDelay
                );
                await TearDownAsync();
                await Task.Delay(_options.RestartDelay, stoppingToken);
                continue;
            }

            await TearDownAsync();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await TearDownAsync();
        _playwright?.Dispose();
    }

    private async Task<(IBrowser, IBrowserContext)> StartBrowserAsync(
        IPlaywright playwright,
        CancellationToken cancellationToken
    )
    {
        // Start the server on a random free host port. The container is removed when it's
        // disposed, or by Testcontainers if the app dies.
        var container = new ContainerBuilder(_options.Image)
            .WithLogger(loggerFactory.CreateLogger(TestcontainersLogCategory))
            .WithPortBinding(9222, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Websocket endpoint"))
            .Build();
        _container = container;
        await container.StartAsync(cancellationToken);

        var browser = await playwright.Firefox.ConnectAsync(
            $"ws://{container.Hostname}:{container.GetMappedPublicPort(9222)}/camoufox"
        );
        _browser = browser;

        // Share one context so the cf_clearance cookie from a solved challenge is reused
        // across requests.
        var context = await browser.NewContextAsync();
        _context = context;
        logger.LogInformation("Camoufox browser is ready");
        return (browser, context);
    }

    // Returns when the browser should be replaced: its connection dropped, or it failed
    // MaxProbeFailures probes in a row.
    private async Task WatchAsync(
        IBrowser browser,
        IBrowserContext context,
        CancellationToken stoppingToken
    )
    {
        var disconnected = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        browser.Disconnected += (_, _) => disconnected.TrySetResult();
        if (!browser.IsConnected)
            disconnected.TrySetResult();

        var failures = 0;
        while (true)
        {
            var wait = Task.Delay(_options.ProbeInterval, stoppingToken);
            if (await Task.WhenAny(wait, disconnected.Task) == disconnected.Task)
            {
                logger.LogWarning("Lost the connection to the Camoufox browser");
                return;
            }
            await wait;

            var probe = ProbeAsync(context);
            try
            {
                await probe.WaitAsync(_options.ProbeTimeout, stoppingToken);
                failures = 0;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                _ = probe.ContinueWith(
                    t => t.Exception,
                    TaskContinuationOptions.OnlyOnFaulted
                        | TaskContinuationOptions.ExecuteSynchronously
                );
                failures++;
                logger.LogWarning(
                    "Camoufox probe failed ({Failures}/{Max}): {Error}",
                    failures,
                    _options.MaxProbeFailures,
                    e.Message
                );
                if (failures >= _options.MaxProbeFailures)
                    return;
            }
        }
    }

    private static async Task ProbeAsync(IBrowserContext context)
    {
        var page = await context.NewPageAsync();
        try
        {
            await page.EvaluateAsync("() => 1");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    // Clears the current browser first so IsReady turns false and new requests are refused,
    // then removes the container, which also ends any calls still waiting on the browser.
    private async Task TearDownAsync()
    {
        var context = Interlocked.Exchange(ref _context, null);
        var browser = Interlocked.Exchange(ref _browser, null);
        var container = Interlocked.Exchange(ref _container, null);

        try
        {
            if (container is not null)
                await container.DisposeAsync().AsTask().WaitAsync(_options.TeardownTimeout);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to remove the Camoufox container");
        }

        try
        {
            if (context is not null)
                await context.DisposeAsync().AsTask().WaitAsync(_options.TeardownTimeout);
            if (browser is not null)
                await browser.DisposeAsync().AsTask().WaitAsync(_options.TeardownTimeout);
        }
        catch (Exception e)
        {
            // Expected when the browser died with the container.
            logger.LogDebug(e, "Failed to close the Camoufox browser connection");
        }
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    ) =>
        Task.FromResult(
            IsReady
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("The Camoufox browser is not connected.")
        );

    // Loads the url (solving Cloudflare if needed). With followRedirects off, a redirect isn't
    // followed and its 3xx response is returned with empty content. The request still goes
    // through the browser so it carries the clearance cookie and the browser's fingerprint.
    // Throws TimeoutException if the whole navigation takes longer than timeout.
    public async Task<NavigateResponseDto> NavigateAsync(
        string url,
        bool followRedirects,
        TimeSpan timeout
    )
    {
        var context =
            _context ?? throw new BrowserUnavailableException("The Camoufox browser is not ready.");
        IPage? page = null;
        var abandoned = false;
        var navigation = OpenAndLoadAsync();
        try
        {
            return await navigation.WaitAsync(timeout);
        }
        catch (TimeoutException) when (!navigation.IsCompleted)
        {
            // The browser may be frozen, so don't wait on it. Closing the page makes the
            // abandoned navigation's pending calls fail; if the page isn't open yet,
            // OpenAndLoadAsync closes it once it is. The watchdog replaces a browser that
            // stays unresponsive.
            Volatile.Write(ref abandoned, true);
            _ = navigation.ContinueWith(
                t => t.Exception,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously
            );
            if (Volatile.Read(ref page) is { } opened)
                _ = opened
                    .CloseAsync()
                    .ContinueWith(
                        t => t.Exception,
                        TaskContinuationOptions.OnlyOnFaulted
                            | TaskContinuationOptions.ExecuteSynchronously
                    );
            throw new TimeoutException(
                $"Navigation to {url} did not finish within {timeout.TotalSeconds:0} seconds."
            );
        }

        async Task<NavigateResponseDto> OpenAndLoadAsync()
        {
            var opened = await context.NewPageAsync();
            Volatile.Write(ref page, opened);
            try
            {
                if (Volatile.Read(ref abandoned))
                    throw new TimeoutException("Navigation was abandoned.");
                return await LoadAsync(opened, url, followRedirects);
            }
            finally
            {
                await opened.CloseAsync();
            }
        }
    }

    private static async Task<NavigateResponseDto> LoadAsync(
        IPage page,
        string url,
        bool followRedirects
    )
    {
        // Firefox doesn't pass a redirect's follow-up request to route handlers, so with
        // followRedirects off, main-frame navigations are fetched here without following
        // redirects. A redirect is recorded and the navigation aborted; anything else (the
        // page, or a Cloudflare challenge) is handed to the browser unchanged. The fetch
        // uses the context's cookies, so it carries the clearance cookie once solved.
        IAPIResponse? redirect = null;
        if (!followRedirects)
        {
            await page.RouteAsync(
                "**/*",
                async route =>
                {
                    if (!route.Request.IsNavigationRequest || route.Request.Frame != page.MainFrame)
                    {
                        await route.ContinueAsync();
                        return;
                    }

                    var fetched = await route.FetchAsync(new() { MaxRedirects = 0 });
                    if (fetched.Status is >= 300 and < 400)
                    {
                        redirect = fetched;
                        await route.AbortAsync();
                    }
                    else
                    {
                        await route.FulfillAsync(new() { Response = fetched });
                    }
                }
            );
        }

        // The latest main-frame response, i.e. the final page.
        IResponse? response = null;
        page.Response += (_, r) =>
        {
            if (r.Request.IsNavigationRequest && r.Frame == page.MainFrame)
                response = r;
        };

        try
        {
            await page.GotoAsync(url);
        }
        catch (PlaywrightException) when (redirect is not null)
        {
            // Navigation fails when we abort at the redirect; the 3xx was recorded.
        }

        if (redirect is null && IsChallenge(response))
        {
            // The real response can arrive before the challenge document is replaced, so
            // tag the challenge window and wait for a document without the tag.
            await page.EvaluateAsync("() => window.__cfChallenge = true");
            await SolveTurnstileAsync(page, passed: () => redirect is not null);

            if (redirect is null)
                await page.WaitForFunctionAsync("() => !window.__cfChallenge");
        }

        if (redirect is not null)
            return new NavigateResponseDto(
                redirect.Status,
                redirect.Url,
                "",
                redirect.Headers.ToDictionary(h => h.Key, object? (h) => h.Value)
            );

        if (response is null)
            throw new PlaywrightException($"No response received for {url}");

        // http:// URLs on HSTS-preloaded sites are upgraded inside Firefox before any
        // request reaches the route, so their redirects are followed natively. Report the
        // first real redirect in the chain instead, skipping the internal https upgrade.
        if (!followRedirects && response.Request.RedirectedFrom is not null)
        {
            var chain = new List<IRequest>();
            for (var r = response.Request; r is not null; r = r.RedirectedFrom)
                chain.Insert(0, r);

            for (var i = 0; i < chain.Count - 1; i++)
            {
                if (IsHttpsUpgrade(new Uri(chain[i].Url), new Uri(chain[i + 1].Url)))
                    continue;

                var hop = await chain[i].ResponseAsync();
                if (hop is not null)
                    return new NavigateResponseDto(
                        hop.Status,
                        hop.Url,
                        "",
                        (await hop.AllHeadersAsync()).ToDictionary(
                            h => h.Key,
                            object? (h) => h.Value
                        )
                    );
            }
        }

        await page.WaitForLoadStateAsync(LoadState.Load);
        var content = await page.ContentAsync();

        var headers = await response.AllHeadersAsync();
        return new NavigateResponseDto(
            response.Status,
            response.Url,
            content,
            headers.ToDictionary(h => h.Key, object? (h) => h.Value)
        );
    }

    private static bool IsHttpsUpgrade(Uri from, Uri to) =>
        from.Scheme == "http"
        && to.Scheme == "https"
        && from.Host == to.Host
        && from.PathAndQuery == to.PathAndQuery;

    // Cloudflare tags its interstitial with this header.
    private static bool IsChallenge(IResponse? response) =>
        response?.Headers.GetValueOrDefault("cf-mitigated") == "challenge";

    // The Turnstile checkbox lives in a cross-origin iframe inside a closed shadow root, so
    // anchor on the widget's container in the main document (parent of the hidden response
    // input) and click by coordinates. Requires the server to be launched with disable_coop=True.
    // Returns once Cloudflare navigates to the real page, or once `passed` reports success
    // (used when that navigation is intercepted and never produces a page response).
    private static async Task SolveTurnstileAsync(
        IPage page,
        Func<bool>? passed = null,
        int timeoutMs = 60_000
    )
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var container = page.Locator("input[name='cf-turnstile-response']").Locator("..");

        while (DateTime.UtcNow < deadline)
        {
            var box =
                await container.CountAsync() > 0 ? await container.First.BoundingBoxAsync() : null;

            if (box is null || box.Width == 0 || box.Height == 0)
            {
                await page.WaitForTimeoutAsync(500);
                continue;
            }

            // Passing the challenge navigates the main frame to the real (unmitigated) page.
            var realPage = page.WaitForResponseAsync(
                r =>
                    r.Request.IsNavigationRequest
                    && r.Frame == page.MainFrame
                    && !r.Headers.ContainsKey("cf-mitigated"),
                new() { Timeout = 20_000 }
            );

            // Give the widget a moment to render the checkbox before clicking.
            await page.WaitForTimeoutAsync(Random.Shared.Next(1000, 2000));
            var x = box.X + 22 + Random.Shared.Next(-3, 4);
            var y = box.Y + box.Height / 2 + Random.Shared.Next(-3, 4);
            await page.Mouse.MoveAsync(x, y, new() { Steps = 15 });
            await page.Mouse.ClickAsync(x, y, new() { Delay = Random.Shared.Next(60, 140) });

            try
            {
                while (!realPage.IsCompleted && passed?.Invoke() != true)
                    await Task.WhenAny(realPage, Task.Delay(250));
                if (passed?.Invoke() == true)
                    return;
                await realPage;
                return;
            }
            catch (TimeoutException)
            {
                // Still on the challenge; try again.
            }
        }

        throw new TimeoutException("Cloudflare challenge was not solved in time.");
    }
}
