// ------------------------------------------------------------------------------------------------
// <copyright file="StatusState.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Notifications;

/// <summary>The state of a commit status, as it is sent to the forge.</summary>
/// <remarks>No pending state: a status is posted once its result is known, so a context is never left waiting.</remarks>
public enum StatusState
{
  /// <summary>The rule holds.</summary>
  Success,

  /// <summary>The rule does not hold.</summary>
  Failure,

  /// <summary>The rule could not be evaluated, or the processing of the coverage failed.</summary>
  Error,
}