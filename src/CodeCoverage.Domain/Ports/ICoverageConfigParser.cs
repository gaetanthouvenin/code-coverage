// ------------------------------------------------------------------------------------------------
// <copyright file="ICoverageConfigParser.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Config;

namespace CodeCoverage.Domain.Ports;

/// <summary>Reads the text of a <c>codecov.yml</c> file.</summary>
public interface ICoverageConfigParser
{
  #region Methods

  /// <summary>Reads a configuration file.</summary>
  /// <param name="yaml">The text of the file.</param>
  /// <returns>The content of the file, or the reason why it cannot be read.</returns>
  ConfigParseResult Parse(string yaml);

  #endregion
}
