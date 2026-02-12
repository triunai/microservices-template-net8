using Rgt.Space.Core.Domain.Entities.PortalRouting;

namespace Rgt.Space.Core.Domain.Validators;

/// <summary>
/// Domain validator for the <see cref="Client"/> entity.
/// Enforces business rules that are not covered by type safety.
/// </summary>
public static class ClientValidator
{
    /// <summary>
    /// Validates the state of a <see cref="Client"/> entity.
    /// </summary>
    /// <param name="client">The client to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> containing any errors found.</returns>
    public static ValidationResult Validate(Client client)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));

        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(client.Code))
        {
            result.AddError(nameof(Client.Code), "Code is required.");
        }

        if (string.IsNullOrWhiteSpace(client.Name))
        {
            result.AddError(nameof(Client.Name), "Name is required.");
        }

        return result;
    }
}
