using System.Net;
using System.Text.RegularExpressions;

namespace Tjslp.CredentialManager.E2E;

public sealed class E2ETests : IClassFixture<E2EFixture>
{
    private readonly E2EFixture fixture;

    public E2ETests(E2EFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Unauthenticated_request_is_redirected_to_login()
    {
        var response = await fixture.AppClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task User_can_create_list_and_revoke_own_credential()
    {
        var client = await LoginAsync("alice");

        var credPage = await client.GetAsync("/");
        credPage.EnsureSuccessStatusCode();
        var html = await credPage.Content.ReadAsStringAsync();

        var token = ExtractAntiforgeryToken(html);
        var createResp = await client.PostAsync(
            "/?handler=Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["alias"] = "ci",
                ["expire"] = "2027-01-01T00:00:00Z",
            }));

        createResp.EnsureSuccessStatusCode();
        var createHtml = await createResp.Content.ReadAsStringAsync();

        var id = ExtractId(createHtml);
        Assert.NotNull(id);

        var listResp = await client.GetAsync("/");
        listResp.EnsureSuccessStatusCode();
        var listHtml = await listResp.Content.ReadAsStringAsync();
        Assert.Contains(id!, listHtml);

        var revokeResp = await client.PostAsync(
            "/?handler=Revoke",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = id!,
            }));
        revokeResp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task User_cannot_see_another_users_credential()
    {
        var alice = await LoginAsync("alice");
        var id = await CreateCredentialAsync(alice, "alice-private");

        var bob = await LoginAsync("bob");
        var bobHtml = await GetCredentialsHtmlAsync(bob);

        Assert.DoesNotContain(id, bobHtml);
    }

    [Fact]
    public async Task Administrator_can_see_and_revoke_other_users_credential()
    {
        var alice = await LoginAsync("alice");
        var id = await CreateCredentialAsync(alice, "alice-for-admin");

        var administrator = await LoginAsync("administrator");
        var administratorHtml = await GetAdministratorHtmlAsync(administrator);
        Assert.Contains(id, administratorHtml);

        var token = ExtractAntiforgeryToken(administratorHtml);
        var revokeResp = await administrator.PostAsync(
            "/administration?handler=Revoke",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = id,
            }));
        revokeResp.EnsureSuccessStatusCode();

        var aliceHtml = await GetCredentialsHtmlAsync(alice);
        Assert.DoesNotContain(id, aliceHtml);
    }

    [Fact]
    public async Task Non_admin_cannot_access_admin_page()
    {
        var bob = await LoginAsync("bob");

        var response = await bob.GetAsync("/administration");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Non_admin_cannot_revoke_other_users_credential()
    {
        var alice = await LoginAsync("alice");
        var id = await CreateCredentialAsync(alice, "alice-protected");

        var bob = await LoginAsync("bob");
        var bobHtml = await GetCredentialsHtmlAsync(bob);
        var token = ExtractAntiforgeryToken(bobHtml);

        var revokeResp = await bob.PostAsync(
            "/?handler=Revoke",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = id,
            }));
        revokeResp.EnsureSuccessStatusCode();

        var aliceHtml = await GetCredentialsHtmlAsync(alice);
        Assert.Contains(id, aliceHtml);
    }

    private async Task<string> CreateCredentialAsync(HttpClient client, string alias)
    {
        var html = await GetCredentialsHtmlAsync(client);
        var token = ExtractAntiforgeryToken(html);

        var createResp = await client.PostAsync(
            "/?handler=Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["alias"] = alias,
                ["expire"] = "2027-01-01T00:00:00Z",
            }));
        createResp.EnsureSuccessStatusCode();

        var createHtml = await createResp.Content.ReadAsStringAsync();
        var id = ExtractId(createHtml);
        Assert.NotNull(id);
        return id!;
    }

    private async Task<string> GetCredentialsHtmlAsync(HttpClient client)
    {
        var response = await client.GetAsync("/");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string> GetAdministratorHtmlAsync(HttpClient client)
    {
        var response = await client.GetAsync("/administration");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<HttpClient> LoginAsync(string sub)
    {
        var client = new HttpClient(fixture.CreateTrustingHandler())
        {
            BaseAddress = new Uri(fixture.AppBaseUrl),
        };

        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var loginUrl = response.Headers.Location!;

        response = await client.GetAsync(loginUrl.ToString());
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var authorizeUrl = response.Headers.Location!;

        response = await client.GetAsync(authorizeUrl.ToString());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var form = await response.Content.ReadAsStringAsync();

        var redirectUri = ExtractHidden(form, "redirect_uri");
        var state = ExtractHidden(form, "state");
        var nonce = ExtractHidden(form, "nonce");

        response = await client.PostAsync(
            new Uri(authorizeUrl, "/authorize/choose").ToString(),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["redirect_uri"] = redirectUri,
                ["state"] = state,
                ["nonce"] = nonce,
                ["sub"] = sub,
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var callbackUrl = response.Headers.Location!;

        response = await client.GetAsync(callbackUrl.ToString());

        while (response.StatusCode == HttpStatusCode.Redirect)
        {
            response = await client.GetAsync(response.Headers.Location!.ToString());
        }

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return client;
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var m = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]*)\"");
        return m.Success ? m.Groups[1].Value : throw new Exception("no antiforgery token");
    }

    private static string? ExtractId(string html)
    {
        var m = Regex.Match(html, "ID: ([0-9a-f]+)");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string ExtractHidden(string html, string name)
    {
        var m = Regex.Match(html, $"name=\"{name}\" value=\"([^\"]*)\"");
        return m.Success ? m.Groups[1].Value : throw new Exception($"missing hidden {name}");
    }
}
