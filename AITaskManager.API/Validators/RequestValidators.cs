using System.ComponentModel.DataAnnotations;
using AITaskManager.API.DTOs.Auth;
using AITaskManager.API.DTOs.Tasks;

namespace AITaskManager.API.Validators;

/// <summary>
/// Validation helpers using DataAnnotations (no FluentValidation package needed).
/// Same validation rules — just using the framework's built-in system.
/// </summary>
public static class RequestValidator
{
    public static List<string> Validate<T>(T request) where T : class
    {
        var results = new List<ValidationResult>();
        var ctx     = new ValidationContext(request);
        Validator.TryValidateObject(request, ctx, results, validateAllProperties: true);
        return results.Select(r => r.ErrorMessage ?? "Invalid field.").ToList();
    }

    public static List<string> ValidateRegister(RegisterRequest r)
    {
        var errors = Validate(r);

        if (!string.IsNullOrEmpty(r.Password))
        {
            if (r.Password.Length < 8)                          errors.Add("Password must be at least 8 characters.");
            if (!r.Password.Any(char.IsUpper))                  errors.Add("Password must contain an uppercase letter.");
            if (!r.Password.Any(char.IsLower))                  errors.Add("Password must contain a lowercase letter.");
            if (!r.Password.Any(char.IsDigit))                  errors.Add("Password must contain a digit.");
            if (!r.Password.Any(c => !char.IsLetterOrDigit(c))) errors.Add("Password must contain a special character.");
        }

        return errors;
    }

    public static List<string> ValidateCreateTask(CreateTaskRequest r)
    {
        var errors = Validate(r);

        if (r.DueDate.HasValue && r.DueDate.Value <= DateTime.UtcNow)
            errors.Add("Due date must be in the future.");

        return errors;
    }
}
