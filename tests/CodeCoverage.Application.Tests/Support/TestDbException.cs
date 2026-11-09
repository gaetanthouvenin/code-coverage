// ------------------------------------------------------------------------------------------------
// <copyright file="TestDbException.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Data.Common;

namespace CodeCoverage.Application.Tests.Support;

/// <summary>A failure of the database, as a provider raises it (connection reset, command timeout).</summary>
public sealed class TestDbException() : DbException("The connection was reset.");
