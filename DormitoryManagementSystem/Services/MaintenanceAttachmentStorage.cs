using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.Services;

public sealed class MaintenanceAttachmentStorage(IWebHostEnvironment environment, DormitoryDbContext db) : IMaintenanceAttachmentStorage
{
    private const int MaximumFiles = 3;
    private const long MaximumFileSize = 5 * 1024 * 1024;
    private readonly string storageRoot = Path.Combine(environment.ContentRootPath, "App_Data", "maintenance-attachments");

    public async Task<AttachmentPreparationResult> PrepareAsync(IReadOnlyList<IFormFile> files, DateTime createdAt, CancellationToken cancellationToken = default)
    {
        var selected = files.Where(x => x.Length > 0).ToList();
        if (selected.Count > MaximumFiles) return new(false, $"Chỉ được gửi tối đa {MaximumFiles} ảnh.", []);
        var attachments = new List<MaintenanceAttachment>();
        Directory.CreateDirectory(storageRoot);
        try
        {
            foreach (var file in selected)
            {
                if (file.Length > MaximumFileSize) throw new InvalidDataException($"Ảnh {Path.GetFileName(file.FileName)} vượt quá 5 MB.");
                await using var input = file.OpenReadStream();
                var signature = new byte[12];
                var read = await input.ReadAsync(signature.AsMemory(0, signature.Length), cancellationToken);
                var detected = ImageSignature.Detect(signature.AsSpan(0, read));
                if (detected is null) throw new InvalidDataException($"{Path.GetFileName(file.FileName)} không phải ảnh JPG, PNG hoặc WebP hợp lệ.");
                input.Position = 0;
                var storedName = $"{Guid.NewGuid():N}{detected.Value.Extension}";
                await using var output = new FileStream(Path.Combine(storageRoot, storedName), FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
                await input.CopyToAsync(output, cancellationToken);
                attachments.Add(new MaintenanceAttachment
                {
                    StoredName = storedName,
                    OriginalName = Path.GetFileName(file.FileName)[..Math.Min(180, Path.GetFileName(file.FileName).Length)],
                    ContentType = detected.Value.ContentType,
                    Length = file.Length,
                    CreatedAt = createdAt
                });
            }
            return new(true, string.Empty, attachments);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            await DeleteAsync(attachments);
            return new(false, exception.Message, []);
        }
    }

    public Task DeleteAsync(IEnumerable<MaintenanceAttachment> attachments)
    {
        foreach (var attachment in attachments)
        {
            var path = SafePath(attachment.StoredName);
            if (path is not null && File.Exists(path)) File.Delete(path);
        }
        return Task.CompletedTask;
    }

    public async Task<AttachmentDownload?> OpenAsync(long attachmentId, string userId, bool canViewAll, CancellationToken cancellationToken = default)
    {
        var attachment = await db.MaintenanceAttachments.AsNoTracking()
            .Include(x => x.MaintenanceRequest).ThenInclude(x => x.ReporterStudent)
            .SingleOrDefaultAsync(x => x.Id == attachmentId, cancellationToken);
        if (attachment is null || (!canViewAll && attachment.MaintenanceRequest.ReporterStudent?.UserId != userId)) return null;
        var path = SafePath(attachment.StoredName);
        if (path is null || !File.Exists(path)) return null;
        return new(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read), attachment.ContentType, attachment.OriginalName);
    }

    private string? SafePath(string storedName)
    {
        if (Path.GetFileName(storedName) != storedName) return null;
        var root = Path.GetFullPath(storageRoot) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(storageRoot, storedName));
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path : null;
    }

}
