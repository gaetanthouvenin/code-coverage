// ------------------------------------------------------------------------------------------------
// <copyright file="AdmitOrganizationCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Organizations;
using CodeCoverage.Application.Features.Organizations.Commands.AdmitOrganization;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Features.Organizations.Commands.AdmitOrganization;

public sealed class AdmitOrganizationCommandHandlerTests
{
  #region Fields

  private const string AccountId = "1001";

  private const string Login = "octo-org";

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  private readonly IOrganizationDirectory _directory = A.Fake<IOrganizationDirectory>();

  private readonly ICurrentUser _user = A.Fake<ICurrentUser>();

  private readonly IClock _clock = A.Fake<IClock>();

  private readonly IAuditLog _audit = A.Fake<IAuditLog>();

  private readonly IJobQueue _jobQueue = A.Fake<IJobQueue>();

  #endregion

  #region Ctors

  public AdmitOrganizationCommandHandlerTests()
  {
    A.CallTo(() => _clock.UtcNow).Returns(Now);
    A.CallTo(() => _user.IsSystem).Returns(true);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_Pending_ShouldActivateAndAudit()
  {
    var pending = Pending();
    Knows(pending);

    var admitted = await Handler().Handle(Command(), Ct);

    admitted.State.ShouldBe(OrganizationState.Active);
    admitted.AdmittedBy.ShouldBe(AuditEntry.SystemActor);
    A.CallTo(() => _audit.RecordAsync("Organization.Admit", $"GitHub/{Login}", null, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_Pending_ShouldQueueTheCoordinatorOfTheInstallations()
  {
    Knows(Pending());

    await Handler().Handle(Command(), Ct);

    // The admission knows no installation: the coordinator of the instance lane finds it, never a job named for
    // the organization.
    A.CallTo(
       () => _jobQueue.EnqueueAsync(
         A<JobRequest>.That.Matches(j => j.Type == JobTypes.SyncInstallations && j.IsInstance && j.OrganizationId == null),
         A<CancellationToken>._
       )
     )
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_AccountOfAnotherForge_ShouldQueueNoListingOfGitHubInstallations()
  {
    var pending = Organization.CreatePending(Forge.Bitbucket, AccountId, Login, OrganizationAccountType.Organization, Now);
    A.CallTo(() => _directory.FindByAccountIdAsync(Forge.Bitbucket, AccountId, A<CancellationToken>._)).Returns(pending);
    Upserts(pending);
    Changes(pending);

    var admitted = await Handler().Handle(new AdmitOrganizationCommand(Forge.Bitbucket, Login, AccountId), Ct);

    admitted.State.ShouldBe(OrganizationState.Active);
    A.CallTo(_jobQueue).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_UnknownAccount_ShouldCreateActive()
  {
    var created = Pending();
    A.CallTo(() => _directory.FindByAccountIdAsync(Forge.GitHub, AccountId, A<CancellationToken>._)).Returns((Organization?)null);
    Upserts(created);
    Changes(created);

    var admitted = await Handler().Handle(Command(), Ct);

    admitted.State.ShouldBe(OrganizationState.Active);
    A.CallTo(() => _directory.UpsertFromForgeAsync(Forge.GitHub, AccountId, Login, OrganizationAccountType.Organization, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_PendingPersonalAccount_ShouldKeepItsAccountType()
  {
    var pending = Organization.CreatePending(Forge.GitHub, AccountId, Login, OrganizationAccountType.User, Now);
    Knows(pending);

    await Handler().Handle(Command(), Ct);

    A.CallTo(() => _directory.UpsertFromForgeAsync(Forge.GitHub, AccountId, Login, OrganizationAccountType.User, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_SignedInAdministrator_ShouldBeNamedAsAdmitter()
  {
    A.CallTo(() => _user.IsSystem).Returns(false);
    A.CallTo(() => _user.Login).Returns("root-admin");
    Knows(Pending());

    var admitted = await Handler().Handle(Command(), Ct);

    admitted.AdmittedBy.ShouldBe("root-admin");
  }

  // M3.43a: a login that names a route of the product is admitted like any other, since an address names its forge
  // first (ADR-014).
  [Theory]
  [InlineData("admin")]
  [InlineData("API")]
  public async Task Handle_LoginOfARouteOfTheProduct_ShouldAdmitIt(string login)
  {
    var pending = Organization.CreatePending(Forge.GitHub, AccountId, login, OrganizationAccountType.Organization, Now);
    Knows(pending);

    var admitted = await Handler().Handle(new AdmitOrganizationCommand(Forge.GitHub, login, AccountId), Ct);

    admitted.State.ShouldBe(OrganizationState.Active);
    admitted.Login.ShouldBe(login);
  }

  [Fact]
  public async Task Handle_AlreadyActive_ShouldThrowConflictWithoutChange()
  {
    var active = Pending();
    active.Admit("someone", Now);
    A.CallTo(() => _directory.FindByAccountIdAsync(Forge.GitHub, AccountId, A<CancellationToken>._)).Returns(active);

    var exception = await Should.ThrowAsync<ConflictException>(async () => await Handler().Handle(Command(), Ct));

    exception.ErrorCode.ShouldBe(OrganizationErrors.NotPending);
    A.CallTo(() => _directory.UpsertFromForgeAsync(A<Forge>._, A<string>._, A<string>._, A<OrganizationAccountType>._, A<CancellationToken>._))
     .MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_Suspended_ShouldThrowConflict()
  {
    var suspended = Pending();
    suspended.Suspend(SuspensionReason.Administrator, Now);
    A.CallTo(() => _directory.FindByAccountIdAsync(Forge.GitHub, AccountId, A<CancellationToken>._)).Returns(suspended);

    var exception = await Should.ThrowAsync<ConflictException>(async () => await Handler().Handle(Command(), Ct));

    exception.ErrorCode.ShouldBe(OrganizationErrors.NotPending);
  }

  [Theory]
  [InlineData("not a login", AccountId, OrganizationErrors.InvalidLogin)]
  [InlineData(Login, " ", OrganizationErrors.InvalidAccountId)]
  public void Validator_InvalidValue_ShouldFail(string login, string accountId, string errorCode)
  {
    var result = new AdmitOrganizationCommandValidator().Validate(new AdmitOrganizationCommand(Forge.GitHub, login, accountId));

    result.Errors.ShouldHaveSingleItem().ErrorCode.ShouldBe(errorCode);
  }

  private static AdmitOrganizationCommand Command() => new(Forge.GitHub, Login, AccountId);

  private static Organization Pending() => Organization.CreatePending(Forge.GitHub, AccountId, Login, OrganizationAccountType.Organization, Now);

  private AdmitOrganizationCommandHandler Handler() => new(_directory, _user, _clock, _audit, _jobQueue);

  private void Knows(Organization organization)
  {
    A.CallTo(() => _directory.FindByAccountIdAsync(Forge.GitHub, AccountId, A<CancellationToken>._)).Returns(organization);
    Upserts(organization);
    Changes(organization);
  }

  private void Upserts(Organization organization) =>
    A.CallTo(() => _directory.UpsertFromForgeAsync(A<Forge>._, A<string>._, A<string>._, A<OrganizationAccountType>._, A<CancellationToken>._))
     .Returns(organization);

  private void Changes(Organization organization) =>
    A.CallTo(() => _directory.ChangeAsync(organization.Forge, AccountId, A<Action<Organization>>._, A<CancellationToken>._))
     .ReturnsLazily((Forge _, string _, Action<Organization> change, CancellationToken _) =>
       {
         change(organization);

         return Task.FromResult<Organization?>(organization);
       });

  #endregion
}
