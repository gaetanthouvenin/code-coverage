// ------------------------------------------------------------------------------------------------
// <copyright file="SampleSender.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Architecture.Tests.Samples.Presentation;

// Stands for ISender.Send<T>: the Domain type only shows up as a generic argument in the IL.
internal static class SampleSender
{
  #region Methods

  public static string Send<TResponse>()
    where TResponse : new() =>
    typeof(TResponse).Name;

  #endregion
}
