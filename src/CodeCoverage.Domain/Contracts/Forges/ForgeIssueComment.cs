// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeIssueComment.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>A comment of a pull request, as its forge reports it.</summary>
/// <param name="Id">The identifier the forge gives the comment.</param>
/// <param name="Body">The text of the comment.</param>
/// <param name="WrittenByThisApplication">
///   Whether the comment was written by this application on the forge. The gateway decides it: how an application
///   signs its comments is particular to each forge (ADR-014).
/// </param>
public sealed record ForgeIssueComment(long Id, string Body, bool WrittenByThisApplication);
