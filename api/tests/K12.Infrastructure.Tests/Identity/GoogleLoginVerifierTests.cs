using K12.Infrastructure.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace K12.Infrastructure.Tests.Identity;

public class GoogleLoginVerifierTests
{
    [Theory]
    [InlineData("pat@gmail.com", null, true)]
    [InlineData("Pat@GMAIL.com", null, true)]
    [InlineData("pat@googlemail.com", null, true)]
    [InlineData("pat@school.org", "school.org", true)]
    [InlineData("pat@SCHOOL.org", "school.org", true)]
    [InlineData("pat@school.org", null, false)]
    [InlineData("pat@school.org", "", false)]
    [InlineData("pat@school.org", "other.org", false)]
    [InlineData("pat@gmail.com.evil.org", null, false)]
    [InlineData("gmail.com", null, false)]
    [InlineData("pat@", null, false)]
    public void Google_owns_only_Gmail_and_Workspace_addresses(string email, string? hostedDomain, bool authoritative) =>
        Assert.Equal(authoritative, GoogleLoginVerifier.IsAuthoritative(email, hostedDomain));

    [Theory]
    [InlineData(null, false, null)]
    [InlineData("  ", false, null)]
    [InlineData(" abc.apps.googleusercontent.com ", true, "abc.apps.googleusercontent.com")]
    public void It_is_configured_by_its_client_id(string? clientId, bool configured, string? shown)
    {
        var google = new GoogleLoginVerifier(clientId, NullLogger<GoogleLoginVerifier>.Instance);
        Assert.Equal((configured, shown), (google.IsConfigured, google.ClientId));
    }

    [Fact]
    public async Task Without_a_client_id_it_refuses_every_token() =>
        Assert.Null(await new GoogleLoginVerifier(null, NullLogger<GoogleLoginVerifier>.Instance).VerifyAsync("anything"));
}
