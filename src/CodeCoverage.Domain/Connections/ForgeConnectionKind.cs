// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeConnectionKind.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Connections;

/// <summary>The kind of credential a forge connection holds, stored as text.</summary>
/// <remarks>Stored as text, a new kind needs no migration (ADR-017).</remarks>
public enum ForgeConnectionKind
{
  /// <summary>An application of Microsoft Entra ID with its client secret, given access to an Azure DevOps organization.</summary>
  AzureDevOpsEntraApplication,
}
