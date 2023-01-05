using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SpaceEngineersOreRedistribution
{
    /// <summary>
    /// Interaction logic for RedistributionSetup.xaml
    /// </summary>
    public partial class RedistributionSetup : Window
    {
        public RedistributionSetup()
        {
            InitializeComponent();
            ViewModel.ConfirmAction = ConfirmAction;
        }

        private void ButtonAbort_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ConfirmAction()
        {
            DialogResult = true;
            Close();
        }
    }
}
