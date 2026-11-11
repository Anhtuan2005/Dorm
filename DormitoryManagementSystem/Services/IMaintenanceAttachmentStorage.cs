using DormitoryManagementSystem.Models;
using Microsoft.AspNetCore.Http;

namespace DormitoryManagementSystem.Services;

public sealed record AttachmentPreparationResult(bool Success, string Message, IReadOnlyList<MaintenanceAttachment> Attachments);
public sealed record AttachmentDownload(Stream Stream, string ContentType, string FileName);

public interface IMaintenanceAttachmentStorage
{
    Task<AttachmentPreparationResult> PrepareAsync(IReadOnlyList<IFormFile> files, DateTime createdAt, CancellationToken cancellationToken = default);
    Task DeleteAsync(IEnumerable<MaintenanceAttachment> attachments);
    Task<AttachmentDownload?> OpenAsync(long attachmentId, string userId, bool canViewAll, CancellationToken cancellationToken = default);
}
