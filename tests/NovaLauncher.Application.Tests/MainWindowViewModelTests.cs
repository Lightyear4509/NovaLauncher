using NovaLauncher.Application;

namespace NovaLauncher.Application.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void ShellCopyStatesThatFeaturesAreNotImplemented()
    {
        var viewModel = new MainWindowViewModel();

        Assert.Equal("NovaLauncher", viewModel.ProductName);
        Assert.Contains("NovaLauncher is ready", viewModel.Status, StringComparison.Ordinal);
    }
}
