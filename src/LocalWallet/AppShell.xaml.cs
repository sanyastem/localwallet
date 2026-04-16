using LocalWallet.Views;

namespace LocalWallet;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(AddTransactionPage), typeof(AddTransactionPage));
        Routing.RegisterRoute(nameof(AccountsPage), typeof(AccountsPage));
        Routing.RegisterRoute(nameof(CategoriesPage), typeof(CategoriesPage));
        Routing.RegisterRoute(nameof(LockPage), typeof(LockPage));
        Routing.RegisterRoute(nameof(FamilyListPage), typeof(FamilyListPage));
        Routing.RegisterRoute(nameof(FamilyDetailsPage), typeof(FamilyDetailsPage));
        Routing.RegisterRoute(nameof(InvitePage), typeof(InvitePage));
        Routing.RegisterRoute(nameof(JoinFamilyPage), typeof(JoinFamilyPage));
    }
}
