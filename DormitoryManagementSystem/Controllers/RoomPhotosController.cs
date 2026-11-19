using System.Data;
using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Security;
using DormitoryManagementSystem.Services;
using DormitoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.Controllers;

[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.DormManager}")]
public sealed class RoomPhotosController(DormitoryDbContext db) : Controller
{
    private const int MaximumPhotos = 5;
    private const int MaximumBytes = 5 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> Index(int roomId, CancellationToken cancellationToken)
    {
        var room = await db.Rooms.AsNoTracking().Where(x => x.Id == roomId)
            .Select(x => new { x.Id, x.Code, x.FloorId, x.Floor.BuildingId }).SingleOrDefaultAsync(cancellationToken);
        if (room is null) return NotFound();
        var photos = await db.RoomPhotos.AsNoTracking().Where(x => x.RoomId == roomId).OrderBy(x => x.Id)
            .Select(x => new RoomPhotoViewModel(x.Id, x.Caption)).ToListAsync(cancellationToken);
        ViewData["Title"] = $"Ảnh phòng {room.Code}";
        ViewData["ActiveNav"] = "catalog";
        return View(new RoomPhotoGalleryViewModel(room.Id, room.Code, room.FloorId, room.BuildingId, MaximumPhotos, photos));
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(MaximumBytes + 64 * 1024)]
    public async Task<IActionResult> Upload(int roomId, IFormFile? photo, string? caption, CancellationToken cancellationToken)
    {
        if (photo is null || photo.Length is <= 0 or > MaximumBytes)
            return Back(roomId, "Hãy chọn một ảnh có dung lượng tối đa 5 MB.");
        caption = caption?.Trim() ?? string.Empty;
        if (caption.Length > 180) return Back(roomId, "Chú thích dài tối đa 180 ký tự.");

        using var buffer = new MemoryStream((int)photo.Length);
        await photo.CopyToAsync(buffer, cancellationToken);
        var data = buffer.ToArray();
        var type = ImageSignature.Detect(data);
        if (type is null) return Back(roomId, "Ảnh phải có định dạng JPG, PNG hoặc WebP.");

        var error = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            if (!await db.Rooms.AnyAsync(x => x.Id == roomId, cancellationToken))
                return "Không tìm thấy phòng.";
            if (await db.RoomPhotos.CountAsync(x => x.RoomId == roomId, cancellationToken) >= MaximumPhotos)
                return $"Phòng đã có đủ {MaximumPhotos} ảnh.";

            db.RoomPhotos.Add(new RoomPhoto { RoomId = roomId, Caption = caption, ContentType = type.Value.ContentType, Data = data });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        });
        return Back(roomId, error, "Đã thêm ảnh phòng.");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int roomId, long id, CancellationToken cancellationToken)
    {
        var removed = await db.RoomPhotos.Where(x => x.Id == id && x.RoomId == roomId).ExecuteDeleteAsync(cancellationToken);
        return Back(roomId, removed == 0 ? "Không tìm thấy ảnh thuộc phòng này." : null, "Đã gỡ ảnh phòng.");
    }

    private IActionResult Back(int roomId, string? error, string? success = null)
    {
        TempData[error is null ? "Success" : "Error"] = error ?? success;
        return RedirectToAction(nameof(Index), new { roomId });
    }
}
