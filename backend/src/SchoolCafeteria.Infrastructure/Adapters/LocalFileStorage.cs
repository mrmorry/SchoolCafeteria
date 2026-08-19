using Microsoft.Extensions.Configuration;
using SchoolCafeteria.Application.Abstractions;

namespace SchoolCafeteria.Infrastructure.Adapters;

/// <summary>
/// Dev/local implementation of IFileStorage backed by a bind-mounted folder outside the
/// container's writable layer (see docker-compose.yml volume "storage-data"). Production targets
/// Azure Blob Storage via an AzureBlobFileStorage adapter selected through configuration
/// ("Storage:Provider") — Application code never depends on either concretely.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath;
    private readonly string _publicBaseUrl;

    public LocalFileStorage(IConfiguration configuration)
    {
        _rootPath = configuration["Storage:Local:RootPath"] ?? "/data/storage";
        _publicBaseUrl = configuration["Storage:Local:PublicBaseUrl"] ?? "/files";
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(string containerName, string fileName, Stream content, string contentType, CancellationToken ct = default)
    {
        var containerPath = Path.Combine(_rootPath, containerName);
        Directory.CreateDirectory(containerPath);
        var fullPath = Path.Combine(containerPath, fileName);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, ct);
        return GetPublicOrSignedUrl(containerName, fileName);
    }

    public Task<Stream> OpenReadAsync(string containerName, string fileName, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_rootPath, containerName, fileName);
        return Task.FromResult<Stream>(File.OpenRead(fullPath));
    }

    public string GetPublicOrSignedUrl(string containerName, string fileName) => $"{_publicBaseUrl}/{containerName}/{fileName}";
}
