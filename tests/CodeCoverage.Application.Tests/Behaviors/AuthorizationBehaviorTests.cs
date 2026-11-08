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

namespace CodeCoverage.Application.Tests.Behaviors;

public sealed class AuthorizationBehaviorTests
{
  #region Fields

  private readonly IOrganizationScope _scope = A.Fake<IOrganizationScope>();

  private readonly IServiceProvider _services = A.Fake<IServiceProvider>();

  private readonly List<string> _steps = [];

  private int _nextCalls;

  #endregion

  #region Ctors

  public AuthorizationBehaviorTests()
  {
    A.CallTo(() => _services.GetService(typeof(IOrganizationScope))).Returns(_scope);
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

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task Handle_RepositoryScopedBeforeAccessCheck_ShouldRefuseUser(bool isAdmin)
  {
    OrganizationsAreActive();

    var exception = await Should.ThrowAsync<NotFoundException>(
                      async () => await SendAsync(new RepositoryScopedQuery(Forge.GitHub, "acme"), Principals.User(isAdmin)));

    exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
    _nextCalls.ShouldBe(0);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task Handle_OrganizationScopedBeforeAccessCheck_ShouldRefuseUser(bool isAdmin)
  {
    OrganizationsAreActive();

    var exception = await Should.ThrowAsync<NotFoundException>(
                      async () => await SendAsync(new OrganizationScopedCommand(Forge.GitHub, "acme"), Principals.User(isAdmin)));

    exception.ErrorCode.ShouldBe(OrganizationErrors.NotFound);
    _nextCalls.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_ActiveAndUnknownOrganization_ShouldRefuseTheSameWay()
  {
    OrganizationsAreActive();
    var active = await Should.ThrowAsync<NotFoundException>(
                   async () => await SendAsync(new RepositoryScopedQuery(Forge.GitHub, "acme"), Principals.Anonymous()));

    OrganizationsAreNotActive();
    var unknown = await Should.ThrowAsync<NotFoundException>(
                    async () => await SendAsync(new RepositoryScopedQuery(Forge.GitHub, "acme"), Principals.Anonymous()));

    unknown.ErrorCode.ShouldBe(active.ErrorCode);
    unknown.Message.ShouldBe(active.Message);
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
