// ------------------------------------------------------------------------------------------------
// <copyright file="UploadStatusPollerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;

using CodeCoverage.Uploader.Commands;
using CodeCoverage.Uploader.Http;
using CodeCoverage.Uploader.Output;

namespace CodeCoverage.Uploader.Tests.Http;

public sealed class UploadStatusPollerTests
{
  #region Fields

  private const string Credential = "cvc_token";

  private const string Processing = """{"id":"0f8a4c1e-7d2b-4e6f-9a3c-5b1d2e3f4a5b","state":"Processing","resolvedFiles":0,"unresolvedFiles":0,"log":{"messages":[],"messageTotal":0,"paths":[],"pathTotal":0},"error":null}""";

  private const string Processed = """{"id":"0f8a4c1e-7d2b-4e6f-9a3c-5b1d2e3f4a5b","state":"Processed","flags":["back"],"name":null,"resolvedFiles":412,"unresolvedFiles":3,"totals":{"lines":10,"hits":9,"misses":1,"partials":0,"coverage":90.0},"log":{"messages":["no file list: paths not verified"],"messageTotal":1,"paths":["src/a.cs","src/b.cs","src/c.cs"],"pathTotal":3},"error":null}""";

  private const string Failed = """{"state":"Failed","resolvedFiles":0,"unresolvedFiles":2,"log":{"messages":[],"messageTotal":0,"paths":["x.cs","y.cs"],"pathTotal":2},"error":"No file of the reports was found in the repository."}""";

  // Not the usual route, on purpose: the uploader asks what the server returned, whatever it is.
  private static readonly Uri StatusUrl = new("https://api.coverage.example.com/somewhere/else/status?of=0f8a4c1e&v=2");

  private readonly FakeHttpHandler _handler = new();

  private readonly ManualTimeProvider _time = new();

  private readonly List<TimeSpan> _waits = [];

  private readonly StringWriter _output = new();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Poll_UntilProcessed_ShouldPrintResolvedCounts()
  {
    _handler.Then(HttpStatusCode.OK, Processing).Then(HttpStatusCode.OK, Processing).Then(HttpStatusCode.OK, Processed);

    (await Poller().PollAsync(StatusUrl, Credential, Ct)).ShouldBeTrue();

    var output = _output.ToString();

    output.ShouldContain("resolved 412/415 files; unresolved: src/a.cs, src/b.cs, src/c.cs");
    output.ShouldContain("no file list: paths not verified");
    output.ShouldNotContain("error:");
    output.ShouldNotContain(Credential);
    _waits.ShouldBe([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)]);
  }

  [Fact]
  public async Task Poll_ShouldOnlyAskTheAddressTheServerReturned()
  {
    _handler.Then(HttpStatusCode.OK, Processing).Then(HttpStatusCode.OK, Processed);

    await Poller().PollAsync(StatusUrl, Credential, Ct);

    _handler.Received.Count.ShouldBe(2);
    _handler.Received.ShouldAllBe(static received => received.Request.Method == HttpMethod.Get);
    _handler.Received.ShouldAllBe(static received => received.Request.RequestUri == StatusUrl);
    _handler.Received.ShouldAllBe(static received => received.Request.RequestUri!.AbsoluteUri == StatusUrl.AbsoluteUri);
    _handler.Received.ShouldAllBe(static received => received.Request.Headers.Authorization!.Scheme == "Bearer"
                                                     && received.Request.Headers.Authorization.Parameter == Credential);
  }

  [Fact]
  public async Task Poll_Failed_ShouldPrintTheErrorAndReturnFalse()
  {
    _handler.Then(HttpStatusCode.OK, Failed);

    (await Poller().PollAsync(StatusUrl, Credential, Ct)).ShouldBeFalse();

    var output = _output.ToString();

    output.ShouldContain("error: The processing of the upload failed: No file of the reports was found in the repository.");
    output.ShouldContain("resolved 0/2 files; unresolved: x.cs, y.cs");
    _waits.ShouldBeEmpty();
  }

  // The processing never ends: the wait stops at 120 s, says where the state is, and the build goes on.
  [Fact]
  public async Task Poll_Timeout_ShouldExitZero()
  {
    for (var i = 0; i < 100; i++)
    {
      _handler.Then(HttpStatusCode.OK, Processing);
    }

    var exitCode = await UploadCommandFactory.RunAsync(
                     ["--url", "https://coverage.example.com", "--wait"],
                     static _ => null,
                     new ConsoleReporter(_output),
                     (_, token) => Poller().PollAsync(StatusUrl, Credential, token),
                     Ct
                   );

    exitCode.ShouldBe(ExitCodePolicy.Success);
    _waits.Sum(static wait => wait.TotalSeconds).ShouldBe(120);
    _waits.ShouldAllBe(static wait => wait <= TimeSpan.FromSeconds(5));
    _handler.Received.Count.ShouldBe(_waits.Count);
    _output.ToString().ShouldContain($"warning: The upload is still being processed after 120 s: its state is at {StatusUrl}");
  }

  // Asked for explicitly, a failure is told to the build: a timeout is not a success.
  [Fact]
  public async Task Poll_Timeout_WithFailOnError_ShouldExitOne()
  {
    for (var i = 0; i < 100; i++)
    {
      _handler.Then(HttpStatusCode.OK, Processing);
    }

    var exitCode = await UploadCommandFactory.RunAsync(
                     ["--url", "https://coverage.example.com", "--wait", "--fail-on-error"],
                     static _ => null,
                     new ConsoleReporter(_output),
                     (_, token) => Poller().PollAsync(StatusUrl, Credential, token),
                     Ct
                   );

    exitCode.ShouldBe(ExitCodePolicy.Failure);
  }

  // Each answer took longer than the timeout: no second question is asked past it.
  [Fact]
  public async Task Poll_AnswerSlowerThanTheTimeout_ShouldStopWithoutWaiting()
  {
    _handler.Then(HttpStatusCode.OK, Processing, _ => _time.Advance(TimeSpan.FromSeconds(121)));

    (await Poller().PollAsync(StatusUrl, Credential, Ct)).ShouldBeFalse();

    _handler.Received.Count.ShouldBe(1);
    _waits.ShouldBeEmpty();
    _output.ToString().ShouldContain("still being processed");
  }

  [Fact]
  public async Task Poll_TransientFailures_ShouldKeepAsking()
  {
    _handler.Then(HttpStatusCode.ServiceUnavailable)
            .ThenThrow(new HttpRequestException("connection reset"))
            .Then(HttpStatusCode.TooManyRequests)
            .Then(HttpStatusCode.OK, "not json")
            .Then(HttpStatusCode.OK, "{}")
            .Then(HttpStatusCode.OK, Processed);

    (await Poller().PollAsync(StatusUrl, Credential, Ct)).ShouldBeTrue();

    _handler.Received.Count.ShouldBe(6);
    _output.ToString().ShouldNotContain("error:");
  }

  [Theory]
  [InlineData(HttpStatusCode.Unauthorized)]
  [InlineData(HttpStatusCode.Forbidden)]
  [InlineData(HttpStatusCode.NotFound)]
  public async Task Poll_Refused_ShouldStopAtOnce(HttpStatusCode status)
  {
    _handler.Then(status, """{"errorCode":"Upload.NotFound"}""");

    (await Poller().PollAsync(StatusUrl, Credential, Ct)).ShouldBeFalse();

    _handler.Received.Count.ShouldBe(1);
    _waits.ShouldBeEmpty();
    _output.ToString().ShouldContain($"error: The server refused to give the state of the upload: {(int)status}.");
  }

  [Fact]
  public async Task Poll_Refused_WithoutFailOnError_ShouldExitZero()
  {
    _handler.Then(HttpStatusCode.NotFound);

    var exitCode = await UploadCommandFactory.RunAsync(
                     ["--url", "https://coverage.example.com", "--wait"],
                     static _ => null,
                     new ConsoleReporter(_output),
                     (_, token) => Poller().PollAsync(StatusUrl, Credential, token),
                     Ct
                   );

    exitCode.ShouldBe(ExitCodePolicy.Success);
  }

  [Fact]
  public async Task Poll_Verbose_ShouldPrintEachState()
  {
    _handler.Then(HttpStatusCode.OK, Processing).Then(HttpStatusCode.OK, Processed);

    await Poller(verbose: true).PollAsync(StatusUrl, Credential, Ct);

    _output.ToString().ShouldContain("The upload is Processing.");
  }

  [Fact]
  public async Task Poll_Canceled_ShouldThrowForTheExitPolicyToDecide()
  {
    using var canceled = new CancellationTokenSource();
    await canceled.CancelAsync();
    _handler.ThenThrow(new TaskCanceledException());

    await Should.ThrowAsync<OperationCanceledException>(() => Poller().PollAsync(StatusUrl, Credential, canceled.Token));
  }

  private UploadStatusPoller Poller(bool verbose = false)
  {
    var client = new HttpClient(_handler, disposeHandler: false);
    var reporter = new ConsoleReporter(_output) { IsVerbose = verbose };

    return new UploadStatusPoller(
      client,
      (wait, _) =>
      {
        _waits.Add(wait);
        _time.Advance(wait);

        return Task.CompletedTask;
      },
      _time,
      reporter
    );
  }

  #endregion
}
