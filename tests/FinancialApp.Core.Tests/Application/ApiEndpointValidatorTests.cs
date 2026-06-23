using FinancialApp.Core.Application;

namespace FinancialApp.Core.Tests.Application;

public sealed class ApiEndpointValidatorTests
{
    [Theory]
    [InlineData("http://127.0.0.1:5000")]
    [InlineData("http://localhost:5000")]
    [InlineData("https://books.example.test")]
    public void Validate_AcceptsSecureOrLoopbackEndpoints(string baseUrl)
    {
        var result = ApiEndpointValidator.Validate(baseUrl, allowInsecureRemoteHttp: false);

        Assert.Equal(baseUrl.TrimEnd('/') + "/", result.AbsoluteUri);
    }

    [Fact]
    public void Validate_RejectsRemoteHttpByDefault()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => ApiEndpointValidator.Validate("http://192.0.2.10:5000", allowInsecureRemoteHttp: false));

        Assert.Contains("HTTPS is required", exception.Message);
    }

    [Fact]
    public void Validate_AllowsExplicitRemoteHttpDevelopmentOverride()
    {
        var result = ApiEndpointValidator.Validate(
            "http://192.0.2.10:5000",
            allowInsecureRemoteHttp: true);

        Assert.Equal("http://192.0.2.10:5000/", result.AbsoluteUri);
    }

    [Fact]
    public void Validate_RejectsCredentialsInUrl()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => ApiEndpointValidator.Validate("https://user:secret@example.test", false));

        Assert.Contains("username or password", exception.Message);
    }
}
