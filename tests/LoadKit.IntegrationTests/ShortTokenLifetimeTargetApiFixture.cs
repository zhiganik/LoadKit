namespace LoadKit.IntegrationTests;

/// <summary>TargetApi issuing tokens that live <see cref="TokenLifetimeSeconds"/> seconds, so refresh tests take seconds.</summary>
public sealed class ShortTokenLifetimeTargetApiFixture : TargetApiFixture
{
    public const int TokenLifetimeSeconds = 3;

    public ShortTokenLifetimeTargetApiFixture()
        : base([$"--TargetApi:TokenLifetimeSeconds={TokenLifetimeSeconds}"])
    {
    }
}
