// ------------------------------------------------------------------------------------------------
// <copyright file="BranchSeed.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Tests.Support;

// Two organizations, each holding one repository.
public sealed record BranchSeed(TwoOrganizationDatabase Organizations, long FirstRepository, long SecondRepository);
