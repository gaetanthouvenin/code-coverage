// ------------------------------------------------------------------------------------------------
// <copyright file="StatusContext.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;

namespace CodeCoverage.Domain.Notifications;

/// <summary>Builds the context of a commit status, the name a branch protection rule requires.</summary>
public static class StatusContext
{
  #region Methods

  /// <summary>Builds the context of a rule.</summary>
  /// <param name="prefix">The prefix of the organization: <c>coverage</c>, or <c>codecov</c> once it took Codecov over.</param>
  /// <param name="type">The type of the status.</param>
  /// <param name="name">The name of the rule; a flag status is named after its flag.</param>
  /// <returns><c>{prefix}/{type}</c> for the rule named <c>default</c>, else <c>{prefix}/{type}/{name}</c>, as Codecov names them.</returns>
  /// <exception cref="ArgumentException"><paramref name="prefix" /> or <paramref name="name" /> is empty.</exception>
  public static string Build(string prefix, StatusType type, string name)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
    ArgumentException.ThrowIfNullOrWhiteSpace(name);

    var kind = type switch
    {
      StatusType.Project => "project",
      StatusType.Patch => "patch",
      var _ => "changes",
    };

    return string.Equals(name, StatusRule.DefaultName, StringComparison.Ordinal) ? $"{prefix}/{kind}" : $"{prefix}/{kind}/{name}";
  }

  #endregion
}