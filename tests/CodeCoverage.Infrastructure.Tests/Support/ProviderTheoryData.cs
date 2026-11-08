// ------------------------------------------------------------------------------------------------
// <copyright file="ProviderTheoryData.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Infrastructure.Persistence;

namespace CodeCoverage.Infrastructure.Tests.Support;

// Every database engine, so that a theory runs once per provider ([ClassData(typeof(ProviderTheoryData))]).
// Built from the enumeration: a new provider is covered without touching the tests.
public sealed class ProviderTheoryData : TheoryData<DatabaseProvider>
{
  #region Ctors

  public ProviderTheoryData()
  {
    AddRange(Enum.GetValues<DatabaseProvider>());
  }

  #endregion
}
