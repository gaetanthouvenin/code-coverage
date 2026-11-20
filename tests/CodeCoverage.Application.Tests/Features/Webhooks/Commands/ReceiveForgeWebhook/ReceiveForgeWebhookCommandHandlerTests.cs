// ------------------------------------------------------------------------------------------------
// <copyright file="ReceiveForgeWebhookCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Cryptography;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Webhooks;
using CodeCoverage.Application.Features.Webhooks.Commands.ReceiveForgeWebhook;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Contracts.Webhooks;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Features.Webhooks.Commands.ReceiveForgeWebhook;

public sealed class ReceiveForgeWebhookCommandHandlerTests
{
  #region Fields

  private const string ConnectionId = "contoso-devops";

  private const string DeliveryId = "d-1";

  private const long OrganizationId = 9;

  private static readonly DateTimeOffset Now = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);

  private static readonly byte[] Body = """{"eventType":"git.push"}"""u8.ToArray();

  private readonly IForgeConnectionRepository _connections = A.Fake<IForgeConnectionRepository>();

  private readonly IForgeGatewayProvider _forges = A.Fake<IForgeGatewayProvider>();

  private readonly IForgeWebhookValidator _validator = A.Fake<IForgeWebhookValidator>();

  private readonly ISecretProtector _protector = A.Fake<ISecretProtector>();

  private readonly IWebhookDeliveryRepository _deliveries = A.Fake<IWebhookDeliveryRepository>();

  private readonly IClock _clock = A.Fake<IClock>();

  private readonly List<(ForgeDelivery Delivery, JobRequest Job, DateTimeOffset At)> _stored = [];

  private readonly HashSet<string> _known = new(StringComparer.Ordinal);

  #endregion

  #region Ctors

  public ReceiveForgeWebhookCommandHandlerTests()
  {
    A.CallTo(() => _clock.UtcNow).Returns(Now);
    A.CallTo(() => _forges.GetWebhookValidator(Forge.AzureDevOps)).Returns(_validator);
    A.CallTo(() => _protector.Unprotect(A<string>._)).ReturnsLazily((string value) => $"plain:{value}");
    A.CallTo(() => _validator.IsValid(A<ForgeWebhookRequest>._, A<IReadOnlyList<string>>._))
     .ReturnsLazily((ForgeWebhookRequest _, IReadOnlyList<string> secrets) => secrets.Contains("plain:current"));
    A.CallTo(() => _deliveries.TryStoreForgeDeliveryAsync(A<ForgeDelivery>._, A<JobRequest>._, A<DateTimeOffset>._, A<CancellationToken>._))
     .ReturnsLazily((ForgeDelivery delivery, JobRequest job, DateTimeOffset at, CancellationToken _) =>
     {
       if (!_known.Add(delivery.DeliveryId))
       {
         return false;
       }

       _stored.Add((delivery, job, at));

       return true;
     });
    OwnedBy(Owner());
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ValidSignature_ShouldStoreAndQueue()
  {
    var headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = "Basic abc" };
    ForgeWebhookRequest? checkedRequest = null;
    A.CallTo(() => _validator.IsValid(A<ForgeWebhookRequest>._, A<IReadOnlyList<string>>._))
     .Invokes((ForgeWebhookRequest request, IReadOnlyList<string> _) => checkedRequest = request)
     .Returns(true);

    var outcome = await SendAsync(Command() with { Headers = headers });

    outcome.ShouldBe(WebhookOutcome.Accepted);
    checkedRequest.ShouldNotBeNull();
    checkedRequest.Forge.ShouldBe(Forge.AzureDevOps);
    checkedRequest.ConnectionId.ShouldBe(ConnectionId);
    checkedRequest.DeliveryId.ShouldBe(DeliveryId);
    checkedRequest.Event.ShouldBe("git.push");
    checkedRequest.Headers["authorization"].ShouldBe("Basic abc");
    checkedRequest.Payload.ShouldBe(Body);

    var (delivery, job, at) = _stored.ShouldHaveSingleItem();
    delivery.Forge.ShouldBe(Forge.AzureDevOps);
    delivery.DeliveryId.ShouldBe(DeliveryId);
    delivery.Event.ShouldBe("git.push");
    delivery.Action.ShouldBeNull();
    delivery.ConnectionId.ShouldBe(ConnectionId);
    delivery.RepositoryExternalId.ShouldBeNull();
    delivery.Payload.ShouldBe(Body);
    job.OrganizationId.ShouldBe(OrganizationId);
    job.Type.ShouldBe(JobTypes.HandleForgeDelivery);
    job.PayloadJson.ShouldBe(ForgeDeliveryJobs.Payload(Forge.AzureDevOps, DeliveryId));
    job.LockKey.ShouldBe(RepositoryReconciliationJobs.ConnectionKey(ConnectionId));
    at.ShouldBe(Now);
  }

  [Fact]
  public async Task Handle_PreviousSecret_ShouldAccept()
  {
    OwnedBy(Owner() with { WebhookSecretProtected = "next", WebhookSecretPreviousProtected = "current" });
    IReadOnlyList<string>? secrets = null;
    A.CallTo(() => _validator.IsValid(A<ForgeWebhookRequest>._, A<IReadOnlyList<string>>._))
     .Invokes((ForgeWebhookRequest _, IReadOnlyList<string> given) => secrets = given)
     .ReturnsLazily((ForgeWebhookRequest _, IReadOnlyList<string> given) => given.Contains("plain:current"));

    (await SendAsync(Command())).ShouldBe(WebhookOutcome.Accepted);

    secrets.ShouldBe(["plain:next", "plain:current"]);
    _stored.ShouldHaveSingleItem();
  }

  [Fact]
  public async Task Handle_NoPreviousSecret_ShouldCheckTheCurrentOneOnly()
  {
    IReadOnlyList<string>? secrets = null;
    A.CallTo(() => _validator.IsValid(A<ForgeWebhookRequest>._, A<IReadOnlyList<string>>._))
     .Invokes((ForgeWebhookRequest _, IReadOnlyList<string> given) => secrets = given)
     .Returns(true);

    await SendAsync(Command());

    secrets.ShouldBe(["plain:current"]);
  }

  [Fact]
  public async Task Handle_CurrentSecretNotDecrypted_ShouldThrowUnauthorized()
  {
    A.CallTo(() => _protector.Unprotect("current")).Throws(new CryptographicException("lost key ring"));

    var exception = await Should.ThrowAsync<UnauthorizedException>(async () => await SendAsync(Command()));

    exception.ErrorCode.ShouldBe(WebhookErrors.InvalidSignature);
    A.CallTo(_validator).MustNotHaveHappened();
    A.CallTo(_deliveries).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_PreviousSecretNotDecrypted_ShouldAcceptWithTheCurrentOne()
  {
    OwnedBy(Owner() with { WebhookSecretPreviousProtected = "previous" });
    A.CallTo(() => _protector.Unprotect("previous")).Throws(new CryptographicException("altered"));
    IReadOnlyList<string>? secrets = null;
    A.CallTo(() => _validator.IsValid(A<ForgeWebhookRequest>._, A<IReadOnlyList<string>>._))
     .Invokes((ForgeWebhookRequest _, IReadOnlyList<string> given) => secrets = given)
     .ReturnsLazily((ForgeWebhookRequest _, IReadOnlyList<string> given) => given.Contains("plain:current"));

    (await SendAsync(Command())).ShouldBe(WebhookOutcome.Accepted);

    secrets.ShouldBe(["plain:current"]);
    _stored.ShouldHaveSingleItem();
  }

  [Fact]
  public async Task Handle_UnknownConnection_ShouldThrowUnauthorized()
  {
    A.CallTo(() => _connections.FindOwnerAsync(ConnectionId, A<CancellationToken>._)).Returns((ForgeConnectionOwner?)null);

    var exception = await Should.ThrowAsync<UnauthorizedException>(async () => await SendAsync(Command()));

    exception.ErrorCode.ShouldBe(WebhookErrors.InvalidSignature);
    A.CallTo(_validator).MustNotHaveHappened();
    A.CallTo(_protector).MustNotHaveHappened();
    A.CallTo(_deliveries).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_WrongSignature_ShouldThrowUnauthorized()
  {
    A.CallTo(() => _validator.IsValid(A<ForgeWebhookRequest>._, A<IReadOnlyList<string>>._)).Returns(false);

    var exception = await Should.ThrowAsync<UnauthorizedException>(async () => await SendAsync(Command()));

    exception.ErrorCode.ShouldBe(WebhookErrors.InvalidSignature);
    A.CallTo(_deliveries).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_UnknownAndWrongSignature_ShouldGiveTheSameRefusal()
  {
    A.CallTo(() => _validator.IsValid(A<ForgeWebhookRequest>._, A<IReadOnlyList<string>>._)).Returns(false);
    var wrong = await Should.ThrowAsync<UnauthorizedException>(async () => await SendAsync(Command()));

    A.CallTo(() => _connections.FindOwnerAsync(ConnectionId, A<CancellationToken>._)).Returns((ForgeConnectionOwner?)null);
    var unknown = await Should.ThrowAsync<UnauthorizedException>(async () => await SendAsync(Command()));

    unknown.ErrorCode.ShouldBe(wrong.ErrorCode);
    unknown.Message.ShouldBe(wrong.Message);
  }

  [Fact]
  public async Task Handle_MalformedConnection_ShouldThrowUnauthorizedWithoutReading()
  {
    var exception = await Should.ThrowAsync<UnauthorizedException>(async () => await SendAsync(Command() with { ConnectionId = "Not/A Connection" }));

    exception.ErrorCode.ShouldBe(WebhookErrors.InvalidSignature);
    A.CallTo(_connections).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_ConnectionOfAnotherForge_ShouldThrowUnauthorized()
  {
    OwnedBy(Owner() with { Forge = Forge.Bitbucket });

    var exception = await Should.ThrowAsync<UnauthorizedException>(async () => await SendAsync(Command()));

    exception.ErrorCode.ShouldBe(WebhookErrors.InvalidSignature);
    A.CallTo(_validator).MustNotHaveHappened();
    A.CallTo(_deliveries).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_SameDeliveryTwice_ShouldStoreOnce()
  {
    (await SendAsync(Command())).ShouldBe(WebhookOutcome.Accepted);
    (await SendAsync(Command())).ShouldBe(WebhookOutcome.Duplicate);

    _stored.ShouldHaveSingleItem();
  }

  [Fact]
  public async Task Handle_BodyNotJson_ShouldThrowBadRequest()
  {
    var exception = await Should.ThrowAsync<BadRequestException>(async () => await SendAsync(Command() with { Payload = "not json"u8.ToArray() }));

    exception.ErrorCode.ShouldBe(WebhookErrors.InvalidPayload);
    A.CallTo(_deliveries).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_BodyNotAnObject_ShouldThrowBadRequest()
  {
    var exception = await Should.ThrowAsync<BadRequestException>(async () => await SendAsync(Command() with { Payload = "[1]"u8.ToArray() }));

    exception.ErrorCode.ShouldBe(WebhookErrors.InvalidPayload);
    A.CallTo(_deliveries).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_BodyNotJsonAndWrongSignature_ShouldThrowUnauthorized()
  {
    A.CallTo(() => _validator.IsValid(A<ForgeWebhookRequest>._, A<IReadOnlyList<string>>._)).Returns(false);

    await Should.ThrowAsync<UnauthorizedException>(async () => await SendAsync(Command() with { Payload = "not json"u8.ToArray() }));
  }

  [Fact]
  public async Task Handle_OrganizationNotActive_ShouldDrop()
  {
    foreach (var state in new[] { OrganizationState.Pending, OrganizationState.Suspended })
    {
      OwnedBy(Owner() with { State = state });

      (await SendAsync(Command())).ShouldBe(WebhookOutcome.Dropped, state.ToString());
    }

    A.CallTo(_deliveries).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_SuspendedConnection_ShouldDrop()
  {
    OwnedBy(Owner() with { Status = ForgeConnectionStatus.Suspended });

    (await SendAsync(Command())).ShouldBe(WebhookOutcome.Dropped);

    A.CallTo(_deliveries).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_FailingOrExpiringConnection_ShouldStore()
  {
    OwnedBy(Owner() with { Status = ForgeConnectionStatus.Failing });
    (await SendAsync(Command())).ShouldBe(WebhookOutcome.Accepted);

    OwnedBy(Owner() with { Status = ForgeConnectionStatus.Expiring });
    (await SendAsync(Command() with { DeliveryId = "d-2" })).ShouldBe(WebhookOutcome.Accepted);

    _stored.Count.ShouldBe(2);
  }

  [Fact]
  public async Task Handle_NullCommand_ShouldThrowArgumentNullException()
  {
    await Should.ThrowAsync<ArgumentNullException>(async () => await Handler().Handle(null!, Ct));
  }

  private static ForgeConnectionOwner Owner() =>
    new(OrganizationId, Forge.AzureDevOps, OrganizationState.Active, ForgeConnectionStatus.Active, "current", null);

  private static ReceiveForgeWebhookCommand Command() =>
    new(Forge.AzureDevOps, ConnectionId, DeliveryId, "git.push", new Dictionary<string, string>(StringComparer.Ordinal), Body);

  private void OwnedBy(ForgeConnectionOwner owner) =>
    A.CallTo(() => _connections.FindOwnerAsync(ConnectionId, A<CancellationToken>._)).Returns(owner);

  private ReceiveForgeWebhookCommandHandler Handler() => new(_connections, _forges, _protector, _deliveries, _clock);

  private async Task<WebhookOutcome> SendAsync(ReceiveForgeWebhookCommand command) => await Handler().Handle(command, Ct);

  #endregion
}
