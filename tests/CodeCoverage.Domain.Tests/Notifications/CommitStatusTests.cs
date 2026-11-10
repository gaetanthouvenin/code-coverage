// ------------------------------------------------------------------------------------------------
// <copyright file="CommitStatusTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Notifications;

namespace CodeCoverage.Domain.Tests.Notifications;

public sealed class CommitStatusTests
{
  #region Methods

  [Fact]
  public void Create_LongDescription_ShouldBeCutAt140Characters()
  {
    var status = new CommitStatus("coverage/project", StatusState.Success, new string('x', 141));

    status.Description.Length.ShouldBe(140);
  }

  [Fact]
  public void Create_DescriptionOf140Characters_ShouldBeKeptWhole()
  {
    var description = new string('x', 140);

    new CommitStatus("coverage/project", StatusState.Failure, description).Description.ShouldBe(description);
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  public void Create_EmptyContext_ShouldThrow(string context)
  {
    Should.Throw<ArgumentException>(() => new CommitStatus(context, StatusState.Success, "ok"));
  }

  [Fact]
  public void Create_NullDescription_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(static () => new CommitStatus("coverage/project", StatusState.Success, null!));
  }

  [Theory]
  [InlineData(StatusType.Project, "default", "coverage/project")]
  [InlineData(StatusType.Patch, "default", "coverage/patch")]
  [InlineData(StatusType.Changes, "default", "coverage/changes")]
  [InlineData(StatusType.Project, "backend", "coverage/project/backend")]
  [InlineData(StatusType.Patch, "Default", "coverage/patch/Default")]
  public void Context_ShouldFollowTheNamesOfCodecov(StatusType type, string name, string expected)
  {
    StatusContext.Build("coverage", type, name).ShouldBe(expected);
  }

  [Fact]
  public void Context_ShouldUseTheGivenPrefix()
  {
    StatusContext.Build("codecov", StatusType.Project, "default").ShouldBe("codecov/project");
  }

  [Theory]
  [InlineData("", "default")]
  [InlineData("coverage", "")]
  public void Context_EmptyPart_ShouldThrow(string prefix, string name)
  {
    Should.Throw<ArgumentException>(() => StatusContext.Build(prefix, StatusType.Project, name));
  }

  #endregion
}