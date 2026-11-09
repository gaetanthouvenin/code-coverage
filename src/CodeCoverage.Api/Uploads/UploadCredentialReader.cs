// ------------------------------------------------------------------------------------------------
// <copyright file="UploadCredentialReader.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Primitives;

namespace CodeCoverage.Api.Uploads;

/// <summary>Reads the credential of an upload from its headers.</summary>
internal static class UploadCredentialReader
{
  #region Fields

  private const string UploadTokenHeader = "X-Upload-Token";

  private static readonly string[] Schemes = ["Bearer ", "token "];

  #endregion

  #region Methods

  /// <summary>Reads the credential: <c>Authorization: Bearer</c>, <c>Authorization: token</c> or <c>X-Upload-Token</c>.</summary>
  /// <param name="headers">The headers of the request.</param>
  /// <returns>The credential, or <see langword="null" /> when the request has none.</returns>
  public static string? Read(IHeaderDictionary headers)
  {
    ArgumentNullException.ThrowIfNull(headers);

    var authorization = headers.Authorization.ToString();

    foreach (var scheme in Schemes)
    {
      if (authorization.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
      {
        return Clean(authorization[scheme.Length..]);
      }
    }

    return headers.TryGetValue(UploadTokenHeader, out StringValues token) ? Clean(token.ToString()) : null;
  }

  /// <summary>Gives the value of a credential.</summary>
  /// <param name="value">The text of the header.</param>
  /// <returns>The value, or <see langword="null" /> when it is blank.</returns>
  private static string? Clean(string value)
  {
    var trimmed = value.Trim();

    return trimmed.Length == 0 ? null : trimmed;
  }

  #endregion
}
