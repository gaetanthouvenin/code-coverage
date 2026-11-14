// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubAppManifestTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Domain.Tests.Settings;

public sealed class GitHubAppManifestTests
{
  #region Fields

  private static readonly Uri Web = new("https://coverage.example.com/");

  private static readonly Uri Api = new("https://coverage-api.example.com");

  #endregion

  #region Methods

  [Fact]
  public void ToJson_ShouldCreateAPublicAppWithItsAddresses()
  {
    using var manifest = JsonDocument.Parse(GitHubAppManifest.ToJson("CodeCoverage", Web, Api));
    var root = manifest.RootElement;

    root.GetProperty("name").GetString().ShouldBe("CodeCoverage");
    root.GetProperty("public").GetBoolean().ShouldBeTrue();
    root.GetProperty("hook_attributes").GetProperty("url").GetString().ShouldBe("https://coverage-api.example.com/api/v1/github/webhooks");
    root.GetProperty("redirect_url").GetString().ShouldBe("https://coverage.example.com/setup");
    root.GetProperty("callback_urls")[0].GetString().ShouldBe("https://coverage.example.com/signin-github");
  }

  [Fact]
  public void ToJson_ShouldAskTheMembersOfTheOrganizationsAndNothingToWriteTheCode()
  {
    using var manifest = JsonDocument.Parse(GitHubAppManifest.ToJson("CodeCoverage", Web, Api));
    var permissions = manifest.RootElement.GetProperty("default_permissions");

    permissions.GetProperty("members").GetString().ShouldBe("read");
    permissions.GetProperty("contents").GetString().ShouldBe("read");
    permissions.GetProperty("statuses").GetString().ShouldBe("write");
    permissions.GetProperty("pull_requests").GetString().ShouldBe("write");
    // M5.05: the Check Runs replace the statuses once an organization turns Notifications:ChecksEnabled on.
    permissions.GetProperty("checks").GetString().ShouldBe("write");
  }

  [Fact]
  public void ToJson_ShouldSubscribeToTheEventsOfTheOrganizations()
  {
    using var manifest = JsonDocument.Parse(GitHubAppManifest.ToJson("CodeCoverage", Web, Api));
    var events = manifest.RootElement.GetProperty("default_events").EnumerateArray().Select(static e => e.GetString()).ToList();

    events.ShouldBe(["installation_target", "organization", "member", "membership", "pull_request", "push", "repository", "delete"], ignoreOrder: true);
  }

  [Fact]
  public void ToJson_BlankName_ShouldThrow()
  {
    Should.Throw<ArgumentException>(() => GitHubAppManifest.ToJson(" ", Web, Api));
  }

  [Theory]
  [InlineData(true, true, true, true)]
  [InlineData(false, true, true, false)]
  [InlineData(true, false, true, false)]
  [InlineData(true, true, false, false)]
  public void SetupState_IsComplete_ShouldNeedEveryStep(bool app, bool urls, bool administrators, bool expected)
  {
    new SetupState(app, urls, administrators).IsComplete.ShouldBe(expected);
  }

  #endregion
}
