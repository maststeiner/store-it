using Microsoft.Extensions.DependencyInjection;

namespace StoreIt.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<CreateStorageUseCase>();
        services.AddScoped<ListStoragesUseCase>();
        services.AddScoped<GetStorageUseCase>();
        services.AddScoped<RenameStorageUseCase>();
        services.AddScoped<DeleteStorageUseCase>();
        services.AddScoped<GetStorageItemsUseCase>();
        services.AddScoped<AddItemUseCase>();
        services.AddScoped<UpdateItemUseCase>();
        services.AddScoped<DeleteItemUseCase>();

        return services;
    }
}
