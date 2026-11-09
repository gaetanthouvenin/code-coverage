// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigResolver.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Services;

/// <summary>Gives the effective configuration of a commit: the file at its SHA, the default branch, then the organization.</summary>
/// <param name="configs">The cache of what was found for each commit.</param>
/// <param name="gateways">The gateways of the forges.</param>
/// <param name="parser">The reader of the YAML.</param>
/// <param name="organizationSettings">The settings of the organization of the scope.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <para>
///     What is found for a commit is kept for good: four calls to the forge at most for a SHA, then none. The YAML of the
///     organization is not kept with it, and is read at each resolution from the settings of the scope.
///   </para>
///   <para>The search follows <c>determine_commit_yaml_location</c> of Codecov.</para>
/// </remarks>
internal sealed class ConfigResolver(
  ICommitConfigRepository configs,
  IForgeGatewayProvider gateways,
  ICoverageConfigParser parser,
  IOrganizationSettingsProvider organizationSettings,
  IClock clock)
{
  #region Fields

  /// <summary>The names of a configuration file, in the order of the search.</summary>
  private static readonly string[] FileNames = ["codecov.yml", ".codecov.yml", "codecov.yaml", ".codecov.yaml"];

  /// <summary>The directories searched after the root, in order.</summary>
  private static readonly string[] Directories = [".github", "dev"];

  #endregion

  #region Methods

  /// <summary>Resolves the effective configuration of a commit.</summary>
  /// <param name="repository">The repository, read in this scope.</param>
  /// <param name="address">The address of the repository on its forge.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The effective configuration.</returns>
  /// <exception cref="ArgumentNullException">A value is <see langword="null" />.</exception>
  public async Task<ResolvedConfig> ResolveAsync(
    TrackedRepository repository,
    ForgeRepositoryRef address,
    GitSha sha,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(address);
    ArgumentNullException.ThrowIfNull(sha);

    var settings = await organizationSettings.GetAsync(cancellationToken).ConfigureAwait(false);
    var organization = ParseOrganizationYaml(settings.OrganizationYaml);
    var found = await configs.FindAsync(repository.Id, sha, cancellationToken).ConfigureAwait(false)
                ?? await FindAndCacheAsync(repository, address, sha, organization, cancellationToken).ConfigureAwait(false);

    var root = CoverageConfigDefaults.Root;

    if (organization.Root is not null)
    {
      root = ConfigMerger.Merge(root, organization.Root);
    }

    if (found.RawYaml is not null && parser.Parse(found.RawYaml).Root is { } repositoryRoot)
    {
      root = ConfigMerger.Merge(root, repositoryRoot);
    }

    var config = CoverageConfig.Create(root);

    return new ResolvedConfig(config, found.Source, [.. organization.Warnings, .. found.Warnings, .. config.Warnings], found.Errors);
  }

  /// <summary>Searches the configuration of a commit, and keeps what it finds.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="address">The address of the repository on its forge.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="organization">The configuration of the organization.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>What was found.</returns>
  private async Task<CommitConfig> FindAndCacheAsync(
    TrackedRepository repository,
    ForgeRepositoryRef address,
    GitSha sha,
    ParsedYaml organization,
    CancellationToken cancellationToken)
  {
    var errors = new List<string>();
    var file = await ReadFileAtCommitAsync(address, sha, cancellationToken).ConfigureAwait(false);
    CommitConfig? found = null;

    if (file is { } located)
    {
      found = TryAccept(repository.Id, sha, ConfigSource.Commit, located.Path, located.Yaml, errors);
    }

    if (found is null && repository.DefaultBranchYaml is { } defaultYaml)
    {
      found = TryAccept(repository.Id, sha, ConfigSource.RepositoryDefault, null, defaultYaml, errors);
    }

    if (organization.Error is { } organizationError)
    {
      errors.Add($"The configuration of the organization: {organizationError}");
    }

    found ??= CommitConfig.Create(
      repository.Id,
      sha,
      organization.Root is null ? ConfigSource.Builtin : ConfigSource.Organization,
      null,
      null,
      [],
      errors,
      clock.UtcNow
    );

    await configs.AddAsync(found, cancellationToken).ConfigureAwait(false);

    return found;
  }

  /// <summary>Accepts a YAML that can be read, or remembers why not.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="source">Where the YAML came from.</param>
  /// <param name="path">The path of the file of the commit, or <see langword="null" />.</param>
  /// <param name="yaml">The YAML.</param>
  /// <param name="errors">The errors met so far.</param>
  /// <returns>The row, or <see langword="null" /> when the YAML cannot be read.</returns>
  private CommitConfig? TryAccept(long repositoryId, GitSha sha, ConfigSource source, string? path, string yaml, List<string> errors)
  {
    var parsed = parser.Parse(yaml);

    if (parsed.Root is null)
    {
      errors.Add($"{path ?? "The configuration of the default branch"}: {parsed.Error}");

      return null;
    }

    return CommitConfig.Create(repositoryId, sha, source, path, yaml, parsed.Warnings, errors, clock.UtcNow);
  }

  /// <summary>Reads the configuration file of a commit, if it has one.</summary>
  /// <param name="address">The address of the repository on its forge.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>Its path and its text, or <see langword="null" />.</returns>
  private async Task<(string Path, string Yaml)?> ReadFileAtCommitAsync(ForgeRepositoryRef address, GitSha sha, CancellationToken cancellationToken)
  {
    var gateway = gateways.GetGateway(address.Forge);
    var root = await gateway.GetTreeAsync(address, sha, recursive: false, cancellationToken).ConfigureAwait(false);

    if (root is null)
    {
      return null;
    }

    var path = FindFile(root, prefix: string.Empty);

    foreach (var directory in Directories)
    {
      if (path is not null)
      {
        break;
      }

      if (root.Entries.FirstOrDefault(e => e.IsDirectory && e.Path == directory) is { } entry
          && await gateway.GetTreeAsync(address, entry.Sha, recursive: false, cancellationToken).ConfigureAwait(false) is { } subtree)
      {
        path = FindFile(subtree, prefix: directory + "/");
      }
    }

    if (path is null)
    {
      return null;
    }

    var yaml = await gateway.GetContentAsync(address, path, sha, cancellationToken).ConfigureAwait(false);

    return yaml is null ? null : (path, yaml);
  }

  /// <summary>Finds a configuration file directly in a tree.</summary>
  /// <param name="tree">The tree.</param>
  /// <param name="prefix">What precedes the entries of the tree in the repository.</param>
  /// <returns>Its path in the repository, or <see langword="null" />.</returns>
  private static string? FindFile(ForgeTree tree, string prefix) =>
    FileNames.FirstOrDefault(name => tree.Entries.Any(e => !e.IsDirectory && e.Path == name)) is { } file ? prefix + file : null;

  /// <summary>Reads the configuration of the organization.</summary>
  /// <param name="yaml">Its text, or <see langword="null" />.</param>
  /// <returns>What it holds.</returns>
  private ParsedYaml ParseOrganizationYaml(string? yaml)
  {
    if (string.IsNullOrWhiteSpace(yaml))
    {
      return new ParsedYaml(null, null, []);
    }

    var parsed = parser.Parse(yaml);

    return new ParsedYaml(parsed.Root, parsed.Error, parsed.Warnings);
  }

  #endregion
}
