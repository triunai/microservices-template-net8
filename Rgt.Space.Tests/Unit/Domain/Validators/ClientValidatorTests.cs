using FluentAssertions;
using Rgt.Space.Core.Domain.Entities.PortalRouting;
using Rgt.Space.Core.Domain.Validators;

namespace Rgt.Space.Tests.Unit.Domain.Validators;

[Trait("Category", "Unit")]
public class ClientValidatorTests
{
    [Fact]
    public void Validate_ShouldRejectEmptyCode()
    {
        // Arrange
        var client = Client.Create("Acme Corp", string.Empty);

        // Act
        var result = ClientValidator.Validate(client);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(Client.Code));
    }

    [Fact]
    public void Validate_ShouldRejectEmptyName()
    {
        // Arrange
        var client = Client.Create(string.Empty, "ACME");

        // Act
        var result = ClientValidator.Validate(client);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(Client.Name));
    }

    [Fact]
    public void Validate_ShouldPassForValidClient()
    {
        // Arrange
        var client = Client.Create("Acme Corp", "ACME");

        // Act
        var result = ClientValidator.Validate(client);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
