// ------------------------------------------------------------------------------------------------
// <copyright file="OpenApiConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Reflection;

using Microsoft.OpenApi;

namespace CodeCoverage.Api.Configuration;

/// <summary>The OpenAPI document of <c>/api/v1</c>, built from the XML documentation.</summary>
/// <remarks>
///   Swashbuckle generates the document <c>/openapi/v1.json</c> and Swagger UI serves it on
///   <c>/swagger</c> (Scalar: <see cref="ScalarConfiguration" />). The documentation is disabled in
///   Production unless <see cref="EnableApiDocsKey" /> is <see langword="true" />.
/// </remarks>
public static class OpenApiConfiguration
{
  #region Fields

  /// <summary>The configuration key that enables the documentation in Production.</summary>
  public const string EnableApiDocsKey = "Features:EnableApiDocs";

  /// <summary>The name of the OpenAPI document.</summary>
  public const string DocumentName = "v1";

  /// <summary>The path of the OpenAPI document.</summary>
  public const string DocumentPath = "/openapi/" + DocumentName + ".json";

  /// <summary>The path of Swagger UI.</summary>
  public const string SwaggerPath = "/swagger";

  /// <summary>The name of the security scheme of the API and upload tokens.</summary>
  public const string BearerScheme = "Bearer";

  #endregion

  #region Methods

  /// <summary>Registers the OpenAPI document generator.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="services" /> is <see langword="null" />.
  /// </exception>
  public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen(options =>
    {
      options.SwaggerDoc(
        DocumentName,
        new OpenApiInfo
        {
          Title = "CodeCoverage",
          Version = DocumentName,
          Description = "Self-hosted code coverage service",
        }
      );

      // The XML documentation of the API and the Application.
      foreach (var assembly in GetDocumentedAssemblies())
      {
        var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");

        if (File.Exists(xmlPath))
        {
          options.IncludeXmlComments(xmlPath, true);
        }
      }

      options.AddSecurityDefinition(
        BearerScheme,
        new OpenApiSecurityScheme
        {
          Type = SecuritySchemeType.Http,
          Scheme = "bearer",
          Description = "An API token or an upload token.",
        }
      );

      options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
      {
        [new OpenApiSecuritySchemeReference(BearerScheme, document)] = [],
      });
    });

    return services;
  }

  /// <summary>Tells whether the API documentation is served.</summary>
  /// <param name="app">The application.</param>
  /// <returns>
  ///   <see langword="true" /> outside Production, or when <see cref="EnableApiDocsKey" /> is
  ///   <see langword="true" />; otherwise, <see langword="false" />.
  /// </returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="app" /> is <see langword="null" />.
  /// </exception>
  public static bool IsApiDocumentationEnabled(this WebApplication app)
  {
    ArgumentNullException.ThrowIfNull(app);

    return !app.Environment.IsProduction() || app.Configuration.GetValue<bool>(EnableApiDocsKey);
  }

  /// <summary>
  ///   Serves the OpenAPI document on <c>/openapi/v1.json</c> and Swagger UI on <c>/swagger</c>.
  /// </summary>
  /// <param name="app">The application builder.</param>
  /// <returns>The same builder, for chaining.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="app" /> is <see langword="null" />.
  /// </exception>
  public static IApplicationBuilder UseApiDocumentation(this IApplicationBuilder app)
  {
    ArgumentNullException.ThrowIfNull(app);

    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.UseSwaggerUI(options =>
    {
      options.SwaggerEndpoint(DocumentPath, $"CodeCoverage {DocumentName}");
      options.RoutePrefix = SwaggerPath.TrimStart('/');
    });

    return app;
  }

  private static IEnumerable<Assembly> GetDocumentedAssemblies()
  {
    yield return typeof(OpenApiConfiguration).Assembly;
    yield return typeof(Application.DependencyInjection).Assembly;
  }

  #endregion
}
