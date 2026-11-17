// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsErrors.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.AzureDevOps;

/// <summary>The error codes proper to the Azure DevOps adapters.</summary>
/// <remarks>
///   A refused status and an unavailable forge carry the codes shared by every forge, those of
///   <see cref="Forges.ForgeErrors" />.
/// </remarks>
public static class AzureDevOpsErrors
{
  #region Fields

  /// <summary>
  ///   Azure DevOps refused the application of the instance access to the organization (<c>TF400813</c>): its service
  ///   principal is not a user of the organization, or the organization is not linked to its Microsoft Entra ID tenant.
  /// </summary>
  public const string AccessDenied = "AzureDevOps.AccessDenied";

  #endregion
}
