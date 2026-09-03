namespace claude_starter.IntegrationTests.Infrastructure;

// One collection per test class. xunit runs collections in parallel, and each
// gets its own DatabaseFixture — so its own database, schema and application.
// Splitting them any finer would not help: a class's tests share a database and
// reset between cases, so they must still run in sequence.

[CollectionDefinition(Name)]
public sealed class RegisterCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "register";
}

[CollectionDefinition(Name)]
public sealed class LoginCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "login";
}

[CollectionDefinition(Name)]
public sealed class LogoutCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "logout";
}

[CollectionDefinition(Name)]
public sealed class MeCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "me";
}

[CollectionDefinition(Name)]
public sealed class RepositoryCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "repositories";
}

[CollectionDefinition(Name)]
public sealed class MigrationCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "migrations";
}

[CollectionDefinition(Name)]
public sealed class PasswordResetCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "password-reset";
}

[CollectionDefinition(Name)]
public sealed class VerificationRequiredCollection : ICollectionFixture<VerificationRequiredFixture>
{
    public const string Name = "verification-required";
}

[CollectionDefinition(Name)]
public sealed class VerificationWithoutSmtpCollection : ICollectionFixture<VerificationWithoutSmtpFixture>
{
    public const string Name = "verification-without-smtp";
}

[CollectionDefinition(Name)]
public sealed class TokenRepositoryCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "token-repository";
}

[CollectionDefinition(Name)]
public sealed class MetricsCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "metrics";
}

[CollectionDefinition(Name)]
public sealed class MetricsEnabledCollection : ICollectionFixture<MetricsEnabledFixture>
{
    public const string Name = "metrics-enabled";
}
