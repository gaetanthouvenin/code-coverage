// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubPullRequestEventTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text;

using CodeCoverage.Domain.Contracts.GitHub;

namespace CodeCoverage.Domain.Tests.Contracts.GitHub;

public sealed class GitHubPullRequestEventTests
{
  #region Methods

  [Theory]
  [InlineData("""{"action":"opened","number":12,"pull_request":{"number":12}}""", 12)]
  [InlineData("""{"action":"closed","pull_request":{"number":7,"merged":true}}""", 7)]
  [InlineData("""{"number":3,"pull_request":{"number":9}}""", 3)]
  public void Parse_ShouldReadTheNumber(string json, int expected)
  {
    Parse(json).Number.ShouldBe(expected);
  }

  [Theory]
  [InlineData("not json")]
  [InlineData("[]")]
  [InlineData("{}")]
  [InlineData("""{"number":"12"}""")]
  [InlineData("""{"number":0}""")]
  [InlineData("""{"number":-4}""")]
  [InlineData("""{"number":99999999999}""")]
  [InlineData("""{"pull_request":12}""")]
  [InlineData("""{"pull_request":{}}""")]
  public void Parse_NoNumber_ShouldNameNoPullRequest(string json)
  {
    Parse(json).Number.ShouldBeNull();
  }

  [Fact]
  public void Parse_Null_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => GitHubPullRequestEvent.Parse(null!));
  }

  private static GitHubPullRequestEvent Parse(string json) => GitHubPullRequestEvent.Parse(Encoding.UTF8.GetBytes(json));

  #endregion
}
