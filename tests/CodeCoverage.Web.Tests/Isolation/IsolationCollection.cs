// ------------------------------------------------------------------------------------------------
// <copyright file="IsolationCollection.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Tests.Isolation;

// Shares the database, the host and the two organizations between the classes of the isolation suite: one container
// and one migration for the whole suite.
[CollectionDefinition(Name)]
public sealed class IsolationCollection : ICollectionFixture<IsolationFixture>
{
  #region Fields

  public const string Name = "Isolation";

  #endregion
}
