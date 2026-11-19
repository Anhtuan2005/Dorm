using System.Net.Mail;

namespace DormitoryManagementSystem.Services;

public sealed class StudentSupportOptions
{
    public const string SectionName = "StudentSupport";

    public string? Office { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    public string OfficeLocation => string.IsNullOrWhiteSpace(Office) ? "văn phòng KTX" : Office.Trim();

    public string? PhoneNumber
    {
        get
        {
            var phone = Phone?.Trim();
            if (string.IsNullOrEmpty(phone)
                || phone.Any(character => !char.IsAsciiDigit(character) && character is not ('+' or ' ' or '-' or '.' or '(' or ')')))
                return null;

            var digits = string.Concat(phone.Where(char.IsAsciiDigit));
            return digits.Length is >= 7 and <= 15 ? phone : null;
        }
    }

    public string? PhoneLink => PhoneNumber is { } phone
        ? "tel:" + (phone.StartsWith('+') ? "+" : "") + string.Concat(phone.Where(char.IsAsciiDigit))
        : null;

    public string? EmailAddress => MailAddress.TryCreate(Email?.Trim(), out var address)
        && address.Address == Email?.Trim() ? address.Address : null;

    public string? EmailLink => EmailAddress is { } address
        ? "mailto:" + Uri.EscapeDataString(address).Replace("%40", "@", StringComparison.Ordinal)
        : null;
}
