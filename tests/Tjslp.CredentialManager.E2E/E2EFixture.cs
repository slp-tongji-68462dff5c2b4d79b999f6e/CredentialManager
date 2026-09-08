using Tjslp.CredentialManager;
using Tjslp.CredentialManager.E2E.MockDownstream;
using Tjslp.CredentialManager.E2E.MockOidc;

namespace Tjslp.CredentialManager.E2E;

public sealed class E2EFixture : IAsyncLifetime
{
    private readonly List<WebApplication> servers = new();

    private TestCertificate? certificate;

    public HttpClient AppClient { get; private set; } = null!;

    public string AppBaseUrl { get; private set; } = null!;

    public MockDownstreamState Downstream { get; private set; } = null!;

    public HttpClientHandler CreateTrustingHandler() => certificate!.CreateTrustingHandler();

    public async Task InitializeAsync()
    {
        Downstream = new MockDownstreamState();

        var users = new List<MockUser>
        {
            new("alice", "Alice", Array.Empty<string>()),
            new("bob", "Bob", Array.Empty<string>()),
            new("administrator", "Administrator", new[] { "administrator" }),
        };
        var oidcState = new MockOidcState(users);

        certificate = TestCertificate.Create(Path.GetTempPath());

        var downstreamUrl = await StartServerAsync(app => app.MapMockDownstream(Downstream));
        var oidcUrl = await StartHttpsServerAsync(app => app.MapMockOidc(oidcState));

        var databasePath = Path.Combine(Path.GetTempPath(), $"credentialmanager-e2e-{Guid.NewGuid():N}.db");

        var app = new ServeCommand
        {
            Listen = "http://127.0.0.1:0",
            Data = databasePath,
            Downstream = downstreamUrl,
            Administrator = "administrator",
            Title = "测试平台",
            Oidc = oidcUrl,
            OidcId = "test-client",
            OidcSecret = "test-secret",
            OidcCa = certificate.RootCaPem,
        }.BuildApp();

        await app.StartAsync();
        servers.Add(app);

        AppBaseUrl = app.Urls.First();
        AppClient = new HttpClient(certificate.CreateTrustingHandler())
        {
            BaseAddress = new Uri(AppBaseUrl),
        };
        AppClient.DefaultRequestHeaders.UserAgent.Clear();
    }

    public async Task DisposeAsync()
    {
        AppClient.Dispose();
        foreach (var server in servers)
        {
            await server.StopAsync();
            await server.DisposeAsync();
        }
        certificate?.Dispose();
    }

    private async Task<string> StartServerAsync(Action<WebApplication> configure)
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
        {
            Args = new[] { "--urls", "http://127.0.0.1:0" },
        });
        builder.WebHost.UseKestrel();
        builder.Services.AddRouting();
        var app = builder.Build();
        configure(app);
        await app.StartAsync();
        servers.Add(app);
        return app.Urls.First();
    }

    private async Task<string> StartHttpsServerAsync(Action<WebApplication> configure)
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
        {
            Args = new[] { "--urls", "https://127.0.0.1:0" },
        });
        builder.WebHost.UseKestrel(options =>
        {
            options.ConfigureHttpsDefaults(defaults => defaults.ServerCertificate = certificate!.ServerCertificate);
        });
        builder.Services.AddRouting();
        var app = builder.Build();
        configure(app);
        await app.StartAsync();
        servers.Add(app);
        return app.Urls.First();
    }
}
