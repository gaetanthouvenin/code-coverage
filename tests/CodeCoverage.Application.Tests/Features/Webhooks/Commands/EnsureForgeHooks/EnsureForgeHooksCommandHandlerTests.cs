// ------------------------------------------------------------------------------------------------
// <copyright file="EnsureForgeHooksCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Features.Webhooks.Commands.EnsureForgeHooks;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Features.Webhooks.Commands.EnsureForgeHooks;

public sealed class EnsureForgeHooksCommandHandlerTests
{
  #region Fields

  private const string ConnectionId = "contoso-devops";

  private const long OrganizationId = 9;

  private static readonly DateTimeOffset Now = new(2026, 11, 23, 8, 0, 0, TimeSpan.Zero);

  private readonly IForgeConnectionRepository _connections = A.Fake<IForgeConnectionRepository>();

  private readonly IForgeHookRepository _hooks = A.Fake<IForgeHookRepository>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IForgeGatewayProvider _forges = A.Fake<IForgeGatewayProvider>();

  private readonly IForgeHookManager _manager = A.Fake<IForgeHookManager>();

  private readonly UrlOptions _urls = new() { ApiPublicUrl = "https://coverage-api.example.com/" };

  private readonly List<ForgeHook> _known = [];

  private readonly List<TrackedRepository> _tracked = [];

  private readonly List<ForgeHook> _saved = [];

  private readonly ForgeConnection _connection = Connection(OrganizationId);

  private readonly EnsureForgeHooksCommandHandler _handler;

  private IReadOnlyList<ForgeHookScope>? _asked;

  private ForgeHookCallback? _callback;

  #endregion

  #region Ctors

  public EnsureForgeHooksCommandHandlerTests()
  {
    var protector = A.Fake<ISecretProtector>();
    var urls = A.Fake<IOptionsMonitor<UrlOptions>>();
    var organization = A.Fake<ICurrentOrganization>();
    var clock = A.Fake<IClock>();

    A.CallTo(() => protector.Unprotect("protected:secret")).Returns("s3cret");
    A.CallTo(() => urls.CurrentValue).ReturnsLazily(() => _urls);
    A.CallTo(() => organization.IsKnown).Returns(true);
    A.CallTo(() => organization.Id).Returns(OrganizationId);
    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => _connections.FindAsync(ConnectionId, A<CancellationToken>._)).ReturnsLazily(() => _connection);
    A.CallTo(() => _hooks.ListAsync(ConnectionId, A<CancellationToken>._)).ReturnsLazily(() => (IReadOnlyList<ForgeHook>)[.. _known]);
    A.CallTo(() => _hooks.UpsertAsync(A<ForgeHook>._, A<CancellationToken>._)).Invokes((ForgeHook hook, CancellationToken _) => _saved.Add(hook));
    A.CallTo(() => _repositories.ListAsync(A<CancellationToken>._)).ReturnsLazily(() => (IReadOnlyList<TrackedRepository>)[.. _tracked]);
    A.CallTo(() => _forges.GetHookManager(Forge.AzureDevOps)).Returns(_manager);
    A.CallTo(() => _manager.ScopeKind).Returns(ForgeHookScopeKind.Project);
    Answer(static s => ForgeHookResult.Active(s, $"hook-{s.ExternalId.ToLowerInvariant()}"));

    _handler = new EnsureForgeHooksCommandHandler(_connections, _hooks, _repositories, _forges, protector, urls, organization, clock);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_MissingScope_ShouldCreateTheHook()
  {
    Track("1", "Platform", "api");
    Track("2", "Platform", "web");
    Track("3", "Tools", "cli");
    Track("4", "Legacy", "old").Deactivate(Now.AddDays(-1));
    _tracked.Add(TrackedRepository.Create("other-devops", null, "5", RepositorySlug.Create("contoso", "Other", "repo"), "main", true, "badge", Now));

    await HandleAsync();

    _asked.ShouldNotBeNull().Select(static s => s.ExternalId).ShouldBe(["Platform", "Tools"]);
    _asked.ShouldAllBe(static s => s.Kind == ForgeHookScopeKind.Project && s.HookExternalId == null);
    _callback.ShouldNotBeNull().Address.ShouldBe(new Uri("https://coverage-api.example.com/api/v1/azure-devops/webhooks/contoso-devops"));
    _callback.Secret.ShouldBe("s3cret");
    A.CallTo(() => _manager.EnsureAsync(new ForgeConnectionRef(Forge.AzureDevOps, ConnectionId), A<IReadOnlyList<ForgeHookScope>>._, A<ForgeHookCallback>._, Ct))
     .MustHaveHappenedOnceExactly();
    _saved.Count.ShouldBe(2);
    _saved.ShouldAllBe(static h => h.State == ForgeHookState.Active && h.ConnectionId == ConnectionId && h.LastCheckedAt == Now);
    _saved.Select(static h => h.ScopeExternalId).ShouldBe(["platform", "tools"]);
    _saved.Select(static h => h.ExternalId).ShouldBe(["hook-platform", "hook-tools"]);
  }

  [Fact]
  public async Task Handle_Forbidden_ShouldRecordForbidden()
  {
    Track("1", "Platform", "api");
    Answer(static s => ForgeHookResult.Forbidden(s, "The service account cannot manage the subscriptions."));

    await HandleAsync();

    var hook = _saved.ShouldHaveSingleItem();
    hook.State.ShouldBe(ForgeHookState.Forbidden);
    hook.LastError.ShouldBe("The service account cannot manage the subscriptions.");
  }

  [Fact]
  public async Task Handle_KnownHook_ShouldNameItAndUpdateItsRow()
  {
    Track("1", "Platform", "api");
    Track("2", "Tools", "cli");
    var otherKind = ForgeHook.Create(ConnectionId, ForgeHookScopeKind.Repository, "Platform", Now.AddDays(-1));
    otherKind.MarkActive("repo-hook", Now.AddDays(-1));
    var known = ForgeHook.Create(ConnectionId, ForgeHookScopeKind.Project, "Platform", Now.AddDays(-1));
    known.MarkActive("sub-1", Now.AddDays(-1));
    _known.Add(otherKind);
    _known.Add(known);
    Answer(static s => s.HookExternalId is { } id ? ForgeHookResult.Disabled(s, id, "Too many failures.") : ForgeHookResult.Active(s, "sub-2"));

    await HandleAsync();

    _asked.ShouldNotBeNull().Select(static s => s.HookExternalId).ShouldBe(["sub-1", null]);
    _saved.Count.ShouldBe(2);
    _saved[0].ShouldBeSameAs(known);
    _saved[1].ShouldNotBeSameAs(otherKind);
    _saved[1].ExternalId.ShouldBe("sub-2");
    otherKind.ExternalId.ShouldBe("repo-hook");
    known.State.ShouldBe(ForgeHookState.Disabled);
    known.LastError.ShouldBe("Too many failures.");
    known.LastCheckedAt.ShouldBe(Now);
  }

  [Fact]
  public async Task Handle_RenamedProject_ShouldAskForTheNewNameAndLeaveTheOldRow()
  {
    Track("1", "Core", "api");
    var known = ForgeHook.Create(ConnectionId, ForgeHookScopeKind.Project, "Platform", Now.AddDays(-1));
    known.MarkActive("sub-1", Now.AddDays(-1));
    _known.Add(known);

    await HandleAsync();

    var scope = _asked.ShouldNotBeNull().ShouldHaveSingleItem();
    scope.ExternalId.ShouldBe("Core");
    scope.HookExternalId.ShouldBeNull();
    var saved = _saved.ShouldHaveSingleItem();
    saved.ShouldNotBeSameAs(known);
    saved.ScopeExternalId.ShouldBe("core");
    known.State.ShouldBe(ForgeHookState.Active);
    known.LastCheckedAt.ShouldBe(Now.AddDays(-1));
  }

  [Fact]
  public async Task Handle_HookGone_ShouldRecordMissing()
  {
    Track("1", "Platform", "api");
    Answer(ForgeHookResult.Missing);

    await HandleAsync();

    var hook = _saved.ShouldHaveSingleItem();
    hook.State.ShouldBe(ForgeHookState.Missing);
    hook.ExternalId.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_ConnectionScope_ShouldAskForTheAccountOfTheConnection()
  {
    A.CallTo(() => _manager.ScopeKind).Returns(ForgeHookScopeKind.Connection);

    await HandleAsync();

    var scope = _asked.ShouldNotBeNull().ShouldHaveSingleItem();
    scope.Kind.ShouldBe(ForgeHookScopeKind.Connection);
    scope.ExternalId.ShouldBe(_connection.ExternalAccountId);
    _saved.ShouldHaveSingleItem().ScopeKind.ShouldBe(ForgeHookScopeKind.Connection);
  }

  [Fact]
  public async Task Handle_RepositoryScope_ShouldAskForEachActiveRepositoryOfTheConnection()
  {
    A.CallTo(() => _manager.ScopeKind).Returns(ForgeHookScopeKind.Repository);
    Track("1", "Platform", "api");
    Track("2", "Platform", "web");
    Track("3", "Platform", "old").Deactivate(Now.AddDays(-1));

    await HandleAsync();

    _asked.ShouldNotBeNull().Select(static s => s.ExternalId).ShouldBe(["1", "2"]);
    _asked.ShouldAllBe(static s => s.Kind == ForgeHookScopeKind.Repository);
    _saved.Count.ShouldBe(2);
  }

  [Fact]
  public async Task Handle_ResultForAScopeNotAsked_ShouldBeIgnored()
  {
    Track("1", "Platform", "api");
    Answer(static _ => ForgeHookResult.Active(new ForgeHookScope(ForgeHookScopeKind.Project, "Elsewhere", null), "hook-x"));

    await HandleAsync();

    _saved.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_NoScope_ShouldNotCallTheForge()
  {
    A.CallTo(() => _manager.ScopeKind).Returns(ForgeHookScopeKind.Repository);

    await HandleAsync();

    A.CallTo(() => _manager.EnsureAsync(A<ForgeConnectionRef>._, A<IReadOnlyList<ForgeHookScope>>._, A<ForgeHookCallback>._, A<CancellationToken>._))
     .MustNotHaveHappened();
    _saved.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_ConnectionNotServingTheScope_ShouldDoNothing()
  {
    Track("1", "Platform", "api");
    var suspended = Connection(OrganizationId);
    suspended.Suspend(Now);

    foreach (var connection in new[] { null, Connection(10), suspended })
    {
      A.CallTo(() => _connections.FindAsync(ConnectionId, A<CancellationToken>._)).Returns(connection);

      await HandleAsync();
    }

    A.CallTo(_forges).MustNotHaveHappened();
    A.CallTo(_hooks).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_ForgeWithoutHookManager_ShouldDoNothing()
  {
    Track("1", "Platform", "api");
    A.CallTo(() => _forges.GetHookManager(Forge.AzureDevOps)).Throws(new NotSupportedException("No hook manager."));

    await HandleAsync();

    A.CallTo(_hooks).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_ApiWithoutPublicAddress_ShouldNotCallTheForge()
  {
    Track("1", "Platform", "api");

    foreach (var address in new[] { null, "not an address" })
    {
      _urls.ApiPublicUrl = address;

      await HandleAsync();
    }

    A.CallTo(() => _manager.EnsureAsync(A<ForgeConnectionRef>._, A<IReadOnlyList<ForgeHookScope>>._, A<ForgeHookCallback>._, A<CancellationToken>._))
     .MustNotHaveHappened();
    A.CallTo(_hooks).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_Null_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(async () => await _handler.Handle(null!, Ct));
  }

  private static ForgeConnection Connection(long organizationId) =>
    ForgeConnection.Create(
      ConnectionId,
      organizationId,
      Forge.AzureDevOps,
      ForgeConnectionKind.AzureDevOpsEntraApplication,
      "account-1001",
      new Uri("https://dev.azure.com/contoso"),
      Guid.Parse("11111111-2222-3333-4444-555555555555"),
      null,
      "protected:credential",
      "protected:secret",
      null,
      Now
    );

  private void Answer(Func<ForgeHookScope, ForgeHookResult> answer)
  {
    A.CallTo(() => _manager.EnsureAsync(A<ForgeConnectionRef>._, A<IReadOnlyList<ForgeHookScope>>._, A<ForgeHookCallback>._, A<CancellationToken>._))
     .ReturnsLazily((ForgeConnectionRef _, IReadOnlyList<ForgeHookScope> scopes, ForgeHookCallback callback, CancellationToken _) =>
     {
       _asked = scopes;
       _callback = callback;

       return Task.FromResult<IReadOnlyList<ForgeHookResult>>([.. scopes.Select(answer)]);
     });
  }

  private async Task HandleAsync() => await _handler.Handle(new EnsureForgeHooksCommand(ConnectionId), Ct);

  private TrackedRepository Track(string externalId, string project, string name)
  {
    var repository = TrackedRepository.Create(ConnectionId, null, externalId, RepositorySlug.Create("contoso", project, name), "main", true, "badge", Now.AddDays(-30));

    _tracked.Add(repository);

    return repository;
  }

  #endregion
}
