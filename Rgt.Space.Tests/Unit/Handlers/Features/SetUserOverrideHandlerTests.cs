using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.Errors;
using Rgt.Space.Core.ReadModels;
using Rgt.Space.Infrastructure.Commands.Features;

namespace Rgt.Space.Tests.Unit.Handlers.Features;

[Trait("Category", "Unit")]
public class SetUserOverrideHandlerTests
{
    private readonly IFeatureReadDac _featureReadDac;
    private readonly IFeatureWriteDac _featureWriteDac;
    private readonly IUserReadDac _userReadDac;
    private readonly IFeatureGate _featureGate;
    private readonly SetUserOverride.Handler _sut;

    private static readonly Guid TestFeatureId = Guid.Parse("019ac92a-0001-7000-0000-000000000001");
    private static readonly Guid TestUserId = Guid.Parse("019ac92a-0003-7000-0000-000000000003");
    private static readonly Guid TestCreatedBy = Guid.Parse("019ac92a-de20-7793-b8df-b88a87ea4e34");
    private const string TestFeatureCode = "DASHBOARD_V2";

    public SetUserOverrideHandlerTests()
    {
        _featureReadDac = Substitute.For<IFeatureReadDac>();
        _featureWriteDac = Substitute.For<IFeatureWriteDac>();
        _userReadDac = Substitute.For<IUserReadDac>();
        _featureGate = Substitute.For<IFeatureGate>();
        _sut = new SetUserOverride.Handler(
            _featureReadDac, _featureWriteDac, _userReadDac, _featureGate);
    }

    private FeatureReadModel CreateTestFeature() =>
        new(TestFeatureId, TestFeatureCode, "Dashboard V2", null,
            true, true, DateTime.UtcNow, null, DateTime.UtcNow, null);

    private UserReadModel CreateTestUser() =>
        new(TestUserId, "Test User", "test@example.com", null,
            true, false, true, "azuread", "ext_123",
            DateTime.UtcNow, "azuread", DateTime.UtcNow, null, DateTime.UtcNow, null);

    [Fact]
    public async Task Handle_ShouldReturnFail_WhenOverrideStateInvalid()
    {
        // Arrange
        var command = new SetUserOverride.Command(
            TestUserId, TestFeatureCode, "INVALID_STATE", null, TestCreatedBy);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message == "OVERRIDE_STATE_INVALID");
    }

    [Fact]
    public async Task Handle_ShouldReturnFail_WhenFeatureNotFound()
    {
        // Arrange
        var command = new SetUserOverride.Command(
            TestUserId, TestFeatureCode, FeatureFlagConstants.OverrideStates.ForceOn,
            "Testing", TestCreatedBy);

        _featureReadDac.GetByCodeAsync(TestFeatureCode, Arg.Any<CancellationToken>())
            .Returns((FeatureReadModel?)null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == ErrorCatalog.FEATURE_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_ShouldReturnFail_WhenUserNotFound()
    {
        // Arrange
        var command = new SetUserOverride.Command(
            TestUserId, TestFeatureCode, FeatureFlagConstants.OverrideStates.ForceOn,
            "Testing", TestCreatedBy);

        _featureReadDac.GetByCodeAsync(TestFeatureCode, Arg.Any<CancellationToken>())
            .Returns(CreateTestFeature());

        _userReadDac.GetByIdAsync(TestUserId, Arg.Any<CancellationToken>())
            .Returns((UserReadModel?)null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == ErrorCatalog.USER_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_ShouldReturnOkAndInvalidateCache_WhenValid()
    {
        // Arrange
        var command = new SetUserOverride.Command(
            TestUserId, TestFeatureCode, FeatureFlagConstants.OverrideStates.ForceOn,
            "Beta tester", TestCreatedBy);

        _featureReadDac.GetByCodeAsync(TestFeatureCode, Arg.Any<CancellationToken>())
            .Returns(CreateTestFeature());

        _userReadDac.GetByIdAsync(TestUserId, Arg.Any<CancellationToken>())
            .Returns(CreateTestUser());

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _featureWriteDac.Received(1).SetUserOverrideAsync(
            TestUserId, TestFeatureId, FeatureFlagConstants.OverrideStates.ForceOn,
            "Beta tester", TestCreatedBy, Arg.Any<CancellationToken>());
        _featureGate.Received(1).InvalidateUserOverride(TestUserId, TestFeatureId);
    }
}
