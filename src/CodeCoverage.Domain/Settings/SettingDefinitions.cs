// ------------------------------------------------------------------------------------------------
// <copyright file="SettingDefinitions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

namespace CodeCoverage.Domain.Settings;

/// <summary>The catalog of the settings kept in the settings store, in display order.</summary>
/// <remarks>
///   <para>
///     The catalog lists every setting that can be seen and changed from the screen or the API, at
///     the level given by its <see cref="SettingScope" />. The bootstrap values (connection string, database provider, data path, Data Protection)
///     come from the environment only and are not listed.
///   </para>
///   <para>
///     A key is found whatever its case, as in <c>IConfiguration</c>.
///   </para>
/// </remarks>
public static class SettingDefinitions
{
  #region Fields

  /// <summary>The group of the GitHub App and OAuth settings.</summary>
  public const string GitHubGroup = "GitHub";

  /// <summary>The group of the public addresses of the hosts.</summary>
  public const string UrlsGroup = "Urls";

  /// <summary>The group of the upload limits and authentication.</summary>
  public const string UploadsGroup = "Uploads";

  /// <summary>The group of the background processing.</summary>
  public const string ProcessingGroup = "Processing";

  /// <summary>The group of the commit statuses and pull request comments.</summary>
  public const string NotificationsGroup = "Notifications";

  /// <summary>The group of the coverage rules.</summary>
  public const string CoverageGroup = "Coverage";

  /// <summary>The group of the badges.</summary>
  public const string BadgesGroup = "Badges";

  /// <summary>The group of the access rules.</summary>
  public const string AccessGroup = "Access";

  /// <summary>The group of the data retention.</summary>
  public const string RetentionGroup = "Retention";

  /// <summary>The key of the identifier of the GitHub App.</summary>
  public const string GitHubAppId = "GitHub:AppId";

  /// <summary>The key of the OAuth client identifier of the GitHub App.</summary>
  public const string GitHubClientId = "GitHub:ClientId";

  /// <summary>The key of the slug of the GitHub App, used in its public links.</summary>
  public const string GitHubAppSlug = "GitHub:AppSlug";

  /// <summary>The key of the OAuth client secret of the GitHub App (secret).</summary>
  public const string GitHubClientSecret = "GitHub:ClientSecret";

  /// <summary>The key of the PEM private key of the GitHub App (secret).</summary>
  public const string GitHubPrivateKeyPem = "GitHub:PrivateKeyPem";

  /// <summary>The key of the secret that signs the GitHub webhooks (secret).</summary>
  public const string GitHubWebhookSecret = "GitHub:WebhookSecret";

  /// <summary>
  ///   The key of the previous webhook secret, still accepted while a rotation is in progress
  ///   (secret).
  /// </summary>
  public const string GitHubWebhookSecretPrevious = "GitHub:WebhookSecretPrevious";

  /// <summary>The key of the base URL of the GitHub REST API.</summary>
  public const string GitHubApiBaseUrl = "GitHub:ApiBaseUrl";

  /// <summary>The key of the public URL of the Api host.</summary>
  public const string UrlsApiPublicUrl = "Urls:ApiPublicUrl";

  /// <summary>The key of the public URL of the Web host.</summary>
  public const string UrlsWebPublicUrl = "Urls:WebPublicUrl";

  /// <summary>The key of the largest accepted upload request, in megabytes.</summary>
  public const string UploadsMaxRequestMegabytes = "Uploads:MaxRequestMegabytes";

  /// <summary>The key of the largest decompressed size of an upload, in megabytes.</summary>
  public const string UploadsMaxDecompressedMegabytes = "Uploads:MaxDecompressedMegabytes";

  /// <summary>The key of the largest number of report files in one upload.</summary>
  public const string UploadsMaxFilesPerUpload = "Uploads:MaxFilesPerUpload";

  /// <summary>The key of the number of uploads accepted per minute, by token and by repository.</summary>
  public const string UploadsRateLimitPerMinute = "Uploads:RateLimitPerMinute";

  /// <summary>
  ///   The key of the audience expected in a GitHub Actions OIDC token; the public URL of the Api
  ///   when not set.
  /// </summary>
  public const string UploadsOidcAudience = "Uploads:OidcAudience";

  /// <summary>The key of the number of jobs run at the same time by a worker.</summary>
  public const string ProcessingWorkerConcurrency = "Processing:WorkerConcurrency";

  /// <summary>The key of the number of reports parsed at the same time.</summary>
  public const string ProcessingMaxParallelParses = "Processing:MaxParallelParses";

  /// <summary>The key of the number of attempts of a job before it fails for good.</summary>
  public const string ProcessingJobMaxAttempts = "Processing:JobMaxAttempts";

  /// <summary>The key of the number of ancestors searched for a carried-forward flag.</summary>
  public const string ProcessingCarryforwardMaxDepth = "Processing:CarryforwardMaxDepth";

  /// <summary>The key of the time waited for the parent of a carried-forward flag, in minutes.</summary>
  public const string ProcessingCarryforwardWaitMinutes = "Processing:CarryforwardWaitMinutes";

  /// <summary>The key of the number of ancestors walked to find the base of a comparison.</summary>
  public const string ProcessingBaseMaxAncestorWalk = "Processing:BaseMaxAncestorWalk";

  /// <summary>The key of the time waited for the base of a comparison, in minutes.</summary>
  public const string ProcessingBaseWaitMinutes = "Processing:BaseWaitMinutes";

  /// <summary>The key of the time after which a pending commit status is closed, in minutes.</summary>
  public const string ProcessingPendingTimeoutMinutes = "Processing:PendingTimeoutMinutes";

  /// <summary>The key of the prefix of the commit status contexts.</summary>
  public const string NotificationsStatusContextPrefix = "Notifications:StatusContextPrefix";

  /// <summary>The key of the switch of the commit statuses.</summary>
  public const string NotificationsStatusesEnabled = "Notifications:StatusesEnabled";

  /// <summary>The key of the switch of the pull request comments.</summary>
  public const string NotificationsCommentsEnabled = "Notifications:CommentsEnabled";

  /// <summary>
  ///   The key of the switch that publishes GitHub Check Runs in place of the commit statuses (M5.05, ADR-007), off by
  ///   default: each organization turns it on once it granted the Checks permission to the App.
  /// </summary>
  public const string NotificationsChecksEnabled = "Notifications:ChecksEnabled";

  /// <summary>The key of the time waited for further uploads before notifying, in seconds.</summary>
  public const string NotificationsDebounceSeconds = "Notifications:DebounceSeconds";

  /// <summary>The key of the longest delay of a notification by the debounce, in seconds.</summary>
  public const string NotificationsMaxDelaySeconds = "Notifications:MaxDelaySeconds";

  /// <summary>
  ///   The key of the switch that counts the carried-forward sessions in <c>after_n_builds</c>.
  /// </summary>
  public const string NotificationsAfterNBuildsCountsCarryforward =
    "Notifications:AfterNBuildsCountsCarryforward";

  /// <summary>The key of the title of the pull request comment.</summary>
  public const string NotificationsCommentTitle = "Notifications:CommentTitle";

  /// <summary>The key of the YAML configuration shared by every repository of the organization.</summary>
  public const string CoverageOrganizationYaml = "Coverage:OrganizationYaml";

  /// <summary>
  ///   The key of the switch that matches a report path to a repository file by its name alone.
  /// </summary>
  public const string CoverageAllowFilenameOnlyMatch = "Coverage:AllowFilenameOnlyMatch";

  /// <summary>The key of the label shown on the left of the badges.</summary>
  public const string BadgesLabel = "Badges:Label";

  /// <summary>
  ///   The key of the numeric GitHub user identifiers of the instance administrators, separated by
  ///   commas.
  /// </summary>
  public const string AccessAdminUserIds = "Access:AdminUserIds";

  /// <summary>The key of the time the GitHub permissions of a user are cached, in minutes.</summary>
  public const string AccessCacheMinutes = "Access:CacheMinutes";

  /// <summary>
  ///   The key of the age, in minutes, beyond which a permission or a membership read from GitHub
  ///   grants nothing, even when it could not be refreshed.
  /// </summary>
  public const string AccessHardExpiryMinutes = "Access:HardExpiryMinutes";

  /// <summary>The key of the number of days the raw uploaded reports are kept.</summary>
  public const string RetentionRawDays = "Retention:RawDays";

  /// <summary>The key of the number of days the reports of a deleted branch are kept.</summary>
  public const string RetentionBranchReportDays = "Retention:BranchReportDays";

  /// <summary>The key of the number of days the finished jobs are kept.</summary>
  public const string RetentionJobDays = "Retention:JobDays";

  /// <summary>The key of the number of days the received webhooks are kept.</summary>
  public const string RetentionWebhookDays = "Retention:WebhookDays";

  /// <summary>The key of the number of days the cached diffs are kept.</summary>
  public const string RetentionDiffCacheDays = "Retention:DiffCacheDays";

  /// <summary>The largest accepted length of the YAML configuration of an organization, in characters.</summary>
  public const int CoverageOrganizationYamlMaxLength = 65_536;

  /// <summary>
  ///   The definitions, by key, whatever its case; built on first use, once <see cref="All" /> is
  ///   initialized.
  /// </summary>
  private static readonly Lazy<Dictionary<string, SettingDefinition>> ByKey = new(Index);

  #endregion

  #region Properties

  /// <summary>Gets every setting of the store, in display order.</summary>
  public static IReadOnlyList<SettingDefinition> All { get; } =
  [
    Text(GitHubAppId, GitHubGroup, SettingScope.Instance),
    Text(GitHubClientId, GitHubGroup, SettingScope.Instance),
    Text(GitHubAppSlug, GitHubGroup, SettingScope.Instance),
    Secret(GitHubClientSecret, GitHubGroup),
    Secret(GitHubPrivateKeyPem, GitHubGroup),
    Secret(GitHubWebhookSecret, GitHubGroup),
    Secret(GitHubWebhookSecretPrevious, GitHubGroup),
    SettingDefinition.Create(GitHubApiBaseUrl, GitHubGroup, SettingType.Url, SettingScope.Instance, "https://api.github.com"),

    SettingDefinition.Create(UrlsApiPublicUrl, UrlsGroup, SettingType.Url, SettingScope.Instance),
    SettingDefinition.Create(UrlsWebPublicUrl, UrlsGroup, SettingType.Url, SettingScope.Instance),

    Number(UploadsMaxRequestMegabytes, UploadsGroup, SettingScope.Instance, 200, 1, 1000),
    Number(UploadsMaxDecompressedMegabytes, UploadsGroup, SettingScope.Instance, 2048),
    Number(UploadsMaxFilesPerUpload, UploadsGroup, SettingScope.Instance, 50),
    Number(UploadsRateLimitPerMinute, UploadsGroup, SettingScope.Instance, 120),
    Text(UploadsOidcAudience, UploadsGroup, SettingScope.Instance),

    SettingDefinition.Create(
      ProcessingWorkerConcurrency,
      ProcessingGroup,
      SettingType.Number,
      SettingScope.Instance,
      "4",
      requiresRestart: true,
      min: 1,
      max: 16
    ),
    Number(ProcessingMaxParallelParses, ProcessingGroup, SettingScope.Instance, 2),
    Number(ProcessingJobMaxAttempts, ProcessingGroup, SettingScope.Instance, 8),
    Number(ProcessingCarryforwardMaxDepth, ProcessingGroup, SettingScope.Instance, 10),
    Number(ProcessingCarryforwardWaitMinutes, ProcessingGroup, SettingScope.Instance, 10),
    Number(ProcessingBaseMaxAncestorWalk, ProcessingGroup, SettingScope.Instance, 30),
    Number(ProcessingBaseWaitMinutes, ProcessingGroup, SettingScope.Instance, 15),
    Number(ProcessingPendingTimeoutMinutes, ProcessingGroup, SettingScope.Instance, 60),

    Text(NotificationsStatusContextPrefix, NotificationsGroup, SettingScope.Organization, "coverage"),
    Flag(NotificationsStatusesEnabled, NotificationsGroup, SettingScope.Organization, true),
    Flag(NotificationsCommentsEnabled, NotificationsGroup, SettingScope.Organization, true),
    Flag(NotificationsChecksEnabled, NotificationsGroup, SettingScope.Organization, false),
    Number(NotificationsDebounceSeconds, NotificationsGroup, SettingScope.Instance, 20, 0, 600),
    Number(NotificationsMaxDelaySeconds, NotificationsGroup, SettingScope.Instance, 300),
    Flag(NotificationsAfterNBuildsCountsCarryforward, NotificationsGroup, SettingScope.Instance, true),
    Text(NotificationsCommentTitle, NotificationsGroup, SettingScope.Instance, "Coverage"),

    SettingDefinition.Create(
      CoverageOrganizationYaml,
      CoverageGroup,
      SettingType.Yaml,
      SettingScope.Organization,
      maxLength: CoverageOrganizationYamlMaxLength
    ),
    Flag(CoverageAllowFilenameOnlyMatch, CoverageGroup, SettingScope.Instance, false),

    Text(BadgesLabel, BadgesGroup, SettingScope.Instance, "coverage"),

    SettingDefinition.Create(AccessAdminUserIds, AccessGroup, SettingType.Csv, SettingScope.Instance),
    Number(AccessCacheMinutes, AccessGroup, SettingScope.Instance, 10, 1, 60),
    Number(AccessHardExpiryMinutes, AccessGroup, SettingScope.Instance, 60, 60, 1440),

    Number(RetentionRawDays, RetentionGroup, SettingScope.Instance, 30),
    Number(RetentionBranchReportDays, RetentionGroup, SettingScope.Instance, 180),
    Number(RetentionJobDays, RetentionGroup, SettingScope.Instance, 14),
    Number(RetentionWebhookDays, RetentionGroup, SettingScope.Instance, 14),
    Number(RetentionDiffCacheDays, RetentionGroup, SettingScope.Instance, 30),
  ];

  #endregion

  #region Methods

  /// <summary>Finds the definition of a setting.</summary>
  /// <param name="key">The configuration key, whatever its case.</param>
  /// <returns>The definition, or <see langword="null" /> when the key is not in the catalog.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="key" /> is <see langword="null" />.</exception>
  public static SettingDefinition? Find(string key)
  {
    ArgumentNullException.ThrowIfNull(key);

    return ByKey.Value.GetValueOrDefault(key);
  }

  /// <summary>Declares a <see cref="SettingType.Boolean" /> setting.</summary>
  /// <param name="key">The configuration key.</param>
  /// <param name="group">The group.</param>
  /// <param name="scope">The level at which the value applies.</param>
  /// <param name="defaultValue">The default.</param>
  /// <returns>The definition.</returns>
  private static SettingDefinition Flag(string key, string group, SettingScope scope, bool defaultValue)
  {
    return SettingDefinition.Create(key, group, SettingType.Boolean, scope, defaultValue ? "true" : "false");
  }

  /// <summary>Indexes the catalog by key, refusing a key declared twice.</summary>
  /// <returns>The definitions, by key, whatever its case.</returns>
  /// <exception cref="InvalidOperationException">A key is declared twice.</exception>
  private static Dictionary<string, SettingDefinition> Index()
  {
    var byKey = new Dictionary<string, SettingDefinition>(StringComparer.OrdinalIgnoreCase);

    foreach (var definition in All)
    {
      if (!byKey.TryAdd(definition.Key, definition))
      {
        throw new InvalidOperationException($"The setting '{definition.Key}' is declared twice.");
      }
    }

    return byKey;
  }

  /// <summary>Declares a <see cref="SettingType.Number" /> setting.</summary>
  /// <param name="key">The configuration key.</param>
  /// <param name="group">The group.</param>
  /// <param name="scope">The level at which the value applies.</param>
  /// <param name="defaultValue">The default.</param>
  /// <param name="min">The smallest accepted value, or <see langword="null" />.</param>
  /// <param name="max">The largest accepted value, or <see langword="null" />.</param>
  /// <returns>The definition.</returns>
  private static SettingDefinition Number(
    string key,
    string group,
    SettingScope scope,
    int defaultValue,
    int? min = null,
    int? max = null)
  {
    return SettingDefinition.Create(
      key,
      group,
      SettingType.Number,
      scope,
      defaultValue.ToString(CultureInfo.InvariantCulture),
      min: min,
      max: max
    );
  }

  /// <summary>Declares a secret, which has no default and belongs to the instance.</summary>
  /// <param name="key">The configuration key.</param>
  /// <param name="group">The group.</param>
  /// <returns>The definition.</returns>
  private static SettingDefinition Secret(string key, string group)
  {
    return SettingDefinition.Create(key, group, SettingType.Text, SettingScope.Instance, isSecret: true);
  }

  /// <summary>Declares a <see cref="SettingType.Text" /> setting.</summary>
  /// <param name="key">The configuration key.</param>
  /// <param name="group">The group.</param>
  /// <param name="scope">The level at which the value applies.</param>
  /// <param name="defaultValue">The default, or <see langword="null" /> for none.</param>
  /// <returns>The definition.</returns>
  private static SettingDefinition Text(string key, string group, SettingScope scope, string? defaultValue = null)
  {
    return SettingDefinition.Create(key, group, SettingType.Text, scope, defaultValue);
  }

  #endregion
}
