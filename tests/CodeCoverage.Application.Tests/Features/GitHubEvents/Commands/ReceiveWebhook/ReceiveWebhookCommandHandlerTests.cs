// ------------------------------------------------------------------------------------------------
// <copyright file="ReceiveWebhookCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.GitHubEvents;
using CodeCoverage.Application.Features.GitHubEvents.Commands.ReceiveWebhook;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Features.GitHubEvents.Commands.ReceiveWebhook;

public sealed class ReceiveWebhookCommandHandlerTests
{
  #region Fields

  private const long Installation = 42;

  private const long Organization = 9;

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  private readonly IWebhookSignatureValidator _signatures = A.Fake<IWebhookSignatureValidator>();

  private readonly IInstallationRepository _installations = A.Fake<IInstallationRepository>();

  private readonly IWebhookDeliveryRepository _deliveries = A.Fake<IWebhookDeliveryRepository>();

  private readonly IClock _clock = A.Fake<IClock>();

  private JobRequest? _queued;

  #endregion

  #region Ctors

  public ReceiveWebhookCommandHandlerTests()
  {
    A.CallTo(() => _clock.UtcNow).Returns(Now);
    A.CallTo(() => _signatures.IsValid(A<byte[]>._, A<string?>._)).Returns(true);
    A.CallTo(() => _deliveries.TryStoreAsync(A<GitHubWebhookEvent>._, A<JobRequest>._, A<DateTimeOffset>._, A<CancellationToken>._))
     .Invokes((GitHubWebhookEvent _, JobRequest job, DateTimeOffset _, CancellationToken _) => _queued = job)
     .Returns(true);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_InvalidSignature_ShouldThrowUnauthorized()
  {
    A.CallTo(() => _signatures.IsValid(A<byte[]>._, A<string?>._)).Returns(false);

    var exception = await Should.ThrowAsync<UnauthorizedException>(async () => await SendAsync("push", PushBody));

    exception.ErrorCode.ShouldBe(GitHubEventErrors.InvalidSignature);
    A.CallTo(_installations).MustNotHaveHappened();
    A.CallTo(_deliveries).MustNotHaveHappened();
  }

  // The real comparison with both secrets is in WebhookSignatureValidatorTests: here, the validator accepted the previous one.
  [Fact]
  public async Task Handle_PreviousSecret_ShouldAccept()
  {
    OwnedBy(OrganizationState.Active);

    (await SendAsync("push", PushBody, signature: "sha256=signed-with-the-previous-secret")).ShouldBe(WebhookOutcome.Accepted);
  }

  [Fact]
  public async Task Handle_KnownDelivery_ShouldDoNothing()
  {
    OwnedBy(OrganizationState.Active);
    A.CallTo(() => _deliveries.TryStoreAsync(A<GitHubWebhookEvent>._, A<JobRequest>._, A<DateTimeOffset>._, A<CancellationToken>._)).Returns(false);

    (await SendAsync("push", PushBody)).ShouldBe(WebhookOutcome.Duplicate);
  }

  [Fact]
  public async Task Handle_ActiveOrganization_ShouldQueueForOrganization()
  {
    OwnedBy(OrganizationState.Active);

    (await SendAsync("push", PushBody)).ShouldBe(WebhookOutcome.Accepted);

    var job = _queued.ShouldNotBeNull();
    job.OrganizationId.ShouldBe(Organization);
    job.IsInstance.ShouldBeFalse();
    job.Type.ShouldBe(JobTypes.HandleWebhook);
    job.LockKey.ShouldBe("repo:7");
    job.PayloadJson.ShouldBe("""{"deliveryId":"d-1"}""");
  }

  [Fact]
  public async Task Handle_Accepted_ShouldStoreTheDeliveryAtTheCurrentTime()
  {
    OwnedBy(OrganizationState.Active);

    await SendAsync("push", PushBody);

    A.CallTo(
      () => _deliveries.TryStoreAsync(
        A<GitHubWebhookEvent>.That.Matches(static d => d.DeliveryId == "d-1" && d.Event == "push" && d.InstallationId == Installation && d.RepositoryId == 7),
        A<JobRequest>._,
        Now,
        A<CancellationToken>._
      )
    ).MustHaveHappenedOnceExactly();
  }

  [Theory]
  [InlineData(OrganizationState.Pending)]
  [InlineData(OrganizationState.Suspended)]
  public async Task Handle_NonActiveOrganizationPush_ShouldDropWith202(OrganizationState state)
  {
    OwnedBy(state);

    (await SendAsync("push", PushBody)).ShouldBe(WebhookOutcome.Dropped);

    A.CallTo(_deliveries).MustNotHaveHappened();
  }

  [Theory]
  [InlineData("installation")]
  [InlineData("installation_repositories")]
  [InlineData("installation_target")]
  public async Task Handle_NonActiveOrganizationInstallation_ShouldQueueInInstanceLane(string @event)
  {
    OwnedBy(OrganizationState.Pending);

    (await SendAsync(@event, InstallationBody)).ShouldBe(WebhookOutcome.Accepted);

    var job = _queued.ShouldNotBeNull();
    job.IsInstance.ShouldBeTrue();
    job.OrganizationId.ShouldBeNull();
    job.LockKey.ShouldBe("installation:42");
  }

  // installation.created arrives before the row of its installation exists.
  [Fact]
  public async Task Handle_UnknownInstallationCreated_ShouldQueueInInstanceLane()
  {
    A.CallTo(() => _installations.FindOwnerAsync(Installation, A<CancellationToken>._)).Returns((InstallationOwner?)null);

    (await SendAsync("installation", InstallationBody)).ShouldBe(WebhookOutcome.Accepted);

    _queued.ShouldNotBeNull().IsInstance.ShouldBeTrue();
  }

  [Fact]
  public async Task Handle_UnknownInstallationPush_ShouldDrop()
  {
    A.CallTo(() => _installations.FindOwnerAsync(Installation, A<CancellationToken>._)).Returns((InstallationOwner?)null);

    (await SendAsync("push", PushBody)).ShouldBe(WebhookOutcome.Dropped);
  }

  [Fact]
  public async Task Handle_WithoutInstallation_ShouldDropWithoutReading()
  {
    (await SendAsync("ping", "{}")).ShouldBe(WebhookOutcome.Dropped);

    A.CallTo(_installations).MustNotHaveHappened();
  }

  // An active organization keeps every event, those of its installation included.
  [Fact]
  public async Task Handle_ActiveOrganizationInstallation_ShouldQueueForOrganization()
  {
    OwnedBy(OrganizationState.Active);

    await SendAsync("installation", InstallationBody);

    _queued.ShouldNotBeNull().OrganizationId.ShouldBe(Organization);
  }

  [Fact]
  public async Task Handle_UnreadableBody_ShouldThrowBadRequest()
  {
    var exception = await Should.ThrowAsync<BadRequestException>(async () => await SendAsync("push", "not json"));

    exception.ErrorCode.ShouldBe(GitHubEventErrors.InvalidPayload);
    A.CallTo(_deliveries).MustNotHaveHappened();
  }

  private const string PushBody = """{"installation":{"id":42},"repository":{"id":7}}""";

  private const string InstallationBody = """{"action":"created","installation":{"id":42}}""";

  private void OwnedBy(OrganizationState state) =>
    A.CallTo(() => _installations.FindOwnerAsync(Installation, A<CancellationToken>._)).Returns(new InstallationOwner(Organization, state));

  private ValueTask<WebhookOutcome> SendAsync(string @event, string body, string signature = "sha256=abc") =>
    new ReceiveWebhookCommandHandler(_signatures, _installations, _deliveries, _clock)
      .Handle(new ReceiveWebhookCommand("d-1", @event, signature, Encoding.UTF8.GetBytes(body)), Ct);

  #endregion
}
