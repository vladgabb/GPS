using MySqlConnector;
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
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MyGIS
{
    /// <summary>
    /// Логика взаимодействия для MySQLWindow.xaml
    /// </summary>
    public partial class MySQLWindow : Window
    {
        ObservableCollection<Layer> _layers;
        public MySQLWindow(ObservableCollection<Layer> layers)
        {
            InitializeComponent();
            _layers = layers;

            List<string> tables = new List<string>();
            for (int  i=1; i < _layers.Count; i++) 
            {
                Layer layer = _layers[i];

                tables.Add(layer.Name);
            }
            ListOfTables.SelectionChanged += ListOfTables_SelectionChanged;
            ColumnsOfTable.SelectionChanged += ColumnsOfTable_SelectionChanged;

            ListOfTables.ItemsSource = tables;
        }

        private void ColumnsOfTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string value = value = (string)ColumnsOfTable.SelectedItem;
            if (TextBoxForColumns.Text != string.Empty)
                value = ", " + value;

            TextBoxForColumns.Text += value;
        }

        private void ListOfTables_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Layer selectedLayer = _layers.First((el) => el.Name == (string) ListOfTables.SelectedValue);
            TextBoxForColumns.Text = string.Empty;
            ColumnsOfTable.ItemsSource = selectedLayer.Header;
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {

            string headers = TextBoxForColumns.Text;

            string nameTable = (string)ListOfTables.SelectedValue;

            string condition = conditionTextBox.Text;

            string[] checkSQLInjection = { "select", "join", "where", "insert", "update", "from"};
            
            foreach(string injection in  checkSQLInjection)
            {
                if (headers.IndexOf(injection) != -1)
                {
                    MessageBox.Show("Неправильный sql запрос");
                    return;
                }
            }

            string command = string.Empty;

            if (condition == string.Empty)
                command = $"select {headers} from {nameTable};";
            else
            {
                command = $"select {headers} from {nameTable} where {condition};";
            }


            DataTable table = new DataTable();

            await DbManager.OpenConnection();
            try
            {
                MySqlDataReader reader = await DbManager.ExecuteCommand(command);

                int countColumns = reader.FieldCount;

                for (int i = 0; i < countColumns; i++)
                {
                    string _head = reader.GetName(i);
                    table.Columns.Add(_head);
                }

                while (await reader.ReadAsync())
                {
                    DataRow row = table.NewRow();
                    for (int i = 0; i < countColumns; i++)
                    {
                        object item = reader.GetValue(i);
                        row[i] = item is DBNull ? null : item;
                    }
                    table.Rows.Add(row);
                }
                await reader.CloseAsync();
                
            }
            catch { }
            await DbManager.CloseConnection();

            if (table != null)
            {
                Window window = new SQLResultWindow(table);
                window.Show();
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
