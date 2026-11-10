// ------------------------------------------------------------------------------------------------
// <copyright file="UploadRunnerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;

using CodeCoverage.Uploader.Http;
using CodeCoverage.Uploader.Output;

namespace CodeCoverage.Uploader.Tests.Http;

// The whole run over a fake server: what --wait, --dry-run and --verbose add to the sending.
public sealed class UploadRunnerTests : IDisposable
{
  #region Fields

  private const string Token = "cvc_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG";

  private const string StatusUrl = "https://api.coverage.example.com/api/v1/repos/github/octo-org/app/uploads/0f8a4c1e-7d2b-4e6f-9a3c-5b1d2e3f4a5b";

  private const string Accepted = $$"""{"uploadId":"0f8a4c1e-7d2b-4e6f-9a3c-5b1d2e3f4a5b","state":"Received","commitUrl":"https://coverage.example.com/github/octo-org/app/commit/aaaa","statusUrl":"{{StatusUrl}}"}""";

  private const string Processed = """{"state":"Processed","resolvedFiles":2,"unresolvedFiles":1,"log":{"messages":[],"messageTotal":0,"paths":["src/c.cs"],"pathTotal":1},"error":null}""";

  private readonly string _directory = Directory.CreateTempSubdirectory("uploader-runner-tests").FullName;

  private readonly FakeHttpHandler _handler = new();

  private readonly ManualTimeProvider _time = new();

  private readonly StringWriter _output = new();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  public void Dispose()
  {
    try
    {
      Directory.Delete(_directory, recursive: true);
    }
    catch (IOException)
    {
    }
  }

  [Fact]
  public async Task Run_WithWait_ShouldAskTheStatusUrlOfTheAnswerAndPrintTheCounts()
  {
    _handler.Then(HttpStatusCode.Accepted, Accepted).Then(HttpStatusCode.OK, Processed);

    (await RunAsync(Settings(wait: true))).ShouldBeTrue();

    _handler.Received.Count.ShouldBe(2);
    _handler.Received[0].Request.Method.ShouldBe(HttpMethod.Post);
    _handler.Received[1].Request.Method.ShouldBe(HttpMethod.Get);
    _handler.Received[1].Request.RequestUri!.AbsoluteUri.ShouldBe(StatusUrl);
    _handler.Received[1].Request.Headers.Authorization!.Parameter.ShouldBe(Token);
    _output.ToString().ShouldContain("resolved 2/3 files; unresolved: src/c.cs");
    _output.ToString().ShouldNotContain(Token);
  }

  [Fact]
  public async Task Run_WithFinalize_ShouldDeclareTheUploadsCompleteAfterTheUpload()
  {
    _handler.Then(HttpStatusCode.Accepted, Accepted).Then(HttpStatusCode.OK, """{"total":1,"processed":0,"processing":1,"failed":0}""");

    (await RunAsync(Settings(finalize: true))).ShouldBeTrue();

    _handler.Received.Count.ShouldBe(2);
    _handler.Received[1].Request.Method.ShouldBe(HttpMethod.Post);
    _handler.Received[1].Request.RequestUri!.AbsoluteUri.ShouldBe($"https://coverage.example.com/api/v1/repos/github/octo-org/app/commits/{new string('a', 40)}/uploads-complete");
    _handler.Received[1].Request.Headers.Authorization!.Parameter.ShouldBe(Token);
    _output.ToString().ShouldNotContain(Token);
  }

  // The wait comes first: the announcement is made once the server said what it could process.
  [Fact]
  public async Task Run_WithWaitAndFinalize_ShouldWaitThenDeclareComplete()
  {
    _handler.Then(HttpStatusCode.Accepted, Accepted).Then(HttpStatusCode.OK, Processed).Then(HttpStatusCode.OK, "{}");

    (await RunAsync(Settings(wait: true, finalize: true))).ShouldBeTrue();

    _handler.Received.Select(static r => r.Request.Method).ShouldBe([HttpMethod.Post, HttpMethod.Get, HttpMethod.Post]);
  }

  // A refused announcement is a failure of the run, which exits 0 without --fail-on-error like any other.
  [Fact]
  public async Task Run_FinalizeRefused_ShouldReturnFalse()
  {
    _handler.Then(HttpStatusCode.Accepted, Accepted).Then(HttpStatusCode.NotFound, """{"errorCode":"Commit.NotFound"}""");

    (await RunAsync(Settings(finalize: true))).ShouldBeFalse();
  }

  [Fact]
  public async Task Run_UploadRefused_ShouldNotDeclareComplete()
  {
    _handler.Then(HttpStatusCode.Unauthorized, """{"errorCode":"Upload.TokenInvalid"}""");

    (await RunAsync(Settings(finalize: true))).ShouldBeFalse();

    _handler.Received.Count.ShouldBe(1);
  }

  [Fact]
  public async Task Run_WithoutFinalize_ShouldNotDeclareComplete()
  {
    _handler.Then(HttpStatusCode.Accepted, Accepted);

    (await RunAsync(Settings())).ShouldBeTrue();

    _handler.Received.Count.ShouldBe(1);
  }

  [Fact]
  public async Task Run_WithoutWait_ShouldNotAskTheState()
  {
    _handler.Then(HttpStatusCode.Accepted, Accepted);

    (await RunAsync(Settings())).ShouldBeTrue();

    _handler.Received.ShouldHaveSingleItem().Request.Method.ShouldBe(HttpMethod.Post);
  }

  // A replay is answered 200 with the same body: its processing is followed the same way.
  [Fact]
  public async Task Run_WithWait_OnAReplay_ShouldStillFollowTheProcessing()
  {
    _handler.Then(HttpStatusCode.OK, Accepted).Then(HttpStatusCode.OK, Processed);

    (await RunAsync(Settings(wait: true))).ShouldBeTrue();

    _handler.Received.Count.ShouldBe(2);
  }

  [Fact]
  public async Task Run_WithWait_RefusedUpload_ShouldNotAskTheState()
  {
    _handler.Then(HttpStatusCode.Unauthorized, """{"errorCode":"Upload.TokenInvalid"}""");

    (await RunAsync(Settings(wait: true))).ShouldBeFalse();

    _handler.Received.ShouldHaveSingleItem();
  }

  // The upload is accepted: an answer without address is no reason to fail it, and none is ever built.
  [Theory]
  [InlineData("""{"uploadId":"0f8a4c1e-7d2b-4e6f-9a3c-5b1d2e3f4a5b","state":"Received"}""")]
  [InlineData("""{"statusUrl":"/api/v1/repos/github/octo-org/app/uploads/0f8a4c1e"}""")]
  [InlineData("""{"statusUrl":"ftp://coverage.example.com/status"}""")]
  [InlineData("""{"statusUrl":"http://coverage.example.com/api/v1/repos/github/octo-org/app/uploads/0f8a4c1e"}""")]
  [InlineData("not json")]
  public async Task Run_WithWait_AnswerWithoutUsableStatusUrl_ShouldWarnAndAskNothing(string body)
  {
    _handler.Then(HttpStatusCode.Accepted, body);

    (await RunAsync(Settings(wait: true))).ShouldBeTrue();

    _handler.Received.ShouldHaveSingleItem().Request.Method.ShouldBe(HttpMethod.Post);
    _output.ToString().ShouldContain("warning: The server gave no address to follow the processing: --wait is ignored.");
  }

  [Fact]
  public async Task Run_DryRun_ShouldSendNothingAndPrintTheRequest()
  {
    (await RunAsync(Settings(wait: true, dryRun: true))).ShouldBeTrue();

    _handler.Received.ShouldBeEmpty();

    var output = _output.ToString();

    output.ShouldContain("Dry run: nothing is sent to https://coverage.example.com/.");
    output.ShouldContain("  slug: octo-org/app");
    output.ShouldContain($"  sha: {new string('a', 40)}");
    output.ShouldContain("  flags: back");
    output.ShouldContain("  credential: an upload token (not printed)");
    output.ShouldContain("  file list: not sent");
    output.ShouldContain("coverage.cobertura.xml (11 bytes)");
    output.ShouldNotContain(Token);
  }

  [Fact]
  public async Task Run_Verbose_ShouldPrintTheContextAndTheRequest()
  {
    _handler.Then(HttpStatusCode.Accepted, Accepted);

    (await RunAsync(Settings(), verbose: true)).ShouldBeTrue();

    var output = _output.ToString();

    output.ShouldContain("Context: ");
    output.ShouldContain("slug octo-org/app");
    output.ShouldContain("POST https://coverage.example.com/api/v1/uploads");
    output.ShouldContain("Idempotency-Key ");
    output.ShouldContain("The server answered 202.");
    output.ShouldNotContain(Token);
  }

  [Fact]
  public async Task Run_NotVerbose_ShouldNotPrintTheDetails()
  {
    _handler.Then(HttpStatusCode.Accepted, Accepted);

    await RunAsync(Settings());

    _output.ToString().ShouldNotContain("Idempotency-Key");
    _output.ToString().ShouldNotContain("Context: ");
  }

  private UploaderSettings Settings(bool wait = false, bool dryRun = false, bool finalize = false)
  {
    var report = Path.Combine(_directory, "coverage.cobertura.xml");
    File.WriteAllText(report, "<coverage/>");

    return new UploaderSettings
    {
      Url = new Uri("https://coverage.example.com"),
      Token = Token,
      Files = [report],
      Flags = ["back"],
      Slug = "octo-org/app",
      Sha = new string('a', 40),
      Branch = "main",
      Root = _directory,
      NoNetwork = true,
      Wait = wait,
      DryRun = dryRun,
      Finalize = finalize,
    };
  }

  private Task<bool> RunAsync(UploaderSettings settings, bool verbose = false)
  {
    var client = new HttpClient(_handler, disposeHandler: false);
    var reporter = new ConsoleReporter(_output) { IsVerbose = verbose };
    string? Environment(string name) => null;
    Task Delay(TimeSpan wait, CancellationToken token)
    {
      _time.Advance(wait);

      return Task.CompletedTask;
    }

    var runner = new UploadRunner(
      new UploadClient(client, new RetryPolicy(Delay, reporter), new OidcTokenFetcher(client, Environment, reporter), reporter),
      new UploadStatusPoller(client, Delay, _time, reporter),
      new FinalizeClient(client, new RetryPolicy(Delay, reporter), reporter),
      Environment,
      reporter
    );

    return runner.RunAsync(settings, Ct);
  }

  #endregion
}
