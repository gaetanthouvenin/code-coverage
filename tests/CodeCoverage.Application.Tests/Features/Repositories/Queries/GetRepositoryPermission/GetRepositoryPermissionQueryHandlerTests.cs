// ------------------------------------------------------------------------------------------------
// <copyright file="GetRepositoryPermissionQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.Repositories.Queries.GetRepositoryPermission;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Tests.Features.Repositories.Queries.GetRepositoryPermission;

public sealed class GetRepositoryPermissionQueryHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly ICurrentUser _currentUser = A.Fake<ICurrentUser>();

  private readonly IRepositoryAccessService _access = A.Fake<IRepositoryAccessService>();

  private readonly IOrganizationRoleResolver _roles = A.Fake<IOrganizationRoleResolver>();

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  #endregion

  #region Ctors

  public GetRepositoryPermissionQueryHandlerTests()
  {
    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);

    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _currentUser.UserId).Returns(42L);
    A.CallTo(() => _organization.Id).Returns(7L);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_RepositoryAdministrator_ShouldAnswerTrue()
  {
    A.CallTo(() => _access.CanAsync(42, "2002", RepositoryPermission.Admin, A<CancellationToken>._)).Returns(true);

    (await Handler().Handle(Query(), Ct)).ShouldBeTrue();
    A.CallTo(_roles).MustNotHaveHappened();
  }

  // The same rule as the gate: an administrator of the organization administers its repositories.
  [Fact]
  public async Task Handle_OrganizationAdministrator_ShouldAnswerTrue()
  {
    A.CallTo(() => _roles.ResolveAsync(42, 7, A<CancellationToken>._)).Returns(OrganizationRole.Admin);

    (await Handler().Handle(Query(), Ct)).ShouldBeTrue();
  }

  [Fact]
  public async Task Handle_Participant_ShouldAnswerFalse()
  {
    A.CallTo(() => _roles.ResolveAsync(42, 7, A<CancellationToken>._)).Returns(OrganizationRole.Participant);

    (await Handler().Handle(Query(), Ct)).ShouldBeFalse();
  }

  // The role is read in the organization of the repository, never in another one.
  [Fact]
  public async Task Handle_AdministratorOfAnotherOrganization_ShouldAnswerFalse()
  {
    A.CallTo(() => _roles.ResolveAsync(42, 9, A<CancellationToken>._)).Returns(OrganizationRole.Admin);

    (await Handler().Handle(Query(), Ct)).ShouldBeFalse();
  }

  // A caller that is not a user (the system, a token without a user) administers nothing here.
  [Fact]
  public async Task Handle_WithoutUser_ShouldAnswerFalseWithoutReading()
  {
    A.CallTo(() => _currentUser.UserId).Returns(null);

    (await Handler().Handle(Query(), Ct)).ShouldBeFalse();
    A.CallTo(_access).MustNotHaveHappened();
    A.CallTo(_roles).MustNotHaveHappened();
  }

  // The query is only a read: a repository the user cannot read is refused by the gate, not here.
  [Fact]
  public void Query_ShouldRequireReadingOnly() => Query().RequiredPermission.ShouldBe(RepositoryPermission.Read);

  private static GetRepositoryPermissionQuery Query() => new(Forge.GitHub, "octo-org", "app");

  private GetRepositoryPermissionQueryHandler Handler() =>
    new(new CommitLocator(_repositories, _commits), _currentUser, _access, _roles, _organization);

  #endregion
}
