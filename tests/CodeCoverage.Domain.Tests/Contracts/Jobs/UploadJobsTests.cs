// ------------------------------------------------------------------------------------------------
// <copyright file="UploadJobsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Jobs;

namespace CodeCoverage.Domain.Tests.Contracts.Jobs;

public sealed class UploadJobsTests
{
  #region Methods

  [Fact]
  public void ProcessUpload_ShouldRunInParallelAndNeverMerge()
  {
    var uploadId = Guid.Parse("0f8a4c1e-7d2b-4e6f-9a3c-5b1d2e3f4a5b");

    var job = ProcessUploadJobs.Create(uploadId);

    job.Type.ShouldBe(JobTypes.ProcessUpload);
    job.PayloadJson.ShouldBe("""{"UploadId":"0f8a4c1e-7d2b-4e6f-9a3c-5b1d2e3f4a5b"}""");
    job.LockKey.ShouldBeNull();
    job.DedupKey.ShouldBeNull();
    job.IsInstance.ShouldBeFalse();
    job.OrganizationId.ShouldBeNull("the job takes the organization of the scope that queues it");
  }

  [Fact]
  public void ReprocessCommit_ShouldShareTheLockOfTheCommitAndMergeWithItsTwin()
  {
    var job = ReprocessCommitJobs.Create(9);

    job.Type.ShouldBe(JobTypes.ReprocessCommit);
    job.PayloadJson.ShouldBe("""{"CommitId":9}""");
    job.LockKey.ShouldBe("commit:9");
    job.LockKey.ShouldBe(FinalizeCommitJobs.Create(9).LockKey, "a reprocessing never runs while the commit is finalized");
    job.DedupKey.ShouldBe("reprocess:9");
    job.Coalesce.ShouldBeTrue();
    job.IsInstance.ShouldBeFalse();
    job.OrganizationId.ShouldBeNull("the job takes the organization of the scope that queues it");
  }

  #endregion
}
