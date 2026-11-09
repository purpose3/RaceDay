namespace RaceDay.Api.Services;

public record ImageCheck(bool IsValid, string? Error, string? ContentType, string? Extension);

/// <summary>
/// Validates uploaded images by size and by their real file signature (magic bytes),
/// so a renamed .exe or .txt cannot be uploaded as a picture.
/// </summary>
public static class ImageValidator
{
    public const long MaxBytes = 5 * 1024 * 1024;   // 5 MB

    public static ImageCheck Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return Fail("Please choose an image file to upload.");
        if (file.Length > MaxBytes)
            return Fail("The image must be 5 MB or smaller.");

        var head = new byte[12];
        int read;
        using (var stream = file.OpenReadStream())
            read = stream.Read(head, 0, head.Length);
        if (read < 12)
            return Fail("The file is not a valid image.");

        if (head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
            return new ImageCheck(true, null, "image/jpeg", ".jpg");

        if (head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47 &&
            head[4] == 0x0D && head[5] == 0x0A && head[6] == 0x1A && head[7] == 0x0A)
            return new ImageCheck(true, null, "image/png", ".png");

        // WebP: "RIFF" .... "WEBP"
        if (head[0] == 0x52 && head[1] == 0x49 && head[2] == 0x46 && head[3] == 0x46 &&
            head[8] == 0x57 && head[9] == 0x45 && head[10] == 0x42 && head[11] == 0x50)
            return new ImageCheck(true, null, "image/webp", ".webp");

        return Fail("Only JPEG, PNG or WebP images are allowed.");
    }

    private static ImageCheck Fail(string error) => new(false, error, null, null);
}
