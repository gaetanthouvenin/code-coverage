// ------------------------------------------------------------------------------------------------
// <copyright file="CommitSeed.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Tests.Support;

// Two organizations, each holding one commit of its own repository.
public sealed record CommitSeed(TwoOrganizationDatabase Organizations, long FirstRepository, long FirstCommit, long SecondCommit);
