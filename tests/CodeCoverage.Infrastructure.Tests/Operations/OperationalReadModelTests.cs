// ------------------------------------------------------------------------------------------------
// <copyright file="OperationalReadModelTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Reflection;

using CodeCoverage.Domain.Operations;

namespace CodeCoverage.Infrastructure.Tests.Operations;

// The read models of the operations console carry metadata only (M3.13): never a payload, a key or the text of an error.
public sealed class OperationalReadModelTests
{
  #region Fields

  private static readonly string[] Forbidden = ["Payload", "PayloadJson", "LockKey", "DedupKey", "LastError", "Error", "Body"];

  #endregion

  #region Methods

  [Theory]
  [InlineData(typeof(JobSummary))]
  [InlineData(typeof(WebhookDeliverySummary))]
  [InlineData(typeof(SystemStatus))]
  [InlineData(typeof(JobPage))]
  public void ReadModels_ShouldExposeNoPayloadOrKeys(Type model)
  {
    var properties = model.GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(static p => p.Name);

    properties.Intersect(Forbidden, StringComparer.Ordinal).ShouldBeEmpty(model.Name);
  }

  #endregion
}
