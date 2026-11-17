// ------------------------------------------------------------------------------------------------
// <copyright file="CommentTruncatorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Services.Comments;

namespace CodeCoverage.Application.Tests.Services.Comments;

public sealed class CommentTruncatorTests
{
  #region Fields

  private const int Limit = 60_000;

  #endregion

  #region Methods

  [Fact]
  public void Fit_BodyThatFits_ShouldBeAskedOnceAndKept()
  {
    var asked = new List<int?>();

    var body = CommentTruncator.Fit(10, Limit, files => Body(asked, files, perFile: 10));

    body.Length.ShouldBe(100 + 10 * 10);
    asked.ShouldBe([10]);
  }

  [Fact]
  public void Fit_BodyTooLong_ShouldKeepTheLargestNumberOfFilesThatFits()
  {
    var asked = new List<int?>();

    // 100 characters without any file, 1 000 per file: 59 files make 59 100 characters, 60 make 60 100.
    var body = CommentTruncator.Fit(500, Limit, files => Body(asked, files, perFile: 1000));

    body.Length.ShouldBe(59_100);
    asked.Count.ShouldBeLessThan(15, "the search halves");
    asked.ShouldNotContain((int?)null);
  }

  [Fact]
  public void Fit_ExactlyAtTheCeiling_ShouldFit()
  {
    var body = CommentTruncator.Fit(1, Limit, files => new string('x', files is 1 ? Limit : 0));

    body.Length.ShouldBe(Limit);
  }

  [Fact]
  public void Fit_TooLongEvenWithoutAnyFile_ShouldGiveUpTheFoldedSections()
  {
    var body = CommentTruncator.Fit(5, Limit, static files => files is null ? "bare" : new string('x', Limit + 1));

    body.ShouldBe("bare");
  }

  [Fact]
  public void Fit_TooLongWhateverIsGivenUp_ShouldCutTheText()
  {
    var body = CommentTruncator.Fit(0, Limit, static _ => new string('x', Limit + 500));

    body.Length.ShouldBe(Limit);
  }

  [Fact]
  public void Fit_ForgeLimit_ShouldRespectIt()
  {
    var asked = new List<int?>();

    // 100 characters without any file, 100 per file: a forge that takes 1 000 characters gets 9 files.
    var body = CommentTruncator.Fit(50, 1000, files => Body(asked, files, perFile: 100));

    body.Length.ShouldBe(1000);
    asked.ShouldNotContain((int?)null);
  }

  [Fact]
  public void Fit_InvalidArguments_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => CommentTruncator.Fit(1, Limit, null!));
    Should.Throw<ArgumentOutOfRangeException>(() => CommentTruncator.Fit(-1, Limit, static _ => string.Empty));
    Should.Throw<ArgumentOutOfRangeException>(() => CommentTruncator.Fit(1, 0, static _ => string.Empty));
  }

  private static string Body(List<int?> asked, int? files, int perFile)
  {
    asked.Add(files);

    return new string('x', 100 + (files ?? 0) * perFile);
  }

  #endregion
}
