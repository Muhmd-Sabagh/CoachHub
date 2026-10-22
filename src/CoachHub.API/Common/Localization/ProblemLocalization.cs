using CoachHub.Application.Common.Localization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Common.Localization;

/// <summary>
/// Puts problem responses in the request's language (P19). It runs for every problem response the API writes
/// (<c>ProblemDetailsOptions.CustomizeProblemDetails</c>): our exceptions and the framework's model validation.
/// Messages the catalog knows also get their stable code: <c>code</c> for the detail, <c>errorCodes</c> per field.
/// Messages it doesn't know stay as written.
/// </summary>
public static class ProblemLocalization
{
    public static void Apply(ProblemDetails problem, string language)
    {
        if (problem.Title is { } title && Messages.Localize(title, language) is { } localTitle) problem.Title = localTitle.Text;
        if (problem.Detail is { } detail && Messages.Localize(detail, language) is { } localDetail)
        {
            problem.Detail = localDetail.Text;
            problem.Extensions["code"] = localDetail.Code;
        }

        var codes = new Dictionary<string, string?[]>();
        if (problem is HttpValidationProblemDetails validation)
        {
            foreach (var field in validation.Errors.Keys.ToArray())
                validation.Errors[field] = Localize(field, validation.Errors[field], language, codes);
        }
        else if (problem.Extensions.TryGetValue("errors", out var value) && value is IReadOnlyDictionary<string, string[]> errors)
        {
            problem.Extensions["errors"] = errors.ToDictionary(x => x.Key, x => Localize(x.Key, x.Value, language, codes));
        }
        if (codes.Count > 0) problem.Extensions["errorCodes"] = codes;
    }

    private static string[] Localize(string field, string[] messages, string language, Dictionary<string, string?[]> codes)
    {
        var localized = messages.Select(message => Messages.Localize(message, language)).ToArray();
        if (localized.Any(x => x is not null)) codes[field] = localized.Select(x => x?.Code).ToArray();
        return messages.Select((message, i) => localized[i]?.Text ?? message).ToArray();
    }
}
