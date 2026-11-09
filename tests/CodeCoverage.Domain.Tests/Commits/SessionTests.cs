// ------------------------------------------------------------------------------------------------
// <copyright file="SessionTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Tests.Commits;

public sealed class SessionTests
{
  #region Fields

  private static readonly GitSha Origin = GitSha.Parse("a94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private static readonly CoverageTotals Totals = new(6, 3, 1, 2, 1);

  #endregion

  #region Methods

  [Fact]
  public void FromUpload_ShouldCopyTheTotalsAndKeepNoOrigin()
  {
    var session = Session.FromUpload(1, 3, 9, ["unit"], null, "github-actions", "https://example.test/1", Totals);

    session.Kind.ShouldBe(SessionKind.Uploaded);
    session.CommitId.ShouldBe(1);
    session.Number.ShouldBe(3);
    session.UploadId.ShouldBe(9);
    session.Flags.ShouldBe(["unit"]);
    session.Name.ShouldBeNull();
    session.Lines.ShouldBe(10);
    session.Hits.ShouldBe(6);
    session.Misses.ShouldBe(3);
    session.Partials.ShouldBe(1);
    session.Coverage.ShouldBe(60m);
    session.CarriedForwardFromSha.ShouldBeNull();
    session.SourceSessionId.ShouldBeNull();
    session.DataUploadId.ShouldBeNull();
    session.Data.ShouldBeNull();
  }

  [Fact]
  public void CarriedForward_WithAnUpload_ShouldKeepItsOrigin()
  {
    var session = Session.CarriedForward(1, 4, Origin, 7, 9, null, ["unit"], "CF[1] - name", null, null, Totals);

    session.Kind.ShouldBe(SessionKind.CarriedForward);
    session.UploadId.ShouldBeNull();
    session.CarriedForwardFromSha.ShouldBe(Origin);
    session.SourceSessionId.ShouldBe(7);
    session.DataUploadId.ShouldBe(9);
    session.Data.ShouldBeNull();
  }

  [Fact]
  public void CarriedForward_WithItsOwnBlob_ShouldKeepIt()
  {
    var session = Session.CarriedForward(1, 4, Origin, 7, null, [1, 2], ["unit"], null, null, null, Totals);

    session.DataUploadId.ShouldBeNull();
    session.Data.ShouldBe([(byte)1, (byte)2]);
  }

  [Theory]
  [InlineData(true, true)]
  [InlineData(false, false)]
  public void CarriedForward_WithoutExactlyOneSource_ShouldThrow(bool hasUpload, bool hasBlob)
  {
    Should.Throw<ArgumentException>(
      () => Session.CarriedForward(1, 4, Origin, 7, hasUpload ? 9 : null, hasBlob ? [1] : null, [], null, null, null, Totals)
    );
  }

  [Fact]
  public void CarriedForward_WithAnEmptyBlob_ShouldThrow()
  {
    Should.Throw<ArgumentException>(() => Session.CarriedForward(1, 4, Origin, 7, null, [], [], null, null, null, Totals));
  }

  [Fact]
  public void FromUpload_WithANameTooLong_ShouldThrow()
  {
    var name = new string('n', Session.NameMaxLength + 1);

    Should.Throw<ArgumentException>(() => Session.FromUpload(1, 3, 9, [], name, null, null, Totals))
          .ParamName.ShouldBe("name");
  }

  [Theory]
  [InlineData(0, 3, 9)]
  [InlineData(1, 0, 9)]
  [InlineData(1, 3, 0)]
  public void FromUpload_WithAnIdentifierNotPositive_ShouldThrow(long commitId, int number, long uploadId)
  {
    Should.Throw<ArgumentOutOfRangeException>(() => Session.FromUpload(commitId, number, uploadId, [], null, null, null, Totals));
  }

  #endregion
}
