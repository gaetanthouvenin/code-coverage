// ------------------------------------------------------------------------------------------------
// <copyright file="SerilogBootstrapTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Api.Tests.Support;

namespace CodeCoverage.Api.Tests.Configuration;

// Several Api hosts started at the same time in one process, as the test collections run in
// parallel: each one must reload and freeze its own bootstrap logger, never the one another host
// put in the static Log.Logger ("The logger is already frozen.").
public sealed class SerilogBootstrapTests
{
  #region Fields

  private const int Rounds = 3;

  private const int HostsPerRound = 8;

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Start_HostsInParallel_ShouldStartEveryHost()
  {
    for (var round = 0; round < Rounds; round++)
    {
      var factories = Enumerable.Range(0, HostsPerRound).Select(_ => new CodeCoverageApiFactory()).ToList();

      try
      {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var starts = factories.Select(factory => Task.Run(async () =>
                                                          {
                                                            await gate.Task;

                                                            return factory.Services;
                                                          }, Ct))
                              .ToList();

        gate.SetResult();

        var services = await Task.WhenAll(starts);

        services.ShouldAllBe(provider => provider != null);
      }
      finally
      {
        foreach (var factory in factories)
        {
          await factory.DisposeAsync();
        }
      }
    }
  }

  #endregion
}
