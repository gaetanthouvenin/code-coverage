// ------------------------------------------------------------------------------------------------
// <copyright file="NotificationServiceCommentTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Services;
using CodeCoverage.Application.Services.Comments;
using CodeCoverage.Application.Tests.Services.Comments;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.PullRequests;
using CodeCoverage.Domain.Repositories;

using static CodeCoverage.Application.Tests.Services.Comments.CommentSamples;

namespace CodeCoverage.Application.Tests.Services;

// The comment of the notification: what NotificationService hands to CommentPublisher once the statuses are
// published (ARCHITECTURE §8.4). The forge and the repository are faked; the composer is the real one.
public sealed class NotificationServiceCommentTests
{
  #region Fields

  private static readonly ForgeRepositoryRef Address = new(Forge.GitHub, "42", "7");

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  private readonly IPullRequestRepository _pullRequests = A.Fake<IPullRequestRepository>();

  private readonly NotificationOptions _options = new();

  private readonly TrackedRepository _repository = TrackedRepository.Create(42, "7", RepositorySlug.Parse("octo/repo"), "main", false, "badge", Now);

  private readonly List<string> _created = [];

  private readonly List<(long Id, string Body)> _updated = [];

  private readonly List<long> _deleted = [];

  private readonly List<ForgeIssueComment> _onTheForge = [];

  private readonly CommentPublisher _publisher;

  private int _saves;

  #endregion

  #region Ctors

  public NotificationServiceCommentTests()
  {
    var gateways = A.Fake<IForgeGatewayProvider>();
    var notificationOptions = A.Fake<IOptionsMonitor<NotificationOptions>>();
    var urls = A.Fake<IOptionsMonitor<UrlOptions>>();
    var clock = A.Fake<IClock>();

    A.CallTo(() => gateways.GetGateway(Forge.GitHub)).Returns(_gateway);
    A.CallTo(() => notificationOptions.CurrentValue).Returns(_options);
    A.CallTo(() => urls.CurrentValue).Returns(new UrlOptions { WebPublicUrl = "https://coverage.example.com" });
    A.CallTo(() => clock.UtcNow).Returns(Now.AddMinutes(5));
    A.CallTo(() => _gateway.ListIssueCommentsAsync(Address, 123, A<CancellationToken>._)).ReturnsLazily(() => Task.FromResult<IReadOnlyList<ForgeIssueComment>>([.. _onTheForge]));
    A.CallTo(() => _gateway.CreateIssueCommentAsync(Address, 123, A<string>._, A<CancellationToken>._))
     .ReturnsLazily((ForgeRepositoryRef _, int _, string body, CancellationToken _) =>
       {
         _created.Add(body);

         return Task.FromResult<long?>(9000 + _created.Count);
       });
    A.CallTo(() => _gateway.UpdateIssueCommentAsync(Address, A<long>._, A<string>._, A<CancellationToken>._))
     .ReturnsLazily((ForgeRepositoryRef _, long id, string body, CancellationToken _) =>
       {
         _updated.Add((id, body));

         return Task.FromResult(true);
       });
    A.CallTo(() => _gateway.DeleteIssueCommentAsync(Address, A<long>._, A<CancellationToken>._))
     .ReturnsLazily((ForgeRepositoryRef _, long id, CancellationToken _) =>
       {
         _deleted.Add(id);

         return Task.FromResult(ForgeCommentDeletion.Deleted);
       });
    A.CallTo(() => _pullRequests.UpdateAsync(A<PullRequest>._, A<CancellationToken>._)).Invokes(() => _saves++);

    _publisher = new CommentPublisher(gateways, _pullRequests, notificationOptions, urls, clock, NullLogger<CommentPublisher>.Instance);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Comment_FirstPublication_ShouldCreateTheCommentAndRecordItOnThePullRequest()
  {
    var content = Content();

    (await PublishAsync(content)).ShouldBe(CommentOutcome.Created);

    var body = _created.ShouldHaveSingleItem();
    body.ShouldBe(Expected(content));
    body.ShouldStartWith(CommentComposer.Marker + "\n");
    var pull = content.Pull;
    pull.CommentId.ShouldBe(9001);
    pull.CommentBodyHash.ShouldBe(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
    pull.CommentedHeadSha.ShouldBe(HeadSha);
    // One save, under the version of the pull request, with what it was compared to.
    pull.ForkPointSha.ShouldBe(BaseSha);
    pull.ComparedToSha.ShouldBe(BaseSha);
    _saves.ShouldBe(1);
    _updated.ShouldBeEmpty();
  }

  // A commit that is no longer the head of its pull request never comments: the comment always describes the head.
  [Fact]
  public async Task Comment_StaleHead_ShouldNotComment()
  {
    var content = Content(pull: PullAt(OlderSha));

    (await PublishAsync(content)).ShouldBe(CommentOutcome.Skipped);

    A.CallTo(_gateway).MustNotHaveHappened();
    // Nor does it say what the pull request is compared to: that belongs to its head.
    content.Pull.ForkPointSha.ShouldBeNull();
    _saves.ShouldBe(0);
  }

  [Fact]
  public async Task Comment_UnchangedHash_ShouldSkipPatch()
  {
    var content = Content();
    await PublishAsync(content);
    var version = content.Pull.Version;

    (await PublishAsync(content)).ShouldBe(CommentOutcome.Unchanged);

    _created.ShouldHaveSingleItem();
    _updated.ShouldBeEmpty();
    content.Pull.Version.ShouldBe(version);
    _saves.ShouldBe(1);
  }

  [Fact]
  public async Task Comment_ChangedBody_ShouldUpdateTheCommentInPlace()
  {
    var content = Content();
    await PublishAsync(content);

    var changed = content with { FailedUploadCount = 1 };

    (await PublishAsync(changed)).ShouldBe(CommentOutcome.Updated);

    _created.ShouldHaveSingleItem();
    var update = _updated.ShouldHaveSingleItem();
    update.Id.ShouldBe(9001);
    update.Body.ShouldContain("1 upload failed");
    content.Pull.CommentBodyHash.ShouldBe(SHA256.HashData(Encoding.UTF8.GetBytes(update.Body)));
    // No search when the identifier is known.
    A.CallTo(() => _gateway.ListIssueCommentsAsync(A<ForgeRepositoryRef>._, A<int>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Comment_Deleted_DefaultBehavior_ShouldRecreate()
  {
    // require_changes would refuse a first publication: writing a deleted comment again is not one.
    var content = Content(Config(("require_changes", ConfigNode.FromText("uncovered_patch"))), Comparison(patch: new CoverageTotals(40, 0, 0, 0, 0)));
    content.Pull.RecordComment(77, new byte[PullRequest.CommentBodyHashLength], HeadSha, Now);
    Deleted(77);

    (await PublishAsync(content)).ShouldBe(CommentOutcome.Created);

    _created.ShouldHaveSingleItem();
    content.Pull.CommentId.ShouldBe(9001);
    content.Pull.HasCommentBody(SHA256.HashData(Encoding.UTF8.GetBytes(_created[0]))).ShouldBeTrue();
    _saves.ShouldBe(1);
  }

  [Fact]
  public async Task Comment_Deleted_Once_ShouldDoNothing()
  {
    var content = Content(Config(("behavior", ConfigNode.FromText("once"))));
    var hash = new byte[PullRequest.CommentBodyHashLength];
    content.Pull.RecordComment(77, hash, HeadSha, Now);
    Deleted(77);

    (await PublishAsync(content)).ShouldBe(CommentOutcome.Gone);

    _created.ShouldBeEmpty();
    // The identifier stays: forgotten, the next notification would search, find nothing and write the comment again.
    content.Pull.CommentId.ShouldBe(77);
    content.Pull.CommentBodyHash.ShouldBe(hash);
  }

  // F1. The known comment was deleted on the forge; the run that wrote it again could not save its identifier.
  // The replay finds the comment that run wrote, by its marker, instead of writing a second one.
  [Fact]
  public async Task Comment_KnownCommentDeletedThenSaveFailed_ShouldNeverWriteASecondComment()
  {
    A.CallTo(() => _pullRequests.UpdateAsync(A<PullRequest>._, A<CancellationToken>._))
     .Throws(new ConflictException("General.ConcurrentChange", "changed in the meantime"))
     .Once();
    var first = Content();
    first.Pull.RecordComment(77, new byte[PullRequest.CommentBodyHashLength], HeadSha, Now);
    Deleted(77);

    await Should.ThrowAsync<ConflictException>(() => PublishAsync(first));

    var written = _created.ShouldHaveSingleItem();
    _onTheForge.Add(new ForgeIssueComment(9001, written, WrittenByThisApplication: true));

    // The replay reads the pull request again: it still holds the identifier of the deleted comment.
    var replay = Content();
    replay.Pull.RecordComment(77, new byte[PullRequest.CommentBodyHashLength], HeadSha, Now);

    (await PublishAsync(replay)).ShouldBe(CommentOutcome.Unchanged);

    _created.ShouldHaveSingleItem();
    replay.Pull.CommentId.ShouldBe(9001);
    // The save of the replay; the one that failed is not counted.
    _saves.ShouldBe(1);
  }

  // F2b. The comment found by its marker disappears before it is updated: behavior once writes no new one, and
  // keeps its identifier so that the next notification does not write one either.
  [Fact]
  public async Task Comment_FoundByItsMarkerThenDeleted_Once_ShouldWriteNoNewComment()
  {
    _onTheForge.Add(new ForgeIssueComment(13, CommentComposer.Marker + "\nstale", WrittenByThisApplication: true));
    Deleted(13);
    var content = Content(Config(("behavior", ConfigNode.FromText("once"))));

    (await PublishAsync(content)).ShouldBe(CommentOutcome.Gone);

    _created.ShouldBeEmpty();
    content.Pull.CommentId.ShouldBe(13);

    _onTheForge.Clear();

    (await PublishAsync(content)).ShouldBe(CommentOutcome.Gone);

    _created.ShouldBeEmpty();
  }

  [Fact]
  public async Task Comment_FoundByItsMarkerThenDeleted_DefaultBehavior_ShouldWriteItAgain()
  {
    _onTheForge.Add(new ForgeIssueComment(13, CommentComposer.Marker + "\nstale", WrittenByThisApplication: true));
    Deleted(13);
    var content = Content();

    (await PublishAsync(content)).ShouldBe(CommentOutcome.Created);

    _created.ShouldHaveSingleItem();
    content.Pull.CommentId.ShouldBe(9001);
  }

  // After a restoration of the database, or a save that lost a race: the comment is found again, never doubled.
  [Fact]
  public async Task Comment_UnknownId_ShouldFindByMarkerAndBotAuthor()
  {
    _onTheForge.AddRange(
      [
        // Someone quoting the marker, a comment of the application without it, then the comment itself, twice.
        new ForgeIssueComment(11, CommentComposer.Marker + "\nquoted by a human", WrittenByThisApplication: false),
        new ForgeIssueComment(12, "Another message of the application", WrittenByThisApplication: true),
        new ForgeIssueComment(14, CommentComposer.Marker + "\n## Coverage Report\nolder duplicate", WrittenByThisApplication: true),
        new ForgeIssueComment(13, CommentComposer.Marker + "\n## Coverage Report\nstale", WrittenByThisApplication: true),
        new ForgeIssueComment(15, "prefix " + CommentComposer.Marker, WrittenByThisApplication: true),
        new ForgeIssueComment(16, CommentComposer.Marker + "-not-the-marker\nbody", WrittenByThisApplication: true),
      ]
    );
    var content = Content();

    (await PublishAsync(content)).ShouldBe(CommentOutcome.Updated);

    _created.ShouldBeEmpty();
    // The lowest identifier among its own, so that every run picks the same one.
    _updated.ShouldHaveSingleItem().Id.ShouldBe(13);
    content.Pull.CommentId.ShouldBe(13);
    _saves.ShouldBe(1);
  }

  [Fact]
  public async Task Comment_UnknownIdAndTheSameBodyOnTheForge_ShouldAdoptItWithoutWriting()
  {
    var content = Content();
    _onTheForge.Add(new ForgeIssueComment(13, Expected(content), WrittenByThisApplication: true));

    (await PublishAsync(content)).ShouldBe(CommentOutcome.Unchanged);

    _created.ShouldBeEmpty();
    _updated.ShouldBeEmpty();
    content.Pull.CommentId.ShouldBe(13);
    _saves.ShouldBe(1);
  }

  // The comment found exists: require_changes is for the first publication only.
  [Fact]
  public async Task Comment_FoundByItsMarker_ShouldBeUpdatedWhateverRequireChangesSays()
  {
    _onTheForge.Add(new ForgeIssueComment(13, CommentComposer.Marker + "\nstale", WrittenByThisApplication: true));
    var content = Content(Config(("require_changes", ConfigNode.FromText("uncovered_patch"))), Comparison(patch: new CoverageTotals(40, 0, 0, 0, 0)));

    (await PublishAsync(content)).ShouldBe(CommentOutcome.Updated);
  }

  [Theory]
  [InlineData("uncovered_patch", true, false)]
  [InlineData("uncovered_patch", false, true)]
  [InlineData("false", true, true)]
  public async Task Comment_FirstPublication_ShouldAskRequireChanges(string requireChanges, bool patchFullyCovered, bool published)
  {
    var patch = patchFullyCovered ? new CoverageTotals(40, 0, 0, 0, 0) : new CoverageTotals(29, 9, 2, 0, 0);
    var value = requireChanges == "false" ? ConfigNode.FromBoolean(false) : ConfigNode.FromText(requireChanges);
    var content = Content(Config(("require_changes", value)), Comparison(patch: patch));

    (await PublishAsync(content)).ShouldBe(published ? CommentOutcome.Created : CommentOutcome.Skipped);

    _created.Count.ShouldBe(published ? 1 : 0);
    content.Pull.CommentId.ShouldBe(published ? 9001 : null);
    // The base is recorded either way.
    content.Pull.ForkPointSha.ShouldBe(BaseSha);
    _saves.ShouldBe(1);
  }

  // coverage_drop compares with the threshold of the default project status (ARCHITECTURE §8.4).
  [Theory]
  [InlineData(0, true)]
  [InlineData(5, false)]
  public async Task Comment_CoverageDrop_ShouldUseTheThresholdOfTheDefaultProjectStatus(int threshold, bool published)
  {
    var config = CoverageConfig.Create(
      ConfigMerger.Merge(
        CoverageConfigDefaults.Root,
        ConfigNode.FromMap(
          ("comment", ConfigNode.FromMap(("require_changes", ConfigNode.FromText("coverage_drop")))),
          ("coverage", ConfigNode.FromMap(("status", ConfigNode.FromMap(("project", ConfigNode.FromMap(("default", ConfigNode.FromMap(("threshold", ConfigNode.FromNumber(threshold))))))))))
        )
      )
    );

    // 81.23% on the base, 80.90% on the head: a drop of a third of a point.
    var content = Content(config, Comparison(head: BaseTotals, @base: HeadTotals));

    (await PublishAsync(content)).ShouldBe(published ? CommentOutcome.Created : CommentOutcome.Skipped);
  }

  [Fact]
  public async Task Comment_BooleanCommentInTheConfiguration_ShouldDisableIt()
  {
    var config = CoverageConfig.Create(ConfigMerger.Merge(CoverageConfigDefaults.Root, ConfigNode.FromMap(("comment", ConfigNode.FromBoolean(true)))));

    (await PublishAsync(Content(config))).ShouldBe(CommentOutcome.Skipped);

    A.CallTo(_gateway).MustNotHaveHappened();
  }

  [Fact]
  public async Task Comment_DisabledForTheOrganization_ShouldOnlyRecordTheBase()
  {
    var content = Content();

    (await PublishAsync(content, enabled: false)).ShouldBe(CommentOutcome.Skipped);

    A.CallTo(_gateway).MustNotHaveHappened();
    content.Pull.CommentId.ShouldBeNull();
    content.Pull.ForkPointSha.ShouldBe(BaseSha);
    content.Pull.ComparedToSha.ShouldBe(BaseSha);
    _saves.ShouldBe(1);
  }

  [Theory]
  [InlineData(ForgePullState.Closed)]
  [InlineData(ForgePullState.Merged)]
  public async Task Comment_PullRequestNoLongerOpen_ShouldNotComment(ForgePullState state)
  {
    var pull = Pull();
    pull.Refresh(new ForgePullInfo(123, state, "Add a feature", "octocat", HeadSha, "feature/x", "main", BaseSha), Now);

    (await PublishAsync(Content(pull: pull))).ShouldBe(CommentOutcome.Skipped);

    A.CallTo(_gateway).MustNotHaveHappened();
  }

  [Theory]
  [InlineData(2, 1, false)]
  [InlineData(2, 2, true)]
  [InlineData(0, 0, true)]
  public async Task Comment_AfterNBuilds_ShouldWaitForTheSessionsOfTheReport(int afterNBuilds, int sessions, bool published)
  {
    var content = Content(
      Config(("after_n_builds", ConfigNode.FromNumber(afterNBuilds))),
      sessions: [.. Enumerable.Range(1, sessions).Select(static i => i == 1 ? Uploaded(i, "a") : CarriedForward(i, OlderSha, "b"))]
    );

    (await PublishAsync(content)).ShouldBe(published ? CommentOutcome.Created : CommentOutcome.Skipped);
  }

  // comment.branches names the branches a pull request targets.
  [Theory]
  [InlineData("main", "main", true)]
  [InlineData("release/*", "main", false)]
  [InlineData("release/*", "release/2026", true)]
  public async Task Comment_Branches_ShouldMatchTheBaseBranch(string pattern, string baseBranch, bool published)
  {
    var content = Content(Config(("branches", ConfigNode.FromList(ConfigNode.FromText(pattern)))), pull: Pull(baseBranch));

    (await PublishAsync(content)).ShouldBe(published ? CommentOutcome.Created : CommentOutcome.Skipped);
  }

  // A locked pull request takes no comment: the statuses are published already, nothing fails.
  [Fact]
  public async Task Comment_RefusedByTheForge_ShouldRecordNoComment()
  {
    A.CallTo(() => _gateway.CreateIssueCommentAsync(Address, 123, A<string>._, A<CancellationToken>._)).Returns((long?)null);
    var content = Content();

    (await PublishAsync(content)).ShouldBe(CommentOutcome.Skipped);

    content.Pull.CommentId.ShouldBeNull();
  }

  [Fact]
  public async Task Comment_NothingChangedOnThePullRequest_ShouldNotSaveIt()
  {
    var content = Content();
    await PublishAsync(content);

    await PublishAsync(content);
    await PublishAsync(content, enabled: false);

    _saves.ShouldBe(1);
  }

  [Fact]
  public async Task Comment_BaseWithoutReport_ShouldRecordTheForkPointOnly()
  {
    var content = Content(withoutBase: true);

    await PublishAsync(content);

    content.Pull.ForkPointSha.ShouldBe(BaseSha);
    content.Pull.ComparedToSha.ShouldBeNull();
  }

  // The forge was called first: the save that loses a race fails the job, and the next run finds the comment by its marker.
  [Fact]
  public async Task Comment_SaveLosesARace_ShouldThrowAfterTheCommentWasWritten()
  {
    A.CallTo(() => _pullRequests.UpdateAsync(A<PullRequest>._, A<CancellationToken>._))
     .Throws(new ConflictException("General.ConcurrentChange", "changed in the meantime"));

    await Should.ThrowAsync<ConflictException>(() => PublishAsync(Content()));

    _created.ShouldHaveSingleItem();
  }

  [Fact]
  public async Task Comment_TitleAndLinks_ShouldComeFromTheSettings()
  {
    _options.CommentTitle = "Team coverage";

    await PublishAsync(Content());

    _created.ShouldHaveSingleItem().ShouldContain("## [Team coverage](https://coverage.example.com/github/octo/repo/pull/123) Report");
  }

  [Fact]
  public async Task Comment_NoPullRequest_ShouldDoNothing()
  {
    var content = Content();
    var context = content.Context with { Resolution = content.Context.Resolution with { PullRequest = null } };

    (await _publisher.PublishAsync(_repository, Forge.GitHub, content.Head, content.Config, context, 0, false, true, Ct)).ShouldBe(CommentOutcome.Skipped);

    A.CallTo(_gateway).MustNotHaveHappened();
    _saves.ShouldBe(0);
  }

  [Fact]
  public async Task Comment_NullArguments_ShouldThrow()
  {
    var content = Content();

    await Should.ThrowAsync<ArgumentNullException>(() => _publisher.PublishAsync(null!, Forge.GitHub, content.Head, content.Config, content.Context, 0, false, true, Ct));
    await Should.ThrowAsync<ArgumentNullException>(() => _publisher.PublishAsync(_repository, Forge.GitHub, null!, content.Config, content.Context, 0, false, true, Ct));
    await Should.ThrowAsync<ArgumentNullException>(() => _publisher.PublishAsync(_repository, Forge.GitHub, content.Head, null!, content.Context, 0, false, true, Ct));
    await Should.ThrowAsync<ArgumentNullException>(() => _publisher.PublishAsync(_repository, Forge.GitHub, content.Head, content.Config, null!, 0, false, true, Ct));
  }

  // new: the comment the pull request records and every comment of the application found by its marker are deleted,
  // then one comment is written at the bottom of the pull request.
  [Fact]
  public async Task Comment_New_ShouldDeleteAndCreate()
  {
    _onTheForge.AddRange(
      [
        new ForgeIssueComment(11, CommentComposer.Marker + "\nquoted by a human", WrittenByThisApplication: false),
        new ForgeIssueComment(13, CommentComposer.Marker + "\nleft by spammy", WrittenByThisApplication: true),
        new ForgeIssueComment(12, "Another message of the application", WrittenByThisApplication: true),
      ]
    );
    var content = Content(Config(("behavior", ConfigNode.FromText("new"))));
    content.Pull.RecordComment(77, new byte[PullRequest.CommentBodyHashLength], HeadSha, Now);

    (await PublishAsync(content)).ShouldBe(CommentOutcome.Created);

    _deleted.ShouldBe([13, 77], ignoreOrder: true);
    _created.ShouldHaveSingleItem().ShouldBe(Expected(content));
    _updated.ShouldBeEmpty();
    content.Pull.CommentId.ShouldBe(9001);
    _saves.ShouldBe(1);
  }

  // As Codecov does: each accepted notification writes again, even with the same body on the same head.
  [Fact]
  public async Task Comment_New_SameBodyOnTheSameHead_ShouldWriteAgain()
  {
    var content = Content(Config(("behavior", ConfigNode.FromText("new"))));
    await PublishAsync(content);
    _onTheForge.Add(new ForgeIssueComment(9001, _created[0], WrittenByThisApplication: true));

    (await PublishAsync(content)).ShouldBe(CommentOutcome.Created);

    _deleted.ShouldBe([9001]);
    _created.Count.ShouldBe(2);
    content.Pull.CommentId.ShouldBe(9002);
  }

  [Fact]
  public async Task Comment_New_DeletionRefused_ShouldStillCreate()
  {
    A.CallTo(() => _gateway.DeleteIssueCommentAsync(Address, 77, A<CancellationToken>._)).Returns(ForgeCommentDeletion.Refused);
    var content = Content(Config(("behavior", ConfigNode.FromText("new"))));
    content.Pull.RecordComment(77, new byte[PullRequest.CommentBodyHashLength], HeadSha, Now);

    (await PublishAsync(content)).ShouldBe(CommentOutcome.Created);

    _created.ShouldHaveSingleItem();
    content.Pull.CommentId.ShouldBe(9001);
  }

  // The deleted comment's identifier stays: the next notification does not take the pull request for one never
  // commented, and require_changes is not asked again.
  [Fact]
  public async Task Comment_New_CreateRefused_ShouldKeepTheDeletedIdentifier()
  {
    A.CallTo(() => _gateway.CreateIssueCommentAsync(Address, 123, A<string>._, A<CancellationToken>._)).Returns((long?)null);
    var content = Content(Config(("behavior", ConfigNode.FromText("new"))));
    content.Pull.RecordComment(77, new byte[PullRequest.CommentBodyHashLength], HeadSha, Now);

    (await PublishAsync(content)).ShouldBe(CommentOutcome.Skipped);

    _deleted.ShouldBe([77]);
    content.Pull.CommentId.ShouldBe(77);
  }

  [Fact]
  public async Task Comment_Spammy_ShouldAlwaysCreate()
  {
    var content = Content(Config(("behavior", ConfigNode.FromText("spammy"))));

    for (var i = 0; i < 3; i++)
    {
      (await PublishAsync(content)).ShouldBe(CommentOutcome.Created);
      _onTheForge.Add(new ForgeIssueComment(9001 + i, _created[i], WrittenByThisApplication: true));
    }

    _created.Count.ShouldBe(3);
    _created.ShouldAllBe(body => body == Expected(content));
    _deleted.ShouldBeEmpty();
    _updated.ShouldBeEmpty();
    content.Pull.CommentId.ShouldBe(9003);
  }

  [Theory]
  [InlineData("new")]
  [InlineData("spammy")]
  public async Task Comment_NewOrSpammy_FirstPublication_ShouldAskRequireChanges(string behavior)
  {
    var content = Content(
      Config(("behavior", ConfigNode.FromText(behavior)), ("require_changes", ConfigNode.FromText("uncovered_patch"))),
      Comparison(patch: new CoverageTotals(40, 0, 0, 0, 0))
    );

    (await PublishAsync(content)).ShouldBe(CommentOutcome.Skipped);

    _created.ShouldBeEmpty();
    _deleted.ShouldBeEmpty();

    // Once a comment exists, require_changes is not asked again.
    _onTheForge.Add(new ForgeIssueComment(13, CommentComposer.Marker + "\nearlier", WrittenByThisApplication: true));

    (await PublishAsync(content)).ShouldBe(CommentOutcome.Created);
  }

  // The file counts come from the commits themselves, not from the sample.
  private static string Expected(CommentContent content) =>
    CommentComposer.Compose(content with { HeadFileCount = content.Head.Files, BaseFileCount = content.Context.Resolution.ComparedTo?.Files });

  private static PullRequest PullAt(GitSha head) =>
    PullRequest.Create(RepositoryId, new ForgePullInfo(123, ForgePullState.Open, "Add a feature", "octocat", head, "feature/x", "main", BaseSha), Now);

  private Task<CommentOutcome> PublishAsync(CommentContent content, bool enabled = true) =>
    _publisher.PublishAsync(_repository, Forge.GitHub, content.Head, content.Config, content.Context, content.FailedUploadCount, content.ConfigInvalid, enabled, Ct);

  private void Deleted(long commentId)
  {
    A.CallTo(() => _gateway.UpdateIssueCommentAsync(Address, commentId, A<string>._, A<CancellationToken>._)).Returns(false);
  }

  #endregion
}
