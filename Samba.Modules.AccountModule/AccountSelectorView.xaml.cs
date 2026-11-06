using System;
using System.ComponentModel.Composition;
using System.Windows.Controls;

namespace Samba.Modules.AccountModule
{
    /// <summary>
    /// Interaction logic for AccountSelectorView.xaml
    /// </summary>

    [Export]
    public partial class AccountSelectorView : UserControl
    {
        [ImportingConstructor]
        public AccountSelectorView(AccountSelectorViewModel viewModel)
        {
            DataContext = viewModel;
            viewModel.Refreshed += viewModel_Refreshed;
            InitializeComponent();
        }
        
        void viewModel_Refreshed(object sender, EventArgs e)
        {
            MainListView.Items.Refresh();
        }
    }
}
