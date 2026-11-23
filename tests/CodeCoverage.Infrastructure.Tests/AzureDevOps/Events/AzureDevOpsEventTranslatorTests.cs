// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsEventTranslatorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text;
using System.Text.Json.Nodes;

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Infrastructure.AzureDevOps;
using CodeCoverage.Infrastructure.AzureDevOps.Events;

namespace CodeCoverage.Infrastructure.Tests.AzureDevOps.Events;

public sealed class AzureDevOpsEventTranslatorTests
{
  #region Fields

  private const string RepositoryId = "1701bc47-1145-4739-a805-64b02e40dbe7";

  private const string AccountId = "c3a5bd8e-3c5c-4bd0-9f2b-0bdbbd4a06e1";

  private const string Head = "be67f8871a4d2c75f13a51c1d3c30ac0d74d4ef4";

  private const string Before = "f47bbc106853afe3c1b07a81754bce5f4b8dbf62";

  private const string NullObjectId = "0000000000000000000000000000000000000000";

  private readonly AzureDevOpsEventTranslator _translator = new();

  #endregion

  #region Methods

  [Fact]
  public void Translate_NullDelivery_ShouldThrow() =>
    Should.Throw<ArgumentNullException>(() => _translator.Translate(null!));

  [Fact]
  public void Translate_Push_ShouldReturnAPushEvent()
  {
    var translated = _translator.Translate(Delivery(AzureDevOpsEventTranslator.PushEvent, Fixture("git.push.json")));

    translated.ShouldBe(new ForgePushEvent(RepositoryId, "main", GitSha.Parse(Head), Deleted: false, TouchesConfig: true, GitSha.Parse(Before)));
  }

  [Fact]
  public void Translate_PushOfANewBranch_ShouldHaveNoPreviousHead()
  {
    var push = (ForgePushEvent?)_translator.Translate(Push(NullObjectId, Head));

    push.ShouldNotBeNull();
    push.Before.ShouldBeNull();
    push.Head.ShouldBe(GitSha.Parse(Head));
  }

  [Fact]
  public void Translate_PushWithoutPreviousHead_ShouldHaveNoPreviousHead()
  {
    var push = (ForgePushEvent?)_translator.Translate(Push(null, Head));

    push.ShouldNotBeNull();
    push.Before.ShouldBeNull();
  }

  [Fact]
  public void Translate_PushDeletingTheBranch_ShouldReturnADeletion()
  {
    var push = (ForgePushEvent?)_translator.Translate(Push(Before, NullObjectId));

    push.ShouldNotBeNull();
    push.Deleted.ShouldBeTrue();
    push.Head.ShouldBeNull();
    push.TouchesConfig.ShouldBeFalse();
  }

  [Fact]
  public void Translate_PushWithAMalformedHead_ShouldIgnore()
  {
    _translator.Translate(Push(Before, "not-a-sha")).ShouldBeNull();
    _translator.Translate(Push(Before, null)).ShouldBeNull();
  }

  [Fact]
  public void Translate_PushWithAMalformedPreviousHead_ShouldIgnore() =>
    _translator.Translate(Push("not-a-sha", Head)).ShouldBeNull();

  [Fact]
  public void Translate_PushOfTagsOnly_ShouldIgnore()
  {
    var body = Fixture("git.push.json");
    body["resource"]!["refUpdates"] = new JsonArray(
      42,
      new JsonObject { ["oldObjectId"] = NullObjectId, ["newObjectId"] = Head },
      new JsonObject { ["name"] = "refs/tags/v2.0.0", ["oldObjectId"] = NullObjectId, ["newObjectId"] = Head }
    );

    _translator.Translate(Delivery(AzureDevOpsEventTranslator.PushEvent, body)).ShouldBeNull();
  }

  [Fact]
  public void Translate_PushOfSeveralBranches_ShouldPreferTheDefaultBranch()
  {
    var body = Fixture("git.push.json");
    body["resource"]!["refUpdates"] = new JsonArray(
      new JsonObject { ["name"] = "refs/heads/feature/login", ["oldObjectId"] = NullObjectId, ["newObjectId"] = Before },
      new JsonObject { ["name"] = "refs/heads/main", ["oldObjectId"] = Before, ["newObjectId"] = Head }
    );

    var push = (ForgePushEvent?)_translator.Translate(Delivery(AzureDevOpsEventTranslator.PushEvent, body));

    push.ShouldNotBeNull();
    push.Branch.ShouldBe("main");
    push.Head.ShouldBe(GitSha.Parse(Head));
  }

  [Fact]
  public void Translate_PushOfSeveralBranchesWithoutTheDefaultBranch_ShouldTakeTheFirstBranch()
  {
    var body = Fixture("git.push.json");
    body["resource"]!["refUpdates"] = new JsonArray(
      new JsonObject { ["name"] = "refs/heads/feature/login", ["oldObjectId"] = NullObjectId, ["newObjectId"] = Before },
      new JsonObject { ["name"] = "refs/heads/feature/logout", ["oldObjectId"] = NullObjectId, ["newObjectId"] = Head }
    );

    var push = (ForgePushEvent?)_translator.Translate(Delivery(AzureDevOpsEventTranslator.PushEvent, body));

    push.ShouldNotBeNull();
    push.Branch.ShouldBe("feature/login");
  }

  [Fact]
  public void Translate_PushWithoutItsReferencesOrRepository_ShouldIgnore()
  {
    var withoutReferences = Fixture("git.push.json");
    withoutReferences["resource"]!.AsObject().Remove("refUpdates");
    var withoutRepository = Fixture("git.push.json");
    withoutRepository["resource"]!.AsObject().Remove("repository");

    _translator.Translate(Delivery(AzureDevOpsEventTranslator.PushEvent, withoutReferences)).ShouldBeNull();
    _translator.Translate(Delivery(AzureDevOpsEventTranslator.PushEvent, withoutRepository)).ShouldBeNull();
  }

  [Fact]
  public void Translate_PushNamingItsRepositoryByIdentifierOnly_ShouldTakeTheFirstBranch()
  {
    var body = Fixture("git.push.json");
    body["resource"]!.AsObject().Remove("repository");
    body["resource"]!["repositoryId"] = RepositoryId;
    body["resource"]!["refUpdates"] = new JsonArray(
      new JsonObject { ["name"] = "refs/heads/feature/login", ["oldObjectId"] = NullObjectId, ["newObjectId"] = Before },
      new JsonObject { ["name"] = "refs/heads/main", ["oldObjectId"] = Before, ["newObjectId"] = Head }
    );

    _translator.Translate(Delivery(AzureDevOpsEventTranslator.PushEvent, body))
               .ShouldBe(new ForgePushEvent(RepositoryId, "feature/login", GitSha.Parse(Before), Deleted: false, TouchesConfig: true));
  }

  [Fact]
  public void Translate_PullCreated_ShouldReturnAPullRequestEvent() =>
    _translator.Translate(Delivery(AzureDevOpsEventTranslator.PullRequestCreatedEvent, Fixture("git.pullrequest.created.json")))
               .ShouldBe(new ForgePullRequestEvent(RepositoryId, 42));

  [Fact]
  public void Translate_PullUpdatedSourceBranch_ShouldReturnAPullRequestEvent() =>
    _translator.Translate(Delivery(AzureDevOpsEventTranslator.PullRequestUpdatedEvent, Fixture("git.pullrequest.updated.json")))
               .ShouldBe(new ForgePullRequestEvent(RepositoryId, 42));

  [Fact]
  public void Translate_PullUpdatedReviewers_ShouldIgnore()
  {
    var body = Fixture("git.pullrequest.updated.json");
    body["message"]!["text"] = "Jamal Hartnett added Mobile Team as a reviewer on pull request 42 (Add the login page) in api";

    _translator.Translate(Delivery(AzureDevOpsEventTranslator.PullRequestUpdatedEvent, body)).ShouldBeNull();
  }

  [Fact]
  public void Translate_PullUpdatedWithoutMessage_ShouldIgnore()
  {
    var withoutMessage = Fixture("git.pullrequest.updated.json");
    withoutMessage.AsObject().Remove("message");
    var withoutText = Fixture("git.pullrequest.updated.json");
    withoutText["message"] = new JsonObject { ["html"] = "updated the source branch" };

    _translator.Translate(Delivery(AzureDevOpsEventTranslator.PullRequestUpdatedEvent, withoutMessage)).ShouldBeNull();
    _translator.Translate(Delivery(AzureDevOpsEventTranslator.PullRequestUpdatedEvent, withoutText)).ShouldBeNull();
  }

  [Fact]
  public void Translate_MergeSucceeded_ShouldReturnAPullRequestEvent() =>
    _translator.Translate(Delivery(AzureDevOpsEventTranslator.PullRequestMergedEvent, Fixture("git.pullrequest.merged.json")))
               .ShouldBe(new ForgePullRequestEvent(RepositoryId, 42));

  [Fact]
  public void Translate_MergeRejected_ShouldIgnore()
  {
    var rejected = Fixture("git.pullrequest.merged.json");
    rejected["resource"]!["mergeStatus"] = "rejectedByPolicy";
    var unknown = Fixture("git.pullrequest.merged.json");
    unknown["resource"]!.AsObject().Remove("mergeStatus");

    _translator.Translate(Delivery(AzureDevOpsEventTranslator.PullRequestMergedEvent, rejected)).ShouldBeNull();
    _translator.Translate(Delivery(AzureDevOpsEventTranslator.PullRequestMergedEvent, unknown)).ShouldBeNull();
  }

  [Fact]
  public void Translate_PullWithoutANumberOrRepository_ShouldIgnore()
  {
    foreach (var number in new JsonNode?[] { null, 0, 1.5, "42" })
    {
      var body = Fixture("git.pullrequest.created.json");
      body["resource"]!["pullRequestId"] = number;

      _translator.Translate(Delivery(AzureDevOpsEventTranslator.PullRequestCreatedEvent, body)).ShouldBeNull();
    }

    var withoutRepository = Fixture("git.pullrequest.created.json");
    withoutRepository["resource"]!.AsObject().Remove("repository");
    var malformedRepository = Fixture("git.pullrequest.created.json");
    malformedRepository["resource"]!["repository"]!["id"] = "api";

    _translator.Translate(Delivery(AzureDevOpsEventTranslator.PullRequestCreatedEvent, withoutRepository)).ShouldBeNull();
    _translator.Translate(Delivery(AzureDevOpsEventTranslator.PullRequestCreatedEvent, malformedRepository)).ShouldBeNull();
  }

  [Fact]
  public void Translate_RepoRenamed_ShouldReturnARepositoryEvent() =>
    _translator.Translate(Delivery(AzureDevOpsEventTranslator.RepositoryRenamedEvent, Fixture("git.repo.renamed.json")))
               .ShouldBe(
                 new ForgeRepositoryEvent(
                   ForgeRepositoryEventKind.Renamed,
                   new ForgeRepositoryInfo(RepositoryId, string.Empty, "Projet A", "api", "main", IsPrivate: true, AccountId)
                 )
               );

  [Fact]
  public void Translate_RepoCreatedInAPublicProject_ShouldReturnAPublicRepository()
  {
    var body = Fixture("git.repo.renamed.json");
    body["resource"]!["repository"]!["project"]!["visibility"] = "public";
    body["resource"]!["repository"]!.AsObject().Remove("defaultBranch");

    var change = (ForgeRepositoryEvent?)_translator.Translate(Delivery(AzureDevOpsEventTranslator.RepositoryCreatedEvent, body));

    change.ShouldNotBeNull();
    change.Kind.ShouldBe(ForgeRepositoryEventKind.Created);
    change.Repository.IsPrivate.ShouldBeFalse();
    change.Repository.DefaultBranch.ShouldBe(AzureDevOpsGatewayMapper.EmptyRepositoryBranch);
  }

  [Fact]
  public void Translate_RepoWithoutDetails_ShouldKeepItsIdentifier()
  {
    var body = Fixture("git.repo.renamed.json");
    body["resource"]!["repository"] = new JsonObject { ["id"] = RepositoryId, ["defaultBranch"] = string.Empty, ["project"] = new JsonObject() };
    body.AsObject().Remove("resourceContainers");

    var change = (ForgeRepositoryEvent?)_translator.Translate(Delivery(AzureDevOpsEventTranslator.RepositoryCreatedEvent, body));

    change.ShouldBe(
      new ForgeRepositoryEvent(
        ForgeRepositoryEventKind.Created,
        new ForgeRepositoryInfo(RepositoryId, string.Empty, string.Empty, string.Empty, AzureDevOpsGatewayMapper.EmptyRepositoryBranch, IsPrivate: true)
      )
    );
  }

  [Fact]
  public void Translate_RepoDeleted_ShouldReturnARepositoryEvent()
  {
    var body = JsonNode.Parse(
      $$"""
        {
          "eventType": "git.repo.deleted",
          "resource": {
            "project": { "id": "6ce954b1-ce1f-45d1-b94d-e6bf2464ba2c", "name": "Projet A", "visibility": "private" },
            "repositoryId": "{{RepositoryId}}",
            "repositoryName": "api",
            "isHardDelete": false
          },
          "resourceContainers": { "account": { "id": "{{AccountId}}" } }
        }
        """
    )!;

    _translator.Translate(Delivery(AzureDevOpsEventTranslator.RepositoryDeletedEvent, body))
               .ShouldBe(
                 new ForgeRepositoryEvent(
                   ForgeRepositoryEventKind.Deleted,
                   new ForgeRepositoryInfo(RepositoryId, string.Empty, "Projet A", "api", AzureDevOpsGatewayMapper.EmptyRepositoryBranch, IsPrivate: true, AccountId)
                 )
               );
  }

  [Fact]
  public void Translate_RepoDeletedWithoutProject_ShouldLeaveTheNamespaceEmpty()
  {
    var body = JsonNode.Parse($$"""{ "resource": { "repositoryId": "{{RepositoryId}}" }, "resourceContainers": { "collection": {} } }""")!;

    var change = (ForgeRepositoryEvent?)_translator.Translate(Delivery(AzureDevOpsEventTranslator.RepositoryDeletedEvent, body));

    change.ShouldNotBeNull();
    change.Repository.Namespace.ShouldBeEmpty();
    change.Repository.Name.ShouldBeEmpty();
    change.Repository.OwnerExternalId.ShouldBeNull();
  }

  [Fact]
  public void Translate_RepoWithoutIdentifier_ShouldIgnore()
  {
    var body = JsonNode.Parse("""{ "resource": { "repositoryName": "api" }, "resourceContainers": { "account": { "id": "contoso" } } }""")!;

    _translator.Translate(Delivery(AzureDevOpsEventTranslator.RepositoryDeletedEvent, body)).ShouldBeNull();
  }

  [Fact]
  public void Translate_UnknownEvent_ShouldIgnore() =>
    _translator.Translate(Delivery("git.repo.forked", Fixture("git.repo.renamed.json"))).ShouldBeNull();

  [Theory]
  [InlineData("GIT.PUSH")]
  [InlineData("git.pusX")]
  [InlineData("GIT.PULLREQUEST.CREATED")]
  [InlineData("git.pullrequest.createX")]
  [InlineData("git.pullrequest.updateX")]
  [InlineData("git.pullrequest.deleted")]
  [InlineData("GIT.PULLREQUEST.MERGED")]
  [InlineData("git.pullrequest.mergeX")]
  [InlineData("GIT.REPO.CREATED")]
  [InlineData("git.repo.createX")]
  [InlineData("git.repo.renameX")]
  [InlineData("git.repo.deleteX")]
  [InlineData("git.repo.updated")]
  public void Translate_EventCloseToAKnownOne_ShouldIgnore(string @event) =>
    _translator.Translate(Delivery(@event, Fixture("git.pullrequest.created.json"))).ShouldBeNull();

  [Fact]
  public void Translate_UnreadableBody_ShouldIgnore()
  {
    foreach (var body in new[] { "{ not json", "[]", """{ "eventType": "git.push" }""", """{ "resource": [] }""" })
    {
      _translator.Translate(new ForgeDelivery(Forge.AzureDevOps, "delivery", AzureDevOpsEventTranslator.PushEvent, null, "connection", null, Encoding.UTF8.GetBytes(body)))
                 .ShouldBeNull();
    }
  }

  private static ForgeDelivery Push(string? oldObjectId, string? newObjectId)
  {
    var body = Fixture("git.push.json");
    body["resource"]!["refUpdates"] = new JsonArray(
      new JsonObject { ["name"] = "refs/heads/feature/login", ["oldObjectId"] = oldObjectId, ["newObjectId"] = newObjectId }
    );

    return Delivery(AzureDevOpsEventTranslator.PushEvent, body);
  }

  private static ForgeDelivery Delivery(string @event, JsonNode body) =>
    new(Forge.AzureDevOps, "delivery", @event, null, "connection", null, Encoding.UTF8.GetBytes(body.ToJsonString()));

  private static JsonNode Fixture(string name) =>
    JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "azure-devops", "hooks", name)))!;

  #endregion
}
