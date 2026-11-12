// ------------------------------------------------------------------------------------------------
// <copyright file="AuthorizationBehaviorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Behaviors;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Diagnostics.Queries.Ping;
using CodeCoverage.Application.Features.Organizations;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Security;
using CodeCoverage.Application.Tests.Behaviors.Support;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Tests.Behaviors;

public sealed class AuthorizationBehaviorTests
{
  #region Fields

  private readonly IOrganizationScope _scope = A.Fake<IOrganizationScope>();

  private readonly IServiceProvider _services = A.Fake<IServiceProvider>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IRepositoryAccessService _access = A.Fake<IRepositoryAccessService>();

  private readonly IOrganizationRoleResolver _roles = A.Fake<IOrganizationRoleResolver>();

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly List<string> _steps = [];

  private int _nextCalls;

  #endregion

  #region Ctors

  public AuthorizationBehaviorTests()
  {
    A.CallTo(() => _services.GetService(typeof(IOrganizationScope))).Returns(_scope);
    A.CallTo(() => _services.GetService(typeof(IRepositoryRepository))).Returns(_repositories);
    A.CallTo(() => _services.GetService(typeof(IRepositoryAccessService))).Returns(_access);
    A.CallTo(() => _services.GetService(typeof(IOrganizationRoleResolver))).Returns(_roles);
    A.CallTo(() => _services.GetService(typeof(ICurrentOrganization))).Returns(_organization);
    A.CallTo(() => _organization.Id).Returns(7L);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_AdminOnlyWithoutAdmin_ShouldThrowForbidden()
  {
    var exception = await Should.ThrowAsync<ForbiddenException>(
                      async () => await SendAsync(new AdminOnlyCommand(), Principals.User(isAdmin: false)));

    exception.ErrorCode.ShouldBe(SecurityErrors.Forbidden);
    _nextCalls.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_AdminOnlyFromAnonymous_ShouldThrowUnauthorized()
  {
    var exception = await Should.ThrowAsync<UnauthorizedException>(
                      async () => await SendAsync(new AdminOnlyCommand(), Principals.Anonymous()));

    exception.ErrorCode.ShouldBe(SecurityErrors.Unauthenticated);
    _nextCalls.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_AdminOnlyFromAdmin_ShouldCallNext()
  {
    var response = await SendAsync(new AdminOnlyCommand(), Principals.User(isAdmin: true));

    response.ShouldBe("handled");
    _nextCalls.ShouldBe(1);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task Handle_SystemOnlyFromUser_ShouldThrowForbidden(bool isAdmin)
  {
    var exception = await Should.ThrowAsync<ForbiddenException>(
                      async () => await SendAsync(new SystemOnlyCommand(), Principals.User(isAdmin)));

    exception.ErrorCode.ShouldBe(SecurityErrors.Forbidden);
    _nextCalls.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_SystemOnlyFromAnonymous_ShouldThrowForbidden()
  {
    var exception = await Should.ThrowAsync<ForbiddenException>(
                      async () => await SendAsync(new SystemOnlyCommand(), Principals.Anonymous()));

    exception.ErrorCode.ShouldBe(SecurityErrors.Forbidden);
    _nextCalls.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_AsSystem_ShouldPassEveryCheck()
  {
    var system = Principals.System();

    (await SendAsync(new SystemOnlyCommand(), system)).ShouldBe("handled");
    (await SendAsync(new AdminOnlyCommand(), system)).ShouldBe("handled");
    (await SendAsync(new SampleCommand("main", "token"), system)).ShouldBe("handled");
    _nextCalls.ShouldBe(3);
  }

  [Fact]
  public async Task Handle_WithoutMarker_ShouldCallNextForAnonymous()
  {
    var response = await SendAsync(new SampleCommand("main", "token"), Principals.Anonymous());

    response.ShouldBe("handled");
    _nextCalls.ShouldBe(1);
  }

  [Fact]
  public async Task Handle_WithNullNext_ShouldThrowArgumentNullException()
  {
    var behavior = new AuthorizationBehavior<AdminOnlyCommand, string>(Principals.System(), _services);

    await Should.ThrowAsync<ArgumentNullException>(
      async () => await behavior.Handle(new AdminOnlyCommand(), null!, Ct));
  }

  [Fact]
  public async Task Handle_WithNullMessage_ShouldThrowArgumentNullException()
  {
    var behavior = new AuthorizationBehavior<AdminOnlyCommand, string>(Principals.System(), _services);

    await Should.ThrowAsync<ArgumentNullException>(
      async () => await behavior.Handle(null!, Next, Ct));
  }

  [Fact]
  public async Task Handle_RepositoryScopedActiveOrganization_ShouldEnterBeforeValidation()
  {
    OrganizationsAreActive();

    await Should.ThrowAsync<NotFoundException>(
      async () => await SendAsync(new RepositoryScopedQuery(Forge.GitHub, "acme"), Principals.User()));

    A.CallTo(() => _scope.TryEnterAsync(Forge.GitHub, "acme", Ct)).MustHaveHappenedOnceExactly();
    _nextCalls.ShouldBe(0, "the validation and the handler run after the gate");
  }

  [Fact]
  public async Task Handle_RepositoryScopedSuspendedOrganization_ShouldThrowRepositoryNotFound()
  {
    OrganizationsAreNotActive();

    var exception = await Should.ThrowAsync<NotFoundException>(
                      async () => await SendAsync(new RepositoryScopedQuery(Forge.GitHub, "acme"), Principals.User()));

    exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
    _nextCalls.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_OrganizationScopedUnknownOrganization_ShouldThrowOrganizationNotFound()
  {
    OrganizationsAreNotActive();

    var exception = await Should.ThrowAsync<NotFoundException>(
                      async () => await SendAsync(new OrganizationScopedCommand(Forge.GitLab, "acme"), Principals.User()));

    exception.ErrorCode.ShouldBe(OrganizationErrors.NotFound);
    A.CallTo(() => _scope.TryEnterAsync(Forge.GitLab, "acme", Ct)).MustHaveHappenedOnceExactly();
    _nextCalls.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_OwnerAddressedUnknownOrganization_ShouldLeaveScopeUnset()
  {
    OrganizationsAreNotActive();

    var response = await SendAsync(new OwnerAddressedCommand(Forge.GitHub, "acme"), Principals.Anonymous());

    response.ShouldBe("handled");
    A.CallTo(() => _scope.TryEnterAsync(Forge.GitHub, "acme", Ct)).MustHaveHappenedOnceExactly();
    _nextCalls.ShouldBe(1);
  }

  [Fact]
  public async Task Handle_OwnerAddressedActiveOrganization_ShouldEnterBeforeNext()
  {
    OrganizationsAreActive();

    await SendAsync(new OwnerAddressedCommand(Forge.GitHub, "acme"), Principals.Anonymous());

    _steps.ShouldBe(["enter", "next"]);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData(":")]
  [InlineData(":project")]
  [InlineData("~stale-1")]
  [InlineData("a/b")]
  [InlineData("a b")]
  public async Task Handle_MalformedOwner_ShouldNotThrowServerError(string? owner)
  {
    OrganizationsAreNotActive();

    var exception = await Should.ThrowAsync<NotFoundException>(
                      async () => await SendAsync(new RepositoryScopedQuery(Forge.GitHub, owner!), Principals.Anonymous()));

    exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
    (await SendAsync(new OwnerAddressedCommand(Forge.GitHub, owner!), Principals.Anonymous())).ShouldBe("handled");
  }

  [Theory]
  [InlineData("acme:project", "acme")]
  [InlineData("group:sub:sub2", "group")]
  [InlineData("acme", "acme")]
  public async Task Handle_OwnerWithNamespace_ShouldEnterItsOrganization(string owner, string organization)
  {
    await SendAsync(new OwnerAddressedCommand(Forge.AzureDevOps, owner), Principals.Anonymous());

    A.CallTo(() => _scope.TryEnterAsync(Forge.AzureDevOps, organization, Ct)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_RepositoryScopedWithoutRead_ShouldThrowNotFound()
  {
    OrganizationsAreActive();
    Tracked();

    var exception = await Should.ThrowAsync<NotFoundException>(
                      async () => await SendAsync(new RepositoryScopedQuery(Forge.GitHub, "acme"), Principals.User()));

    exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
    _nextCalls.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_RepositoryScopedWithRead_ShouldCallNext()
  {
    OrganizationsAreActive();
    Tracked();
    Grants(RepositoryPermission.Read);

    (await SendAsync(new RepositoryScopedQuery(Forge.GitHub, "acme"), Principals.User())).ShouldBe("handled");

    A.CallTo(() => _access.CanAsync(42, "1296269", RepositoryPermission.Read, Ct)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_RepositoryScopedWithoutAdmin_ShouldThrowForbidden()
  {
    OrganizationsAreActive();
    Tracked();
    Grants(RepositoryPermission.Read, RepositoryPermission.Write);

    var exception = await Should.ThrowAsync<ForbiddenException>(
                      async () => await SendAsync(new RepositoryScopedQuery(Forge.GitHub, "acme", RepositoryPermission.Admin), Principals.User()));

    exception.ErrorCode.ShouldBe(SecurityErrors.Forbidden);
    _nextCalls.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_RepositoryScopedWithAdmin_ShouldCallNext()
  {
    OrganizationsAreActive();
    Tracked();
    Grants(RepositoryPermission.Read, RepositoryPermission.Write, RepositoryPermission.Admin);

    (await SendAsync(new RepositoryScopedQuery(Forge.GitHub, "acme", RepositoryPermission.Admin), Principals.User())).ShouldBe("handled");
  }

  // ADR-013: the administration of the instance grants nothing on the data of an organization.
  [Fact]
  public async Task Handle_InstanceAdminNotMember_ShouldThrowNotFound()
  {
    OrganizationsAreActive();
    Tracked();

    var repository = await Should.ThrowAsync<NotFoundException>(
                       async () => await SendAsync(new RepositoryScopedQuery(Forge.GitHub, "acme"), Principals.User(isAdmin: true)));
    var organization = await Should.ThrowAsync<NotFoundException>(
                         async () => await SendAsync(new OrganizationScopedCommand(Forge.GitHub, "acme"), Principals.User(isAdmin: true)));

    repository.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
    organization.ErrorCode.ShouldBe(OrganizationErrors.NotFound);
    _nextCalls.ShouldBe(0);
  }

  // Decision 7: readable on GitHub, but deactivated or departed here, it answers as a missing repository.
  [Fact]
  public async Task Handle_RepositoryScopedInactiveRepository_ShouldThrowNotFound()
  {
    OrganizationsAreActive();
    Tracked(active: false);
    Grants(RepositoryPermission.Read, RepositoryPermission.Write, RepositoryPermission.Admin);

    var exception = await Should.ThrowAsync<NotFoundException>(
                      async () => await SendAsync(new RepositoryScopedQuery(Forge.GitHub, "acme"), Principals.User()));

    exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
    A.CallTo(_access).MustNotHaveHappened();
  }

  // The acceptance of M3.04: a private repository the user cannot read cannot be told from a missing one.
  [Fact]
  public async Task Handle_EveryCauseOfRefusal_ShouldRefuseTheSameWay()
  {
    var refusals = new List<NotFoundException>();

    OrganizationsAreNotActive();
    refusals.Add(await Should.ThrowAsync<NotFoundException>(async () => await SendAsync(new RepositoryScopedQuery(Forge.GitHub, "acme"), Principals.User())));

    OrganizationsAreActive();
    refusals.Add(await Should.ThrowAsync<NotFoundException>(async () => await SendAsync(new RepositoryScopedQuery(Forge.GitHub, "acme"), Principals.Anonymous())));
    refusals.Add(await Should.ThrowAsync<NotFoundException>(async () => await SendAsync(new RepositoryScopedQuery(Forge.GitHub, "acme"), Principals.User())));

    Tracked();
    refusals.Add(await Should.ThrowAsync<NotFoundException>(async () => await SendAsync(new RepositoryScopedQuery(Forge.GitHub, "acme"), Principals.User())));

    refusals.Select(static e => (e.ErrorCode, e.Message)).Distinct().ShouldHaveSingleItem();
  }

  [Fact]
  public async Task Handle_OrganizationScopedAdminAsParticipant_ShouldThrowForbidden()
  {
    OrganizationsAreActive();
    A.CallTo(() => _roles.ResolveAsync(42, 7, Ct)).Returns(OrganizationRole.Participant);

    var exception = await Should.ThrowAsync<ForbiddenException>(
                      async () => await SendAsync(new OrganizationScopedCommand(Forge.GitHub, "acme"), Principals.User()));

    exception.ErrorCode.ShouldBe(SecurityErrors.Forbidden);
    _nextCalls.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_OrganizationScopedAsAdmin_ShouldCallNext()
  {
    OrganizationsAreActive();
    A.CallTo(() => _roles.ResolveAsync(42, 7, Ct)).Returns(OrganizationRole.Admin);

    (await SendAsync(new OrganizationScopedCommand(Forge.GitHub, "acme"), Principals.User())).ShouldBe("handled");
  }

  [Fact]
  public async Task Handle_OrganizationScopedParticipantAsParticipant_ShouldCallNext()
  {
    OrganizationsAreActive();
    A.CallTo(() => _roles.ResolveAsync(42, 7, Ct)).Returns(OrganizationRole.Participant);

    (await SendAsync(new OrganizationScopedCommand(Forge.GitHub, "acme", OrganizationRole.Participant), Principals.User())).ShouldBe("handled");
  }

  [Fact]
  public async Task Handle_OrganizationScopedFromAnonymous_ShouldThrowNotFound()
  {
    OrganizationsAreActive();

    var exception = await Should.ThrowAsync<NotFoundException>(
                      async () => await SendAsync(new OrganizationScopedCommand(Forge.GitHub, "acme"), Principals.Anonymous()));

    exception.ErrorCode.ShouldBe(OrganizationErrors.NotFound);
    A.CallTo(_roles).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_SystemPrincipal_ShouldKeepTheScopeOfTheJob()
  {
    var system = Principals.System();

    await SendAsync(new RepositoryScopedQuery(Forge.GitHub, "acme"), system);
    await SendAsync(new OrganizationScopedCommand(Forge.GitHub, "acme"), system);
    await SendAsync(new OwnerAddressedCommand(Forge.GitHub, "acme"), system);

    A.CallTo(_scope).MustNotHaveHappened();
    _nextCalls.ShouldBe(3);
  }

  [Fact]
  public async Task Handle_UserScopedFromAnonymous_ShouldThrowUnauthorized()
  {
    var exception = await Should.ThrowAsync<UnauthorizedException>(
                      async () => await SendAsync(new UserScopedQuery(), Principals.Anonymous()));

    exception.ErrorCode.ShouldBe(SecurityErrors.Unauthenticated);
    _nextCalls.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_UserScopedFromUser_ShouldCallNextWithoutEntering()
  {
    var response = await SendAsync(new UserScopedQuery(), Principals.User());

    response.ShouldBe("handled");
    A.CallTo(_services).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_AnonymousEntry_ShouldCallNextWithoutEntering()
  {
    var response = await SendAsync(new PingQuery(), Principals.Anonymous());

    response.ShouldBe("handled");
    A.CallTo(_services).MustNotHaveHappened();
  }

  private void OrganizationsAreActive()
  {
    A.CallTo(() => _scope.TryEnterAsync(A<Forge>._, A<string?>._, A<CancellationToken>._))
     .Invokes(() => _steps.Add("enter"))
     .Returns(true);
  }

  private void Tracked(bool active = true)
  {
    var repository = TrackedRepository.Create(9, "1296269", RepositorySlug.Parse("acme/repository"), "main", isPrivate: true, "protected", DateTimeOffset.UnixEpoch);

    if (!active)
    {
      repository.Deactivate(DateTimeOffset.UnixEpoch);
    }

    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
  }

  private void Grants(params RepositoryPermission[] permissions)
  {
    foreach (var permission in permissions)
    {
      A.CallTo(() => _access.CanAsync(42, "1296269", permission, A<CancellationToken>._)).Returns(true);
    }
  }

  private void OrganizationsAreNotActive()
  {
    A.CallTo(() => _scope.TryEnterAsync(A<Forge>._, A<string?>._, A<CancellationToken>._)).Returns(false);
  }

  private ValueTask<string> SendAsync<TMessage>(TMessage message, ICurrentUser currentUser)
    where TMessage : IMessage
  {
    var behavior = new AuthorizationBehavior<TMessage, string>(currentUser, _services);

    return behavior.Handle(message, Next, Ct);
  }

  private ValueTask<string> Next<TMessage>(TMessage message, CancellationToken cancellationToken)
  {
    _nextCalls++;
    _steps.Add("next");

    return ValueTask.FromResult("handled");
  }

  #endregion
}
