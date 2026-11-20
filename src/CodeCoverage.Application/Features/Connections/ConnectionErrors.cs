// ------------------------------------------------------------------------------------------------
// <copyright file="ConnectionErrors.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Features.Connections;

/// <summary>The error codes of the connections of the organizations to the forges other than GitHub.</summary>
public static class ConnectionErrors
{
  #region Fields

  /// <summary>The forge refuses the connection access to the account it names.</summary>
  public const string ForgeAccessDenied = "Connection.ForgeAccessDenied";

  /// <summary>The forge is not offered for an admission: its adapter is not registered or its settings are not filled.</summary>
  public const string ForgeNotAvailable = "Connection.ForgeNotAvailable";

  /// <summary>
  ///   Neither an installation of the GitHub App nor a connection that is not suspended serves the organization, so there
  ///   is nothing to read again.
  /// </summary>
  public const string NotConnected = "Connection.NotConnected";

  /// <summary>A GitHub account is admitted by the installation of the GitHub App, never by a connection.</summary>
  public const string UseInstallation = "Connection.UseInstallation";

  #endregion
}
