using DormitoryManagementSystem.ViewModels;

namespace DormitoryManagementSystem.Services;

public interface IStudentManagementService
{
    Task<StudentDirectoryViewModel> GetDirectoryAsync(StudentDirectoryQuery query, CancellationToken cancellationToken = default);
    Task<StudentManagementResult> CreateProfileAsync(CreateStudentProfileInput input, CancellationToken cancellationToken = default);
    Task<StudentManagementResult> UpdateProfileAsync(UpdateStudentProfileInput input, CancellationToken cancellationToken = default);
    Task<StudentManagementResult> CreateAccountAsync(CreateStudentAccountInput input, CancellationToken cancellationToken = default);
    Task<StudentManagementResult> SetAccountLockAsync(int studentId, bool locked, CancellationToken cancellationToken = default);
    Task<StudentManagementResult> ResetPasswordAsync(ResetStudentPasswordInput input, CancellationToken cancellationToken = default);
}
