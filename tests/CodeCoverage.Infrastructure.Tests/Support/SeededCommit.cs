// ------------------------------------------------------------------------------------------------
// <copyright file="SeededCommit.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Tests.Support;

// A finalized commit seeded with one processed upload and its session.
public sealed record SeededCommit(long Commit, long Upload, long Session);
