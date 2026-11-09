// ------------------------------------------------------------------------------------------------
// <copyright file="ValidateCoverageYamlQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Configuration.Queries.ValidateCoverageYaml;

/// <summary>Handler for the <see cref="ValidateCoverageYamlQuery" /> query.</summary>
/// <param name="parser">The reader of configuration files.</param>
internal sealed class ValidateCoverageYamlQueryHandler(ICoverageConfigParser parser)
  : IQueryHandler<ValidateCoverageYamlQuery, ConfigValidationResult>
{
  #region Fields

  private const string EmptyFile = "The file is empty.";

  #endregion

  #region Implementation of IQueryHandler<ValidateCoverageYamlQuery,ConfigValidationResult>

  /// <inheritdoc />
  /// <remarks>
  ///   <para>
  ///     Only the posted file is read: neither the built-in defaults nor the organization YAML are merged,
  ///     as <c>/validate</c> of Codecov returns what was posted.
  ///   </para>
  ///   <para>
  ///     A file without any key is refused, as Codecov refuses an empty or comment-only file. The parser
  ///     gives the same empty map for <c>{}</c>, which Codecov accepts: here it is refused too.
  ///   </para>
  /// </remarks>
  public ValueTask<ConfigValidationResult> Handle(ValidateCoverageYamlQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var parsed = parser.Parse(query.Yaml);

    if (parsed.Root is not { Entries.Count: > 0 } root)
    {
      return ValueTask.FromResult(new ConfigValidationResult(null, [], [parsed.Error ?? EmptyFile]));
    }

    // The parser and the typed view both warn on the keys without effect: they give equal records.
    var warnings = parsed.Warnings.Concat(CoverageConfig.Create(root).Warnings).Distinct();

    return ValueTask.FromResult(new ConfigValidationResult(root, [.. warnings], []));
  }

  #endregion
}
