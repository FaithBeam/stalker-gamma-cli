using System.ComponentModel;

namespace stalker_gamma_cli_server;

// Serialized as snake_case (see Program.cs) to match the Python models.

public sealed record NavigateRequestDto(
    [property: Description("Absolute http or https URL to load.")] string Url,
    [property: Description(
        "Follow redirects to the final page. When false, a 3xx response is returned as-is."
    )]
        bool FollowRedirects = true,
    [property: Description(
        "Seconds the whole navigation may take, including solving a Cloudflare challenge. 1 to 300."
    )]
        int TimeoutSeconds = 90
);

public sealed record NavigateResponseDto(
    [property: Description("HTTP status of the returned response.")] int StatusCode,
    [property: Description(
        "URL of the returned response; the final URL when redirects were followed."
    )]
        string Url,
    [property: Description("Rendered HTML of the page. Empty for a redirect response.")]
        string Content,
    [property: Description("Response headers, keyed by lowercase name.")]
        Dictionary<string, object?> Headers
);
