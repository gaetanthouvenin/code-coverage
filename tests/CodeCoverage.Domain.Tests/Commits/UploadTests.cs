// ------------------------------------------------------------------------------------------------
// <copyright file="UploadTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;

namespace CodeCoverage.Domain.Tests.Commits;

public sealed class UploadTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Methods

  [Fact]
  public void Receive_ShouldStartReceivedWithItsMetadata()
  {
    var upload = Upload.Receive(
      1,
      2,
      "key",
      ["unit"],
      "linux",
      "github-actions",
      "build",
      "job",
      "https://example.test/build",
      "/work",
      "1.0.0",
      5,
      PrincipalKind.RepositoryToken,
      Now
    );

    upload.State.ShouldBe(UploadState.Received);
    upload.CommitId.ShouldBe(1);
    upload.Sequence.ShouldBe(2);
    upload.IdempotencyKey.ShouldBe("key");
    upload.Flags.ShouldBe(["unit"]);
    upload.Name.ShouldBe("linux");
    upload.CiService.ShouldBe("github-actions");
    upload.BuildId.ShouldBe("build");
    upload.JobId.ShouldBe("job");
    upload.BuildUrl.ShouldBe("https://example.test/build");
    upload.RepoRoot.ShouldBe("/work");
    upload.UploaderVersion.ShouldBe("1.0.0");
    upload.TokenId.ShouldBe(5);
    upload.PrincipalKind.ShouldBe(PrincipalKind.RepositoryToken);
    upload.ReceivedAt.ShouldBe(Now);
    upload.ProcessedAt.ShouldBeNull();
    upload.ProcessingLog.ShouldBeSameAs(ProcessingLog.Empty);
  }

  [Fact]
  public void Receive_ShouldGiveEachUploadItsOwnPublicIdentifier()
  {
    var first = New("key");
    var second = New("key");

    first.PublicId.ShouldNotBe(Guid.Empty);
    first.PublicId.ShouldNotBe(second.PublicId);
  }

  [Fact]
  public void Receive_ShouldCopyTheFlags()
  {
    var flags = new List<string> { "unit" };
    var upload = Upload.Receive(1, 1, "key", flags, null, null, null, null, null, null, null, null, PrincipalKind.GitHubOidc, Now);

    flags.Clear();

    upload.Flags.ShouldBe(["unit"]);
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  public void Receive_BlankKey_ShouldThrow(string key)
  {
    Should.Throw<ArgumentException>(() => New(key));
  }

  [Fact]
  public void Receive_KeyOverTheLimit_ShouldThrow()
  {
    Should.Throw<ArgumentException>(() => New(new string('a', Upload.IdempotencyKeyMaxLength + 1)));
  }

  [Fact]
  public void Receive_BuildUrlOverTheLimit_ShouldThrow()
  {
    var url = new string('a', Upload.BuildUrlMaxLength + 1);

    Should.Throw<ArgumentException>(
      () => Upload.Receive(1, 1, "key", [], null, null, null, null, url, null, null, null, PrincipalKind.GitHubOidc, Now)
    );
  }

  [Fact]
  public void Receive_UndefinedPrincipal_ShouldThrow()
  {
    Should.Throw<ArgumentOutOfRangeException>(
      () => Upload.Receive(1, 1, "key", [], null, null, null, null, null, null, null, null, (PrincipalKind)99, Now)
    );
  }

  [Theory]
  [InlineData(0, 1)]
  [InlineData(1, 0)]
  public void Receive_IdentifierNotPositive_ShouldThrow(long commitId, int sequence)
  {
    Should.Throw<ArgumentOutOfRangeException>(
      () => Upload.Receive(commitId, sequence, "key", [], null, null, null, null, null, null, null, null, PrincipalKind.GitHubOidc, Now)
    );
  }

  private static Upload New(string key) =>
    Upload.Receive(1, 1, key, [], null, null, null, null, null, null, null, null, PrincipalKind.GitHubOidc, Now);

  #endregion
}
