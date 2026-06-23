namespace FinancialApp.Core.Application;

public static class ApiEndpointValidator
{
    public static Uri Validate(string? baseUrl, bool allowInsecureRemoteHttp)
    {
        if (!Uri.TryCreate(baseUrl?.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("API URL must be an absolute HTTP or HTTPS address.", nameof(baseUrl));
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException("API URL must not contain a username or password.", nameof(baseUrl));
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException("API URL must not contain a query string or fragment.", nameof(baseUrl));
        }

        if (uri.Scheme == Uri.UriSchemeHttp && !uri.IsLoopback && !allowInsecureRemoteHttp)
        {
            throw new ArgumentException(
                "HTTPS is required for API connections beyond this computer. Enable insecure remote HTTP only for a trusted development network.",
                nameof(baseUrl));
        }

        return new Uri(uri.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/", UriKind.Absolute);
    }
}
