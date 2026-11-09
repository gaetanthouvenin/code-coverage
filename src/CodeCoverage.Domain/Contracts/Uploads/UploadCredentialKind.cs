// ------------------------------------------------------------------------------------------------
// <copyright file="UploadCredentialKind.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Uploads;

/// <summary>What proves the right to upload.</summary>
public enum UploadCredentialKind
{
  /// <summary>An upload token.</summary>
  UploadToken,

  /// <summary>A GitHub Actions OIDC token.</summary>
  Jwt,
}
