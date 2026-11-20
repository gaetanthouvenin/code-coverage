// ------------------------------------------------------------------------------------------------
// <copyright file="RotateConnectionSecretCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Connections;
using CodeCoverage.Application.Features.Connections.Commands.RotateConnectionSecret;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Features.Connections.Commands.RotateConnectionSecret;

public sealed class RotateConnectionSecretCommandHandlerTests
{
  #region Fields

  private const long OrganizationId = 42;

  private const string ConnectionId = "azure-devops-1";

  private const string AccountId = "8c5b2d1e-0000-4000-8000-000000000001";

  private const string Login = "fabrikam";

  private const string NewCredential = "plain-new-credential";

  private const string NewWebhookSecret = "plain-new-webhook-secret";

  private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");

  private static readonly Uri BaseAddress = new("https://dev.azure.com/fabrikam/");

  private static readonly DateTimeOffset Now = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);

  private readonly IForgeConnectionRepository _connections = A.Fake<IForgeConnectionRepository>();

  private readonly IForgeGatewayProvider _forges = A.Fake<IForgeGatewayProvider>();

  private readonly IForgeConnectionVerifier _verifier = A.Fake<IForgeConnectionVerifier>();

  private readonly ISecretProtector _protector = A.Fake<ISecretProtector>();

  private readonly ITokenGenerator _tokens = A.Fake<ITokenGenerator>();

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly IAuditLog _audit = A.Fake<IAuditLog>();

  private readonly ForgeConnection _connection = Connection(OrganizationId);

  #endregion

  #region Ctors

  public RotateConnectionSecretCommandHandlerTests()
  {
    A.CallTo(() => _organization.Id).Returns(OrganizationId);
    A.CallTo(() => _connections.FindAsync(ConnectionId, A<CancellationToken>._)).Returns(_connection);
    A.CallTo(() => _forges.GetVerifier(A<Forge>._)).Returns(_verifier);
    A.CallTo(() => _protector.Protect(A<string>._)).ReturnsLazily((string plain) => $"protected({plain})");
    A.CallTo(() => _tokens.NewWebhookSecret()).Returns(NewWebhookSecret);
    Verifies(new ForgeAccountInfo(AccountId.ToUpperInvariant(), Login, "service-account-2", Now.AddDays(90)));
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_WebhookSecret_ShouldKeepThePreviousOne()
  {
    await Handler().Handle(Command(ConnectionSecretKind.WebhookSecret, null), Ct);

    _connection.WebhookSecretProtected.ShouldBe($"protected({NewWebhookSecret})");
    _connection.WebhookSecretPreviousProtected.ShouldBe("protected(old-secret)");
    _connection.CredentialProtected.ShouldBe("protected(old-credential)");
    A.CallTo(() => _connections.UpdateAsync(_connection, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(_forges).MustNotHaveHappened();
    A.CallTo(
       () => _audit.RecordAsync(
         "Connection.RotateWebhookSecret",
         $"AzureDevOps/{Login}",
         A<IReadOnlyDictionary<string, string>?>.That.Matches(d => d != null && d.Count == 1 && d["ConnectionId"] == ConnectionId),
         A<CancellationToken>._
       )
     )
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_Credential_ShouldStoreTheVerifiedOne()
  {
    await Handler().Handle(Command(ConnectionSecretKind.Credential, NewCredential), Ct);

    _connection.CredentialProtected.ShouldBe($"protected({NewCredential})");
    _connection.ServiceAccountExternalId.ShouldBe("service-account-2");
    _connection.ExpiresAt.ShouldBe(Now.AddDays(90));
    _connection.WebhookSecretProtected.ShouldBe("protected(old-secret)");
    _connection.WebhookSecretPreviousProtected.ShouldBeNull();
    A.CallTo(() => _forges.GetVerifier(Forge.AzureDevOps)).MustHaveHappenedOnceExactly();
    A.CallTo(
       () => _verifier.VerifyAsync(
         A<ForgeConnectionRequest>.That.Matches(r => r.Forge == Forge.AzureDevOps
                                                    && r.Kind == ForgeConnectionKind.AzureDevOpsEntraApplication
                                                    && r.Login == Login
                                                    && r.TenantId == TenantId
                                                    && r.Credential == NewCredential),
         A<CancellationToken>._
       )
     )
     .MustHaveHappenedOnceExactly();
    A.CallTo(() => _connections.UpdateAsync(_connection, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(
       () => _audit.RecordAsync(
         "Connection.RotateCredential",
         $"AzureDevOps/{Login}",
         A<IReadOnlyDictionary<string, string>?>.That.Matches(d => d != null && !d.Values.Any(v => v.Contains(NewCredential))),
         A<CancellationToken>._
       )
     )
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_CredentialOfAFailingConnection_ShouldClearTheFailure()
  {
    _connection.RecordCheck(Now, "TF400813: the identity is not authorized.", TimeSpan.FromDays(14));

    await Handler().Handle(Command(ConnectionSecretKind.Credential, NewCredential), Ct);

    _connection.Status.ShouldBe(ForgeConnectionStatus.Active);
    _connection.LastError.ShouldBeNull();
    A.CallTo(() => _connections.UpdateAsync(_connection, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_NoCredential_ShouldAskTheForgeWithAnEmptyOne()
  {
    await Handler().Handle(Command(ConnectionSecretKind.Credential, null), Ct);

    A.CallTo(() => _verifier.VerifyAsync(A<ForgeConnectionRequest>.That.Matches(r => r.Credential == string.Empty), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    _connection.CredentialProtected.ShouldBe("protected()");
  }

  [Fact]
  public async Task Handle_CredentialRefused_ShouldKeepTheOldOne()
  {
    A.CallTo(() => _verifier.VerifyAsync(A<ForgeConnectionRequest>._, A<CancellationToken>._))
     .Returns(ForgeConnectionCheck.Refused("TF400813: the identity is not authorized."));

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(
                      async () => await Handler().Handle(Command(ConnectionSecretKind.Credential, NewCredential), Ct)
                    );

    exception.ErrorCode.ShouldBe(ConnectionErrors.CredentialRefused);
    exception.Message.ShouldContain("TF400813");
    exception.Message.ShouldNotContain(NewCredential);
    _connection.CredentialProtected.ShouldBe("protected(old-credential)");
    A.CallTo(() => _connections.UpdateAsync(A<ForgeConnection>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(_protector).MustNotHaveHappened();
    A.CallTo(_audit).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_CredentialOfAnotherAccount_ShouldKeepTheOldOne()
  {
    Verifies(new ForgeAccountInfo("another-account", "contoso"));

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(
                      async () => await Handler().Handle(Command(ConnectionSecretKind.Credential, NewCredential), Ct)
                    );

    exception.ErrorCode.ShouldBe(ConnectionErrors.CredentialRefused);
    _connection.CredentialProtected.ShouldBe("protected(old-credential)");
    A.CallTo(() => _connections.UpdateAsync(A<ForgeConnection>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(_audit).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_OtherOrganizationConnection_ShouldThrowNotFound()
  {
    A.CallTo(() => _connections.FindAsync(ConnectionId, A<CancellationToken>._)).Returns(Connection(OrganizationId + 1));

    var exception = await Should.ThrowAsync<NotFoundException>(
                      async () => await Handler().Handle(Command(ConnectionSecretKind.WebhookSecret, null), Ct)
                    );

    exception.ErrorCode.ShouldBe(ConnectionErrors.NotFound);
    A.CallTo(() => _connections.UpdateAsync(A<ForgeConnection>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(_tokens).MustNotHaveHappened();
    A.CallTo(_audit).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_UnknownConnection_ShouldThrowNotFound()
  {
    A.CallTo(() => _connections.FindAsync(ConnectionId, A<CancellationToken>._)).Returns((ForgeConnection?)null);

    var exception = await Should.ThrowAsync<NotFoundException>(
                      async () => await Handler().Handle(Command(ConnectionSecretKind.Credential, NewCredential), Ct)
                    );

    exception.ErrorCode.ShouldBe(ConnectionErrors.NotFound);
    A.CallTo(_forges).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_NullCommand_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(async () => await Handler().Handle(null!, Ct));
  }

  [Fact]
  public void Command_ToString_ShouldNotShowTheCredential()
  {
    var command = Command(ConnectionSecretKind.Credential, NewCredential);

    var text = command.ToString();

    text.ShouldContain(ConnectionId);
    text.ShouldNotContain(NewCredential);
    command.RequiredRole.ShouldBe(OrganizationRole.Admin);
  }

  private static RotateConnectionSecretCommand Command(ConnectionSecretKind kind, string? credential) =>
    new(Forge.AzureDevOps, Login, ConnectionId, kind, credential);

  private static ForgeConnection Connection(long organizationId) =>
    ForgeConnection.Create(
      ConnectionId,
      organizationId,
      Forge.AzureDevOps,
      ForgeConnectionKind.AzureDevOpsEntraApplication,
      AccountId,
      BaseAddress,
      TenantId,
      "service-account-1",
      "protected(old-credential)",
      "protected(old-secret)",
      Now.AddDays(10),
      Now.AddDays(-30)
    );

  private RotateConnectionSecretCommandHandler Handler() => new(_connections, _forges, _protector, _tokens, _organization, _audit);

  private void Verifies(ForgeAccountInfo account) =>
    A.CallTo(() => _verifier.VerifyAsync(A<ForgeConnectionRequest>._, A<CancellationToken>._)).Returns(ForgeConnectionCheck.Verified(account, BaseAddress));

  #endregion
}
