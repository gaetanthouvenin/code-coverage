// ------------------------------------------------------------------------------------------------
// <copyright file="CrossOrganizationRouteTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.RegularExpressions;
using System.Net;
using System.Text;
using System.Text.Json;

namespace CodeCoverage.Api.Tests.Isolation;

// M3.43a: what an outsider receives at the external boundary. Two organizations live on a real database; the user of
// the suite belongs to the first one only. For every action, its credential against the second organization must give
// the same answer as the same request sent at an owner that never existed.
[Collection(IsolationCollection.Name)]
public sealed partial class CrossOrganizationRouteTests(IsolationFixture fixture)
{
  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // A new action with no probe fails here: whoever adds a route says what an outsider receives from it.
  [Fact]
  public void EveryAction_ShouldDeclareProbe()
  {
    var declared = ApiProbes.All.Select(static p => p.Action).ToHashSet(StringComparer.Ordinal);
    var actions = ApiProbes.Actions();

    actions.ShouldNotBeEmpty();
    actions.Where(a => !declared.Contains(a))
           .ShouldBeEmpty("every action of the API declares a probe in ApiProbes.All, addressed to an organization or with the reason it addresses none");
    declared.Where(d => !actions.Contains(d))
            .ShouldBeEmpty("a probe names an action that no longer exists");
  }

  // Every organization-addressed action: the organization the caller knows nothing about answers exactly as an owner
  // that never existed. Status, error code and body, all three.
  [Theory]
  [MemberData(nameof(Addressed))]
  public async Task Request_OtherOrganization_ShouldMatchUnknownOwner(string action)
  {
    var probe = ApiProbes.All.Single(p => string.Equals(p.Action, action, StringComparison.Ordinal));
    var api = await fixture.ApiAsync();
    using var client = api.Client();

    var other = await SendAsync(client, probe, TwoOrganizationApi.SecondOwner);
    var unknown = await SendAsync(client, probe, TwoOrganizationApi.UnknownOwner);

    other.Status.ShouldBe(unknown.Status, $"{action}: the second organization answers {other.Status}, an unknown owner {unknown.Status}");
    other.ErrorCode.ShouldBe(unknown.ErrorCode, $"{action}: the error codes differ");
    other.Body.ShouldBe(unknown.Body, $"{action}: the bodies differ");

    // A badge answers 200 with the badge of an unknown repository, the same bytes for both; everything else refuses.
    other.Status.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.UnprocessableEntity, HttpStatusCode.BadRequest);
  }

  // A badge of a private repository is public by route: a wrong token and an unknown repository answer the same
  // bytes, the ETag included, so nothing tells one from the other.
  [Fact]
  public async Task Badge_PrivateRepositoryWrongToken_ShouldMatchUnknown()
  {
    var api = await fixture.ApiAsync();
    using var client = api.Client(authenticated: false);

    using var wrong = await client.GetAsync(Address($"/api/v1/badges/github/{TwoOrganizationApi.SecondOwner}/app.svg?token=junk"), Ct);
    using var unknown = await client.GetAsync(Address($"/api/v1/badges/github/{TwoOrganizationApi.UnknownOwner}/app.svg?token=junk"), Ct);

    wrong.StatusCode.ShouldBe(unknown.StatusCode);
    wrong.Headers.ETag?.Tag.ShouldBe(unknown.Headers.ETag?.Tag);
    (await wrong.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(await unknown.Content.ReadAsByteArrayAsync(Ct));
  }

  // An upload token that is not of this instance and an owner that never existed answer the same refusal: a junk
  // token never tells whether the repository it names exists.
  [Fact]
  public async Task Upload_JunkTokenActiveAndUnknownOwner_ShouldBeIdentical()
  {
    var api = await fixture.ApiAsync();
    using var client = api.Client(authenticated: false);

    var active = await PostUploadAsync(client, TwoOrganizationApi.SecondOwner);
    var unknown = await PostUploadAsync(client, TwoOrganizationApi.UnknownOwner);

    active.Status.ShouldBe(unknown.Status);
    active.ErrorCode.ShouldBe(unknown.ErrorCode);
    active.Body.ShouldBe(unknown.Body);
  }

  // A real upload of the second organization, asked for with the token of the first: the same answer as an owner that
  // never existed, so the identifier of an upload tells nothing either.
  [Fact]
  public async Task GetUpload_ApiTokenOnRealUploadOfOtherOrganization_ShouldMatchUnknownOwner()
  {
    var api = await fixture.ApiAsync();
    using var client = api.Client();
    var upload = api.SecondUpload.ToString("D", CultureInfo.InvariantCulture);

    var real = await ReadAsync(client, HttpMethod.Get, $"/api/v1/repos/github/{TwoOrganizationApi.SecondOwner}/app/uploads/{upload}");
    var unknown = await ReadAsync(client, HttpMethod.Get, $"/api/v1/repos/github/{TwoOrganizationApi.UnknownOwner}/app/uploads/{upload}");

    real.Status.ShouldBe(unknown.Status);
    real.ErrorCode.ShouldBe(unknown.ErrorCode);
    real.Body.ShouldBe(unknown.Body);
  }

  // The user reads the repository of its own organization, and only that one: the same request on the other
  // organization answers as an unknown owner.
  [Fact]
  public async Task Request_OutsideCollaborator_ShouldReachOnlyThatRepository()
  {
    var api = await fixture.ApiAsync();
    using var client = api.Client();

    var own = await ReadAsync(client, HttpMethod.Get, $"/api/v1/repos/github/{TwoOrganizationApi.FirstOwner}/app");
    var other = await ReadAsync(client, HttpMethod.Get, $"/api/v1/repos/github/{TwoOrganizationApi.SecondOwner}/app");

    own.Status.ShouldBe(HttpStatusCode.OK);
    other.Status.ShouldBe(HttpStatusCode.NotFound);
  }

  public static TheoryData<string> Addressed()
  {
    var data = new TheoryData<string>();

    foreach (var probe in ApiProbes.All.Where(static p => p.IsCrossOrganization))
    {
      data.Add(probe.Action);
    }

    return data;
  }

  private static Uri Address(string path) => new(path, UriKind.Relative);

  private static async Task<Answer> SendAsync(HttpClient client, ApiProbe probe, string owner) =>
    await ReadAsync(client, probe.Method!, probe.Path!.Replace("{owner}", owner, StringComparison.Ordinal), probe.Body);

  private static async Task<Answer> PostUploadAsync(HttpClient client, string owner)
  {
    var body = JsonSerializer.Serialize(new
    {
      service = "github",
      slug = $"{owner}/app",
      commit = "b2222222222222222222222222222222222222222",
      branch = "main",
    });

    using var request = new HttpRequestMessage(HttpMethod.Post, Address("/api/v1/uploads"))
    {
      Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    request.Headers.Add("X-Upload-Token", "junk-token-that-is-of-no-instance");

    return await ReadAsync(client, request);
  }

  private static async Task<Answer> ReadAsync(HttpClient client, HttpMethod method, string path, string? body = null)
  {
    using var request = new HttpRequestMessage(method, Address(path));

    if (body is not null)
    {
      request.Content = new StringContent(body, Encoding.UTF8, "application/json");
    }

    return await ReadAsync(client, request);
  }

  private static async Task<Answer> ReadAsync(HttpClient client, HttpRequestMessage request)
  {
    using var response = await client.SendAsync(request, Ct);
    var text = await response.Content.ReadAsStringAsync(Ct);

    return new Answer(response.StatusCode, ErrorCodeOf(text), WithoutTraceId(text));
  }

  // The trace identifier belongs to the request, not to the answer: two answers are the same when everything else is.
  private static string WithoutTraceId(string body) => TraceId().Replace(body, "\"traceId\":\"trace\"");

  private static string? ErrorCodeOf(string body)
  {
    if (body.Length == 0)
    {
      return null;
    }

    try
    {
      return JsonDocument.Parse(body).RootElement.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
    }
    catch (JsonException)
    {
      return null;
    }
  }

  #endregion

  [GeneratedRegex("\"traceId\":\"[^\"]*\"")]
  private static partial Regex TraceId();

}
