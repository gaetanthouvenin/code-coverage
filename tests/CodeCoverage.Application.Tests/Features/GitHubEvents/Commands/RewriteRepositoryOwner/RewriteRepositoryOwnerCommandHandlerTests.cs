// ------------------------------------------------------------------------------------------------
// <copyright file="RewriteRepositoryOwnerCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.GitHubEvents.Commands.RewriteRepositoryOwner;
using CodeCoverage.Application.Tests.Support;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Features.GitHubEvents.Commands.RewriteRepositoryOwner;

public sealed class RewriteRepositoryOwnerCommandHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

  private readonly InMemoryOrganizationDirectory _directory = new(Now);

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly List<(string Owner, long AfterId, int BatchSize)> _batches = [];

  private readonly RewriteRepositoryOwnerCommandHandler _handler;

  #endregion

  #region Ctors

  public RewriteRepositoryOwnerCommandHandlerTests()
  {
    _handler = new RewriteRepositoryOwnerCommandHandler(_directory, _repositories);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ShouldRewriteEveryBatchWithTheLoginOfTheOrganization()
  {
    _directory.Current = _directory.Add("1001", "New-Octo", admitted: true);
    GivenBatchesEndingAt(500, 1200, 1201);

    await HandleAsync();

    _batches.ShouldBe([("New-Octo", 0, 500), ("New-Octo", 500, 500), ("New-Octo", 1200, 500), ("New-Octo", 1201, 500)]);
  }

  // The login is the one of the row when the job runs, whatever the rename that queued it said.
  [Fact]
  public async Task Handle_RenamedAgainBeforeTheJobRuns_ShouldWriteTheLastLogin()
  {
    _directory.Current = _directory.Add("1001", "first-rename", admitted: true);
    _directory.Current.Rename("second-rename", Now);
    GivenBatchesEndingAt();

    await HandleAsync();

    _batches.ShouldBe([("second-rename", 0, 500)]);
  }

  [Fact]
  public async Task Handle_OrganizationWithoutRepositories_ShouldStopAfterOneReading()
  {
    _directory.Current = _directory.Add("583231", "octocat", admitted: true);
    GivenBatchesEndingAt();

    await HandleAsync();

    _batches.Count.ShouldBe(1);
  }

  // The login was taken by another account: what the row holds is a tombstone, never written to a repository.
  [Fact]
  public async Task Handle_TombstonedLogin_ShouldWriteNothing()
  {
    _directory.Current = _directory.Add("1001", "octo", admitted: true);
    _directory.Current.Tombstone(Now);

    await HandleAsync();

    A.CallTo(_repositories).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_ScopeWithoutOrganization_ShouldWriteNothing()
  {
    await HandleAsync();

    A.CallTo(_repositories).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_Null_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(async () => await _handler.Handle(null!, Ct));
  }

  private async Task HandleAsync() => await _handler.Handle(new RewriteRepositoryOwnerCommand(), Ct);

  private void GivenBatchesEndingAt(params long[] lastIds)
  {
    var answers = new Queue<long?>(lastIds.Select(static id => (long?)id));

    A.CallTo(() => _repositories.RewriteOwnerAsync(A<string>._, A<long>._, A<int>._, A<CancellationToken>._))
     .ReturnsLazily(
       (string owner, long afterId, int batchSize, CancellationToken _) =>
       {
         _batches.Add((owner, afterId, batchSize));

         return answers.Count > 0 ? answers.Dequeue() : null;
       }
     );
  }

  #endregion
}
