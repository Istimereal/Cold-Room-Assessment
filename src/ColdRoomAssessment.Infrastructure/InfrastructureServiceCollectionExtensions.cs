using ColdRoomAssessment.Application;
using ColdRoomAssessment.Application.Coverage;
using ColdRoomAssessment.Application.Imports;
using ColdRoomAssessment.Application.Investigation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ColdRoomAssessment.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string ConnectionStringName = "ColdRoom";

    public static IServiceCollection AddColdRoomInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured.");
        }

        services.AddDbContext<ColdRoomDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IColdRoomStore, EfColdRoomStore>();
        services.AddScoped<IMeasurementImportStore, EfMeasurementImportStore>();
        services.AddScoped<IFacilityCoverageStore, EfFacilityCoverageStore>();
        services.AddScoped<IAnalysisPeriodStore, EfAnalysisPeriodStore>();
        services.AddScoped<IAnalysisDataStore, EfAnalysisDataStore>();
        var openAI = configuration.GetSection(OpenAIOptions.SectionName);
        services.AddScoped<IInvestigatorClient>(_ => new OpenAIInvestigatorClient(Options.Create(new OpenAIOptions
        {
            ApiKey = openAI["ApiKey"],
            Model = openAI["Model"]
        })));
        return services;
    }
}
