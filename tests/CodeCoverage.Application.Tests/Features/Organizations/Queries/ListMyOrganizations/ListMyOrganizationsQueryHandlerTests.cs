// ------------------------------------------------------------------------------------------------
// <copyright file="ListMyOrganizationsQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.Organizations.Queries.ListMyOrganizations;
using CodeCoverage.Application.Tests.Support;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Features.Organizations.Queries.ListMyOrganizations;

public sealed class ListMyOrganizationsQueryHandlerTests
{
  #region Fields

  private const long UserId = 7;

  private static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

  private readonly ICurrentUser _currentUser = A.Fake<ICurrentUser>();

  private readonly IOrganizationMembershipRepository _memberships = A.Fake<IOrganizationMembershipRepository>();

  private readonly IOrganizationRoleResolver _roles = A.Fake<IOrganizationRoleResolver>();

  private readonly InMemoryOrganizationDirectory _directory = new(Now);

  private readonly ListMyOrganizationsQueryHandler _handler;

  #endregion

  #region Ctors

  public ListMyOrganizationsQueryHandlerTests()
  {
    A.CallTo(() => _currentUser.UserId).Returns(UserId);
    A.CallTo(() => _roles.ResolveAsync(A<long>._, A<long>._, A<CancellationToken>._)).Returns((OrganizationRole?)null);

    _handler = new ListMyOrganizationsQueryHandler(_currentUser, _memberships, _roles, _directory);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ShouldListTheOrganizationsOfThePrincipalWithItsRoleByLogin()
  {
    var zeta = _directory.Add("1001", "Zeta-org", admitted: true);
    var alpha = _directory.Add("2002", "alpha-org", admitted: true);
    Member(zeta, OrganizationRole.Participant);
    Member(alpha, OrganizationRole.Admin);

    var listed = await HandleAsync();

    listed.Select(static o => (o.Organization.Login, o.Role)).ShouldBe([("alpha-org", OrganizationRole.Admin), ("Zeta-org", OrganizationRole.Participant)]);
    // Only the memberships of the principal are read.
    A.CallTo(() => _memberships.ListOfUserAsync(UserId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _memberships.ListOfUserAsync(A<long>.That.Not.IsEqualTo(UserId), A<CancellationToken>._)).MustNotHaveHappened();
  }

  // The resolver fails closed: a membership that is too old, or that grants nothing, lists nothing.
  [Fact]
  public async Task Handle_MembershipThatGrantsNothing_ShouldNotBeListed()
  {
    var expired = _directory.Add("1001", "expired-org", admitted: true);
    var fresh = _directory.Add("2002", "fresh-org", admitted: true);
    Member(expired, null);
    Member(fresh, OrganizationRole.Participant);

    var listed = await HandleAsync();

    listed.ShouldHaveSingleItem().Organization.Id.ShouldBe(fresh.Id);
  }

  [Fact]
  public async Task Handle_OrganizationThatIsNotActive_ShouldNotBeListed()
  {
    var suspended = _directory.Add("1001", "suspended-org", admitted: true);
    suspended.Suspend(SuspensionReason.Administrator, Now);
    Member(suspended, OrganizationRole.Admin);

    (await HandleAsync()).ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_PrincipalWithoutAUserRow_ShouldListNothing()
  {
    A.CallTo(() => _currentUser.UserId).Returns((long?)null);

    (await HandleAsync()).ShouldBeEmpty();

    A.CallTo(_memberships).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_Null_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(async () => await _handler.Handle(null!, Ct));
  }

  private async Task<IReadOnlyList<MemberOrganization>> HandleAsync() => await _handler.Handle(new ListMyOrganizationsQuery(), Ct);

  private readonly List<OrganizationMembership> _rows = [];

  private void Member(Organization organization, OrganizationRole? resolved)
  {
    _rows.Add(OrganizationMembership.Create(UserId, organization.Id, OrganizationRole.Admin, Now));
    A.CallTo(() => _memberships.ListOfUserAsync(UserId, A<CancellationToken>._)).Returns([.. _rows]);
    A.CallTo(() => _roles.ResolveAsync(UserId, organization.Id, A<CancellationToken>._)).Returns(resolved);
  }

  #endregion
}
