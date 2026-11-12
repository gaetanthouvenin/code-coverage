// ------------------------------------------------------------------------------------------------
// <copyright file="SessionModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Commits;

/// <summary>A session of a commit: one upload, or what is carried forward from an ancestor.</summary>
/// <param name="Number">The number of the session in the commit, from 1.</param>
/// <param name="Kind">Whether it was uploaded or carried forward.</param>
/// <param name="Flags">Its flags.</param>
/// <param name="Name">The name of the session, or <see langword="null" />.</param>
/// <param name="CiService">The CI service that sent it, or <see langword="null" />.</param>
/// <param name="CarriedForwardFromSha">The commit it was carried forward from, or <see langword="null" />.</param>
public sealed record SessionModel(int Number, string Kind, IReadOnlyList<string> Flags, string? Name, string? CiService, string? CarriedForwardFromSha);
