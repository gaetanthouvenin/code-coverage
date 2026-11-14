// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubAppGatewayCommentTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.GitHub;

public sealed class GitHubAppGatewayCommentTests : IDisposable
{
  #region Fields

  private const long Installation = 42;

  private const string InstallationToken = "ghs_installation";

  private const string Comments = "/repositories/1296269/issues/12/comments";

  private const string Comment = "/repositories/1296269/issues/comments/77";

  private static readonly RSA Key = RSA.Create(2048);

  private static readonly ForgeRepositoryRef Repository = new(Forge.GitHub, "42", "1296269");

  private readonly GitHubApiHarness _github = new();

  #endregion

  #region Ctors

  public GitHubAppGatewayCommentTests()
  {
    _github.UsePrivateKey(Key);
    _github.Options.AppSlug = "code-coverage";
    _github.Server
           .Given(Request.Create().WithPath($"/app/installations/{Installation}/access_tokens").UsingPost())
           .RespondWith(
             Response.Create()
                     .WithStatusCode(201)
                     .WithBody("{\"token\":\"" + InstallationToken + "\",\"expires_at\":\"" + DateTimeOffset.UtcNow.AddHours(1).ToString("O", CultureInfo.InvariantCulture) + "\"}")
           );
  }

  #endregion

  #region Properties

  private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task ListIssueComments_ShouldTellWhichOnesThisApplicationWrote()
  {
    GivenPage(
      1,
      """
      [
        {"id":11,"body":"A human wrote this","user":{"login":"octocat","type":"User"}},
        {"id":12,"body":"<!-- code-coverage:comment -->\n## Coverage Report","user":{"login":"code-coverage[bot]","type":"Bot"}},
        {"id":13,"body":"From another application","user":{"login":"other-app[bot]","type":"Bot"}},
        {"id":14,"body":null,"user":null},
        {"id":15,"body":"Case does not matter","user":{"login":"Code-Coverage[bot]"}}
      ]
      """
    );

    var comments = await _github.Gateway.ListIssueCommentsAsync(Repository, 12, Cancellation);

    comments.Select(static c => (c.Id, c.WrittenByThisApplication)).ShouldBe([(11, false), (12, true), (13, false), (14, false), (15, true)]);
    comments[1].Body.ShouldBe("<!-- code-coverage:comment -->\n## Coverage Report");
    comments[3].Body.ShouldBeEmpty();

    var request = _github.Server.LogEntries.Select(static e => e.RequestMessage!).Single(static r => r.Path == Comments);
    request.Query!["per_page"].ShouldBe(["100"]);
    request.Headers!["Authorization"].ShouldHaveSingleItem().ShouldBe($"Bearer {InstallationToken}");
  }

  // Without the slug of the App, its bot account is unknown: no comment is taken for one of its own.
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  public async Task ListIssueComments_WithoutTheSlugOfTheApp_ShouldTakeNoCommentForItsOwn(string? slug)
  {
    _github.Options.AppSlug = slug;
    GivenPage(1, """[{"id":12,"body":"x","user":{"login":"code-coverage[bot]"}},{"id":13,"body":"y","user":{"login":"[bot]"}}]""");

    var comments = await _github.Gateway.ListIssueCommentsAsync(Repository, 12, Cancellation);

    comments.ShouldAllBe(static c => !c.WrittenByThisApplication);
  }

  // A busy pull request has hundreds of comments: a listing cut short could miss the one to update.
  [Fact]
  public async Task ListIssueComments_FullPage_ShouldReadTheNextOne()
  {
    GivenPage(1, Page(1, 100));
    GivenPage(2, Page(101, 100));
    GivenPage(3, Page(201, 7));

    var comments = await _github.Gateway.ListIssueCommentsAsync(Repository, 12, Cancellation);

    comments.Count.ShouldBe(207);
    comments[^1].Id.ShouldBe(207);
  }

  [Fact]
  public async Task ListIssueComments_PullRequestUnknownToGitHub_ShouldBeEmpty()
  {
    _github.Server.Given(Request.Create().WithPath(Comments).UsingGet()).RespondWith(Response.Create().WithStatusCode(404));

    (await _github.Gateway.ListIssueCommentsAsync(Repository, 12, Cancellation)).ShouldBeEmpty();
  }

  [Theory]
  [InlineData(403)]
  [InlineData(500)]
  public async Task ListIssueComments_Refused_ShouldThrowBadGateway(int status)
  {
    _github.Server.Given(Request.Create().WithPath(Comments).UsingGet()).RespondWith(Response.Create().WithStatusCode(status));

    await Should.ThrowAsync<BadGatewayException>(() => _github.Gateway.ListIssueCommentsAsync(Repository, 12, Cancellation));
  }

  [Fact]
  public async Task CreateIssueComment_ShouldPostTheBodyAndGiveTheIdentifier()
  {
    _github.Server.Given(Request.Create().WithPath(Comments).UsingPost())
           .RespondWith(Response.Create().WithStatusCode(201).WithBody("""{"id":123456789012,"body":"ignored","user":{"login":"code-coverage[bot]"}}"""));

    var id = await _github.Gateway.CreateIssueCommentAsync(Repository, 12, "<!-- code-coverage:comment -->\n## Coverage Report", Cancellation);

    id.ShouldBe(123456789012);

    var request = _github.Server.LogEntries.Select(static e => e.RequestMessage!).Single(static r => r.Path == Comments);
    request.Method.ShouldBe("POST");
    request.Headers!["Authorization"].ShouldHaveSingleItem().ShouldBe($"Bearer {InstallationToken}");
    using var body = JsonDocument.Parse(request.Body!);
    body.RootElement.GetProperty("body").GetString().ShouldBe("<!-- code-coverage:comment -->\n## Coverage Report");
  }

  // A pull request that is gone or locked takes no comment: not a failure of the notification.
  [Theory]
  [InlineData(403)]
  [InlineData(404)]
  [InlineData(410)]
  [InlineData(422)]
  public async Task CreateIssueComment_PullRequestThatTakesNoComment_ShouldGiveNoIdentifier(int status)
  {
    _github.Server.Given(Request.Create().WithPath(Comments).UsingPost()).RespondWith(Response.Create().WithStatusCode(status).WithBody("{}"));

    (await _github.Gateway.CreateIssueCommentAsync(Repository, 12, "body", Cancellation)).ShouldBeNull();
  }

  [Fact]
  public async Task CreateIssueComment_GitHubDown_ShouldThrowBadGateway()
  {
    _github.Server.Given(Request.Create().WithPath(Comments).UsingPost()).RespondWith(Response.Create().WithStatusCode(500));

    await Should.ThrowAsync<BadGatewayException>(() => _github.Gateway.CreateIssueCommentAsync(Repository, 12, "body", Cancellation));
  }

  // F2. GitHub may have written the comment and lost its answer: sending it again would write it twice. The job
  // fails, and its next run finds the comment by its marker.
  [Theory]
  [InlineData(500)]
  [InlineData(502)]
  [InlineData(503)]
  [InlineData(504)]
  public async Task CreateIssueComment_TransientFailure_ShouldBeSentOnce(int status)
  {
    _github.Server.Given(Request.Create().WithPath(Comments).UsingPost()).RespondWith(Response.Create().WithStatusCode(status));

    await Should.ThrowAsync<BadGatewayException>(() => _github.Gateway.CreateIssueCommentAsync(Repository, 12, "body", Cancellation));

    _github.Server.LogEntries.Count(static e => e.RequestMessage!.Path == Comments).ShouldBe(1);
  }

  // An update writes the same body again: it is still retried.
  [Fact]
  public async Task UpdateIssueComment_TransientFailure_ShouldBeRetried()
  {
    const string Scenario = "transient";
    _github.Server.Given(Request.Create().WithPath(Comment).UsingPatch())
           .InScenario(Scenario)
           .WillSetStateTo("recovered")
           .RespondWith(Response.Create().WithStatusCode(502));
    _github.Server.Given(Request.Create().WithPath(Comment).UsingPatch())
           .InScenario(Scenario)
           .WhenStateIs("recovered")
           .RespondWith(Response.Create().WithStatusCode(200).WithBody("""{"id":77}"""));

    (await _github.Gateway.UpdateIssueCommentAsync(Repository, 77, "body", Cancellation)).ShouldBeTrue();

    _github.Server.LogEntries.Count(static e => e.RequestMessage!.Path == Comment).ShouldBe(2);
  }

  [Fact]
  public async Task UpdateIssueComment_ShouldPatchTheBody()
  {
    _github.Server.Given(Request.Create().WithPath(Comment).UsingPatch()).RespondWith(Response.Create().WithStatusCode(200).WithBody("""{"id":77}"""));

    (await _github.Gateway.UpdateIssueCommentAsync(Repository, 77, "new body", Cancellation)).ShouldBeTrue();

    var request = _github.Server.LogEntries.Select(static e => e.RequestMessage!).Single(static r => r.Path == Comment);
    request.Method.ShouldBe("PATCH");
    using var body = JsonDocument.Parse(request.Body!);
    body.RootElement.GetProperty("body").GetString().ShouldBe("new body");
  }

  // Deleted on GitHub: told as a value, the caller decides whether to write it again.
  [Fact]
  public async Task UpdateIssueComment_Deleted_ShouldSaySo()
  {
    _github.Server.Given(Request.Create().WithPath(Comment).UsingPatch()).RespondWith(Response.Create().WithStatusCode(404));

    (await _github.Gateway.UpdateIssueCommentAsync(Repository, 77, "new body", Cancellation)).ShouldBeFalse();
  }

  [Theory]
  [InlineData(403)]
  [InlineData(422)]
  [InlineData(500)]
  public async Task UpdateIssueComment_Refused_ShouldThrowBadGateway(int status)
  {
    _github.Server.Given(Request.Create().WithPath(Comment).UsingPatch()).RespondWith(Response.Create().WithStatusCode(status));

    await Should.ThrowAsync<BadGatewayException>(() => _github.Gateway.UpdateIssueCommentAsync(Repository, 77, "new body", Cancellation));
  }

  [Fact]
  public async Task DeleteIssueComment_ShouldSendADelete()
  {
    _github.Server.Given(Request.Create().WithPath(Comment).UsingDelete()).RespondWith(Response.Create().WithStatusCode(204));

    (await _github.Gateway.DeleteIssueCommentAsync(Repository, 77, Cancellation)).ShouldBe(ForgeCommentDeletion.Deleted);

    _github.Server.LogEntries.Single(static e => e.RequestMessage!.Path == Comment).RequestMessage!.Method.ShouldBe("DELETE");
  }

  // Deleted already: the caller goes on as if it had deleted it.
  [Fact]
  public async Task DeleteIssueComment_NotFound_ShouldSayAbsent()
  {
    _github.Server.Given(Request.Create().WithPath(Comment).UsingDelete()).RespondWith(Response.Create().WithStatusCode(404));

    (await _github.Gateway.DeleteIssueCommentAsync(Repository, 77, Cancellation)).ShouldBe(ForgeCommentDeletion.Absent);
  }

  [Theory]
  [InlineData(403)]
  [InlineData(422)]
  public async Task DeleteIssueComment_Refused_ShouldSayRefused(int status)
  {
    _github.Server.Given(Request.Create().WithPath(Comment).UsingDelete()).RespondWith(Response.Create().WithStatusCode(status));

    (await _github.Gateway.DeleteIssueCommentAsync(Repository, 77, Cancellation)).ShouldBe(ForgeCommentDeletion.Refused);
  }

  [Fact]
  public async Task DeleteIssueComment_TransientFailure_ShouldBeRetried()
  {
    const string Scenario = "transient-delete";
    _github.Server.Given(Request.Create().WithPath(Comment).UsingDelete())
           .InScenario(Scenario)
           .WillSetStateTo("recovered")
           .RespondWith(Response.Create().WithStatusCode(502));
    _github.Server.Given(Request.Create().WithPath(Comment).UsingDelete())
           .InScenario(Scenario)
           .WhenStateIs("recovered")
           .RespondWith(Response.Create().WithStatusCode(204));

    (await _github.Gateway.DeleteIssueCommentAsync(Repository, 77, Cancellation)).ShouldBe(ForgeCommentDeletion.Deleted);

    _github.Server.LogEntries.Count(static e => e.RequestMessage!.Path == Comment).ShouldBe(2);
  }

  [Fact]
  public async Task DeleteIssueComment_InvalidArguments_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(() => _github.Gateway.DeleteIssueCommentAsync(null!, 77, Cancellation));
    await Should.ThrowAsync<ArgumentOutOfRangeException>(() => _github.Gateway.DeleteIssueCommentAsync(Repository, 0, Cancellation));
    await Should.ThrowAsync<ArgumentException>(() => _github.Gateway.DeleteIssueCommentAsync(new ForgeRepositoryRef(Forge.GitLab, "42", "1296269"), 77, Cancellation));
  }

  [Fact]
  public async Task Comments_InvalidArguments_ShouldThrow()
  {
    var other = new ForgeRepositoryRef(Forge.GitLab, "42", "1296269");

    await Should.ThrowAsync<ArgumentNullException>(() => _github.Gateway.ListIssueCommentsAsync(null!, 12, Cancellation));
    await Should.ThrowAsync<ArgumentOutOfRangeException>(() => _github.Gateway.ListIssueCommentsAsync(Repository, 0, Cancellation));
    await Should.ThrowAsync<ArgumentException>(() => _github.Gateway.ListIssueCommentsAsync(other, 12, Cancellation));
    await Should.ThrowAsync<ArgumentNullException>(() => _github.Gateway.CreateIssueCommentAsync(null!, 12, "body", Cancellation));
    await Should.ThrowAsync<ArgumentNullException>(() => _github.Gateway.CreateIssueCommentAsync(Repository, 12, null!, Cancellation));
    await Should.ThrowAsync<ArgumentOutOfRangeException>(() => _github.Gateway.CreateIssueCommentAsync(Repository, 0, "body", Cancellation));
    await Should.ThrowAsync<ArgumentException>(() => _github.Gateway.CreateIssueCommentAsync(other, 12, "body", Cancellation));
    await Should.ThrowAsync<ArgumentNullException>(() => _github.Gateway.UpdateIssueCommentAsync(null!, 77, "body", Cancellation));
    await Should.ThrowAsync<ArgumentNullException>(() => _github.Gateway.UpdateIssueCommentAsync(Repository, 77, null!, Cancellation));
    await Should.ThrowAsync<ArgumentOutOfRangeException>(() => _github.Gateway.UpdateIssueCommentAsync(Repository, 0, "body", Cancellation));
    await Should.ThrowAsync<ArgumentException>(() => _github.Gateway.UpdateIssueCommentAsync(other, 77, "body", Cancellation));
  }

  private static string Page(int first, int count) =>
    "[" + string.Join(',', Enumerable.Range(first, count).Select(static id => "{\"id\":" + id.ToString(CultureInfo.InvariantCulture) + ",\"body\":\"c\",\"user\":{\"login\":\"octocat\"}}")) + "]";

  private void GivenPage(int page, string body) =>
    _github.Server.Given(Request.Create().WithPath(Comments).WithParam("page", page.ToString(CultureInfo.InvariantCulture)).UsingGet())
           .RespondWith(Response.Create().WithStatusCode(200).WithBody(body));

  #endregion

  #region Implementation of IDisposable

  public void Dispose() => _github.Dispose();

  #endregion
}
