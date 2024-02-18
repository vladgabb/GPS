using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
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
    /// Логика взаимодействия для SQLResultWindow.xaml
    /// </summary>
    public partial class SQLResultWindow : Window
    {
        public SQLResultWindow(DataTable table)
        {
            InitializeComponent();

            DataGridResult.ItemsSource = table.DefaultView;
        }
    }
}
