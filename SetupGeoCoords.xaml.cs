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

namespace MyGIS
{
    /// <summary>
    /// Логика взаимодействия для SetupGeoCoords.xaml
    /// </summary>
    public partial class SetupGeoCoords : Window
    {
        public SetupGeoCoords(PointsMapBinding pointsMap)
        {
            InitializeComponent();

            pointsBinding.ItemsSource = (System.Collections.IEnumerable)pointsMap;


        }

        public void Accept_Click(object sender, RoutedEventArgs e) 
        {
            this.DialogResult = true;
        }
    }
}
