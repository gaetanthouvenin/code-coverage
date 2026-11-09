// ------------------------------------------------------------------------------------------------
// <copyright file="GetUploadTests.cs" company="Gaëtan Thouvenin">
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
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Api.Tests.Uploads;

[Collection(CodeCoverageApiCollection.Name)]
public sealed class GetUploadTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  private readonly CodeCoverageApiFactory _factory;

  private readonly IOrganizationScope _scope = A.Fake<IOrganizationScope>();

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly IUploadCredentialCache _cache = A.Fake<IUploadCredentialCache>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IUploadRepository _uploads = A.Fake<IUploadRepository>();

  private readonly Upload _upload = Upload.Receive(9, 1, "key", ["back"], "run", null, null, null, null, null, null, 3, PrincipalKind.OrganizationToken, Now);

  #endregion

  #region Ctors

  public GetUploadTests(CodeCoverageApiFactory factory)
  {
    _factory = factory;

    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);

    A.CallTo(() => _scope.TryEnterAsync(A<Forge>._, A<string?>._, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _organization.IsKnown).Returns(true);
    A.CallTo(() => _cache.Find(A<byte[]>._)).Returns(new UploadTokenIdentity(3, UploadTokenScope.Organization, null, null));
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _commits.FindAsync(9, A<CancellationToken>._)).Returns(Commit.Create(5, GitSha.Parse(new string('a', GitSha.Length)), Now));
    A.CallTo(() => _uploads.FindByPublicIdAsync(_upload.PublicId, A<CancellationToken>._)).Returns(_upload);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Get_WithCredential_ShouldReturnTheUploadAndItsLog()
  {
    await using var host = Host();
    using var client = host.CreateClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", "cvc_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG");

    using var response = await client.GetAsync(Path("github", _upload.PublicId), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
    var root = json.RootElement;

    root.GetProperty("id").GetGuid().ShouldBe(_upload.PublicId);
    root.GetProperty("state").GetString().ShouldBe("Received");
    root.GetProperty("flags")[0].GetString().ShouldBe("back");
    root.GetProperty("totals").ValueKind.ShouldBe(JsonValueKind.Null);
    root.GetProperty("log").GetProperty("pathTotal").GetInt32().ShouldBe(0);
  }

  [Fact]
  public async Task Get_WithoutCredential_ShouldReturn401()
  {
    await using var host = Host();
    using var client = host.CreateClient();

    using var response = await client.GetAsync(Path("github", _upload.PublicId), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Get_UnknownForge_ShouldReturn404()
  {
    await using var host = Host();
    using var client = host.CreateClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", "cvc_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG");

    using var response = await client.GetAsync(Path("svn", _upload.PublicId), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  private static string Path(string forge, Guid id) => $"{CodeCoverageApi.Prefix}/repos/{forge}/octo-org/app/uploads/{id}";

  private WebApplicationFactory<Program> Host() =>
    _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
    {
      services.AddSingleton(_scope);
      services.AddSingleton(_organization);
      services.AddSingleton(_cache);
      services.AddSingleton(_repositories);
      services.AddSingleton(_commits);
      services.AddSingleton(_uploads);
      services.AddSingleton(A.Fake<IClock>());
    }));

  #endregion
}
