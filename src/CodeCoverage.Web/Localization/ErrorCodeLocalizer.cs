// ------------------------------------------------------------------------------------------------
// <copyright file="ErrorCodeLocalizer.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Localization;

using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Localization;

/// <summary>
///   Translates the error code <c>Feature.Reason</c> of an Application exception into a message for
///   the user, in the culture of the circuit.
/// </summary>
/// <remarks>
///   Every code is translated in <c>Resources/Common.resx</c> and <c>Common.fr.resx</c> under its
///   own name. A code without translation falls back to the message of
///   <see cref="GeneralErrors.Unexpected" />: the user never sees a raw code, nor the message of the
///   exception, which is written for developers.
/// </remarks>
/// <param name="localizer">The strings of the application.</param>
public sealed class ErrorCodeLocalizer(IStringLocalizer<Common> localizer)
{
  #region Methods

  /// <summary>Gets the message of an error code.</summary>
  /// <param name="errorCode">The error code, e.g. <c>Access.Forbidden</c>.</param>
  /// <returns>
  ///   The translation of <paramref name="errorCode" />, or the one of
  ///   <see cref="GeneralErrors.Unexpected" /> when the code is empty or has no translation.
  /// </returns>
  public string Localize(string? errorCode)
  {
    if (!string.IsNullOrWhiteSpace(errorCode))
    {
      var message = localizer[errorCode];

      if (!message.ResourceNotFound)
      {
        return message.Value;
      }
    }

    return localizer[GeneralErrors.Unexpected].Value;
  }

  #endregion
}
