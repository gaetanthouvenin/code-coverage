// ------------------------------------------------------------------------------------------------
// <copyright file="CheckRunPublisherTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Notifications;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Services;

public sealed class CheckRunPublisherTests
{
  #region Fields

  private static readonly ForgeRepositoryRef Repository = new(Forge.GitHub, "42", "1296269");

  private static readonly GitSha Sha = GitSha.Parse(new string('a', GitSha.Length));

  private static readonly CommitStatus Status = new("coverage/patch", StatusState.Failure, "50.00% of diff hit");

  private readonly IGitHubAppGateway _gitHub = A.Fake<IGitHubAppGateway>();

  private readonly CheckRunPublisher _publisher;

  #endregion

  #region Ctors

  public CheckRunPublisherTests()
  {
    A.CallTo(() => _gitHub.CreateCheckRunAsync(A<ForgeRepositoryRef>._, A<GitHubCheckRun>._, A<CancellationToken>._)).Returns(501L);
    _publisher = new CheckRunPublisher(_gitHub);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // A batch that fails after the creation: the identifier still reaches the caller, which stores it, so that a retry of
  // the job does not create a second check run.
  [Fact]
  public async Task Publish_BatchFailsAfterTheCreation_ShouldStillReturnTheIdentifier()
  {
    A.CallTo(() => _gitHub.UpdateCheckRunAsync(A<ForgeRepositoryRef>._, A<long>._, A<GitHubCheckRun>._, A<CancellationToken>._))
     .Throws(new BadGatewayException("GitHub.Unavailable", "GitHub does not answer."));

    var id = await _publisher.PublishAsync(Repository, Sha, Status, null, Annotations(120), Ct);

    id.ShouldBe(501);
    // The second batch failed: the third one is not tried.
    A.CallTo(() => _gitHub.UpdateCheckRunAsync(Repository, 501, A<GitHubCheckRun>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Publish_Canceled_ShouldPropagate()
  {
    A.CallTo(() => _gitHub.UpdateCheckRunAsync(A<ForgeRepositoryRef>._, A<long>._, A<GitHubCheckRun>._, A<CancellationToken>._))
     .Throws(new OperationCanceledException());

    await Should.ThrowAsync<OperationCanceledException>(() => _publisher.PublishAsync(Repository, Sha, Status, null, Annotations(60), Ct));
  }

  [Fact]
  public async Task Supersede_ShouldGiveTheEarlierCheckRunTheNewState()
  {
    var success = new CommitStatus("coverage/patch", StatusState.Success, "100.00% of diff hit");

    await _publisher.SupersedeAsync(Repository, 777, Sha, success, "https://coverage.example.com", Ct);

    A.CallTo(() => _gitHub.UpdateCheckRunAsync(
                     Repository,
                     777,
                     A<GitHubCheckRun>.That.Matches(static r => r.State == StatusState.Success && r.Annotations.Count == 0),
                     A<CancellationToken>._
                   ))
     .MustHaveHappenedOnceExactly();
  }

  private static CheckRunAnnotation[] Annotations(int count) =>
    [.. Enumerable.Range(1, count).Select(static i => new CheckRunAnnotation("src/a.cs", i, i, "m"))];

  #endregion
}
