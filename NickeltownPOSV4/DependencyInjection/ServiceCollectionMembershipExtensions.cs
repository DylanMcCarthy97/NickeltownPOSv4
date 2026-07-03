using Microsoft.Extensions.DependencyInjection;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services.Membership;
using NickeltownPOSV4.ViewModels.Membership;
using NickeltownPOSV4.Views.Membership;

namespace NickeltownPOSV4.DependencyInjection;

internal static class ServiceCollectionMembershipExtensions
{
    public static IServiceCollection AddMembershipServices(this IServiceCollection services)
    {
        services.AddSingleton<IMembershipSettingsRepository, SqliteMembershipSettingsRepository>();
        services.AddSingleton<IMembershipFormContentRepository, SqliteMembershipFormContentRepository>();
        services.AddSingleton<IMembershipApplicationRepository, SqliteMembershipApplicationRepository>();
        services.AddSingleton<IMembershipMemberRepository, SqliteMembershipMemberRepository>();

        services.AddSingleton<IMembershipSettingsService, MembershipSettingsService>();
        services.AddSingleton<IMembershipFormContentService, MembershipFormContentService>();
        services.AddSingleton<IMembershipDashboardService, MembershipDashboardService>();
        services.AddSingleton<IMembershipApplicationService, MembershipApplicationService>();
        services.AddSingleton<IMembershipApplicationReviewService, MembershipApplicationReviewService>();

        services.AddSingleton<MembershipHomeViewModel>();
        services.AddTransient<MembershipDashboardViewModel>();
        services.AddTransient<MembershipApplicationsViewModel>();
        services.AddTransient<MembershipPaperApplicationViewModel>();
        services.AddTransient<MembershipApplicationReviewViewModel>();
        services.AddTransient<MembershipMembersViewModel>();
        services.AddTransient<MembershipRenewalsViewModel>();
        services.AddTransient<MembershipPaymentsViewModel>();
        services.AddTransient<MembershipCardsViewModel>();
        services.AddTransient<MembershipDocumentsViewModel>();
        services.AddTransient<MembershipReportsViewModel>();
        services.AddTransient<MembershipSettingsViewModel>();

        services.AddTransient<MembershipHomePage>();
        services.AddTransient<MembershipDashboardPage>();
        services.AddTransient<MembershipApplicationsPage>();
        services.AddTransient<MembershipPaperApplicationPage>();
        services.AddTransient<MembershipApplicationReviewPage>();
        services.AddTransient<MembershipMembersPage>();
        services.AddTransient<MembershipRenewalsPage>();
        services.AddTransient<MembershipPaymentsPage>();
        services.AddTransient<MembershipCardsPage>();
        services.AddTransient<MembershipDocumentsPage>();
        services.AddTransient<MembershipReportsPage>();
        services.AddTransient<MembershipSettingsPage>();

        return services;
    }
}
