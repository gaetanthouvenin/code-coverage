// ------------------------------------------------------------------------------------------------
// <copyright file="UploadContractTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Api.Tests.Support;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Api.Tests.Uploads;

// The request of tests/fixtures/uploads/multipart-contract.json, which Uploader.Tests also checks on its side.
[Collection(CodeCoverageApiCollection.Name)]
public sealed class UploadContractTests(CodeCoverageApiFactory factory)
{
  #region Fields

  private const string Boundary = "----upload-contract";

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  private static readonly byte[] Hash = Enumerable.Repeat((byte)7, UploadToken.HashLength).ToArray();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Post_ContractFixture_ShouldStoreTheFieldsAndTheParts()
  {
    using var contract = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", "uploads", "multipart-contract.json"), Ct));
    var root = contract.RootElement;
    var fields = root.GetProperty("fields").EnumerateArray().Select(static f => (Name: f[0].GetString()!, Value: f[1].GetString()!)).ToList();
    var network = root.GetProperty("network").GetString()!;
    var report = root.GetProperty("report");
    var sha = fields.Single(static f => f.Name == "sha").Value;

    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse(fields.Single(static f => f.Name == "slug").Value), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);
    var commit = Commit.Create(5, GitSha.Parse(sha), Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(commit, 9L);

    var scope = A.Fake<IOrganizationScope>();
    A.CallTo(() => scope.TryEnterAsync(A<Forge>._, A<string?>._, A<CancellationToken>._)).Returns(true);
    var organization = A.Fake<ICurrentOrganization>();
    A.CallTo(() => organization.IsKnown).Returns(true);
    var cache = A.Fake<IUploadCredentialCache>();
    A.CallTo(() => cache.Find(A<byte[]>._)).Returns(new UploadTokenIdentity(3, UploadTokenScope.Organization, null, null));
    var repositories = A.Fake<IRepositoryRepository>();
    A.CallTo(() => repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => repositories.FindAsync(5, A<CancellationToken>._)).Returns(repository);
    var commits = A.Fake<ICommitRepository>();
    A.CallTo(() => commits.GetOrCreateAsync(A<long>._, A<GitSha>._, A<DateTimeOffset>._, A<CancellationToken>._)).Returns(commit);
    A.CallTo(() => commits.NextUploadSequenceAsync(A<long>._, A<CancellationToken>._)).Returns(1);
    var stored = new List<(UploadPartKind Kind, string Name, byte[] Content)>();
    var raw = A.Fake<IRawUploadStore>();
    A.CallTo(() => raw.WriteAsync(A<Guid>._, A<UploadPart>._, A<CancellationToken>._))
     .ReturnsLazily(async (Guid _, UploadPart part, CancellationToken token) =>
       {
         using var content = new MemoryStream();
         await part.Content.CopyToAsync(content, token);
         stored.Add((part.Kind, part.FileName, content.ToArray()));

         return new StoredRawFile(1, Hash, 1, 1);
       });
    var reports = A.Fake<ICommitReportRepository>();
    Upload? saved = null;
    A.CallTo(() => reports.TryAddReceivedUploadAsync(A<Upload>._, A<JobRequest>._, A<CancellationToken>._))
     .Invokes((Upload upload, JobRequest _, CancellationToken _) => saved = upload)
     .Returns(true);

    await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
    {
      services.AddSingleton(scope);
      services.AddSingleton(organization);
      services.AddSingleton(cache);
      services.AddSingleton(repositories);
      services.AddSingleton(commits);
      services.AddSingleton(raw);
      services.AddSingleton(reports);
      services.AddSingleton(A.Fake<IUploadRepository>());
      services.AddSingleton(A.Fake<IClock>());
    }));
    using var client = host.CreateClient();
    using var request = new HttpRequestMessage(HttpMethod.Post, CodeCoverageApi.Prefix + "/uploads") { Content = Body(fields, network, report) };
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "cvc_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG");

    using var response = await client.SendAsync(request, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    saved.ShouldNotBeNull();
    saved.Name.ShouldBe("backend-linux");
    saved.Flags.ShouldBe(["back", "front"]);
    saved.BuildId.ShouldBe("99");
    saved.CiService.ShouldBe("github-actions");
    stored.Count.ShouldBe(2);
    stored[0].Kind.ShouldBe(UploadPartKind.Network);
    Encoding.UTF8.GetString(stored[0].Content).ShouldBe(network);
    stored[1].Kind.ShouldBe(UploadPartKind.Report);
    stored[1].Name.ShouldBe(report.GetProperty("fileName").GetString());
    Gunzip(stored[1].Content).ShouldBe(report.GetProperty("content").GetString());
  }

  private static ByteArrayContent Body(List<(string Name, string Value)> fields, string network, JsonElement report)
  {
    using var body = new MemoryStream();

    void Write(string text) => body.Write(Encoding.UTF8.GetBytes(text));

    foreach (var (name, value) in fields)
    {
      Write($"--{Boundary}\r\nContent-Disposition: form-data; name={name}\r\n\r\n{value}\r\n");
    }

    Write($"--{Boundary}\r\nContent-Disposition: form-data; name=network; filename=network.txt\r\n\r\n{network}\r\n");
    Write($"--{Boundary}\r\nContent-Disposition: form-data; name=file; filename={report.GetProperty("fileName").GetString()}\r\n\r\n");
    body.Write(Gzip(report.GetProperty("content").GetString()!));
    Write($"\r\n--{Boundary}--\r\n");

    var content = new ByteArrayContent(body.ToArray());
    content.Headers.ContentType = MediaTypeHeaderValue.Parse($"multipart/form-data; boundary={Boundary}");

    return content;
  }

  private static byte[] Gzip(string text)
  {
    using var output = new MemoryStream();

    using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
    {
      gzip.Write(Encoding.UTF8.GetBytes(text));
    }

    return output.ToArray();
  }

  private static string Gunzip(byte[] content)
  {
    using var input = new GZipStream(new MemoryStream(content), CompressionMode.Decompress);
    using var output = new MemoryStream();

    input.CopyTo(output);

    return Encoding.UTF8.GetString(output.ToArray());
  }

  #endregion
}
