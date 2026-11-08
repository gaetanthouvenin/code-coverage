// ------------------------------------------------------------------------------------------------
// <copyright file="SettingDefinitionsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Domain.Tests.Settings;

public sealed class SettingDefinitionsTests
{
  #region Fields

  // The catalog of ARCHITECTURE.md §4bis.3, one line per key: key, type, default.
  public static readonly TheoryData<string, SettingType, string?> ExpectedCatalog = new()
  {
    { "GitHub:AppId", SettingType.Text, null },
    { "GitHub:ClientId", SettingType.Text, null },
    { "GitHub:AppSlug", SettingType.Text, null },
    { "GitHub:ClientSecret", SettingType.Text, null },
    { "GitHub:PrivateKeyPem", SettingType.Text, null },
    { "GitHub:WebhookSecret", SettingType.Text, null },
    { "GitHub:WebhookSecretPrevious", SettingType.Text, null },
    { "GitHub:ApiBaseUrl", SettingType.Url, "https://api.github.com" },
    { "Urls:ApiPublicUrl", SettingType.Url, null },
    { "Urls:WebPublicUrl", SettingType.Url, null },
    { "Uploads:MaxRequestMegabytes", SettingType.Number, "200" },
    { "Uploads:MaxDecompressedMegabytes", SettingType.Number, "2048" },
    { "Uploads:MaxFilesPerUpload", SettingType.Number, "50" },
    { "Uploads:RateLimitPerMinute", SettingType.Number, "120" },
    { "Uploads:OidcAudience", SettingType.Text, null },
    { "Processing:WorkerConcurrency", SettingType.Number, "4" },
    { "Processing:MaxParallelParses", SettingType.Number, "2" },
    { "Processing:JobMaxAttempts", SettingType.Number, "8" },
    { "Processing:CarryforwardMaxDepth", SettingType.Number, "10" },
    { "Processing:CarryforwardWaitMinutes", SettingType.Number, "10" },
    { "Processing:BaseMaxAncestorWalk", SettingType.Number, "30" },
    { "Processing:BaseWaitMinutes", SettingType.Number, "15" },
    { "Processing:PendingTimeoutMinutes", SettingType.Number, "60" },
    { "Notifications:StatusContextPrefix", SettingType.Text, "coverage" },
    { "Notifications:StatusesEnabled", SettingType.Boolean, "true" },
    { "Notifications:CommentsEnabled", SettingType.Boolean, "true" },
    { "Notifications:DebounceSeconds", SettingType.Number, "20" },
    { "Notifications:MaxDelaySeconds", SettingType.Number, "300" },
    { "Notifications:AfterNBuildsCountsCarryforward", SettingType.Boolean, "true" },
    { "Notifications:CommentTitle", SettingType.Text, "Coverage" },
    { "Coverage:OrganizationYaml", SettingType.Yaml, null },
    { "Coverage:AllowFilenameOnlyMatch", SettingType.Boolean, "false" },
    { "Badges:Label", SettingType.Text, "coverage" },
    { "Access:AdminUserIds", SettingType.Csv, null },
    { "Access:CacheMinutes", SettingType.Number, "10" },
    { "Access:HardExpiryMinutes", SettingType.Number, "60" },
    { "Retention:RawDays", SettingType.Number, "30" },
    { "Retention:BranchReportDays", SettingType.Number, "180" },
    { "Retention:JobDays", SettingType.Number, "14" },
    { "Retention:WebhookDays", SettingType.Number, "14" },
    { "Retention:DiffCacheDays", SettingType.Number, "30" },
  };

  private static readonly string[] Secrets =
  [
    "GitHub:ClientSecret",
    "GitHub:PrivateKeyPem",
    "GitHub:WebhookSecret",
    "GitHub:WebhookSecretPrevious",
  ];

  private static readonly string[] OrganizationKeys =
  [
    "Notifications:StatusContextPrefix",
    "Notifications:StatusesEnabled",
    "Notifications:CommentsEnabled",
    "Coverage:OrganizationYaml",
  ];

  #endregion

  #region Methods

  [Fact]
  public void Catalog_ShouldHaveUniqueKeys()
  {
    SettingDefinitions.All.Select(d => d.Key)
                      .Distinct(StringComparer.OrdinalIgnoreCase)
                      .Count()
                      .ShouldBe(SettingDefinitions.All.Count);
  }

  [Fact]
  public void Catalog_SecretsShouldHaveNoDefault()
  {
    SettingDefinitions.All.Where(d => d.IsSecret)
                      .ShouldAllBe(d => d.Default == null && d.Type == SettingType.Text);
  }

  [Fact]
  public void Find_UnknownKey_ShouldReturnNull()
  {
    SettingDefinitions.Find("GitHub:Unknown").ShouldBeNull();
  }

  [Theory]
  [InlineData("GitHub:Organization")]
  [InlineData("Uploads:AllowedOwners")]
  [InlineData("Access:AdminLogins")]
  public void Find_RemovedKey_ShouldReturnNull(string key)
  {
    SettingDefinitions.Find(key).ShouldBeNull();
  }

  [Fact]
  public void Find_WithOtherCase_ShouldReturnTheDefinition()
  {
    SettingDefinitions.Find("processing:workerconcurrency")
                      .ShouldBeSameAs(SettingDefinitions.Find(SettingDefinitions.ProcessingWorkerConcurrency));
  }

  [Fact]
  public void Find_WithNull_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => SettingDefinitions.Find(null!));
  }

  [Fact]
  public void All_ShouldDeclareExpectedScope()
  {
    SettingDefinitions.All.Count.ShouldBe(41);
    SettingDefinitions.All.Where(d => d.Scope == SettingScope.Organization)
                      .Select(d => d.Key)
                      .ShouldBe(OrganizationKeys);
    SettingDefinitions.All.Count(d => d.Scope == SettingScope.Instance).ShouldBe(37);
  }

  [Fact]
  public void All_Secret_ShouldBeInstanceScope()
  {
    SettingDefinitions.All.Where(d => d.IsSecret).ShouldAllBe(d => d.Scope == SettingScope.Instance);
  }

  [Fact]
  public void Catalog_ShouldListTheKeysOfTheArchitectureInOrder()
  {
    var expected = ExpectedCatalog.Select(row => row.Data.Item1).ToArray();

    SettingDefinitions.All.Select(d => d.Key).ToArray().ShouldBe(expected);
  }

  [Theory]
  [MemberData(nameof(ExpectedCatalog))]
  public void Catalog_ShouldDeclareTheTypeAndDefaultOfTheArchitecture(
    string key,
    SettingType type,
    string? defaultValue)
  {
    var definition = SettingDefinitions.Find(key);

    definition.ShouldNotBeNull();
    definition.Type.ShouldBe(type);
    definition.Default.ShouldBe(defaultValue);
  }

  [Fact]
  public void Catalog_ShouldMarkExactlyTheSecretsOfTheArchitecture()
  {
    SettingDefinitions.All.Where(d => d.IsSecret).Select(d => d.Key).ShouldBe(Secrets);
  }

  [Fact]
  public void Catalog_ShouldOnlyRequireRestartForWorkerConcurrency()
  {
    SettingDefinitions.All.Where(d => d.RequiresRestart)
                      .Select(d => d.Key)
                      .ShouldBe([SettingDefinitions.ProcessingWorkerConcurrency]);
  }

  [Theory]
  [InlineData("Uploads:MaxRequestMegabytes", 1, 1000)]
  [InlineData("Processing:WorkerConcurrency", 1, 16)]
  [InlineData("Notifications:DebounceSeconds", 0, 600)]
  [InlineData("Access:CacheMinutes", 1, 60)]
  [InlineData("Access:HardExpiryMinutes", 60, 1440)]
  public void Catalog_ShouldBoundTheNumbersOfTheArchitecture(string key, int min, int max)
  {
    var definition = SettingDefinitions.Find(key);

    definition.ShouldNotBeNull();
    definition.Min.ShouldBe(min);
    definition.Max.ShouldBe(max);
  }

  [Fact]
  public void Catalog_HardExpiry_ShouldNeverBeShorterThanTheCache()
  {
    var cache = SettingDefinitions.Find(SettingDefinitions.AccessCacheMinutes);
    var hardExpiry = SettingDefinitions.Find(SettingDefinitions.AccessHardExpiryMinutes);

    cache.ShouldNotBeNull();
    hardExpiry.ShouldNotBeNull();
    hardExpiry.Min!.Value.ShouldBeGreaterThanOrEqualTo(cache.Max!.Value);
  }

  [Fact]
  public void Catalog_ShouldOnlyLimitTheLengthOfTheOrganizationYaml()
  {
    var limited = SettingDefinitions.All.Where(d => d.MaxLength is not null).ToList();

    limited.ShouldHaveSingleItem().Key.ShouldBe(SettingDefinitions.CoverageOrganizationYaml);
    limited[0].MaxLength.ShouldBe(65_536);
  }

  [Fact]
  public void Catalog_ShouldGroupEachKeyUnderItsSection()
  {
    SettingDefinitions.All.ShouldAllBe(d => d.Key.StartsWith(d.Group + ":", StringComparison.Ordinal));
  }

  [Fact]
  public void Catalog_DefaultsShouldBeAcceptedByTheirOwnBounds()
  {
    foreach (var definition in SettingDefinitions.All.Where(d => d.Type == SettingType.Number))
    {
      var value = int.Parse(definition.Default!, System.Globalization.CultureInfo.InvariantCulture);

      value.ShouldBeGreaterThanOrEqualTo(definition.Min ?? int.MinValue, definition.Key);
      value.ShouldBeLessThanOrEqualTo(definition.Max ?? int.MaxValue, definition.Key);
    }
  }

  #endregion
}
