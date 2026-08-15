using NovaLauncher.Domain;

namespace NovaLauncher.Domain.Tests;

public sealed class ProductIdentityTests
{
    [Fact]
    public void IdentityIsStableAndVersioned()
    {
        Assert.Equal("NovaLauncher", ProductIdentity.Name);
        Assert.Matches(@"^\d+\.\d+\.\d+-alpha\.\d+$", ProductIdentity.Version);
    }
}
