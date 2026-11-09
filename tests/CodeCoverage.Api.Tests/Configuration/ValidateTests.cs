// ------------------------------------------------------------------------------------------------
// <copyright file="ValidateTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using CodeCoverage.Api.Configuration.RateLimiting;
using CodeCoverage.Api.Contracts.Configuration;
using CodeCoverage.Api.Tests.Support;

namespace CodeCoverage.Api.Tests.Configuration;

[Collection(CodeCoverageApiCollection.Name)]
public sealed class ValidateTests(CodeCoverageApiFactory factory)
{
  #region Fields

  private const string ValidatePath = CodeCoverageApi.Prefix + "/validate";

  private const string ValidYaml = "comment: false\ncoverage:\n  precision: 2\n";

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Post_ShouldBeRateLimited()
  {
    // A host of its own: the limiter of the shared host must stay available to the other tests.
    await using var host = factory.WithWebHostBuilder(_ => { });
    using var client = host.CreateClient();

    for (var request = 0; request < ValidateRateLimiterConfiguration.PermitLimit; request++)
    {
      using var accepted = await PostAsync(client, ValidYaml);

      accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    using var refused = await PostAsync(client, ValidYaml);
    using var problem = JsonDocument.Parse(await refused.Content.ReadAsStringAsync(Ct));

    refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    refused.Headers.RetryAfter.ShouldNotBeNull();
    problem.RootElement.GetProperty("status").GetInt32().ShouldBe(429);
  }

  [Fact]
  public async Task Post_ValidFile_ShouldReturnItsNormalizedContent()
  {
    using var client = factory.CreateClient();

    using var response = await PostAsync(client, ValidYaml);
    var body = await response.Content.ReadFromJsonAsync<ValidationResultResponse>(Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    body.ShouldNotBeNull().Valid.ShouldBeTrue();
    body.Warnings.ShouldBeEmpty();
    body.Errors.ShouldBeEmpty();

    var normalized = body.Normalized.ShouldNotBeNull();
    normalized.GetProperty("comment").GetBoolean().ShouldBeFalse();
    normalized.GetProperty("coverage").GetProperty("precision").GetInt32().ShouldBe(2);
    normalized.TryGetProperty("codecov", out _).ShouldBeFalse();
  }

  [Fact]
  public async Task Post_UnknownKey_ShouldWarnAndStayValid()
  {
    using var client = factory.CreateClient();

    using var response = await PostAsync(client, "nope: 1\n");
    var body = await response.Content.ReadFromJsonAsync<ValidationResultResponse>(Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    body.ShouldNotBeNull().Valid.ShouldBeTrue();
    body.Warnings.ShouldBe([new ConfigWarningModel("Config.UnknownKey", "nope", null)]);
  }

  [Fact]
  public async Task Post_KeyWarnedByTheParserAndTheStatusRules_ShouldWarnOnce()
  {
    const string Yaml = "coverage:\n  status:\n    project:\n      default:\n        if_ci_failed: error\n";
    using var client = factory.CreateClient();

    using var response = await PostAsync(client, Yaml);
    var body = await response.Content.ReadFromJsonAsync<ValidationResultResponse>(Ct);

    body.ShouldNotBeNull().Warnings.ShouldBe(
      [new ConfigWarningModel("Config.WithoutEffectKey", "coverage.status.project.default.if_ci_failed", null)]);
  }

  [Theory]
  [InlineData("")]
  [InlineData("# only a comment\n")]
  [InlineData("foo: - 123")]
  [InlineData("- a list")]
  public async Task Post_UnreadableFile_ShouldReturn400WithTheError(string yaml)
  {
    using var client = factory.CreateClient();

    using var response = await PostAsync(client, yaml);
    var body = await response.Content.ReadFromJsonAsync<ValidationResultResponse>(Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    body.ShouldNotBeNull().Valid.ShouldBeFalse();
    body.Normalized.ShouldBeNull();
    body.Errors.Count.ShouldBe(1);
  }

  [Theory]
  [InlineData(40)]
  [InlineData(64)]
  public async Task Post_DeepestFileTheParserAccepts_ShouldReturnIt(int levels)
  {
    var yaml = string.Concat(Enumerable.Repeat("{a: ", levels)) + "1" + new string('}', levels);
    using var client = factory.CreateClient();

    using var response = await PostAsync(client, yaml);
    var body = await response.Content.ReadAsStringAsync(Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
    body.ShouldContain("\"valid\":true");
  }

  [Fact]
  public async Task Post_FormContentTypeOfCurl_ShouldReadTheBodyAsTheFile()
  {
    using var client = factory.CreateClient();
    using var content = new StringContent(ValidYaml, Encoding.UTF8, "application/x-www-form-urlencoded");

    using var response = await client.PostAsync(ValidatePath, content, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
  }

  private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string yaml)
  {
    using var content = new StringContent(yaml, Encoding.UTF8, "text/plain");

    return await client.PostAsync(ValidatePath, content, Ct);
  }

  #endregion
}
