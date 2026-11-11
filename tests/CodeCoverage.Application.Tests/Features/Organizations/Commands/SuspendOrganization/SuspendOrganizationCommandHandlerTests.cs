// ------------------------------------------------------------------------------------------------
// <copyright file="SuspendOrganizationCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Organizations;
using CodeCoverage.Application.Features.Organizations.Commands.SuspendOrganization;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Features.Organizations.Commands.SuspendOrganization;

public sealed class SuspendOrganizationCommandHandlerTests
{
  #region Fields

  private const string AccountId = "1001";

  private const string Login = "octo-org";

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  private readonly IOrganizationDirectory _directory = A.Fake<IOrganizationDirectory>();

  private readonly IClock _clock = A.Fake<IClock>();

  private readonly IAuditLog _audit = A.Fake<IAuditLog>();

  #endregion

  #region Ctors

  public SuspendOrganizationCommandHandlerTests()
  {
    A.CallTo(() => _clock.UtcNow).Returns(Now);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // The entry of a scope only accepts an active organization (OrganizationScopeTests): the state is what blocks it.
  [Fact]
  public async Task Handle_ShouldBlockTheNextEntry()
  {
    var organization = Active();
    Finds(organization);

    await Handler().Handle(new SuspendOrganizationCommand(Forge.GitHub, Login), Ct);

    organization.State.ShouldBe(OrganizationState.Suspended);
    organization.SuspensionReason.ShouldBe(SuspensionReason.Administrator);
    A.CallTo(() => _audit.RecordAsync("Organization.Suspend", $"GitHub/{Login}", null, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Theory]
  [InlineData(SuspensionReason.Administrator)]
  [InlineData(SuspensionReason.ReservedLogin)]
  public async Task Handle_AlreadySuspended_ShouldChangeNothing(SuspensionReason reason)
  {
    var organization = Active();
    organization.Suspend(reason, Now);
    var version = organization.Version;
    Finds(organization);

    await Handler().Handle(new SuspendOrganizationCommand(Forge.GitHub, Login), Ct);

    organization.SuspensionReason.ShouldBe(reason);
    organization.Version.ShouldBe(version);
    A.CallTo(_audit).MustNotHaveHappened();
  }

  // Otherwise a reinstallation would lift a suspension the administrator meant to keep.
  [Fact]
  public async Task Handle_SuspendedByTheRemovalOfTheApplication_ShouldTakeTheSuspensionOver()
  {
    var organization = Active();
    organization.Suspend(SuspensionReason.AppUninstalled, Now);
    Finds(organization);

    await Handler().Handle(new SuspendOrganizationCommand(Forge.GitHub, Login), Ct);

    organization.State.ShouldBe(OrganizationState.Suspended);
    organization.SuspensionReason.ShouldBe(SuspensionReason.Administrator);
    organization.Reactivate(Now).ShouldBeFalse();
    A.CallTo(() => _audit.RecordAsync("Organization.Suspend", $"GitHub/{Login}", null, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_UnknownLogin_ShouldThrowNotFound()
  {
    A.CallTo(() => _directory.FindByLoginAsync(Forge.GitHub, Login, A<CancellationToken>._)).Returns((Organization?)null);

    var exception = await Should.ThrowAsync<NotFoundException>(
                      async () => await Handler().Handle(new SuspendOrganizationCommand(Forge.GitHub, Login), Ct)
                    );

    exception.ErrorCode.ShouldBe(OrganizationErrors.NotFound);
  }

  private static Organization Active()
  {
    var organization = Organization.CreatePending(Forge.GitHub, AccountId, Login, OrganizationAccountType.Organization, Now);
    organization.Admit("someone", Now);

    return organization;
  }

  private SuspendOrganizationCommandHandler Handler() => new(_directory, _clock, _audit);

  private void Finds(Organization organization)
  {
    A.CallTo(() => _directory.FindByLoginAsync(Forge.GitHub, Login, A<CancellationToken>._)).Returns(organization);

    A.CallTo(() => _directory.ChangeAsync(Forge.GitHub, AccountId, A<Action<Organization>>._, A<CancellationToken>._))
     .ReturnsLazily((Forge _, string _, Action<Organization> change, CancellationToken _) =>
       {
         change(organization);

         return Task.FromResult<Organization?>(organization);
       });
  }

  #endregion
}
