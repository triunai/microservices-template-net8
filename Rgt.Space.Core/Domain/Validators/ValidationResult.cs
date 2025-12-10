namespace Rgt.Space.Core.Domain.Validators;

public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<ValidationError> Errors { get; } = new();

    public void AddError(string propertyName, string errorMessage)
    {
        Errors.Add(new ValidationError(propertyName, errorMessage));
    }
}

public record ValidationError(string PropertyName, string ErrorMessage);
