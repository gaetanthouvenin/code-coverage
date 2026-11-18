// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeConnectionCredentialReaderTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Caching.Memory;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Forges;

namespace CodeCoverage.Infrastructure.Tests.Forges;

public sealed class ForgeConnectionCredentialReaderTests : IDisposable
{
  #region Fields

  private const string ConnectionId = "contoso";

  private const long OrganizationId = 7;

  private const long OtherOrganizationId = 8;

  private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

  private static readonly Guid TenantId = new("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

  private readonly IForgeConnectionRepository _connections = A.Fake<IForgeConnectionRepository>();

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly ISecretProtector _protector = A.Fake<ISecretProtector>();

  private readonly MemoryCache _memory = new(new MemoryCacheOptions());

  #endregion

  #region Ctors

  public ForgeConnectionCredentialReaderTests()
  {
    A.CallTo(() => _organization.IsKnown).Returns(true);
    A.CallTo(() => _organization.Id).Returns(OrganizationId);
    A.CallTo(() => _protector.Unprotect(A<string>._)).ReturnsLazily((string value) => value["protected:".Length..]);
  }

  #endregion

  #region Properties

  private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task ReadAsync_ConnectionOfTheOrganization_ShouldDecryptTheCredential()
  {
    Given(Connection(OrganizationId));

    var credential = await Reader().ReadAsync(ConnectionId, Cancellation);

    credential.ConnectionId.ShouldBe(ConnectionId);
    credential.Forge.ShouldBe(Forge.AzureDevOps);
    credential.TenantId.ShouldBe(TenantId);
    credential.Credential.ShouldBe("entra-application");
  }

  [Fact]
  public async Task ReadAsync_Twice_ShouldDecryptOnce()
  {
    Given(Connection(OrganizationId));

    await Reader().ReadAsync(ConnectionId, Cancellation);
    await Reader().ReadAsync(ConnectionId, Cancellation);

    A.CallTo(() => _protector.Unprotect(A<string>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _connections.FindAsync(ConnectionId, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
  }

  [Fact]
  public async Task ReadAsync_OtherOrganizationConnection_ShouldThrowUnavailable()
  {
    Given(Connection(OtherOrganizationId));

    var exception = await Should.ThrowAsync<BadGatewayException>(() => Reader().ReadAsync(ConnectionId, Cancellation));

    exception.ErrorCode.ShouldBe(ForgeErrors.Unavailable);
    A.CallTo(() => _protector.Unprotect(A<string>._)).MustNotHaveHappened();
  }

  // The credential is in the cache of the other organization: the key and the reading of the connection both keep it out.
  [Fact]
  public async Task ReadAsync_OtherOrganizationAfterACachedRead_ShouldThrowUnavailable()
  {
    Given(Connection(OrganizationId));
    await Reader().ReadAsync(ConnectionId, Cancellation);
    A.CallTo(() => _organization.Id).Returns(OtherOrganizationId);

    var exception = await Should.ThrowAsync<BadGatewayException>(() => Reader().ReadAsync(ConnectionId, Cancellation));

    exception.ErrorCode.ShouldBe(ForgeErrors.Unavailable);
  }

  [Fact]
  public async Task ReadAsync_SuspendedConnection_ShouldThrowUnavailable()
  {
    var connection = Connection(OrganizationId);
    Given(connection);
    await Reader().ReadAsync(ConnectionId, Cancellation);
    connection.Suspend(Now);

    var exception = await Should.ThrowAsync<BadGatewayException>(() => Reader().ReadAsync(ConnectionId, Cancellation));

    exception.ErrorCode.ShouldBe(ForgeErrors.Unavailable);
  }

  [Fact]
  public async Task ReadAsync_UnknownConnection_ShouldThrowUnavailable()
  {
    A.CallTo(() => _connections.FindAsync("unknown", A<CancellationToken>._)).Returns((ForgeConnection?)null);

    var exception = await Should.ThrowAsync<BadGatewayException>(() => Reader().ReadAsync("unknown", Cancellation));

    exception.ErrorCode.ShouldBe(ForgeErrors.Unavailable);
  }

  [Fact]
  public async Task ReadAsync_OutsideAnOrganization_ShouldThrowUnavailable()
  {
    Given(Connection(OrganizationId));
    A.CallTo(() => _organization.IsKnown).Returns(false);

    var exception = await Should.ThrowAsync<BadGatewayException>(() => Reader().ReadAsync(ConnectionId, Cancellation));

    exception.ErrorCode.ShouldBe(ForgeErrors.Unavailable);
  }

  [Fact]
  public async Task ListAsync_ConnectionsOfTheOrganization_ShouldKeepTheUsableOnesOfTheForge()
  {
    var usable = Connection(OrganizationId);
    var suspended = Connection(OrganizationId, "contoso-old");
    suspended.Suspend(Now);
    var other = Connection(OrganizationId, "contoso-bitbucket", Forge.Bitbucket);
    A.CallTo(() => _connections.ListForOrganizationAsync(OrganizationId, A<CancellationToken>._)).Returns([usable, suspended, other]);

    var credentials = await Reader().ListAsync(Forge.AzureDevOps, Cancellation);

    var credential = credentials.ShouldHaveSingleItem();
    credential.ConnectionId.ShouldBe(ConnectionId);
    credential.ExternalAccountId.ShouldBe("0f6b6c4e-3d1a-4a52-9b0e-3c2d1e0f9a8b");
    credential.Credential.ShouldBe("entra-application");
  }

  [Fact]
  public async Task ListAsync_OutsideAnOrganization_ShouldBeEmpty()
  {
    A.CallTo(() => _organization.IsKnown).Returns(false);

    var credentials = await Reader().ListAsync(Forge.AzureDevOps, Cancellation);

    credentials.ShouldBeEmpty();
    A.CallTo(() => _connections.ListForOrganizationAsync(A<long>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  private static ForgeConnection Connection(long organizationId, string id = ConnectionId, Forge forge = Forge.AzureDevOps)
  {
    return ForgeConnection.Create(
      id,
      organizationId,
      forge,
      ForgeConnectionKind.AzureDevOpsEntraApplication,
      "0f6b6c4e-3d1a-4a52-9b0e-3c2d1e0f9a8b",
      new Uri("https://dev.azure.com/contoso/"),
      TenantId,
      null,
      "protected:entra-application",
      "protected:webhook",
      null,
      Now
    );
  }

  private void Given(ForgeConnection connection)
  {
    A.CallTo(() => _connections.FindAsync(ConnectionId, A<CancellationToken>._)).Returns(connection);
  }

  private ForgeConnectionCredentialReader Reader()
  {
    return new ForgeConnectionCredentialReader(_connections, _organization, _protector, _memory);
  }

  #endregion

  #region Implementation of IDisposable

  public void Dispose()
  {
    _memory.Dispose();
  }

  #endregion
}
