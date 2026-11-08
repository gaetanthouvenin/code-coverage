// ------------------------------------------------------------------------------------------------
// <copyright file="ApiExceptionHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Text.Json;

using CodeCoverage.Api.Configuration;
using CodeCoverage.Api.Tests.Support;

namespace CodeCoverage.Api.Tests.Errors;

[Collection(CodeCoverageApiCollection.Name)]
public sealed class ApiExceptionHandlerTests(CodeCoverageApiFactory factory)
{
  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Get_WhenValidationFails_ShouldReturn400WithErrors()
  {
    var (response, problem) = await GetProblemAsync("validation");

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    problem.GetProperty("status").GetInt32().ShouldBe(400);
    problem.GetProperty("errorCode").GetString().ShouldBe("General.ValidationFailed");

    var errors = problem.GetProperty("errors");
    errors.GetProperty("Name").EnumerateArray().Select(e => e.GetString()).ShouldBe(
      ["'Name' must not be empty.", "'Name' must be 100 characters or fewer."]);
    errors.GetProperty("Branch").EnumerateArray().Select(e => e.GetString()).ShouldBe(["'Branch' must not be empty."]);
  }

  [Fact]
  public async Task Get_WhenNotFound_ShouldReturn404WithErrorCode()
  {
    var (response, problem) = await GetProblemAsync("not-found");

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    problem.GetProperty("errorCode").GetString().ShouldBe("Repository.NotFound");
    problem.GetProperty("detail").GetString().ShouldNotBeNullOrWhiteSpace();
    problem.TryGetProperty("errors", out _).ShouldBeFalse();
  }

  [Fact]
  public async Task Get_WhenBug_ShouldReturn500WithoutDetail()
  {
    using var client = factory.CreateClient();

    using var response = await client.GetAsync($"{ThrowingController.Path}/bug", Ct);
    var body = await response.Content.ReadAsStringAsync(Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    body.ShouldNotContain(ThrowingController.BugMessage);
    body.ShouldNotContain("secret");

    using var document = JsonDocument.Parse(body);
    var problem = document.RootElement;
    problem.GetProperty("title").GetString().ShouldBe("Internal server error");
    problem.TryGetProperty("detail", out _).ShouldBeFalse();
    problem.TryGetProperty("errorCode", out _).ShouldBeFalse();
  }

  [Fact]
  public async Task Get_WhenBadGateway_ShouldReturn502()
  {
    var (response, problem) = await GetProblemAsync("bad-gateway");

    response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
    problem.GetProperty("errorCode").GetString().ShouldBe("GitHub.Unavailable");
  }

  [Theory]
  [InlineData("bad-request", HttpStatusCode.BadRequest)]
  [InlineData("validation", HttpStatusCode.BadRequest)]
  [InlineData("unauthorized", HttpStatusCode.Unauthorized)]
  [InlineData("forbidden", HttpStatusCode.Forbidden)]
  [InlineData("not-found", HttpStatusCode.NotFound)]
  [InlineData("conflict", HttpStatusCode.Conflict)]
  [InlineData("unprocessable", HttpStatusCode.UnprocessableEntity)]
  [InlineData("bad-gateway", HttpStatusCode.BadGateway)]
  [InlineData("retry-later", HttpStatusCode.InternalServerError)]
  [InlineData("bug", HttpStatusCode.InternalServerError)]
  public async Task Get_WhenThrown_ShouldReturnTheStatusOfTheException(string kind, HttpStatusCode expected)
  {
    var (response, problem) = await GetProblemAsync(kind);

    response.StatusCode.ShouldBe(expected);
    response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    problem.GetProperty("status").GetInt32().ShouldBe((int)expected);
  }

  [Fact]
  public async Task SecurityHeaders_ShouldBePresent()
  {
    using var client = factory.CreateClient();

    using var ok = await client.GetAsync(HealthProbes.LivePath, Ct);
    using var refused = await client.GetAsync($"{ThrowingController.Path}/forbidden", Ct);

    foreach (var response in new[] { ok, refused })
    {
      response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
      response.Headers.GetValues("Referrer-Policy").ShouldBe(["strict-origin-when-cross-origin"]);
      response.Headers.GetValues("Permissions-Policy").ShouldBe(["camera=(), microphone=(), geolocation=(), payment=()"]);
      response.Headers.GetValues("Cross-Origin-Opener-Policy").ShouldBe(["same-origin"]);
      response.Headers.GetValues("Content-Security-Policy").ShouldBe(["frame-ancestors 'none'"]);
      response.Headers.Contains("X-Frame-Options").ShouldBeFalse();
    }
  }

  private async Task<(HttpResponseMessage Response, JsonElement Problem)> GetProblemAsync(string kind)
  {
    using var client = factory.CreateClient();

    var response = await client.GetAsync($"{ThrowingController.Path}/{kind}", Ct);
    var body = await response.Content.ReadAsStringAsync(Ct);

    using var document = JsonDocument.Parse(body);
    return (response, document.RootElement.Clone());
  }

  #endregion
}
