using NovaLauncher.Application.Localization;

namespace NovaLauncher.Application.Tests;

public sealed class InterfaceLocalizerTests
{
    [Fact]
    public void ReviewedCatalogCoversEveryDeclaredInterfaceKey()
    {
        Assert.Equal(["en-US"], InterfaceLocalizer.ReviewedCultures);
        foreach (var key in Enum.GetValues<InterfaceText>())
            Assert.False(string.IsNullOrWhiteSpace(InterfaceLocalizer.Get(key, InterfaceLocalizer.ReviewedCulture)));
    }

    [Fact]
    public void NonShippingPseudoLocaleExpandsEveryStringForLayoutFeasibility()
    {
        foreach (var key in Enum.GetValues<InterfaceText>())
        {
            var source = InterfaceLocalizer.Get(key, InterfaceLocalizer.ReviewedCulture);
            var expanded = InterfaceLocalizer.Get(key, "en-XA");
            Assert.StartsWith("⟦", expanded, StringComparison.Ordinal);
            Assert.EndsWith("⟧", expanded, StringComparison.Ordinal);
            Assert.True(expanded.Length > source.Length);
        }
    }
}
