using NovaLauncher.Domain;
using NovaLauncher.Application.Library;

namespace NovaLauncher.Application;

public sealed class MainWindowViewModel
{
    private readonly string _productName = ProductIdentity.Name;
    private readonly string _version = ProductIdentity.Version;
    private readonly string _heading = "Your games, one calm home.";
    private readonly string _status = "NovaLauncher is ready. Add a local game or import your Steam library.";

    public MainWindowViewModel()
    {
    }

    public MainWindowViewModel(LibraryWorkspaceViewModel workspace)
    {
        Workspace = workspace;
    }

    public LibraryWorkspaceViewModel? Workspace { get; }

    public string ProductName => _productName;

    public string Version => _version;

    public string Heading => _heading;

    public string Status => _status;
}
