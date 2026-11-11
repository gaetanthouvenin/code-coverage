// ------------------------------------------------------------------------------------------------
// <copyright file="TestDeadlockException.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Data.Common;

namespace CodeCoverage.Infrastructure.Tests.Support;

// What an engine throws for the victim of a deadlock, as a test raises it: the dialect of the test tells it apart.
internal sealed class TestDeadlockException() : DbException("Chosen as the victim of a deadlock.");
