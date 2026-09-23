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

        services.AddScoped<ProvisionUserUseCase>();
        services.AddScoped<DeleteAccountUseCase>();

        // SPEC-007 sharing
        services.AddScoped<StorageSummaries>();
        services.AddScoped<CreateInvitationUseCase>();
        services.AddScoped<GetInvitationUseCase>();
        services.AddScoped<DeactivateInvitationUseCase>();
        services.AddScoped<PreviewInvitationUseCase>();
        services.AddScoped<AcceptInvitationUseCase>();
        services.AddScoped<ListMembersUseCase>();
        services.AddScoped<RemoveMemberUseCase>();
        services.AddScoped<LeaveStorageUseCase>();

        return services;
    }
}
