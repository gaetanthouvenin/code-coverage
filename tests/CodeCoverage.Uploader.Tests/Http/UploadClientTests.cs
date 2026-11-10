// ------------------------------------------------------------------------------------------------
// <copyright file="UploadClientTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Text.Json;

using CodeCoverage.Uploader.Http;
using CodeCoverage.Uploader.Output;

namespace CodeCoverage.Uploader.Tests.Http;

public sealed class UploadClientTests : IDisposable
{
  #region Fields

  private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

  private static readonly Uri Server = new("https://coverage.example.com/");

  private readonly string _directory = Directory.CreateTempSubdirectory("uploader-tests").FullName;

  private readonly FakeHttpHandler _handler = new();

  private readonly List<TimeSpan> _waits = [];

  private readonly Dictionary<string, string?> _environment = [];

  private readonly StringWriter _output = new();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Send_ShouldWriteFieldsBeforeFiles()
  {
    _handler.Then(HttpStatusCode.Accepted);

    (await SendAsync(Request())).ShouldBeTrue();

    var names = Parts().Select(static part => NameOf(part.Headers)).ToList();

    names.ShouldBe(["slug", "sha", "branch", "pr", "parentSha", "flags", "name", "ciService", "buildUrl", "buildId", "jobId", "root", "network", "file"]);
    names.IndexOf("file").ShouldBeGreaterThan(names.IndexOf("network"));
  }

  [Fact]
  public async Task Send_ShouldGzipReports()
  {
    _handler.Then(HttpStatusCode.Accepted);

    await SendAsync(Request());

    var file = Parts().Single(static part => NameOf(part.Headers) == "file");

    file.Content[0].ShouldBe((byte)0x1F);
    file.Content[1].ShouldBe((byte)0x8B);
    MultipartBody.Gunzip(file.Content).ShouldBe("<coverage/>");
  }

  [Fact]
  public async Task Send_ShouldSendTheCredentialAndAnIdempotencyKey()
  {
    _handler.Then(HttpStatusCode.Accepted);

    await SendAsync(Request());

    var request = _handler.Received.Single().Request;

    request.Headers.Authorization!.ToString().ShouldBe("Bearer cvc_token");
    request.Headers.GetValues("Idempotency-Key").Single().Length.ShouldBe(64);
    request.RequestUri.ShouldBe(new Uri("https://coverage.example.com/api/v1/uploads"));
  }

  [Fact]
  public async Task Send_429WithRetryAfter_ShouldWait()
  {
    _handler.Then(HttpStatusCode.TooManyRequests, "{}", static response => response.Headers.RetryAfter = new(TimeSpan.FromSeconds(7)))
            .Then(HttpStatusCode.Accepted);

    (await SendAsync(Request())).ShouldBeTrue();

    _waits.ShouldBe([TimeSpan.FromSeconds(7)]);
    _handler.Received.Count.ShouldBe(2);
  }

  [Fact]
  public async Task Send_ServerError_ShouldBackOffUpToFiveAttempts()
  {
    for (var attempt = 0; attempt < RetryPolicy.MaxAttempts; attempt++)
    {
      _handler.Then(HttpStatusCode.ServiceUnavailable);
    }

    (await SendAsync(Request())).ShouldBeFalse();

    _handler.Received.Count.ShouldBe(RetryPolicy.MaxAttempts);
    _waits.ShouldBe([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8)]);
    _output.ToString().ShouldContain("503");
  }

  [Fact]
  public async Task Send_NetworkError_ShouldTryAgainWithANewBody()
  {
    _handler.ThenThrow(new HttpRequestException("reset")).Then(HttpStatusCode.Accepted);

    (await SendAsync(Request())).ShouldBeTrue();

    MultipartBody.Gunzip(Parts().Single(static part => NameOf(part.Headers) == "file").Content).ShouldBe("<coverage/>");
  }

  [Fact]
  public async Task Send_SameContentTwice_ShouldGiveTheSameKey()
  {
    _handler.Then(HttpStatusCode.Accepted).Then(HttpStatusCode.OK);

    (await SendAsync(Request())).ShouldBeTrue();
    (await SendAsync(Request())).ShouldBeTrue();

    var keys = _handler.Received.Select(static r => r.Request.Headers.GetValues("Idempotency-Key").Single()).ToList();

    keys[1].ShouldBe(keys[0]);
    _output.ToString().ShouldContain("already been received");
  }

  [Fact]
  public async Task Send_Refused_ShouldReportTheErrorCode()
  {
    _handler.Then(HttpStatusCode.Unauthorized, """{"errorCode":"Upload.TokenInvalid"}""");

    (await SendAsync(Request())).ShouldBeFalse();

    _output.ToString().ShouldContain("Upload.TokenInvalid");
    _handler.Received.Count.ShouldBe(1);
  }

  [Fact]
  public async Task Send_NoToken_WithActionsOidc_ShouldFetchJwt()
  {
    _environment["ACTIONS_ID_TOKEN_REQUEST_URL"] = "https://token.actions.example/req?api-version=2";
    _environment["ACTIONS_ID_TOKEN_REQUEST_TOKEN"] = "request-token";
    _handler.Then(HttpStatusCode.OK, """{"value":"aaa.bbb.ccc"}""").Then(HttpStatusCode.Accepted);

    (await SendAsync(Request(), token: null)).ShouldBeTrue();

    var (oidc, _) = _handler.Received[0];

    oidc.RequestUri!.ToString().ShouldBe("https://token.actions.example/req?api-version=2&audience=https%3A%2F%2Fcoverage.example.com");
    oidc.Headers.Authorization!.ToString().ShouldBe("bearer request-token");
    _handler.Received[1].Request.Headers.Authorization!.ToString().ShouldBe("Bearer aaa.bbb.ccc");
  }

  [Fact]
  public async Task Send_NoCredential_ShouldFailWithoutSending()
  {
    (await SendAsync(Request(), token: null)).ShouldBeFalse();

    _handler.Received.ShouldBeEmpty();
    _output.ToString().ShouldContain("No credential");
  }

  // The fixture is the one Api.Tests posts to the real controller: both sides agree on it.
  [Fact]
  public async Task Build_ContractFixture_ShouldProduceWhatTheServerExpects()
  {
    using var contract = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", "uploads", "multipart-contract.json"), Ct));
    var root = contract.RootElement;
    var fields = root.GetProperty("fields").EnumerateArray().Select(static f => (Name: f[0].GetString()!, Value: f[1].GetString()!)).ToList();
    var report = root.GetProperty("report");
    var path = Path.Combine(_directory, report.GetProperty("fileName").GetString()!);
    await File.WriteAllTextAsync(path, report.GetProperty("content").GetString(), Ct);

    var request = new UploadRequest
    {
      Slug = Field(fields, "slug"),
      Sha = Field(fields, "sha"),
      Branch = Field(fields, "branch"),
      PullRequest = int.Parse(Field(fields, "pr"), System.Globalization.CultureInfo.InvariantCulture),
      Flags = Field(fields, "flags").Split(','),
      Name = Field(fields, "name"),
      CiService = Field(fields, "ciService"),
      BuildId = Field(fields, "buildId"),
      JobId = Field(fields, "jobId"),
      Network = root.GetProperty("network").GetString()!.Split('\n'),
      Reports = [path],
    };

    _handler.Then(HttpStatusCode.Accepted);
    await SendAsync(request);

    var parts = Parts();

    parts.Select(static part => NameOf(part.Headers)).ShouldBe([.. fields.Select(static f => f.Name), "network", "file"]);

    foreach (var (name, value) in fields)
    {
      MultipartBody.Text(parts.Single(part => NameOf(part.Headers) == name).Content).ShouldBe(value);
    }

    MultipartBody.Text(parts.Single(static part => NameOf(part.Headers) == "network").Content).ShouldBe(root.GetProperty("network").GetString());
    MultipartBody.Gunzip(parts.Single(static part => NameOf(part.Headers) == "file").Content).ShouldBe(report.GetProperty("content").GetString());
    parts.Single(static part => NameOf(part.Headers) == "file").Headers.ShouldContain("coverage.xml");
  }

  public void Dispose()
  {
    _handler.Dispose();
    _output.Dispose();
    Directory.Delete(_directory, recursive: true);
  }

  private static string Field(List<(string Name, string Value)> fields, string name) => fields.Single(f => f.Name == name).Value;

  private static string NameOf(string headers)
  {
    var start = headers.IndexOf("name=", StringComparison.Ordinal) + "name=".Length;
    var end = headers.IndexOfAny([';', '\r', '\n'], start);

    return (end < 0 ? headers[start..] : headers[start..end]).Trim().Trim('"');
  }

  private UploadRequest Request()
  {
    var path = Path.Combine(_directory, "coverage.xml");
    File.WriteAllText(path, "<coverage/>");

    return new UploadRequest
    {
      Slug = "octo-org/app",
      Sha = Sha,
      Branch = "main",
      PullRequest = 12,
      ParentSha = new string('b', 40),
      Flags = ["back"],
      Name = "run",
      CiService = "github-actions",
      BuildUrl = "https://example.test/build",
      BuildId = "1",
      JobId = "2",
      Root = _directory,
      Network = ["src/A.cs"],
      Reports = [path],
    };
  }

  private IReadOnlyList<(string Headers, byte[] Content)> Parts()
  {
    var (request, body) = _handler.Received[^1];
    var boundary = request.Content!.Headers.ContentType!.Parameters.Single(static p => p.Name == "boundary").Value!.Trim('"');

    return MultipartBody.Parse(body, boundary);
  }

  private Task<bool> SendAsync(UploadRequest request, string? token = "cvc_token")
  {
    var client = new HttpClient(_handler, disposeHandler: false);
    var reporter = new ConsoleReporter(_output);
    var retry = new RetryPolicy((wait, _) => { _waits.Add(wait); return Task.CompletedTask; }, reporter);
    var oidc = new OidcTokenFetcher(client, name => _environment.GetValueOrDefault(name), reporter);

    return new UploadClient(client, retry, oidc, reporter).SendAsync(Server, token, request, Ct);
  }

  #endregion
}
