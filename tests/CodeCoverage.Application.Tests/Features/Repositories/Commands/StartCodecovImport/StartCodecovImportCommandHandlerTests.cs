// ------------------------------------------------------------------------------------------------
// <copyright file="StartCodecovImportCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Features.Repositories.Commands.StartCodecovImport;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Codecov;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Tests.Features.Repositories.Commands.StartCodecovImport;

public sealed class StartCodecovImportCommandHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 14, 8, 0, 0, TimeSpan.Zero);

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly ICodecovImportRepository _imports = A.Fake<ICodecovImportRepository>();

  private readonly ISecretProtector _protector = A.Fake<ISecretProtector>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  private readonly StartCodecovImportCommandHandler _handler;

  #endregion

  #region Ctors

  public StartCodecovImportCommandHandlerTests()
  {
    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "badge", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);

    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => _repositories.FindBySlugAsync(RepositorySlug.Parse("octo-org/app"), A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _protector.Protect("plain-token")).Returns("protected");

    _handler = new StartCodecovImportCommandHandler(new CommitLocator(_repositories, A.Fake<ICommitRepository>()), _imports, _protector, _auditLog, clock);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ShouldStoreTheProtectedTokenAndStartTheImport()
  {
    await _handler.Handle(new StartCodecovImportCommand(Forge.GitHub, "octo-org", "app", "plain-token"), Ct);

    A.CallTo(() => _imports.StartAsync(
                     A<CodecovImport>.That.Matches(static i => i.RepositoryId == 5 && i.ProtectedToken == "protected" && i.CreatedAt == Now && i.NextPage == 1),
                     A<CancellationToken>._
                   ))
     .MustHaveHappenedOnceExactly();
    A.CallTo(() => _auditLog.RecordAsync(StartCodecovImportCommandHandler.AuditAction, "5", null, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_UnknownRepository_ShouldRefuseAsNotFound()
  {
    var exception = await Should.ThrowAsync<NotFoundException>(() => _handler.Handle(new StartCodecovImportCommand(Forge.GitHub, "octo-org", "other", "plain-token"), Ct).AsTask());

    exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
    A.CallTo(_imports).MustNotHaveHappened();
  }

  // Azure DevOps has no Codecov: refused at once, instead of a job that fails until it dies.
  [Fact]
  public async Task Handle_AzureDevOps_ShouldRefuseAsUnprocessable()
  {
    var exception = await Should.ThrowAsync<UnprocessableEntityException>(() => _handler.Handle(new StartCodecovImportCommand(Forge.AzureDevOps, "octo-org", "app", "plain-token"), Ct).AsTask());

    exception.ErrorCode.ShouldBe(RepositoryErrors.CodecovUnsupportedForge);
    exception.Message.ShouldNotContain("plain-token");
    A.CallTo(_imports).MustNotHaveHappened();
    A.CallTo(_protector).MustNotHaveHappened();
  }

  [Fact]
  public void Command_ToString_ShouldNeverShowTheTokenOfAnAdministratorUseCase()
  {
    var command = new StartCodecovImportCommand(Forge.GitHub, "octo-org", "app", "plain-token");

    command.RequiredPermission.ShouldBe(RepositoryPermission.Admin);
    command.ToString().ShouldNotContain("plain-token");
  }

  [Theory]
  [InlineData("", false)]
  [InlineData("with space", false)]
  [InlineData("abc-123_DEF.xyz", true)]
  public void Validate_Token_ShouldAcceptOnlyPrintableCharacters(string token, bool valid)
  {
    var result = new StartCodecovImportCommandValidator().Validate(new StartCodecovImportCommand(Forge.GitHub, "octo-org", "app", token));

    result.IsValid.ShouldBe(valid);

    if (!valid)
    {
      result.Errors.ShouldAllBe(static e => e.ErrorCode == RepositoryErrors.InvalidCodecovToken);
    }
  }

  #endregion
}
