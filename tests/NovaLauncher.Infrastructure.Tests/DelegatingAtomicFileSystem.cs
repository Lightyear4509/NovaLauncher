using NovaLauncher.Infrastructure.Persistence;

namespace NovaLauncher.Infrastructure.Tests;

internal class DelegatingAtomicFileSystem(IAtomicFileSystem inner) : IAtomicFileSystem
{
    protected IAtomicFileSystem Inner { get; } = inner;

    public bool FileExists(string path) => Inner.FileExists(path);

    public void CreateDirectory(string path) => Inner.CreateDirectory(path);

    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken) =>
        Inner.ReadAllBytesAsync(path, cancellationToken);

    public virtual Task WriteAllBytesDurableAsync(
        string path,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken) =>
        Inner.WriteAllBytesDurableAsync(path, content, cancellationToken);

    public void CopyFile(string sourcePath, string destinationPath, bool overwrite) =>
        Inner.CopyFile(sourcePath, destinationPath, overwrite);

    public virtual void MoveFile(string sourcePath, string destinationPath, bool overwrite) =>
        Inner.MoveFile(sourcePath, destinationPath, overwrite);

    public virtual void ReplaceFile(string sourcePath, string destinationPath, string backupPath) =>
        Inner.ReplaceFile(sourcePath, destinationPath, backupPath);

    public void DeleteFile(string path) => Inner.DeleteFile(path);

    public ValueTask<IAsyncDisposable> AcquireExclusiveLockAsync(
        string lockPath,
        CancellationToken cancellationToken) =>
        Inner.AcquireExclusiveLockAsync(lockPath, cancellationToken);
}
