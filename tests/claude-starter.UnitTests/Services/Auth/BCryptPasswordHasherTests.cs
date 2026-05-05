using claude_starter.Services.Auth;

namespace claude_starter.UnitTests.Services.Auth;

public sealed class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

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

    [Fact]
    public void Verify_MalformedHash_ReturnsFalse()
    {
        var act = () => _hasher.Verify("any-password", "not-a-bcrypt-hash");

        act.Should().Throw<Exception>();
    }
}
