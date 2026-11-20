// ------------------------------------------------------------------------------------------------
// <copyright file="CheckConnectionCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Cryptography;

using CodeCoverage.Application.Features.Connections.Commands.CheckConnection;
using CodeCoverage.Application.Tests.Support;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Features.Connections.Commands.CheckConnection;

public sealed class CheckConnectionCommandHandlerTests
{
  #region Fields

  private const string ConnectionId = "fabrikam-devops";

  private const string AccountId = "8c5b2d1e-0000-4000-8000-000000000001";

  private const string Login = "fabrikam";

  private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");

  private static readonly Uri BaseAddress = new("https://dev.azure.com/fabrikam/");

  private static readonly DateTimeOffset Now = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);

  private readonly IForgeConnectionRepository _connections = A.Fake<IForgeConnectionRepository>();

  private readonly IForgeGatewayProvider _forges = A.Fake<IForgeGatewayProvider>();

  private readonly IForgeConnectionVerifier _verifier = A.Fake<IForgeConnectionVerifier>();

  private readonly ISecretProtector _protector = A.Fake<ISecretProtector>();

  private readonly InMemoryOrganizationDirectory _directory = new(Now);

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly IClock _clock = A.Fake<IClock>();

  private readonly Organization _current;

  #endregion

  #region Ctors

  public CheckConnectionCommandHandlerTests()
  {
    _current = _directory.Add(AccountId, Login, admitted: true);
    _directory.Current = _current;

    A.CallTo(() => _organization.Id).Returns(_current.Id);
    A.CallTo(() => _forges.GetVerifier(A<Forge>._)).Returns(_verifier);
    A.CallTo(() => _protector.Unprotect(A<string>._)).ReturnsLazily((string value) => $"plain({value})");
    A.CallTo(() => _clock.UtcNow).Returns(Now);
    Verifies(AccountId.ToUpperInvariant());
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_VerifierRefuses_ShouldMarkFailing()
  {
    var connection = Stored(_current.Id);
    A.CallTo(() => _verifier.VerifyAsync(A<ForgeConnectionRequest>._, A<CancellationToken>._))
     .Returns(ForgeConnectionCheck.Refused("the forge refuses the credential."));

    await Handler().Handle(new CheckConnectionCommand(ConnectionId), Ct);

    connection.Status.ShouldBe(ForgeConnectionStatus.Failing);
    connection.LastError.ShouldBe("the forge refuses the credential.");
    connection.LastCheckedAt.ShouldBe(Now);
    connection.SuspendedAt.ShouldBeNull();
    A.CallTo(() => _connections.UpdateAsync(connection, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_ExpiresSoon_ShouldMarkExpiring()
  {
    var connection = Stored(_current.Id, Now.AddDays(10));

    await Handler().Handle(new CheckConnectionCommand(ConnectionId), Ct);

    connection.Status.ShouldBe(ForgeConnectionStatus.Expiring);
    connection.LastError.ShouldBeNull();
    connection.LastCheckedAt.ShouldBe(Now);
    A.CallTo(() => _connections.UpdateAsync(connection, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_ForgeReportsAnExpirySoon_ShouldMarkExpiring()
  {
    var connection = Stored(_current.Id);
    Verifies(AccountId, Now.AddDays(10));

    await Handler().Handle(new CheckConnectionCommand(ConnectionId), Ct);

    connection.Status.ShouldBe(ForgeConnectionStatus.Expiring);
    connection.ExpiresAt.ShouldBe(Now.AddDays(10));
    connection.LastError.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_ForgeExtendsTheExpiry_ShouldMarkActive()
  {
    var connection = Stored(_current.Id, Now.AddDays(10));
    Verifies(AccountId, Now.AddDays(365));

    await Handler().Handle(new CheckConnectionCommand(ConnectionId), Ct);

    connection.Status.ShouldBe(ForgeConnectionStatus.Active);
    connection.ExpiresAt.ShouldBe(Now.AddDays(365));
  }

  [Fact]
  public async Task Handle_AnotherAccountReportsAnExpiry_ShouldKeepTheKnownExpiry()
  {
    var connection = Stored(_current.Id, Now.AddDays(90));
    Verifies("another-account", Now.AddDays(5));

    await Handler().Handle(new CheckConnectionCommand(ConnectionId), Ct);

    connection.Status.ShouldBe(ForgeConnectionStatus.Failing);
    connection.ExpiresAt.ShouldBe(Now.AddDays(90));
  }

  [Fact]
  public async Task Handle_ExpiresLater_ShouldMarkActive()
  {
    var connection = Stored(_current.Id, Now + CheckConnectionCommandHandler.ExpiryWarning + TimeSpan.FromDays(1));
    connection.RecordCheck(Now.AddDays(-1), "revoked", TimeSpan.Zero);

    await Handler().Handle(new CheckConnectionCommand(ConnectionId), Ct);

    connection.Status.ShouldBe(ForgeConnectionStatus.Active);
    connection.LastError.ShouldBeNull();
    A.CallTo(() => _forges.GetVerifier(Forge.AzureDevOps)).MustHaveHappenedOnceExactly();
    A.CallTo(
       () => _verifier.VerifyAsync(
         A<ForgeConnectionRequest>.That.Matches(r => r.Forge == Forge.AzureDevOps
                                                     && r.Kind == ForgeConnectionKind.AzureDevOpsEntraApplication
                                                     && r.Login == Login
                                                     && r.TenantId == TenantId
                                                     && r.Credential == "plain(protected:credential)"),
         A<CancellationToken>._
       )
     )
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_AnotherAccount_ShouldMarkFailing()
  {
    var connection = Stored(_current.Id);
    Verifies("another-account");

    await Handler().Handle(new CheckConnectionCommand(ConnectionId), Ct);

    connection.Status.ShouldBe(ForgeConnectionStatus.Failing);
    connection.LastError.ShouldBe("The credential reaches another account.");
  }

  [Fact]
  public async Task Handle_ForgeUnreachable_ShouldMarkFailingWithoutTheMessage()
  {
    var connection = Stored(_current.Id);
    A.CallTo(() => _verifier.VerifyAsync(A<ForgeConnectionRequest>._, A<CancellationToken>._))
     .ThrowsAsync(new HttpRequestException("secret details"));

    await Handler().Handle(new CheckConnectionCommand(ConnectionId), Ct);

    connection.Status.ShouldBe(ForgeConnectionStatus.Failing);
    connection.LastError.ShouldBe("The check could not reach the forge (HttpRequestException).");
    A.CallTo(() => _connections.UpdateAsync(connection, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_CredentialUnreadable_ShouldMarkFailing()
  {
    var connection = Stored(_current.Id);
    A.CallTo(() => _protector.Unprotect(A<string>._)).Throws(new CryptographicException("key ring lost"));

    await Handler().Handle(new CheckConnectionCommand(ConnectionId), Ct);

    connection.Status.ShouldBe(ForgeConnectionStatus.Failing);
    connection.LastError.ShouldBe("The check could not reach the forge (CryptographicException).");
    A.CallTo(_verifier).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_Cancelled_ShouldRecordNothing()
  {
    var connection = Stored(_current.Id);
    A.CallTo(() => _verifier.VerifyAsync(A<ForgeConnectionRequest>._, A<CancellationToken>._))
     .ThrowsAsync(new OperationCanceledException());

    await Should.ThrowAsync<OperationCanceledException>(async () => await Handler().Handle(new CheckConnectionCommand(ConnectionId), Ct));

    connection.LastCheckedAt.ShouldBeNull();
    A.CallTo(() => _connections.UpdateAsync(A<ForgeConnection>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_UnknownConnection_ShouldCheckNothing()
  {
    await Handler().Handle(new CheckConnectionCommand(ConnectionId), Ct);

    A.CallTo(_forges).MustNotHaveHappened();
    A.CallTo(() => _connections.UpdateAsync(A<ForgeConnection>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_ConnectionOfAnotherOrganization_ShouldCheckNothing()
  {
    var connection = Stored(_current.Id + 1);

    await Handler().Handle(new CheckConnectionCommand(ConnectionId), Ct);

    connection.LastCheckedAt.ShouldBeNull();
    A.CallTo(_forges).MustNotHaveHappened();
    A.CallTo(() => _connections.UpdateAsync(A<ForgeConnection>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_SuspendedConnection_ShouldCheckNothing()
  {
    var connection = Stored(_current.Id);
    connection.Suspend(Now.AddDays(-1));

    await Handler().Handle(new CheckConnectionCommand(ConnectionId), Ct);

    connection.Status.ShouldBe(ForgeConnectionStatus.Suspended);
    connection.LastCheckedAt.ShouldBeNull();
    A.CallTo(_forges).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_OrganizationGone_ShouldCheckNothing()
  {
    var connection = Stored(_current.Id);
    _directory.Current = null;

    await Handler().Handle(new CheckConnectionCommand(ConnectionId), Ct);

    connection.LastCheckedAt.ShouldBeNull();
    A.CallTo(_forges).MustNotHaveHappened();
    A.CallTo(() => _connections.UpdateAsync(A<ForgeConnection>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_NullCommand_ShouldThrowArgumentNullException()
  {
    await Should.ThrowAsync<ArgumentNullException>(async () => await Handler().Handle(null!, Ct));
  }

  private CheckConnectionCommandHandler Handler() => new(_connections, _forges, _protector, _directory, _organization, _clock);

  private ForgeConnection Stored(long organizationId, DateTimeOffset? expiresAt = null)
  {
    var connection = ForgeConnection.Create(
      ConnectionId,
      organizationId,
      Forge.AzureDevOps,
      ForgeConnectionKind.AzureDevOpsEntraApplication,
      AccountId,
      BaseAddress,
      TenantId,
      "service-account-1",
      "protected:credential",
      "protected:secret",
      expiresAt,
      Now.AddDays(-30)
    );

    A.CallTo(() => _connections.FindAsync(ConnectionId, A<CancellationToken>._)).Returns(connection);

    return connection;
  }

  private void Verifies(string accountId, DateTimeOffset? expiresAt = null) =>
    A.CallTo(() => _verifier.VerifyAsync(A<ForgeConnectionRequest>._, A<CancellationToken>._))
     .Returns(ForgeConnectionCheck.Verified(new ForgeAccountInfo(accountId, Login, "service-account-1", expiresAt), BaseAddress));

  #endregion
}
