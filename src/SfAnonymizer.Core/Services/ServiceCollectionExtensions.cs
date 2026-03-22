using Microsoft.Extensions.DependencyInjection;
using SfAnonymizer.Core.Detectors;

namespace SfAnonymizer.Core.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSfAnonymizerCore(this IServiceCollection services)
    {
        services.AddSingleton<ISensitiveColumnDetector, SalesforceColumnDetector>();
        services.AddTransient<TokenGenerator>();
        services.AddTransient<IAnonymizationEngine, AnonymizationEngine>();
        services.AddTransient<IDeAnonymizationEngine, DeAnonymizationEngine>();
        services.AddSingleton<IFileParser, FileParser>();
        services.AddSingleton<IFileWriter, FileWriter>();
        return services;
    }
}
