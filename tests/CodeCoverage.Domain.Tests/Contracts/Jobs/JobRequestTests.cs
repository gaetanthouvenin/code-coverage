// ------------------------------------------------------------------------------------------------
// <copyright file="JobRequestTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Jobs;

namespace CodeCoverage.Domain.Tests.Contracts.Jobs;

public sealed class JobRequestTests
{
  #region Methods

  [Fact]
  public void Create_WithDebounce_ShouldKeepEveryValueInUtc()
  {
    var runAfter = new DateTimeOffset(2026, 11, 8, 12, 0, 20, TimeSpan.FromHours(2));

    var request = JobRequest.Create(
      "NotifyCommit",
      """{"commitId":42}""",
      lockKey: "commit:42",
      dedupKey: "notify:42",
      runAfter: runAfter,
      maxDelay: TimeSpan.FromMinutes(5)
    );

    request.Type.ShouldBe("NotifyCommit");
    request.PayloadJson.ShouldBe("""{"commitId":42}""");
    request.LockKey.ShouldBe("commit:42");
    request.DedupKey.ShouldBe("notify:42");
    request.RunAfter.ShouldBe(runAfter);
    request.RunAfter!.Value.Offset.ShouldBe(TimeSpan.Zero);
    request.Coalesce.ShouldBeFalse();
    request.MaxDelay.ShouldBe(TimeSpan.FromMinutes(5));
  }

  [Fact]
  public void Create_WithTypeAndPayloadOnly_ShouldRunAtOnceWithoutKeys()
  {
    var request = JobRequest.Create("ProcessUpload", """{"uploadId":7}""");

    request.LockKey.ShouldBeNull();
    request.DedupKey.ShouldBeNull();
    request.RunAfter.ShouldBeNull();
    request.Coalesce.ShouldBeFalse();
    request.MaxDelay.ShouldBeNull();
    request.ToString().ShouldBe("ProcessUpload (lock: -, dedup: -)");
  }

  [Fact]
  public void Create_WithCoalesceButNoDedupKey_ShouldThrowArgumentException()
  {
    Should.Throw<ArgumentException>(() => JobRequest.Create("FinalizeCommit", "{}", coalesce: true))
          .ParamName.ShouldBe("dedupKey");
  }

  [Fact]
  public void Create_WithMaxDelayButNoDedupKey_ShouldThrowArgumentException()
  {
    Should.Throw<ArgumentException>(() => JobRequest.Create("NotifyCommit", "{}", maxDelay: TimeSpan.FromMinutes(5)))
          .ParamName.ShouldBe("dedupKey");
  }

  [Fact]
  public void Create_WithCoalesceAndMaxDelay_ShouldThrowArgumentException()
  {
    Should.Throw<ArgumentException>(
            () => JobRequest.Create("FinalizeCommit", "{}", dedupKey: "finalize:42", coalesce: true, maxDelay: TimeSpan.FromMinutes(5))
          )
          .ParamName.ShouldBe("maxDelay");
  }

  [Fact]
  public void Create_WithNonPositiveMaxDelay_ShouldThrowArgumentOutOfRangeException()
  {
    Should.Throw<ArgumentOutOfRangeException>(
      () => JobRequest.Create("NotifyCommit", "{}", dedupKey: "notify:42", maxDelay: TimeSpan.Zero)
    );
  }

  [Theory]
  [InlineData(null, "{}", typeof(ArgumentNullException))]
  [InlineData(" ", "{}", typeof(ArgumentException))]
  [InlineData("FinalizeCommit", null, typeof(ArgumentNullException))]
  [InlineData("FinalizeCommit", "", typeof(ArgumentException))]
  public void Create_WithMissingTypeOrPayload_ShouldThrow(string? type, string? payloadJson, Type expected)
  {
    Should.Throw(() => JobRequest.Create(type!, payloadJson!), expected);
  }

  [Fact]
  public void Create_WithTooLongValues_ShouldThrowArgumentException()
  {
    var tooLongKey = new string('k', JobRequest.KeyMaxLength + 1);

    Should.Throw<ArgumentException>(() => JobRequest.Create(new string('t', JobRequest.TypeMaxLength + 1), "{}"))
          .ParamName.ShouldBe("type");
    Should.Throw<ArgumentException>(() => JobRequest.Create("FinalizeCommit", "{}", lockKey: tooLongKey))
          .ParamName.ShouldBe("lockKey");
    Should.Throw<ArgumentException>(() => JobRequest.Create("FinalizeCommit", "{}", dedupKey: tooLongKey))
          .ParamName.ShouldBe("dedupKey");
    Should.Throw<ArgumentException>(() => JobRequest.Create("FinalizeCommit", "{}", lockKey: " "))
          .ParamName.ShouldBe("lockKey");
  }

  [Fact]
  public void Create_InsideOrganizationScope_ShouldTakeIt()
  {
    var request = JobRequest.Create("ProcessUpload", "{}");

    request.IsInstance.ShouldBeFalse();
    request.OrganizationId.ShouldBeNull();
    request.ResolveOrganizationId(7).ShouldBe(7);
  }

  [Fact]
  public void Create_OutsideOrganizationScope_ShouldThrow()
  {
    var request = JobRequest.Create("ProcessUpload", "{}");

    Should.Throw<InvalidOperationException>(() => request.ResolveOrganizationId(null));
  }

  [Theory]
  [InlineData(null)]
  [InlineData(7L)]
  public void ForInstance_WhateverTheScope_ShouldStayOutOfAnyOrganization(long? current)
  {
    var request = JobRequest.ForInstance("SweepUploads", "{}");

    request.IsInstance.ShouldBeTrue();
    request.ResolveOrganizationId(current).ShouldBeNull();
  }

  [Theory]
  [InlineData(null)]
  [InlineData(7L)]
  public void ForOrganization_OutsideOrInsideItsOrganization_ShouldNameIt(long? current)
  {
    var request = JobRequest.ForOrganization(7, "HandleWebhook", "{}", lockKey: "repo:1", dedupKey: "delivery:1");

    request.OrganizationId.ShouldBe(7);
    request.LockKey.ShouldBe("repo:1");
    request.DedupKey.ShouldBe("delivery:1");
    request.ResolveOrganizationId(current).ShouldBe(7);
  }

  [Fact]
  public void ForOrganization_InsideOtherOrganizationScope_ShouldThrow()
  {
    var request = JobRequest.ForOrganization(7, "HandleWebhook", "{}");

    Should.Throw<InvalidOperationException>(() => request.ResolveOrganizationId(8));
  }

  [Theory]
  [InlineData(0L)]
  [InlineData(-1L)]
  public void ForOrganization_WithNonPositiveIdentifier_ShouldThrowArgumentOutOfRangeException(long organizationId)
  {
    Should.Throw<ArgumentOutOfRangeException>(() => JobRequest.ForOrganization(organizationId, "HandleWebhook", "{}"))
          .ParamName.ShouldBe("organizationId");
  }

  #endregion
}
