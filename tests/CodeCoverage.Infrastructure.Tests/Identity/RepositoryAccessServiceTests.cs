// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryAccessServiceTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;
using CodeCoverage.Infrastructure.Identity;

namespace CodeCoverage.Infrastructure.Tests.Identity;

public sealed class RepositoryAccessServiceTests
{
  #region Fields

  private const long UserId = 7;

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly IUserRepositoryAccessRepository _accesses = A.Fake<IUserRepositoryAccessRepository>();

  private readonly IJobQueue _jobQueue = A.Fake<IJobQueue>();

  private readonly List<JobRequest> _queued = [];

  private readonly RepositoryAccessService _service;

  #endregion

  #region Ctors

  public RepositoryAccessServiceTests()
  {
    var clock = A.Fake<IClock>();
    var options = A.Fake<IOptionsMonitor<AccessOptions>>();

    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => options.CurrentValue).Returns(new AccessOptions { CacheMinutes = 10, HardExpiryMinutes = 60 });
    A.CallTo(() => _jobQueue.TryEnqueueAsync(A<JobRequest>._, A<CancellationToken>._)).Invokes((JobRequest job, CancellationToken _) => _queued.Add(job)).Returns(true);

    _service = new RepositoryAccessService(_accesses, _jobQueue, options, clock);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Can_FreshRow_ShouldAnswerWithoutRefresh()
  {
    Row(RepositoryPermission.Write, Now.AddMinutes(-5));

    (await _service.CanAsync(UserId, "100", RepositoryPermission.Read, Ct)).ShouldBeTrue();
    (await _service.CanAsync(UserId, "100", RepositoryPermission.Write, Ct)).ShouldBeTrue();
    (await _service.CanAsync(UserId, "100", RepositoryPermission.Admin, Ct)).ShouldBeFalse();
    _queued.ShouldBeEmpty();
    // The row is read once for the scope.
    A.CallTo(() => _accesses.FindAsync(UserId, "100", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Can_CacheExpired_ShouldEnqueueRefresh()
  {
    Row(RepositoryPermission.Read, Now.AddMinutes(-30));

    (await _service.CanAsync(UserId, "100", RepositoryPermission.Read, Ct)).ShouldBeTrue();
    (await _service.CanAsync(UserId, "100", RepositoryPermission.Read, Ct)).ShouldBeTrue();

    var job = _queued.ShouldHaveSingleItem();
    job.Type.ShouldBe(JobTypes.RefreshUserAccess);
    job.OrganizationId.ShouldBeNull("the job takes the organization of the scope");
    job.IsInstance.ShouldBeFalse();
    job.DedupKey.ShouldBe("access:7");
    job.Coalesce.ShouldBeTrue();
  }

  [Fact]
  public async Task Can_PastHardExpiry_ShouldDeny()
  {
    Row(RepositoryPermission.Admin, Now.AddMinutes(-60));

    (await _service.CanAsync(UserId, "100", RepositoryPermission.Read, Ct)).ShouldBeFalse();
    _queued.ShouldHaveSingleItem();
  }

  [Fact]
  public async Task Can_NoRowAndNoFreshRowOfTheUser_ShouldDenyAndEnqueueRefresh()
  {
    A.CallTo(() => _accesses.ListOfUserAsync(UserId, A<CancellationToken>._)).Returns([RepositoryAccess.Create(UserId, "200", RepositoryPermission.Read, Now.AddMinutes(-30))]);

    (await _service.CanAsync(UserId, "100", RepositoryPermission.Read, Ct)).ShouldBeFalse();
    _queued.ShouldHaveSingleItem();
  }

  // A refresh ran a moment ago and wrote no row for this repository: it already answered.
  [Fact]
  public async Task Can_NoRowButAFreshRowOfTheUser_ShouldDenyWithoutRefresh()
  {
    A.CallTo(() => _accesses.ListOfUserAsync(UserId, A<CancellationToken>._)).Returns([RepositoryAccess.Create(UserId, "200", RepositoryPermission.Read, Now.AddMinutes(-1))]);

    (await _service.CanAsync(UserId, "100", RepositoryPermission.Read, Ct)).ShouldBeFalse();
    _queued.ShouldBeEmpty();
  }

  [Fact]
  public async Task Can_IdentifierInUpperCase_ShouldReadTheNormalizedRow()
  {
    A.CallTo(() => _accesses.FindAsync(UserId, "abc", A<CancellationToken>._)).Returns(RepositoryAccess.Create(UserId, "abc", RepositoryPermission.Read, Now));

    (await _service.CanAsync(UserId, "ABC", RepositoryPermission.Read, Ct)).ShouldBeTrue();
  }

  private void Row(RepositoryPermission permission, DateTimeOffset refreshedAt)
  {
    A.CallTo(() => _accesses.FindAsync(UserId, "100", A<CancellationToken>._)).Returns(RepositoryAccess.Create(UserId, "100", permission, refreshedAt));
  }

  #endregion
}
