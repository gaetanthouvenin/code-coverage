// ------------------------------------------------------------------------------------------------
// <copyright file="AuditEntryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Operations;

namespace CodeCoverage.Domain.Tests.Operations;

public sealed class AuditEntryTests
{
  #region Fields

  private static readonly DateTimeOffset At = new(2026, 11, 8, 10, 30, 0, TimeSpan.Zero);

  #endregion

  #region Methods

  [Fact]
  public void Create_WithValidValues_ShouldKeepThem()
  {
    var entry = AuditEntry.Create(
      At,
      "octocat",
      "Settings.Update",
      "Processing:WorkerConcurrency",
      new Dictionary<string, string> { ["Previous"] = "4", ["Value"] = "8" }
    );

    entry.Id.ShouldBe(0);
    entry.At.ShouldBe(At);
    entry.Actor.ShouldBe("octocat");
    entry.Action.ShouldBe("Settings.Update");
    entry.Target.ShouldBe("Processing:WorkerConcurrency");
    entry.Details.ShouldBe(new Dictionary<string, string> { ["Previous"] = "4", ["Value"] = "8" });
  }

  [Fact]
  public void Create_WithoutDetails_ShouldHaveEmptyDetails()
  {
    var entry = AuditEntry.Create(At, AuditEntry.SystemActor, "Commits.Reprocess", "42", null);

    entry.Details.ShouldBeEmpty();
  }

  [Fact]
  public void Create_WithLocalTime_ShouldStoreUtc()
  {
    var local = new DateTimeOffset(2026, 11, 8, 11, 30, 0, TimeSpan.FromHours(1));

    var entry = AuditEntry.Create(local, "octocat", "Settings.Update", "Key", null);

    entry.At.Offset.ShouldBe(TimeSpan.Zero);
    entry.At.ShouldBe(At);
  }

  // The entry keeps its own copy: a later change of the caller's dictionary does not alter the trail.
  [Fact]
  public void Create_ThenDetailsChanged_ShouldKeepTheOriginalDetails()
  {
    var details = new Dictionary<string, string> { ["Label"] = "ci" };

    var entry = AuditEntry.Create(At, "octocat", "UploadTokens.Create", "17", details);
    details["Label"] = "changed";
    details["Other"] = "added";

    entry.Details.ShouldBe(new Dictionary<string, string> { ["Label"] = "ci" });
  }

  [Theory]
  [InlineData("Secret")]
  [InlineData("WebhookSecret")]
  [InlineData("clientsecret")]
  [InlineData("SECRET_VALUE")]
  public void Create_WithSecretKey_ShouldThrowArgumentException(string key)
  {
    var details = new Dictionary<string, string> { ["Label"] = "ci", [key] = "s3cr3t" };

    var exception = Should.Throw<ArgumentException>(
      () => AuditEntry.Create(At, "octocat", "Settings.ReplaceSecret", "GitHub:WebhookSecret", details)
    );

    exception.ParamName.ShouldBe("details");
    exception.Message.ShouldNotContain("s3cr3t");
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  public void Create_WithBlankDetailKey_ShouldThrowArgumentException(string key)
  {
    var details = new Dictionary<string, string> { [key] = "value" };

    Should.Throw<ArgumentException>(() => AuditEntry.Create(At, "octocat", "Settings.Update", "Key", details))
          .ParamName.ShouldBe("details");
  }

  [Fact]
  public void Create_WithNullDetailValue_ShouldThrowArgumentNullException()
  {
    var details = new Dictionary<string, string> { ["Value"] = null! };

    Should.Throw<ArgumentNullException>(() => AuditEntry.Create(At, "octocat", "Settings.Update", "Key", details))
          .ParamName.ShouldBe("details");
  }

  [Theory]
  [InlineData(null, "Settings.Update", "Key", "actor")]
  [InlineData("octocat", null, "Key", "action")]
  [InlineData("octocat", "Settings.Update", null, "target")]
  public void Create_WithNullText_ShouldThrowArgumentNullException(
    string? actor,
    string? action,
    string? target,
    string parameter)
  {
    Should.Throw<ArgumentNullException>(() => AuditEntry.Create(At, actor!, action!, target!, null))
          .ParamName.ShouldBe(parameter);
  }

  [Theory]
  [InlineData(" ", "Settings.Update", "Key", "actor")]
  [InlineData("octocat", "", "Key", "action")]
  [InlineData("octocat", "Settings.Update", " ", "target")]
  public void Create_WithBlankText_ShouldThrowArgumentException(
    string actor,
    string action,
    string target,
    string parameter)
  {
    Should.Throw<ArgumentException>(() => AuditEntry.Create(At, actor, action, target, null))
          .ParamName.ShouldBe(parameter);
  }

  [Fact]
  public void Create_WithTooLongTexts_ShouldThrowArgumentException()
  {
    var actor = new string('a', AuditEntry.ActorMaxLength + 1);
    var action = new string('a', AuditEntry.ActionMaxLength + 1);
    var target = new string('a', AuditEntry.TargetMaxLength + 1);

    Should.Throw<ArgumentException>(() => AuditEntry.Create(At, actor, "Settings.Update", "Key", null))
          .ParamName.ShouldBe("actor");
    Should.Throw<ArgumentException>(() => AuditEntry.Create(At, "octocat", action, "Key", null))
          .ParamName.ShouldBe("action");
    Should.Throw<ArgumentException>(() => AuditEntry.Create(At, "octocat", "Settings.Update", target, null))
          .ParamName.ShouldBe("target");
  }

  // The markers hold characters a GitHub login cannot contain: they never name a real user.
  [Fact]
  public void Markers_ShouldNotBeValidGitHubLogins()
  {
    AuditEntry.SystemActor.ShouldNotMatch("^[A-Za-z0-9-]+$");
    AuditEntry.AnonymousActor.ShouldNotMatch("^[A-Za-z0-9-]+$");
  }

  #endregion
}
