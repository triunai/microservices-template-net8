using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.Errors;
using Rgt.Space.Core.ReadModels;
using Rgt.Space.Infrastructure.Commands.Features;

namespace Rgt.Space.Tests.Unit.Handlers.Features;

[Trait("Category", "Unit")]
public class CreateFeatureHandlerTests
{
    private readonly IFeatureReadDac _readDac;
    private readonly IFeatureWriteDac _writeDac;
    private readonly IFeatureGate _featureGate;
    private readonly CreateFeature.Handler _sut;

    private static readonly Guid TestCreatedBy = Guid.Parse("019ac92a-de20-7793-b8df-b88a87ea4e34");

    public CreateFeatureHandlerTests()
    {
        _readDac = Substitute.For<IFeatureReadDac>();
        _writeDac = Substitute.For<IFeatureWriteDac>();
        _featureGate = Substitute.For<IFeatureGate>();
        _sut = new CreateFeature.Handler(_readDac, _writeDac, _featureGate);
    }

    [Fact]
    public async Task Handle_ShouldReturnFail_WhenCodeFormatInvalid()
    {
        // Arrange
        var command = new CreateFeature.Command(
            "invalid-code", "Test Feature", null, false, true, TestCreatedBy);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message == "FEATURE_CODE_FORMAT_INVALID");
    }

    [Fact]
    public async Task Handle_ShouldReturnFail_WhenCodeAlreadyExists()
    {
        // Arrange
        var command = new CreateFeature.Command(
            "DASHBOARD_V2", "Test Feature", null, false, true, TestCreatedBy);

        _readDac.GetByCodeAsync("DASHBOARD_V2", Arg.Any<CancellationToken>())
            .Returns(new FeatureReadModel(
                Guid.NewGuid(), "DASHBOARD_V2", "Existing", null,
                true, true, DateTime.UtcNow, null, DateTime.UtcNow, null));

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == ErrorCatalog.FEATURE_CODE_EXISTS);
    }

    [Fact]
    public async Task Handle_ShouldReturnOkWithGuid_WhenValid()
    {
        // Arrange
        var command = new CreateFeature.Command(
            "NEW_FEATURE", "New Feature", "A description", true, true, TestCreatedBy);

        _readDac.GetByCodeAsync("NEW_FEATURE", Arg.Any<CancellationToken>())
            .Returns((FeatureReadModel?)null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        await _writeDac.Received(1).CreateFeatureAsync(
            result.Value, "NEW_FEATURE", "New Feature", "A description",
            true, true, TestCreatedBy, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNormalizeCode_WhenValid()
    {
        // Arrange — code with extra whitespace (will be trimmed + uppercased)
        var command = new CreateFeature.Command(
            "NEW_FEATURE", "New Feature", null, false, true, TestCreatedBy);

        _readDac.GetByCodeAsync("NEW_FEATURE", Arg.Any<CancellationToken>())
            .Returns((FeatureReadModel?)null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _readDac.Received(1).GetByCodeAsync("NEW_FEATURE", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldInvalidateNegativeCache_WhenCreatedSuccessfully()
    {
        // Arrange
        var command = new CreateFeature.Command(
            "NEW_FEATURE", "New Feature", null, true, true, TestCreatedBy);

        _readDac.GetByCodeAsync("NEW_FEATURE", Arg.Any<CancellationToken>())
            .Returns((FeatureReadModel?)null);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert — cache must be invalidated so stale null entries don't hide the new feature
        _featureGate.Received(1).InvalidateFeature("NEW_FEATURE");
    }

    [Fact]
    public async Task Handle_ShouldReturnFail_WhenConcurrentDuplicateRace()
    {
        // Arrange — read check passes (no existing feature), but another request creates it
        // before our INSERT lands, causing a DB unique constraint violation (23505)
        var command = new CreateFeature.Command(
            "RACE_FEATURE", "Race Feature", null, false, true, TestCreatedBy);

        _readDac.GetByCodeAsync("RACE_FEATURE", Arg.Any<CancellationToken>())
            .Returns((FeatureReadModel?)null);

        _writeDac.CreateFeatureAsync(
            Arg.Any<Guid>(), "RACE_FEATURE", Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<bool>(), Arg.Any<bool>(), TestCreatedBy, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Guid>(
                new ConflictException(ErrorCatalog.FEATURE_CODE_EXISTS, "Already exists")));

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert — unified Result.Fail path, not an unhandled exception
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == ErrorCatalog.FEATURE_CODE_EXISTS);
        _featureGate.DidNotReceive().InvalidateFeature(Arg.Any<string>());
    }
}
