using Rgt.Space.Core.Domain.Entities.PortalRouting;

namespace Rgt.Space.Core.Domain.Validators;

public static class ClientValidator
{
    public static ValidationResult Validate(Client client)
    {
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
