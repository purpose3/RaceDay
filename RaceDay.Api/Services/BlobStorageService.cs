using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDay.Api.Services;

public record BlobContent(Stream Content, string ContentType);

/// <summary>
/// All file storage goes through this interface. The MVC front end never talks to Azure directly:
/// it uploads to and downloads from the API, and the API uses this service.
/// </summary>
public interface IBlobStorageService
{
    /// <summary>Stores a file and returns its blob name (the value saved in the database).</summary>
    Task<string> UploadAsync(string folder, Stream content, string contentType, string extension, CancellationToken ct = default);

    /// <summary>Opens a stored file for reading, or returns null if it does not exist.</summary>
    Task<BlobContent?> OpenReadAsync(string blobName, CancellationToken ct = default);

    Task DeleteAsync(string blobName, CancellationToken ct = default);
}

/// <summary>Azure Blob Storage implementation (works with a real account or the Azurite emulator).</summary>
public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _container;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialised;

    public AzureBlobStorageService(IConfiguration config)
    {
        var connectionString = config["AzureStorage:ConnectionString"]
            ?? throw new InvalidOperationException("Missing AzureStorage:ConnectionString.");
        var containerName = config["AzureStorage:ContainerName"] ?? "raceday-images";
        _container = new BlobContainerClient(connectionString, containerName);
    }

    // The container is private: images are only ever served through the API.
    private async Task EnsureContainerAsync(CancellationToken ct)
    {
        if (_initialised) return;
        await _initLock.WaitAsync(ct);
        try
        {
            if (!_initialised)
            {
                await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
                _initialised = true;
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<string> UploadAsync(string folder, Stream content, string contentType, string extension, CancellationToken ct = default)
    {
        await EnsureContainerAsync(ct);
        var blobName = $"{folder}/{Guid.NewGuid():N}{extension}";
        var blob = _container.GetBlobClient(blobName);
        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, ct);
        return blobName;
    }

    public async Task<BlobContent?> OpenReadAsync(string blobName, CancellationToken ct = default)
    {
        await EnsureContainerAsync(ct);
        try
        {
            var result = await _container.GetBlobClient(blobName).DownloadStreamingAsync(cancellationToken: ct);
            return new BlobContent(result.Value.Content, result.Value.Details.ContentType);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string blobName, CancellationToken ct = default)
    {
        await EnsureContainerAsync(ct);
        await _container.DeleteBlobIfExistsAsync(blobName, cancellationToken: ct);
    }
}
