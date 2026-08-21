using NovaLauncher.Domain;

namespace NovaLauncher.Domain.Tests;

public sealed class ProductIdentityTests
{
    [Fact]
    public void IdentityIsStableAndVersioned()
    {
        Assert.Equal("NovaLauncher", ProductIdentity.Name);
        Assert.Equal("0.4.0-beta.1", ProductIdentity.Version);
    }
}
