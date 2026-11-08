// ------------------------------------------------------------------------------------------------
// <copyright file="ReplaceSecretCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Settings.Commands.ReplaceSecret;

/// <summary>
///   Replaces the value of a secret setting: the value is protected before it is saved, and never
///   shown again; refused when a stronger source provides the value.
/// </summary>
/// <param name="Key">The configuration key of the secret, as <c>Section:Name</c>.</param>
/// <param name="Value">The new secret, in clear.</param>
public sealed record ReplaceSecretCommand(string Key, string Value) : ICommand, IAdminOnly
{
  #region Methods

  /// <summary>Returns the key only: the secret never reaches a log or a trace.</summary>
  /// <returns>The name of the command and its key.</returns>
  public override string ToString()
  {
    return $"{nameof(ReplaceSecretCommand)} {{ {nameof(Key)} = {Key} }}";
  }

  #endregion
}
