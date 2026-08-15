using NovaLauncher.App;

namespace NovaLauncher.Presentation.Tests;

public sealed class ShellAccessibilityTests
{
    [Fact]
    public void ShellDeclaresMeaningfulAutomationNames()
    {
        Assert.Equal("NovaLauncher main window", ShellAccessibility.MainWindow);
        Assert.Equal("Add a manual game", ShellAccessibility.FutureLibraryAction);
        Assert.DoesNotContain("button1", ShellAccessibility.FutureLibraryAction, StringComparison.OrdinalIgnoreCase);
    }
}
