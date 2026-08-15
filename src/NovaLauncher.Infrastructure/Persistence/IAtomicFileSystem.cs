namespace NovaLauncher.Infrastructure.Persistence;

public interface IAtomicFileSystem
{
    bool FileExists(string path);

    void CreateDirectory(string path);

    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken);

    Task WriteAllBytesDurableAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);

    void CopyFile(string sourcePath, string destinationPath, bool overwrite);

    void MoveFile(string sourcePath, string destinationPath, bool overwrite);

    void ReplaceFile(string sourcePath, string destinationPath, string backupPath);

    void DeleteFile(string path);

    ValueTask<IAsyncDisposable> AcquireExclusiveLockAsync(string lockPath, CancellationToken cancellationToken);
}
