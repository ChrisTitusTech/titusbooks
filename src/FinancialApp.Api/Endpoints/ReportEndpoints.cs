using FinancialApp.Api.Reporting;
using FinancialApp.Core.Organizations;
using FinancialApp.Reports;
using Microsoft.AspNetCore.Mvc;

namespace FinancialApp.Api.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/organizations/{organizationId:guid}/reports/profit-and-loss", async (
            Guid organizationId,
            DateOnly startDate,
            DateOnly endDate,
            [FromServices] IOrganizationRepository organizationRepository,
            [FromServices] FinancialReportService reportService,
            CancellationToken cancellationToken) =>
        {
            if (!await EndpointGuards.OrganizationExistsAsync(organizationRepository, organizationId, cancellationToken))
            {
                return Results.NotFound();
            }

            try
            {
                var report = await reportService.GetProfitAndLossAsync(
                    organizationId,
                    startDate,
                    endDate,
                    cancellationToken);
                return Results.Ok(ProfitAndLossReportResponse.FromReport(report));
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(EndpointGuards.Error(exception.Message));
            }
        });

        endpoints.MapGet("/organizations/{organizationId:guid}/reports/expenses-by-category", async (
            Guid organizationId,
            DateOnly startDate,
            DateOnly endDate,
            [FromServices] IOrganizationRepository organizationRepository,
            [FromServices] FinancialReportService reportService,
            CancellationToken cancellationToken) =>
        {
            return await GetBreakdownReportAsync(
                organizationId,
                startDate,
                endDate,
                organizationRepository,
                reportService.GetExpenseByCategoryAsync,
                cancellationToken);
        });

        endpoints.MapGet("/organizations/{organizationId:guid}/reports/income-by-source", async (
            Guid organizationId,
            DateOnly startDate,
            DateOnly endDate,
            [FromServices] IOrganizationRepository organizationRepository,
            [FromServices] FinancialReportService reportService,
            CancellationToken cancellationToken) =>
        {
            return await GetBreakdownReportAsync(
                organizationId,
                startDate,
                endDate,
                organizationRepository,
                reportService.GetIncomeBySourceAsync,
                cancellationToken);
        });

        endpoints.MapGet("/organizations/{organizationId:guid}/reports/{reportName}/csv", async (
            Guid organizationId,
            string reportName,
            DateOnly startDate,
            DateOnly endDate,
            [FromServices] IOrganizationRepository organizationRepository,
            [FromServices] FinancialReportService reportService,
            CancellationToken cancellationToken) =>
        {
            if (!await EndpointGuards.OrganizationExistsAsync(organizationRepository, organizationId, cancellationToken))
            {
                return Results.NotFound();
            }

            try
            {
                var csv = reportName switch
                {
                    "profit-and-loss" => ReportCsvExporter.Export(
                        await reportService.GetProfitAndLossAsync(organizationId, startDate, endDate, cancellationToken)),
                    "expenses-by-category" => ReportCsvExporter.Export(
                        "Expenses by Category",
                        await reportService.GetExpenseByCategoryAsync(organizationId, startDate, endDate, cancellationToken)),
                    "income-by-source" => ReportCsvExporter.Export(
                        "Income by Source",
                        await reportService.GetIncomeBySourceAsync(organizationId, startDate, endDate, cancellationToken)),
                    _ => null
                };

                return csv is null
                    ? Results.NotFound()
                    : Results.Text(csv, "text/csv");
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(EndpointGuards.Error(exception.Message));
            }
        });

        return endpoints;
    }

    private static async Task<IResult> GetBreakdownReportAsync(
        Guid organizationId,
        DateOnly startDate,
        DateOnly endDate,
        IOrganizationRepository organizationRepository,
        Func<Guid, DateOnly, DateOnly, CancellationToken, Task<AccountBreakdownReport>> getReport,
        CancellationToken cancellationToken)
    {
        if (!await EndpointGuards.OrganizationExistsAsync(organizationRepository, organizationId, cancellationToken))
        {
            return Results.NotFound();
        }

        try
        {
            var report = await getReport(organizationId, startDate, endDate, cancellationToken);
            return Results.Ok(AccountBreakdownReportResponse.FromReport(report));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(EndpointGuards.Error(exception.Message));
        }
    }
}
