using SchoolCafeteria.Application.Abstractions;

namespace SchoolCafeteria.UnitTests.TestSupport;

public class FakeEntraIdTokenValidator : IEntraIdTokenValidator
{
    public bool IsConfigured { get; set; } = true;
    public ExternalIdentityClaims? NextClaims { get; set; }

    public Task<ExternalIdentityClaims> ValidateAsync(string idToken, CancellationToken ct = default) =>
        Task.FromResult(NextClaims ?? throw new InvalidOperationException("Configure NextClaims before calling ValidateAsync in a test."));
}
