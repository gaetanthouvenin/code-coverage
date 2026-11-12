// ------------------------------------------------------------------------------------------------
// <copyright file="CommitActionsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Api.Tests.Support;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Api.Tests.Commits;

// Notify and reprocess through the API: an administrator of the repository, audited; the database is faked behind its ports.
[Collection(CodeCoverageApiCollection.Name)]
public sealed class CommitActionsTests
{
  #region Fields

  private const string Base = "/api/v1/repos/github/octo-org/app/commits";

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Sha = GitSha.Parse(new string('a', GitSha.Length));

  private readonly CodeCoverageApiFactory _factory;

  private readonly IOrganizationScope _scope = A.Fake<IOrganizationScope>();

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IRepositoryAccessService _access = A.Fake<IRepositoryAccessService>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IUploadRepository _uploads = A.Fake<IUploadRepository>();

  private readonly IJobQueue _jobs = A.Fake<IJobQueue>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  #endregion

  #region Ctors

  public CommitActionsTests(CodeCoverageApiFactory factory)
  {
    _factory = factory;

    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);
    var commit = Commit.Create(5, Sha, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(commit, 9L);

    A.CallTo(() => _scope.TryEnterAsync(A<Forge>._, A<string?>._, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _organization.Id).Returns(3L);
    A.CallTo(() => _organization.IsKnown).Returns(true);
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _access.CanAsync(ApiTokens.UserId, "2002", RepositoryPermission.Read, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _commits.FindByShaAsync(5, Sha, A<CancellationToken>._)).Returns(commit);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // The acceptance of M3.09: the action is audited.
  [Fact]
  public async Task Post_Notify_AsRepoAdmin_ShouldReturn202()
  {
    AdministerTheRepository();
    using var client = Client();

    using var response = await client.PostAsync(new Uri($"{Base}/{Sha.Value}/notify", UriKind.Relative), null, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    A.CallTo(() => _jobs.EnqueueAsync(A<JobRequest>._, A<CancellationToken>._)).MustHaveHappened();
    A.CallTo(() => _auditLog.RecordAsync("Commits.Notify", A<string>._, A<IReadOnlyDictionary<string, string>?>._, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Post_Notify_AsReader_ShouldReturn403()
  {
    using var client = Client();

    using var response = await client.PostAsync(new Uri($"{Base}/{Sha.Value}/notify", UriKind.Relative), null, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    A.CallTo(() => _jobs.EnqueueAsync(A<JobRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Post_Reprocess_AsReader_ShouldReturn403()
  {
    using var client = Client();

    using var response = await client.PostAsync(new Uri($"{Base}/{Sha.Value}/reprocess", UriKind.Relative), null, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    A.CallTo(() => _auditLog.RecordAsync(A<string>._, A<string>._, A<IReadOnlyDictionary<string, string>?>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Post_Reprocess_WithoutUpload_ShouldReturn422()
  {
    AdministerTheRepository();
    A.CallTo(() => _uploads.ListByCommitAsync(9, A<CancellationToken>._)).Returns([]);
    using var client = Client();

    using var response = await client.PostAsync(new Uri($"{Base}/{Sha.Value}/reprocess", UriKind.Relative), null, Ct);
    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    body.RootElement.GetProperty("errorCode").GetString().ShouldBe("Commit.NoUpload");
  }

  [Fact]
  public async Task Post_Notify_WithAnUploadToken_ShouldReturn401()
  {
    using var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", "cvc_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG");

    using var response = await client.PostAsync(new Uri($"{Base}/{Sha.Value}/notify", UriKind.Relative), null, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  private void AdministerTheRepository() =>
    A.CallTo(() => _access.CanAsync(ApiTokens.UserId, "2002", RepositoryPermission.Admin, A<CancellationToken>._)).Returns(true);

  private HttpClient Client() =>
    _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
                                {
                                  s.AddApiToken(organizationId: 3);
                                  s.AddScoped(_ => _scope);
                                  s.AddScoped(_ => _organization);
                                  s.AddScoped(_ => _repositories);
                                  s.AddScoped(_ => _access);
                                  s.AddScoped(_ => _commits);
                                  s.AddScoped(_ => _uploads);
                                  s.AddScoped(_ => _jobs);
                                  s.AddScoped(_ => _auditLog);
                                }))
            .CreateClient()
            .WithApiToken();

  #endregion
}
