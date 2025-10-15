using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace TutoriaApi.Core.Attributes;

/// <summary>
/// Validates password complexity requirements.
/// Requires: Minimum 8 characters, at least one uppercase, one lowercase, one digit, and one special character.
/// </summary>
public class PasswordComplexityAttribute : ValidationAttribute
{
    private readonly int _minLength;
    private readonly bool _requireUppercase;
    private readonly bool _requireLowercase;
    private readonly bool _requireDigit;
    private readonly bool _requireSpecialChar;

    public PasswordComplexityAttribute(
        int minLength = 8,
        bool requireUppercase = true,
        bool requireLowercase = true,
        bool requireDigit = true,
        bool requireSpecialChar = true)
    {
        _minLength = minLength;
        _requireUppercase = requireUppercase;
        _requireLowercase = requireLowercase;
        _requireDigit = requireDigit;
        _requireSpecialChar = requireSpecialChar;

        ErrorMessage = BuildErrorMessage();
    }

    private string BuildErrorMessage()
    {
        var requirements = new List<string>();

        if (_minLength > 0)
            requirements.Add($"at least {_minLength} characters");
        if (_requireUppercase)
            requirements.Add("one uppercase letter");
        if (_requireLowercase)
            requirements.Add("one lowercase letter");
        if (_requireDigit)
            requirements.Add("one digit");
        if (_requireSpecialChar)
            requirements.Add("one special character (!@#$%^&*()_+-=[]{}|;:,.<>?)");

        return $"Password must contain {string.Join(", ", requirements)}.";
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return new ValidationResult("Password is required.");
        }

        var password = value.ToString()!;

        // Check minimum length
        if (password.Length < _minLength)
        {
            return new ValidationResult($"Password must be at least {_minLength} characters long.");
        }

        // Check uppercase requirement
        if (_requireUppercase && !Regex.IsMatch(password, @"[A-Z]"))
        {
            return new ValidationResult("Password must contain at least one uppercase letter.");
        }

        // Check lowercase requirement
        if (_requireLowercase && !Regex.IsMatch(password, @"[a-z]"))
        {
            return new ValidationResult("Password must contain at least one lowercase letter.");
        }

        // Check digit requirement
        if (_requireDigit && !Regex.IsMatch(password, @"[0-9]"))
        {
            return new ValidationResult("Password must contain at least one digit.");
        }

        // Check special character requirement
        if (_requireSpecialChar && !Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{}|;:,.<>?]"))
        {
            return new ValidationResult("Password must contain at least one special character (!@#$%^&*()_+-=[]{}|;:,.<>?).");
        }

        return ValidationResult.Success;
    }
}
