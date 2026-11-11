// ------------------------------------------------------------------------------------------------
// <copyright file="CommitStatusSuffixTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Notifications;

namespace CodeCoverage.Domain.Tests.Notifications;

public sealed class CommitStatusSuffixTests
{
  #region Methods

  [Fact]
  public void WithSuffix_ShouldAddItAtTheEnd()
  {
    var status = new CommitStatus("coverage/project", StatusState.Failure, "79.00% (-1.00%) compared to bbbbbbb").WithSuffix(" (partial: 1/3 uploads)");

    status.Context.ShouldBe("coverage/project");
    status.State.ShouldBe(StatusState.Failure);
    status.Description.ShouldBe("79.00% (-1.00%) compared to bbbbbbb (partial: 1/3 uploads)");
  }

  // The description is cut, never the suffix: it is what tells the reader the status rests on a part of the uploads.
  [Fact]
  public void WithSuffix_DescriptionTooLong_ShouldCutTheDescriptionAndKeepTheSuffixWhole()
  {
    var status = new CommitStatus("coverage/project", StatusState.Success, new string('d', 200)).WithSuffix(" (partial: 1/3 uploads)");

    status.Description.Length.ShouldBe(CommitStatus.DescriptionMaxLength);
    status.Description.ShouldEndWith(" (partial: 1/3 uploads)");
    status.Description.ShouldStartWith(new string('d', CommitStatus.DescriptionMaxLength - 23));
  }

  [Fact]
  public void WithSuffix_AlreadyThere_ShouldChangeNothing()
  {
    var status = new CommitStatus("coverage/project", StatusState.Success, "ok (partial: 1/3 uploads)");

    status.WithSuffix(" (partial: 1/3 uploads)").ShouldBeSameAs(status);
    status.WithSuffix(string.Empty).ShouldBeSameAs(status);
  }

  [Fact]
  public void WithSuffix_Invalid_ShouldThrow()
  {
    var status = new CommitStatus("coverage/project", StatusState.Success, "ok");

    Should.Throw<ArgumentNullException>(() => status.WithSuffix(null!));
    Should.Throw<ArgumentException>(() => status.WithSuffix(new string('s', CommitStatus.DescriptionMaxLength + 1)));
  }

  #endregion
}
