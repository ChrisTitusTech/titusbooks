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

    public async Task<JournalEntrySummary> PostExpenseAsync(
        Guid organizationId,
        PostExpenseCommand command,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"/organizations/{organizationId}/transactions/expenses",
            command,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadRequiredJsonAsync<JournalEntrySummary>(
            response,
            "API returned an empty journal entry response.",
            cancellationToken);
    }

    public async Task<JournalEntrySummary> PostIncomeAsync(
        Guid organizationId,
        PostIncomeCommand command,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"/organizations/{organizationId}/transactions/income",
            command,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadRequiredJsonAsync<JournalEntrySummary>(
            response,
            "API returned an empty journal entry response.",
            cancellationToken);
    }

    public async Task<JournalEntrySummary> PostTransferAsync(
        Guid organizationId,
        PostTransferCommand command,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"/organizations/{organizationId}/transactions/transfers",
            command,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadRequiredJsonAsync<JournalEntrySummary>(
            response,
            "API returned an empty journal entry response.",
            cancellationToken);
    }

    public async Task<IReadOnlyList<AccountRegisterEntrySummary>> ListRegisterAsync(
        Guid organizationId,
        Guid accountId,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (startDate is not null)
        {
            query.Add($"startDate={Uri.EscapeDataString(startDate.Value.ToString("O"))}");
        }

        if (endDate is not null)
        {
            query.Add($"endDate={Uri.EscapeDataString(endDate.Value.ToString("O"))}");
        }

        var path = $"/organizations/{organizationId}/accounts/{accountId}/register";
        if (query.Count > 0)
        {
            path = $"{path}?{string.Join('&', query)}";
        }

        return await httpClient.GetFromJsonAsync<List<AccountRegisterEntrySummary>>(path, cancellationToken)
            ?? [];
    }

    public async Task<ProfitAndLossReportSummary> GetProfitAndLossAsync(
        Guid organizationId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        return await GetRequiredAsync<ProfitAndLossReportSummary>(
            BuildReportPath(organizationId, "profit-and-loss", startDate, endDate),
            "API returned an empty Profit and Loss report.",
            cancellationToken);
    }

    public async Task<AccountBreakdownReportSummary> GetExpensesByCategoryAsync(
        Guid organizationId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        return await GetRequiredAsync<AccountBreakdownReportSummary>(
            BuildReportPath(organizationId, "expenses-by-category", startDate, endDate),
            "API returned an empty expense report.",
            cancellationToken);
    }

    public async Task<AccountBreakdownReportSummary> GetIncomeBySourceAsync(
        Guid organizationId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        return await GetRequiredAsync<AccountBreakdownReportSummary>(
            BuildReportPath(organizationId, "income-by-source", startDate, endDate),
            "API returned an empty income report.",
            cancellationToken);
    }

    public async Task<string> GetReportCsvAsync(
        Guid organizationId,
        string reportName,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var path = BuildReportPath(organizationId, $"{reportName}/csv", startDate, endDate);
        using var response = await httpClient.GetAsync(path, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private async Task<T> GetRequiredAsync<T>(
        string path,
        string emptyResponseMessage,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(path, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadRequiredJsonAsync<T>(response, emptyResponseMessage, cancellationToken);
    }

    private static string BuildReportPath(
        Guid organizationId,
        string reportName,
        DateOnly startDate,
        DateOnly endDate)
    {
        return $"/organizations/{organizationId}/reports/{reportName}"
            + $"?startDate={Uri.EscapeDataString(startDate.ToString("O"))}"
            + $"&endDate={Uri.EscapeDataString(endDate.ToString("O"))}";
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
