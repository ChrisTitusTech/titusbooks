using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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

        return await ReadRequiredJsonAsync<OrganizationSummary>(
            response,
            "API returned an empty organization response.",
            cancellationToken);
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

        return await ReadRequiredJsonAsync<AccountSummary>(
            response,
            "API returned an empty account response.",
            cancellationToken);
    }

    public async Task<AccountSummary> UpdateAccountAsync(
        Guid organizationId,
        Guid accountId,
        UpdateAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync(
            $"/organizations/{organizationId}/accounts/{accountId}",
            command,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadRequiredJsonAsync<AccountSummary>(
            response,
            "API returned an empty account response.",
            cancellationToken);
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

        return await ReadRequiredJsonAsync<AccountSummary>(
            response,
            "API returned an empty account response.",
            cancellationToken);
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

        return await ReadRequiredJsonAsync<SeedAccountsResult>(
            response,
            "API returned an empty seed response.",
            cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = TryReadErrorMessage(responseBody);

        if (string.IsNullOrWhiteSpace(message) && response.StatusCode == HttpStatusCode.Conflict)
        {
            message = "That record already exists.";
        }

        throw new InvalidOperationException(string.IsNullOrWhiteSpace(message)
            ? $"API returned HTTP {(int)response.StatusCode}."
            : message);
    }

    private static async Task<T> ReadRequiredJsonAsync<T>(
        HttpResponseMessage response,
        string emptyResponseMessage,
        CancellationToken cancellationToken)
    {
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            ?? throw new InvalidOperationException(emptyResponseMessage);
    }

    private static string? TryReadErrorMessage(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            var error = JsonSerializer.Deserialize<ApiErrorResponse>(
                responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return string.IsNullOrWhiteSpace(error?.Error) ? responseBody : error.Error;
        }
        catch (JsonException)
        {
            return responseBody;
        }
    }

    private sealed record ApiErrorResponse(string? Error);
}
