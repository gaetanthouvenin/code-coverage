// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubRepositoryEventTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text;

using CodeCoverage.Domain.Contracts.GitHub;

namespace CodeCoverage.Domain.Tests.Contracts.GitHub;

public sealed class GitHubRepositoryEventTests
{
  #region Methods

  [Fact]
  public void Parse_Repository_ShouldReadItsDescriptionAndOwner()
  {
    var body = Parse("""{"action":"renamed","repository":{"id":7,"name":"New","private":true,"default_branch":"main","owner":{"login":"Octo","id":1001}}}""");

    var repository = body.Repository.ShouldNotBeNull();
    repository.ExternalId.ShouldBe("7");
    repository.Owner.ShouldBe("Octo");
    repository.Namespace.ShouldBeEmpty();
    repository.Name.ShouldBe("New");
    repository.DefaultBranch.ShouldBe("main");
    repository.IsPrivate.ShouldBeTrue();
    repository.OwnerExternalId.ShouldBe("1001");
    body.OwnerAccountId.ShouldBe("1001");
    body.PreviousOwnerAccountId.ShouldBeNull();
    body.RemovedRepositoryIds.ShouldBeEmpty();
  }

  [Theory]
  [InlineData("organization")]
  [InlineData("user")]
  public void Parse_Transferred_ShouldReadThePreviousOwnerOfEitherKind(string kind)
  {
    var from = "{\"" + kind + "\":{\"id\":2002,\"login\":\"old\"}}";
    var body = Parse(
      "{\"changes\":{\"owner\":{\"from\":" + from + "}},\"repository\":{\"id\":7,\"name\":\"r\",\"private\":false,\"owner\":{\"login\":\"new\",\"id\":1001}}}"
    );

    body.PreviousOwnerAccountId.ShouldBe("2002");
    body.OwnerAccountId.ShouldBe("1001");
    body.Repository.ShouldNotBeNull().DefaultBranch.ShouldBeEmpty();
  }

  [Fact]
  public void Parse_RepositoriesRemoved_ShouldListTheirIdentifiers()
  {
    var body = Parse("""{"action":"removed","repositories_removed":[{"id":7,"name":"a"},{"id":"x"},{"name":"no-id"},3,{"id":9}]}""");

    body.RemovedRepositoryIds.ShouldBe(["7", "9"]);
    body.Repository.ShouldBeNull();
    body.OwnerAccountId.ShouldBeNull();
  }

  [Theory]
  [InlineData("not json")]
  [InlineData("[]")]
  [InlineData("""{"repository":"text"}""")]
  [InlineData("""{"repository":{"id":0,"name":"r","private":false,"owner":{"login":"o","id":1}}}""")]
  [InlineData("""{"repository":{"id":7,"name":"","private":false,"owner":{"login":"o","id":1}}}""")]
  [InlineData("""{"repository":{"id":7,"name":"r","owner":{"login":"o","id":1}}}""")]
  [InlineData("""{"repository":{"id":7,"name":"r","private":"yes","owner":{"login":"o","id":1}}}""")]
  [InlineData("""{"repository":{"id":7,"name":"r","private":false,"owner":{"login":"o"}}}""")]
  [InlineData("""{"repository":{"id":7,"name":"r","private":false}}""")]
  public void Parse_MalformedRepository_ShouldLeaveItEmpty(string json)
  {
    var body = Parse(json);

    body.Repository.ShouldBeNull();
    body.OwnerAccountId.ShouldBeNull();
  }

  [Fact]
  public void Parse_Null_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(static () => GitHubRepositoryEvent.Parse(null!));
  }

  private static GitHubRepositoryEvent Parse(string json) => GitHubRepositoryEvent.Parse(Encoding.UTF8.GetBytes(json));

  #endregion
}