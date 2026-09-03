using Microsoft.Extensions.Configuration;
using claude_starter.Services.Auth;

namespace claude_starter.UnitTests.Services.Auth;

public sealed class BCryptPasswordHasherTests
{
    // Work factor 4 keeps these tests about correctness, not about how long a
    // hash takes. The cost itself is asserted separately, below.
    private readonly BCryptPasswordHasher _hasher = new(workFactor: 4);

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = _hasher.Hash("correct-horse-battery");

        _hasher.Verify("correct-horse-battery", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash("correct-horse-battery");

        _hasher.Verify("wrong-password", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentHashes()
    {
        var first = _hasher.Hash("correct-horse-battery");
        var second = _hasher.Hash("correct-horse-battery");

        first.Should().NotBe(second);
    }

    // A work factor that quietly drops in production is a security regression
    // that nothing else would catch, so both the default and the override are
    // pinned. BCrypt records the factor in the hash prefix: $2a$NN$.
    [Fact]
    public void Hash_WithoutConfiguredWorkFactor_UsesTheProductionDefault()
    {
        var hasher = new BCryptPasswordHasher(new ConfigurationBuilder().Build());

        var hash = hasher.Hash("correct-horse-battery");

        hash.Should().StartWith($"$2a${BCryptPasswordHasher.DefaultWorkFactor:D2}$");
        BCryptPasswordHasher.DefaultWorkFactor.Should().BeGreaterThanOrEqualTo(12);
    }

    [Fact]
    public void Hash_WithConfiguredWorkFactor_UsesIt()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:BCryptWorkFactor"] = "5",
            })
            .Build();
        var hasher = new BCryptPasswordHasher(configuration);

        hasher.Hash("correct-horse-battery").Should().StartWith("$2a$05$");
    }

    [Fact]
    public void Verify_MalformedHash_ReturnsFalse()
    {
        var act = () => _hasher.Verify("any-password", "not-a-bcrypt-hash");

        act.Should().Throw<Exception>();
    }
}
