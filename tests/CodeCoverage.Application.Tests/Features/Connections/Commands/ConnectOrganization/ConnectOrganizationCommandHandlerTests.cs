// ------------------------------------------------------------------------------------------------
// <copyright file="ConnectOrganizationCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Connections;
using CodeCoverage.Application.Features.Connections.Commands.ConnectOrganization;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Features.Connections.Commands.ConnectOrganization;

public sealed class ConnectOrganizationCommandHandlerTests
{
  #region Fields

  private const string AccountId = "8C5B2D1E-0000-4000-8000-000000000001";

  private const string Login = "fabrikam";

  private const string Credential = "plain-credential";

  private const string WebhookSecret = "plain-webhook-secret";

  private const string ServiceAccountId = "service-account-1";

  private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");

  private static readonly Uri BaseAddress = new("https://dev.azure.com/fabrikam/");

  private static readonly DateTimeOffset Now = new(2026, 11, 19, 8, 0, 0, TimeSpan.Zero);

  private static readonly DateTimeOffset ExpiresAt = Now.AddDays(180);

  private readonly IForgeGatewayProvider _forges = A.Fake<IForgeGatewayProvider>();

  private readonly IForgeConnectionVerifier _verifier = A.Fake<IForgeConnectionVerifier>();

  private readonly IForgeConnectionRepository _connections = A.Fake<IForgeConnectionRepository>();

  private readonly ISecretProtector _protector = A.Fake<ISecretProtector>();

  private readonly ITokenGenerator _tokens = A.Fake<ITokenGenerator>();

  private readonly ICurrentUser _user = A.Fake<ICurrentUser>();

  private readonly IClock _clock = A.Fake<IClock>();

  private readonly IAuditLog _audit = A.Fake<IAuditLog>();

  private readonly List<ForgeConnection> _savedConnections = [];

  private readonly List<JobRequest> _savedJobs = [];

  private long _nextOrganizationId = 41;

  #endregion

  #region Ctors

  public ConnectOrganizationCommandHandlerTests()
  {
    A.CallTo(() => _clock.UtcNow).Returns(Now);
    A.CallTo(() => _user.IsSystem).Returns(false);
    A.CallTo(() => _user.Login).Returns("root-admin");
    A.CallTo(() => _user.UserId).Returns(7L);
    A.CallTo(() => _forges.GetVerifier(A<Forge>._)).Returns(_verifier);
    A.CallTo(() => _protector.Protect(A<string>._)).ReturnsLazily((string plain) => $"protected({plain})");
    A.CallTo(() => _tokens.NewWebhookSecret()).Returns(WebhookSecret);
    Verifies(new ForgeAccountInfo(AccountId, Login, ServiceAccountId, ExpiresAt));

    A.CallTo(
       () => _connections.AddWithOrganizationAsync(
         A<Organization>._,
         A<Func<Organization, ForgeConnection>>._,
         A<Func<Organization, IReadOnlyList<JobRequest>>>._,
         A<CancellationToken>._
       )
     )
     .ReturnsLazily((Organization organization, Func<Organization, ForgeConnection> connection, Func<Organization, IReadOnlyList<JobRequest>> jobs, CancellationToken _) =>
       {
         typeof(Organization).GetProperty(nameof(Organization.Id))!.SetValue(organization, ++_nextOrganizationId);
         var saved = connection(organization);
         _savedConnections.Add(saved);
         _savedJobs.AddRange(jobs(organization));

         return Task.FromResult(saved);
       });
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_Verified_ShouldCreateOrganizationAndConnection()
  {
    var organization = await Handler().Handle(Command(), Ct);

    organization.Forge.ShouldBe(Forge.AzureDevOps);
    organization.Login.ShouldBe(Login);
    organization.ExternalAccountId.ShouldBe(AccountId.ToLowerInvariant());
    organization.State.ShouldBe(OrganizationState.Active);
    organization.AdmittedBy.ShouldBe("root-admin");

    var connection = _savedConnections.ShouldHaveSingleItem();
    connection.OrganizationId.ShouldBe(organization.Id);
    connection.Forge.ShouldBe(Forge.AzureDevOps);
    connection.Kind.ShouldBe(ForgeConnectionKind.AzureDevOpsEntraApplication);
    connection.ExternalAccountId.ShouldBe(AccountId.ToLowerInvariant());
    connection.BaseAddress.ShouldBe(BaseAddress);
    connection.TenantId.ShouldBe(TenantId);
    connection.Status.ShouldBe(ForgeConnectionStatus.Active);
    connection.ConnectedAt.ShouldBe(Now);
    ForgeConnection.IsWellFormedId(connection.Id).ShouldBeTrue();
    connection.Id.ShouldStartWith("azure-devops-");
    A.CallTo(() => _forges.GetVerifier(Forge.AzureDevOps)).MustHaveHappenedOnceExactly();
    A.CallTo(
       () => _verifier.VerifyAsync(
         A<ForgeConnectionRequest>.That.Matches(r => r.Forge == Forge.AzureDevOps
                                                    && r.Kind == ForgeConnectionKind.AzureDevOpsEntraApplication
                                                    && r.Login == Login
                                                    && r.TenantId == TenantId
                                                    && r.Credential == Credential),
         A<CancellationToken>._
       )
     )
     .MustHaveHappenedOnceExactly();
    A.CallTo(
       () => _audit.RecordAsync(
         "Connection.Connect",
         $"AzureDevOps/{Login}",
         A<IReadOnlyDictionary<string, string>?>.That.Matches(d => d != null && d["ConnectionId"] == connection.Id),
         A<CancellationToken>._
       )
     )
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_Verified_ShouldStoreServiceAccountAndExpiry()
  {
    await Handler().Handle(Command(), Ct);

    var connection = _savedConnections.ShouldHaveSingleItem();
    connection.ServiceAccountExternalId.ShouldBe(ServiceAccountId);
    connection.ExpiresAt.ShouldBe(ExpiresAt);
  }

  [Fact]
  public async Task Handle_Verified_ShouldStoreOnlyProtectedSecrets()
  {
    await Handler().Handle(Command(), Ct);

    var connection = _savedConnections.ShouldHaveSingleItem();
    connection.CredentialProtected.ShouldBe($"protected({Credential})");
    connection.WebhookSecretProtected.ShouldBe($"protected({WebhookSecret})");
    connection.WebhookSecretPreviousProtected.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_NoCredential_ShouldStoreAnEmptyOneProtected()
  {
    await Handler().Handle(Command() with { Credential = null }, Ct);

    _savedConnections.ShouldHaveSingleItem().CredentialProtected.ShouldBe("protected()");
    A.CallTo(() => _verifier.VerifyAsync(A<ForgeConnectionRequest>.That.Matches(r => r.Credential == null), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_VerifierRefuses_ShouldThrowForgeAccessDenied()
  {
    A.CallTo(() => _verifier.VerifyAsync(A<ForgeConnectionRequest>._, A<CancellationToken>._))
     .Returns(ForgeConnectionCheck.Refused("TF400813: the identity is not authorized."));

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(async () => await Handler().Handle(Command(), Ct));

    exception.ErrorCode.ShouldBe(ConnectionErrors.ForgeAccessDenied);
    exception.Message.ShouldContain("TF400813");
    exception.Message.ShouldNotContain(Credential);
    A.CallTo(_connections).MustNotHaveHappened();
    A.CallTo(_protector).MustNotHaveHappened();
    A.CallTo(_audit).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_SameLoginOnAnotherForge_ShouldCreateASecondOrganization()
  {
    var azureDevOps = await Handler().Handle(Command(), Ct);
    var bitbucket = await Handler().Handle(Command() with { Forge = Forge.Bitbucket }, Ct);

    azureDevOps.Forge.ShouldBe(Forge.AzureDevOps);
    bitbucket.Forge.ShouldBe(Forge.Bitbucket);
    bitbucket.Login.ShouldBe(azureDevOps.Login);
    bitbucket.Id.ShouldNotBe(azureDevOps.Id);
    _savedConnections.Count.ShouldBe(2);
    _savedConnections[1].Forge.ShouldBe(Forge.Bitbucket);
    _savedConnections[1].OrganizationId.ShouldBe(bitbucket.Id);
    _savedConnections[1].Id.ShouldStartWith("bitbucket-");
    _savedConnections[1].Id.ShouldNotBe(_savedConnections[0].Id);
    A.CallTo(() => _forges.GetVerifier(Forge.Bitbucket)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_Connected_ShouldQueueSyncAndMemberships()
  {
    var organization = await Handler().Handle(Command(), Ct);

    var connection = _savedConnections.ShouldHaveSingleItem();
    var refresh = RefreshUserMembershipsJobs.Create(7);
    _savedJobs.Count.ShouldBe(2);

    var sync = _savedJobs[0];
    sync.Type.ShouldBe(JobTypes.SyncConnectionRepositories);
    sync.IsInstance.ShouldBeFalse();
    sync.OrganizationId.ShouldBe(organization.Id);
    sync.PayloadJson.ShouldBe(RepositoryReconciliationJobs.ConnectionPayload(Forge.AzureDevOps, connection.Id));
    sync.LockKey.ShouldBe(RepositoryReconciliationJobs.ConnectionKey(connection.Id));
    sync.DedupKey.ShouldBe($"sync:{RepositoryReconciliationJobs.ConnectionKey(connection.Id)}");

    var memberships = _savedJobs[1];
    memberships.Type.ShouldBe(JobTypes.RefreshUserMemberships);
    memberships.IsInstance.ShouldBeTrue();
    memberships.PayloadJson.ShouldBe(refresh.PayloadJson);
    memberships.DedupKey.ShouldBe(refresh.DedupKey);
  }

  [Fact]
  public async Task Handle_System_ShouldQueueNoRefreshOfMemberships()
  {
    A.CallTo(() => _user.IsSystem).Returns(true);
    A.CallTo(() => _user.Login).Returns(null);
    A.CallTo(() => _user.UserId).Returns(null);

    var organization = await Handler().Handle(Command(), Ct);

    organization.AdmittedBy.ShouldBe(AuditEntry.SystemActor);
    _savedJobs.ShouldHaveSingleItem().Type.ShouldBe(JobTypes.SyncConnectionRepositories);
  }

  [Fact]
  public async Task Handle_RepositoryConflict_ShouldPropagateWithoutAudit()
  {
    A.CallTo(
       () => _connections.AddWithOrganizationAsync(
         A<Organization>._,
         A<Func<Organization, ForgeConnection>>._,
         A<Func<Organization, IReadOnlyList<JobRequest>>>._,
         A<CancellationToken>._
       )
     )
     .ThrowsAsync(new ConflictException("General.ConcurrentChange", "An organization of that account exists."));

    await Should.ThrowAsync<ConflictException>(async () => await Handler().Handle(Command(), Ct));

    A.CallTo(_audit).MustNotHaveHappened();
  }

  [Fact]
  public void Command_ToString_ShouldNotShowTheCredential()
  {
    var text = Command().ToString();

    text.ShouldContain(Login);
    text.ShouldNotContain(Credential);
  }

  private static ConnectOrganizationCommand Command() =>
    new(Forge.AzureDevOps, Login, ForgeConnectionKind.AzureDevOpsEntraApplication, TenantId, Credential);

  private ConnectOrganizationCommandHandler Handler() => new(_forges, _connections, _protector, _tokens, _user, _clock, _audit);

  private void Verifies(ForgeAccountInfo account) =>
    A.CallTo(() => _verifier.VerifyAsync(A<ForgeConnectionRequest>._, A<CancellationToken>._)).Returns(ForgeConnectionCheck.Verified(account, BaseAddress));

  #endregion
}
