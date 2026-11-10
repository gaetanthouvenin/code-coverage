// ------------------------------------------------------------------------------------------------
// <copyright file="CompleteUploadsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Api.Tests.Support;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Api.Tests.Uploads;

[Collection(CodeCoverageApiCollection.Name)]
public sealed class CompleteUploadsTests
{
  #region Fields

  private const string Token = "cvc_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG";

  private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Sha = GitSha.Parse(new string('a', GitSha.Length));

  private readonly CodeCoverageApiFactory _factory;

  private readonly IOrganizationScope _scope = A.Fake<IOrganizationScope>();

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly IUploadCredentialCache _cache = A.Fake<IUploadCredentialCache>();

  private readonly IUploadTokenRepository _tokens = A.Fake<IUploadTokenRepository>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IUploadRepository _uploads = A.Fake<IUploadRepository>();

  private readonly IJobQueue _jobQueue = A.Fake<IJobQueue>();

  private readonly IForgeGatewayProvider _gateways = A.Fake<IForgeGatewayProvider>();

  private readonly IGitHubAppGateway _gitHub = A.Fake<IGitHubAppGateway>();

  private readonly List<JobRequest> _queued = [];

  #endregion

  #region Ctors

  public CompleteUploadsTests(CodeCoverageApiFactory factory)
  {
    _factory = factory;

    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);

    var commit = Commit.Create(5, Sha, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(commit, 9L);

    var processed = Upload.Receive(9, 1, "k1", [], null, null, null, null, null, null, null, 3, PrincipalKind.OrganizationToken, Now);
    processed.MarkProcessed(1, 1, 0, default, ProcessingLog.Empty, Now);
    var received = Upload.Receive(9, 2, "k2", [], null, null, null, null, null, null, null, 3, PrincipalKind.OrganizationToken, Now);

    A.CallTo(() => _scope.TryEnterAsync(A<Forge>._, A<string?>._, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _organization.IsKnown).Returns(true);
    A.CallTo(() => _cache.Find(A<byte[]>._)).Returns(new UploadTokenIdentity(3, UploadTokenScope.Organization, null, null));
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _commits.FindByShaAsync(5, Sha, A<CancellationToken>._)).Returns(commit);
    A.CallTo(() => _uploads.ListByCommitAsync(9, A<CancellationToken>._)).Returns([processed, received]);
    A.CallTo(() => _jobQueue.EnqueueAsync(A<JobRequest>._, A<CancellationToken>._))
     .Invokes((JobRequest job, CancellationToken _) => _queued.Add(job))
     .Returns(1L);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // The acceptance of rule 3: the request answers with the counts and queues the job; nothing calls GitHub.
  [Fact]
  public async Task Post_WithCredential_ShouldReturnTheCountsAndQueueTheForcedNotificationWithoutCallingGitHub()
  {
    await using var host = Host();
    using var client = host.CreateClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", Token);

    using var response = await client.PostAsync(Path("github", Sha.Value), null, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
    var root = json.RootElement;

    root.GetProperty("total").GetInt32().ShouldBe(2);
    root.GetProperty("processed").GetInt32().ShouldBe(1);
    root.GetProperty("processing").GetInt32().ShouldBe(1);
    root.GetProperty("failed").GetInt32().ShouldBe(0);

    var job = _queued.ShouldHaveSingleItem();
    job.Type.ShouldBe(JobTypes.NotifyCommit);
    job.PayloadJson.ShouldBe("""{"CommitId":9,"Force":true}""");
    A.CallTo(_gateways).MustNotHaveHappened();
    A.CallTo(_gitHub).MustNotHaveHappened();
  }

  [Fact]
  public async Task Post_WithoutCredential_ShouldReturn401AndQueueNothing()
  {
    await using var host = Host();
    using var client = host.CreateClient();

    using var response = await client.PostAsync(Path("github", Sha.Value), null, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    _queued.ShouldBeEmpty();
  }

  // A token of another organization is unknown under the filter of the organization of the address.
  [Fact]
  public async Task Post_CredentialOfAnotherOrganization_ShouldReturn401AndQueueNothing()
  {
    A.CallTo(() => _cache.Find(A<byte[]>._)).Returns(null);
    A.CallTo(() => _tokens.FindByHashAsync(A<byte[]>._, A<CancellationToken>._)).Returns((UploadToken?)null);

    await using var host = Host();
    using var client = host.CreateClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", Token);

    using var response = await client.PostAsync(Path("github", Sha.Value), null, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    _queued.ShouldBeEmpty();
  }

  [Theory]
  [InlineData("svn", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
  [InlineData("github", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
  [InlineData("github", "abc1234")]
  public async Task Post_UnknownForgeOrCommit_ShouldReturn404(string forge, string sha)
  {
    A.CallTo(() => _commits.FindByShaAsync(5, A<GitSha>.That.Not.IsEqualTo(Sha), A<CancellationToken>._)).Returns((Commit?)null);

    await using var host = Host();
    using var client = host.CreateClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", Token);

    using var response = await client.PostAsync(Path(forge, sha), null, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    _queued.ShouldBeEmpty();
  }

  private static string Path(string forge, string sha) => $"{CodeCoverageApi.Prefix}/repos/{forge}/octo-org/app/commits/{sha}/uploads-complete";

  private WebApplicationFactory<Program> Host() =>
    _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
    {
      services.AddSingleton(_scope);
      services.AddSingleton(_organization);
      services.AddSingleton(_cache);
      services.AddSingleton(_tokens);
      services.AddSingleton(_repositories);
      services.AddSingleton(_commits);
      services.AddSingleton(_uploads);
      services.AddSingleton(_jobQueue);
      services.AddSingleton(_gateways);
      services.AddSingleton(_gitHub);
      services.AddSingleton(A.Fake<IClock>());
    }));

  #endregion
}