using System.Net;
using System.Net.Http.Json;

namespace FinancialApp.Core.Api;

public sealed class TitusBooksApiClient
{
    private readonly HttpClient httpClient;

    public TitusBooksApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<IReadOnlyList<OrganizationSummary>> ListOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<List<OrganizationSummary>>("/organizations", cancellationToken)
            ?? [];
    }

    public async Task<OrganizationSummary> CreateOrganizationAsync(
        CreateOrganizationCommand command,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("/organizations", command, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<OrganizationSummary>(cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty organization response.");
    }

    public async Task<IReadOnlyList<AccountSummary>> ListAccountsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<List<AccountSummary>>(
            $"/organizations/{organizationId}/accounts",
            cancellationToken) ?? [];
    }

    public async Task<AccountSummary> CreateAccountAsync(
        Guid organizationId,
        CreateAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"/organizations/{organizationId}/accounts",
            command,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<AccountSummary>(cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty account response.");
    }

    public async Task<AccountSummary> UpdateAccountAsync(
        Guid organizationId,
        Guid accountId,
        string name,
        string? accountSubtype,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync(
            $"/organizations/{organizationId}/accounts/{accountId}",
            new { Name = name, AccountSubtype = accountSubtype },
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<AccountSummary>(cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty account response.");
    }

    public async Task<AccountSummary> DeactivateAccountAsync(
        Guid organizationId,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync(
            $"/organizations/{organizationId}/accounts/{accountId}/deactivate",
            null,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<AccountSummary>(cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty account response.");
    }

    public async Task<SeedAccountsResult> SeedDefaultAccountsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync(
            $"/organizations/{organizationId}/accounts/defaults",
            null,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<SeedAccountsResult>(cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty seed response.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = response.StatusCode == HttpStatusCode.Conflict
            ? "That record already exists."
            : await response.Content.ReadAsStringAsync(cancellationToken);

        throw new InvalidOperationException(string.IsNullOrWhiteSpace(message)
            ? $"API returned HTTP {(int)response.StatusCode}."
            : message);
    }
}
