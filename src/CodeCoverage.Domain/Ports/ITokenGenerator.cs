// ------------------------------------------------------------------------------------------------
// <copyright file="ITokenGenerator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Ports;

/// <summary>Generates the values of the tokens.</summary>
public interface ITokenGenerator
{
  #region Methods

  /// <summary>Generates the value of an upload token.</summary>
  /// <returns>The value in clear: <c>cvc_</c> followed by 43 base64url characters.</returns>
  string NewUploadToken();

  #endregion
}
