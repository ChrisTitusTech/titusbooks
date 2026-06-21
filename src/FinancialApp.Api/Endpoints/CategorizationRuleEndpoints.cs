using System.Globalization;
using System.Text.RegularExpressions;
using FinancialApp.Api.Categorization;
using FinancialApp.Core.Accounting;
using FinancialApp.Core.Categorization;
using FinancialApp.Core.Organizations;
using Microsoft.AspNetCore.Mvc;

namespace FinancialApp.Api.Endpoints;

public static class CategorizationRuleEndpoints
{
    private static readonly HashSet<string> TextMatchFields =
    [
        "description",
        "raw_description",
        "source",
        "source_type",
        "source_status",
        "kind",
        "currency"
    ];

    public static IEndpointRouteBuilder MapCategorizationRuleEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/organizations/{organizationId:guid}/categorization-rules", async (
            Guid organizationId,
            [FromServices] IOrganizationRepository organizationRepository,
            [FromServices] ICategorizationRuleRepository ruleRepository,
            CancellationToken cancellationToken) =>
        {
            if (!await EndpointGuards.OrganizationExistsAsync(
                    organizationRepository,
                    organizationId,
                    cancellationToken))
            {
                return Results.NotFound();
            }

            var rules = await ruleRepository.ListAsync(organizationId, cancellationToken);
            return Results.Ok(rules.Select(CategorizationRuleResponse.FromRule));
        });

        endpoints.MapPost("/organizations/{organizationId:guid}/categorization-rules", async (
            Guid organizationId,
            CreateCategorizationRuleRequest request,
            [FromServices] IOrganizationRepository organizationRepository,
            [FromServices] IAccountRepository accountRepository,
            [FromServices] ICategorizationRuleRepository ruleRepository,
            CancellationToken cancellationToken) =>
        {
            if (!await EndpointGuards.OrganizationExistsAsync(
                    organizationRepository,
                    organizationId,
                    cancellationToken))
            {
                return Results.NotFound();
            }

            if (!TryValidate(request, out var matchOperator, out var validationError))
            {
                return Results.BadRequest(EndpointGuards.Error(validationError));
            }

            var account = await accountRepository.GetByIdAsync(
                organizationId,
                request.TargetAccountId,
                cancellationToken);
            if (account is null || !account.IsActive)
            {
                return Results.BadRequest(EndpointGuards.Error(
                    "Target account must be an active account in this organization."));
            }

            var rule = new CategorizationRule
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = request.Name.Trim(),
                MatchField = request.MatchField.Trim().ToLowerInvariant(),
                MatchOperator = matchOperator,
                MatchValue = request.MatchValue.Trim(),
                TargetAccountId = request.TargetAccountId,
                Priority = request.Priority
            };

            await ruleRepository.AddAsync(rule, cancellationToken);

            return Results.Created(
                $"/organizations/{organizationId}/categorization-rules/{rule.Id}",
                CategorizationRuleResponse.FromRule(rule));
        });

        return endpoints;
    }

    private static bool TryValidate(
        CreateCategorizationRuleRequest request,
        out CategorizationRuleOperator matchOperator,
        out string validationError)
    {
        matchOperator = default;

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            validationError = "Rule name is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.MatchValue))
        {
            validationError = "Rule match value is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.MatchField))
        {
            validationError = "Rule match field is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.MatchOperator))
        {
            validationError = "Rule match operator is required.";
            return false;
        }

        if (!CategorizationRuleOperatorNames.TryParse(request.MatchOperator, out matchOperator))
        {
            validationError = "Rule match operator is not supported.";
            return false;
        }

        if (request.Priority < 0)
        {
            validationError = "Rule priority must be zero or greater.";
            return false;
        }

        var matchField = request.MatchField.Trim().ToLowerInvariant();
        var isAmountOperator = matchOperator is
            CategorizationRuleOperator.AmountEquals or
            CategorizationRuleOperator.AmountBetween;
        if (isAmountOperator)
        {
            if (!string.Equals(matchField, "amount", StringComparison.Ordinal))
            {
                validationError = "Amount rules must use the amount match field.";
                return false;
            }

            if (!IsValidAmountValue(matchOperator, request.MatchValue))
            {
                validationError = matchOperator == CategorizationRuleOperator.AmountEquals
                    ? "Amount equals rules require one decimal value."
                    : "Amount between rules require two decimal values separated by '..' or ','.";
                return false;
            }
        }
        else if (!TextMatchFields.Contains(matchField))
        {
            validationError = "Rule match field is not supported.";
            return false;
        }

        if (matchOperator == CategorizationRuleOperator.Regex)
        {
            try
            {
                _ = new Regex(
                    request.MatchValue,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(250));
            }
            catch (ArgumentException)
            {
                validationError = "Rule regular expression is invalid.";
                return false;
            }
        }

        validationError = string.Empty;
        return true;
    }

    private static bool IsValidAmountValue(
        CategorizationRuleOperator matchOperator,
        string matchValue)
    {
        if (matchOperator == CategorizationRuleOperator.AmountEquals)
        {
            return decimal.TryParse(
                matchValue,
                NumberStyles.Number | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out _);
        }

        var bounds = matchValue.Split(
            ["..", ","],
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return bounds.Length == 2
            && bounds.All(bound => decimal.TryParse(
                bound,
                NumberStyles.Number | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out _));
    }
}
