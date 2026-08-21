using NovaLauncher.Domain;

namespace NovaLauncher.Domain.Tests;

public sealed class ProductIdentityTests
{
    [Fact]
    public void IdentityIsStableAndVersioned()
    {
        Assert.Equal("NovaLauncher", ProductIdentity.Name);
        Assert.Equal("0.5.0-experimental.1", ProductIdentity.Version);
    }
}
