// ------------------------------------------------------------------------------------------------
// <copyright file="UploadsControllerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Api.Tests.Support;
using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Application.Features.Uploads;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.Identity;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Api.Tests.Uploads;

[Collection(CodeCoverageApiCollection.Name)]
public sealed class UploadsControllerTests
{
  #region Fields

  private const string UploadsPath = CodeCoverageApi.Prefix + "/uploads";

  private const string Boundary = "----upload-test";

  private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

  private const string Jwt = "header.payload.signature";

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  private static readonly byte[] Hash = Enumerable.Repeat((byte)7, UploadToken.HashLength).ToArray();

  private readonly CodeCoverageApiFactory _factory;

  private readonly IOrganizationScope _scope = A.Fake<IOrganizationScope>();

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly IUploadCredentialCache _cache = A.Fake<IUploadCredentialCache>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IRawUploadStore _raw = A.Fake<IRawUploadStore>();

  private readonly IUploadRepository _uploads = A.Fake<IUploadRepository>();

  private readonly ICommitReportRepository _reports = A.Fake<ICommitReportRepository>();

  private readonly IOidcTokenValidator _oidc = A.Fake<IOidcTokenValidator>();

  private readonly IOrganizationDirectory _directory = A.Fake<IOrganizationDirectory>();

  private readonly IForgeGatewayProvider _gateways = A.Fake<IForgeGatewayProvider>();

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  #endregion

  #region Ctors

  public UploadsControllerTests(CodeCoverageApiFactory factory)
  {
    _factory = factory;

    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);

    var commit = Commit.Create(5, GitSha.Parse(Sha), Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(commit, 9L);

    A.CallTo(() => _scope.TryEnterAsync(A<Forge>._, A<string?>._, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _organization.IsKnown).Returns(true);
    A.CallTo(() => _cache.Find(A<byte[]>._)).Returns(new UploadTokenIdentity(3, UploadTokenScope.Organization, null, null));
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _repositories.FindAsync(5, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _commits.GetOrCreateAsync(A<long>._, A<GitSha>._, A<DateTimeOffset>._, A<CancellationToken>._)).Returns(commit);
    A.CallTo(() => _commits.NextUploadSequenceAsync(A<long>._, A<CancellationToken>._)).Returns(1);
    A.CallTo(() => _raw.WriteAsync(A<Guid>._, A<UploadPart>._, A<CancellationToken>._)).Returns(new StoredRawFile(1, Hash, 1, 1));
    A.CallTo(() => _reports.TryAddReceivedUploadAsync(A<Upload>._, A<JobRequest>._, A<CancellationToken>._)).Returns(true);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Post_FileBeforeSlug_ShouldReturn400()
  {
    await using var host = Host();
    using var client = host.CreateClient();

    using var response = await SendAsync(client, Multipart(("file:coverage.xml", "<coverage/>"), ("slug", "octo-org/app"), ("sha", Sha)));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    await ShouldHaveErrorCode(response, UploadErrors.MetadataMustPrecedeFiles);
  }

  [Fact]
  public async Task Post_TextFieldAfterFile_ShouldReturn400AndDeleteTheParts()
  {
    await using var host = Host();
    using var client = host.CreateClient();

    using var response = await SendAsync(client, Multipart(("slug", "octo-org/app"), ("sha", Sha), ("file:coverage.xml", "<coverage/>"), ("flags", "late")));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    await ShouldHaveErrorCode(response, UploadErrors.MetadataMustPrecedeFiles);
    A.CallTo(() => _raw.DeleteAsync(A<Guid>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Post_Valid_ShouldReturn202()
  {
    await using var host = Host();
    using var client = host.CreateClient();

    using var response = await SendAsync(client, ValidBody(), "key-1");

    response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
    var root = json.RootElement;

    root.GetProperty("state").GetString().ShouldBe("Received");
    root.GetProperty("statusUrl").GetString()!.ShouldContain($"{CodeCoverageApi.Prefix}/repos/github/octo-org/app/uploads/{root.GetProperty("uploadId").GetString()}");
    root.GetProperty("commitUrl").GetString()!.ShouldEndWith($"/github/octo-org/app/commit/{Sha}");
    A.CallTo(() => _raw.WriteAsync(A<Guid>._, A<UploadPart>.That.Matches(static p => p.Kind == UploadPartKind.Report && p.FileName == "coverage.xml"), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Post_NetworkPart_ShouldBeStoredAsTheListOfFiles()
  {
    await using var host = Host();
    using var client = host.CreateClient();

    using var response = await SendAsync(client, Multipart(("slug", "octo-org/app"), ("sha", Sha), ("network:network.txt", "src/A.cs"), ("file:coverage.xml", "<coverage/>")));

    response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    A.CallTo(() => _raw.WriteAsync(A<Guid>._, A<UploadPart>.That.Matches(static p => p.Kind == UploadPartKind.Network), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Post_Replay_ShouldReturn200()
  {
    var existing = Upload.Receive(9, 1, "key-1", [], null, null, null, null, null, null, null, 3, PrincipalKind.OrganizationToken, Now);
    A.CallTo(() => _reports.TryAddReceivedUploadAsync(A<Upload>._, A<JobRequest>._, A<CancellationToken>._)).Returns(false);
    A.CallTo(() => _uploads.FindByIdempotencyKeyAsync(9, "key-1", A<CancellationToken>._)).Returns(existing);
    await using var host = Host();
    using var client = host.CreateClient();

    using var response = await SendAsync(client, ValidBody(), "key-1");

    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
    json.RootElement.GetProperty("uploadId").GetGuid().ShouldBe(existing.PublicId);
  }

  [Fact]
  public async Task Post_OverLimit_ShouldReturn413()
  {
    A.CallTo(() => _raw.WriteAsync(A<Guid>._, A<UploadPart>._, A<CancellationToken>._)).Throws(new UploadTooLargeException("over"));
    await using var host = Host();
    using var client = host.CreateClient();

    using var response = await SendAsync(client, ValidBody());

    response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    await ShouldHaveErrorCode(response, UploadErrors.TooLarge);
  }

  [Fact]
  public async Task Post_RateLimited_ShouldReturn429()
  {
    await using var host = Host(limit: 1);
    using var client = host.CreateClient();

    using var accepted = await SendAsync(client, ValidBody());
    using var refused = await SendAsync(client, ValidBody());

    accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    refused.Headers.RetryAfter.ShouldNotBeNull();
    await ShouldHaveErrorCode(refused, UploadErrors.RateLimited);
  }

  [Fact]
  public async Task Post_OtherRepositoryWithTheSameToken_ShouldHaveItsOwnWindow()
  {
    await using var host = Host(limit: 1);
    using var client = host.CreateClient();

    using var first = await SendAsync(client, ValidBody());
    using var second = await SendAsync(client, Multipart(("slug", "octo-org/other"), ("sha", Sha), ("file:coverage.xml", "<coverage/>")));

    first.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    second.StatusCode.ShouldBe(HttpStatusCode.Accepted);
  }

  // The controller authorizes before reading the files, and the command checks again: GitHub is asked once.
  [Fact]
  public async Task Post_OidcOnUntrackedRepository_ShouldReadTheRepositoryConnectionOnce()
  {
    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns((TrackedRepository?)null);
    A.CallTo(() => _repositories.FindByExternalIdAsync("2002", A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _oidc.ValidateAsync(Jwt, A<CancellationToken>._)).Returns(new GitHubOidcIdentity("octo-org/app", "2002", "octo-org", "1001", null, null));
    A.CallTo(() => _directory.FindCurrentAsync(A<CancellationToken>._))
     .Returns(Organization.CreatePending(Forge.GitHub, "1001", "octo-org", OrganizationAccountType.Organization, Now));
    A.CallTo(() => _gateways.GetGateway(Forge.GitHub)).Returns(_gateway);
    A.CallTo(() => _gateway.GetRepositoryConnectionAsync("octo-org", "app", A<CancellationToken>._)).Returns(new ForgeConnectionInfo("77", "1001", "2002"));
    await using var host = Host(oidc: true);
    using var client = host.CreateClient();

    using var response = await SendAsync(client, ValidBody(), credential: Jwt);

    response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    A.CallTo(() => _oidc.ValidateAsync(A<string>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _gateway.GetRepositoryConnectionAsync(A<string>._, A<string>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Theory]
  [InlineData("sha", "not-a-sha")]
  [InlineData("forge", "svn")]
  [InlineData("pr", "abc")]
  public async Task Post_InvalidField_ShouldReturn400(string field, string value)
  {
    await using var host = Host();
    using var client = host.CreateClient();
    var sha = field == "sha" ? value : Sha;
    (string, string)[] fields = field == "sha"
                                  ? [("slug", "octo-org/app"), ("sha", sha), ("file:coverage.xml", "<coverage/>")]
                                  : [("slug", "octo-org/app"), ("sha", sha), (field, value), ("file:coverage.xml", "<coverage/>")];

    using var response = await SendAsync(client, Multipart(fields));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    await ShouldHaveErrorCode(response, UploadErrors.InvalidMetadata);
  }

  // The published uploaders up to 0.1.0-beta.2 send their version with its build metadata, 72 characters long.
  [Fact]
  public async Task Post_UploaderVersionWithBuildMetadata_ShouldReturn202AndKeepTheVersionWithoutIt()
  {
    Upload? received = null;
    A.CallTo(() => _reports.TryAddReceivedUploadAsync(A<Upload>._, A<JobRequest>._, A<CancellationToken>._))
     .Invokes((Upload upload, JobRequest _, CancellationToken _) => received = upload)
     .Returns(true);
    await using var host = Host();
    using var client = host.CreateClient();

    using var response = await SendAsync(
                           client,
                           Multipart(
                             ("slug", "octo-org/app"),
                             ("sha", Sha),
                             ("uploaderVersion", "0.1.0-beta.2+Branch.develop.Sha.c37d8a367310aa9b967b5213753f378d8c5679af"),
                             ("file:coverage.xml", "<coverage/>")
                           )
                         );

    response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    received.ShouldNotBeNull().UploaderVersion.ShouldBe("0.1.0-beta.2");
  }

  // The 64 characters limit is unchanged: only the build metadata is dropped.
  [Fact]
  public async Task Post_UploaderVersionTooLongWithoutBuildMetadata_ShouldReturn400()
  {
    await using var host = Host();
    using var client = host.CreateClient();

    using var response = await SendAsync(
                           client,
                           Multipart(("slug", "octo-org/app"), ("sha", Sha), ("uploaderVersion", new string('1', Upload.UploaderVersionMaxLength + 1)), ("file:coverage.xml", "<coverage/>"))
                         );

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

    json.RootElement.GetProperty("errorCode").GetString().ShouldBe(GeneralErrors.ValidationFailed);
    json.RootElement.GetProperty("errors").TryGetProperty("Metadata.UploaderVersion", out _).ShouldBeTrue();
  }

  [Fact]
  public async Task Post_NotMultipart_ShouldReturn400()
  {
    await using var host = Host();
    using var client = host.CreateClient();
    using var request = new HttpRequestMessage(HttpMethod.Post, UploadsPath) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "cvc_token");

    using var response = await client.SendAsync(request, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  // A body that is not well-formed multipart is a refusal of the request, not a failure of the server.
  [Theory]
  [InlineData("")]
  [InlineData("------upload-test\r\nContent-Disposition: form-data; name=\"slug\"\r\n\r\nocto-or")]
  public async Task Post_MalformedMultipart_ShouldReturn400(string body)
  {
    await using var host = Host();
    using var client = host.CreateClient();

    using var response = await SendAsync(client, body);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    await ShouldHaveErrorCode(response, UploadErrors.InvalidPart);
  }

  [Fact]
  public async Task Post_FileWithoutClosingBoundary_ShouldReturn400AndDeleteTheParts()
  {
    A.CallTo(() => _raw.WriteAsync(A<Guid>._, A<UploadPart>._, A<CancellationToken>._))
     .ReturnsLazily(async (Guid _, UploadPart part, CancellationToken token) =>
       {
         await part.Content.CopyToAsync(Stream.Null, token);

         return new StoredRawFile(1, Hash, 1, 1);
       });
    var body = Multipart(("slug", "octo-org/app"), ("sha", Sha), ("file:coverage.xml", "<coverage/>"));
    body = body[..body.LastIndexOf("--" + Boundary, StringComparison.Ordinal)];

    await using var host = Host();
    using var client = host.CreateClient();

    using var response = await SendAsync(client, body);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    await ShouldHaveErrorCode(response, UploadErrors.InvalidPart);
    A.CallTo(() => _raw.DeleteAsync(A<Guid>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  private static async Task ShouldHaveErrorCode(HttpResponseMessage response, string errorCode)
  {
    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

    json.RootElement.GetProperty("errorCode").GetString().ShouldBe(errorCode);
  }

  private static string ValidBody() => Multipart(("slug", "octo-org/app"), ("sha", Sha), ("flags", "back,front"), ("file:coverage.xml", "<coverage/>"));

  // A name like "file:coverage.xml" is a file part; another is a text field. The closing boundary comes last.
  private static string Multipart(params (string Name, string Value)[] parts)
  {
    var body = new StringBuilder();

    foreach (var (name, value) in parts)
    {
      var separator = name.IndexOf(':', StringComparison.Ordinal);

      body.Append("--").Append(Boundary).Append("\r\n");
      body.Append(
        separator < 0
          ? $"Content-Disposition: form-data; name=\"{name}\"\r\n\r\n"
          : $"Content-Disposition: form-data; name=\"{name[..separator]}\"; filename=\"{name[(separator + 1)..]}\"\r\n\r\n"
      );
      body.Append(value).Append("\r\n");
    }

    return body.Append("--").Append(Boundary).Append("--\r\n").ToString();
  }

  private static async Task<HttpResponseMessage> SendAsync(
    HttpClient client,
    string body,
    string? idempotencyKey = null,
    string credential = "cvc_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG")
  {
    using var request = new HttpRequestMessage(HttpMethod.Post, UploadsPath) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body)) };
    request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse($"multipart/form-data; boundary={Boundary}");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);

    if (idempotencyKey is not null)
    {
      request.Headers.Add("Idempotency-Key", idempotencyKey);
    }

    return await client.SendAsync(request, Ct);
  }

  private WebApplicationFactory<Program> Host(int limit = 120, bool oidc = false) =>
    _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
    {
      services.AddSingleton(_scope);
      services.AddSingleton(_organization);
      services.AddSingleton(_cache);
      services.AddSingleton(_repositories);
      services.AddSingleton(_commits);
      services.AddSingleton(_raw);
      services.AddSingleton(_uploads);
      services.AddSingleton(_reports);
      services.AddSingleton(A.Fake<IClock>());
      services.Configure<UploadOptions>(options => options.RateLimitPerMinute = limit);

      if (oidc)
      {
        services.AddSingleton(_oidc);
        services.AddSingleton(_directory);
        services.AddSingleton(_gateways);
      }
    }));

  #endregion
}
