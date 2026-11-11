// ------------------------------------------------------------------------------------------------
// <copyright file="CommentTruncatorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Services.Comments;

namespace CodeCoverage.Application.Tests.Services.Comments;

public sealed class CommentTruncatorTests
{
  #region Methods

  [Fact]
  public void Fit_BodyThatFits_ShouldBeAskedOnceAndKept()
  {
    var asked = new List<int?>();

    var body = CommentTruncator.Fit(10, files => Body(asked, files, perFile: 10));

    body.Length.ShouldBe(100 + 10 * 10);
    asked.ShouldBe([10]);
  }

  [Fact]
  public void Fit_BodyTooLong_ShouldKeepTheLargestNumberOfFilesThatFits()
  {
    var asked = new List<int?>();

    // 100 characters without any file, 1 000 per file: 59 files make 59 100 characters, 60 make 60 100.
    var body = CommentTruncator.Fit(500, files => Body(asked, files, perFile: 1000));

    body.Length.ShouldBe(59_100);
    asked.Count.ShouldBeLessThan(15, "the search halves");
    asked.ShouldNotContain((int?)null);
  }

  [Fact]
  public void Fit_ExactlyAtTheCeiling_ShouldFit()
  {
    var body = CommentTruncator.Fit(1, files => new string('x', files is 1 ? CommentTruncator.MaxLength : 0));

    body.Length.ShouldBe(CommentTruncator.MaxLength);
  }

  [Fact]
  public void Fit_TooLongEvenWithoutAnyFile_ShouldGiveUpTheFoldedSections()
  {
    var body = CommentTruncator.Fit(5, static files => files is null ? "bare" : new string('x', CommentTruncator.MaxLength + 1));

    body.ShouldBe("bare");
  }

  [Fact]
  public void Fit_TooLongWhateverIsGivenUp_ShouldCutTheText()
  {
    var body = CommentTruncator.Fit(0, static _ => new string('x', CommentTruncator.MaxLength + 500));

    body.Length.ShouldBe(CommentTruncator.MaxLength);
  }

  [Fact]
  public void Fit_InvalidArguments_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => CommentTruncator.Fit(1, null!));
    Should.Throw<ArgumentOutOfRangeException>(() => CommentTruncator.Fit(-1, static _ => string.Empty));
  }

  private static string Body(List<int?> asked, int? files, int perFile)
  {
    asked.Add(files);

    return new string('x', 100 + (files ?? 0) * perFile);
  }

  #endregion
}
