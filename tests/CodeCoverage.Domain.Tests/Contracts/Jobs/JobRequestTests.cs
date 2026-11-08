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

  #endregion
}
