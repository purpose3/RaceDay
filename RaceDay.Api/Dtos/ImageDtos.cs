namespace RaceDay.Api.Dtos;

public class UploadImageRequest
{
    /// <summary>The image file (JPEG, PNG or WebP, up to 5 MB).</summary>
    public IFormFile? File { get; set; }
}

/// <summary>Relative URL (served by this API) where the stored image can be fetched.</summary>
public record ImageUploadResponse(string ImageUrl);
