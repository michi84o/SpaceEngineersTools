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
        }

        private void ButtonAbort_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ButtonConfirm_Click(object sender, RoutedEventArgs e)
        {
            var ores = ViewModel.OreInfos.Select(o => o.Name).Distinct().ToList();
            if (ores.Count > 17)
            {
                MessageBox.Show(
                    "You defined more than 17 different ores.\r\nEach ore definition uses a set of 15 different values.\r\nWe can only store 255 different values in the map file.\r\nPlease reduce the number of ores!",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}
