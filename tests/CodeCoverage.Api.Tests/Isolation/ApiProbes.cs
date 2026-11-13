// ------------------------------------------------------------------------------------------------
// <copyright file="ApiProbes.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Reflection;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace CodeCoverage.Api.Tests.Isolation;

// The probe of every action of the API, and how the suite finds the actions. A new action with no probe fails the
// suite: whoever adds a route says, here, what an outsider receives from it.
public static class ApiProbes
{
  #region Fields

  private const string Repository = "/api/v1/repos/github/{owner}/app";

  private const string Organization = "/api/v1/orgs/github/{owner}";

  private const string Sha = "b2222222222222222222222222222222222222222";

  #endregion

  #region Properties

  // Every probe, by action. The ones with a path are sent at the other organization and at an owner that never
  // existed; the ones with a reason address no organization and are only declared.
  public static IReadOnlyList<ApiProbe> All { get; } =
  [
    // Personal tokens of the API: they belong to the user, not to an organization.
    ApiProbe.NotAddressed("ApiTokensController.CreateAsync", "A token of the signed-in user, named by no organization."),
    ApiProbe.NotAddressed("ApiTokensController.ListAsync", "The tokens of the signed-in user only."),
    ApiProbe.NotAddressed("ApiTokensController.RevokeAsync", "A token of the signed-in user, found by its identifier under that user."),

    // Badges: public by design, and probed by their own tests below.
    ApiProbe.Addressed("BadgesController.GetAsync", HttpMethod.Get, "/api/v1/badges/github/{owner}/app.svg"),
    ApiProbe.Addressed("BadgesController.GetCodecovAsync", HttpMethod.Get, "/github/{owner}/app/graph/badge.svg"),
    ApiProbe.Addressed("BadgesController.GetCodecovBranchAsync", HttpMethod.Get, "/github/{owner}/app/branch/main/graph/badge.svg"),

    ApiProbe.Addressed("CommitsController.ListCommitsAsync", HttpMethod.Get, $"{Repository}/commits"),
    ApiProbe.Addressed("CommitsController.GetCommitAsync", HttpMethod.Get, $"{Repository}/commits/{Sha}"),
    ApiProbe.Addressed("CommitsController.GetCommitFilesAsync", HttpMethod.Get, $"{Repository}/commits/{Sha}/files"),
    ApiProbe.Addressed("CommitsController.GetFileCoverageAsync", HttpMethod.Get, $"{Repository}/commits/{Sha}/files/src/app.cs"),
    ApiProbe.Addressed("CommitsController.NotifyAsync", HttpMethod.Post, $"{Repository}/commits/{Sha}/notify"),
    ApiProbe.Addressed("CommitsController.ReprocessAsync", HttpMethod.Post, $"{Repository}/commits/{Sha}/reprocess"),
    ApiProbe.Addressed("CommitsController.CompleteUploadsAsync", HttpMethod.Post, $"{Repository}/commits/{Sha}/uploads-complete"),

    ApiProbe.Addressed("ComparisonsController.CompareAsync", HttpMethod.Get, $"{Repository}/compare?base={Sha}&head={Sha}"),

    ApiProbe.NotAddressed("ConfigurationController.ValidateAsync", "A YAML is validated on its own, without a repository."),
    ApiProbe.NotAddressed("GitHubWebhooksController.ReceiveAsync", "The delivery names its own account, and its signature is the whole gate."),

    ApiProbe.NotAddressed("OperationsController.ListJobsAsync", "The queue of the instance, reserved to its administrators."),
    ApiProbe.NotAddressed("OperationsController.RetryJobAsync", "A job of the instance, reserved to its administrators."),
    ApiProbe.NotAddressed("OperationsController.ListWebhooksAsync", "The deliveries of the instance, reserved to its administrators."),
    ApiProbe.NotAddressed("OperationsController.GetStatusAsync", "The state of the instance, reserved to its administrators."),

    ApiProbe.NotAddressed("OrganizationsController.ListAsync", "The organizations of the signed-in user, which names none."),
    ApiProbe.Addressed("OrganizationsController.GetSettingsAsync", HttpMethod.Get, $"{Organization}/settings"),
    ApiProbe.Addressed("OrganizationsController.UpdateSettingAsync", HttpMethod.Put, $"{Organization}/settings/coverage.yaml", "\"coverage: {}\""),
    ApiProbe.Addressed("OrganizationsController.ResetSettingAsync", HttpMethod.Delete, $"{Organization}/settings/coverage.yaml"),

    ApiProbe.NotAddressed("PingController.PingAsync", "The version of the instance, the same for everyone."),

    ApiProbe.Addressed("PullRequestsController.ListPullRequestsAsync", HttpMethod.Get, $"{Repository}/pulls"),
    ApiProbe.Addressed("PullRequestsController.GetPullRequestComparisonAsync", HttpMethod.Get, $"{Repository}/pulls/1"),

    ApiProbe.Addressed("RepositoriesController.ListRepositoriesAsync", HttpMethod.Get, $"{Organization}/repos"),
    ApiProbe.Addressed("RepositoriesController.GetRepositoryAsync", HttpMethod.Get, Repository),
    ApiProbe.Addressed("RepositoriesController.ListBranchesAsync", HttpMethod.Get, $"{Repository}/branches"),
    ApiProbe.Addressed("RepositoriesController.GetCoverageHistoryAsync", HttpMethod.Get, $"{Repository}/history"),
    ApiProbe.Addressed("RepositoriesController.ActivateAsync", HttpMethod.Post, $"{Repository}/activate"),
    ApiProbe.Addressed("RepositoriesController.DeactivateAsync", HttpMethod.Post, $"{Repository}/deactivate"),
    ApiProbe.Addressed("RepositoriesController.RotateBadgeTokenAsync", HttpMethod.Post, $"{Repository}/badge-token"),
    ApiProbe.Addressed("RepositoriesController.ImportBadgeTokenAsync", HttpMethod.Put, $"{Repository}/badge-token", "{\"value\":\"imported\"}"),
    ApiProbe.Addressed("RepositoriesController.GetEffectiveConfigAsync", HttpMethod.Get, $"{Repository}/config"),

    ApiProbe.NotAddressed("SettingsController.GetAsync", "The settings of the instance, reserved to its administrators."),
    ApiProbe.NotAddressed("SettingsController.UpdateAsync", "A setting of the instance, reserved to its administrators."),
    ApiProbe.NotAddressed("SettingsController.ReplaceSecretAsync", "A secret of the instance, reserved to its administrators."),
    ApiProbe.NotAddressed("SettingsController.ResetAsync", "A setting of the instance, reserved to its administrators."),

    ApiProbe.Addressed("UploadTokensController.CreateOrganizationTokenAsync", HttpMethod.Post, $"{Organization}/upload-tokens", "{\"label\":\"probe\"}"),
    ApiProbe.Addressed("UploadTokensController.ListOrganizationTokensAsync", HttpMethod.Get, $"{Organization}/upload-tokens"),
    ApiProbe.Addressed("UploadTokensController.RevokeOrganizationTokenAsync", HttpMethod.Delete, $"{Organization}/upload-tokens/1"),
    ApiProbe.Addressed("UploadTokensController.CreateRepositoryTokenAsync", HttpMethod.Post, $"{Repository}/upload-tokens", "{\"label\":\"probe\"}"),
    ApiProbe.Addressed("UploadTokensController.ListRepositoryTokensAsync", HttpMethod.Get, $"{Repository}/upload-tokens"),
    ApiProbe.Addressed("UploadTokensController.RevokeRepositoryTokenAsync", HttpMethod.Delete, $"{Repository}/upload-tokens/1"),

    ApiProbe.NotAddressed("UploaderController.DownloadAsync", "A binary of the instance, the same for everyone."),

    ApiProbe.NotAddressed("UploadsController.CreateAsync", "The upload names its own repository, and its token is the whole gate; probed by its own test."),
    ApiProbe.Addressed("UploadsController.GetAsync", HttpMethod.Get, $"{Repository}/uploads/00000000-0000-0000-0000-000000000001"),
  ];

  #endregion

  #region Methods

  // Every action of the API, as "Controller.Method": the controllers of the host only, never the ones of the tests.
  public static IReadOnlyList<string> Actions()
  {
    var controllers = typeof(CodeCoverageApi).Assembly.GetTypes()
                                             .Where(static t => t is { IsAbstract: false, IsPublic: true } && typeof(ControllerBase).IsAssignableFrom(t));

    return
    [
      .. controllers.SelectMany(static c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                                             .Where(static m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
                                             .Select(m => $"{c.Name}.{m.Name}"))
                    .Order(StringComparer.Ordinal),
    ];
  }

  #endregion
}
