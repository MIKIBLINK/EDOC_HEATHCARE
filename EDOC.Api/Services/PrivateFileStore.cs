namespace EDOC.Api.Services;

public interface IPrivateFileStore
{
    Task<string> SaveAsync(Stream stream, string extension, CancellationToken cancellationToken);
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken);
}

public sealed class LocalPrivateFileStore(IWebHostEnvironment environment, IConfiguration configuration) : IPrivateFileStore
{
    private readonly string _root = Path.GetFullPath(configuration["Storage:RootPath"] ?? Path.Combine(environment.ContentRootPath, "App_Data", "MedicalRecords"));

    public async Task<string> SaveAsync(Stream stream, string extension, CancellationToken cancellationToken)
    {
        var key = Guid.NewGuid().ToString("N") + extension;
        Directory.CreateDirectory(_root);
        var path = Resolve(key);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await stream.CopyToAsync(output, cancellationToken);
        return key;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(key);
        Stream? result = File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan) : null;
        return Task.FromResult(result);
    }

    private string Resolve(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || Path.GetFileName(key) != key || key.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Invalid storage key.", nameof(key));
        var path = Path.GetFullPath(Path.Combine(_root, key));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Invalid storage key.", nameof(key));
        return path;
    }
}
