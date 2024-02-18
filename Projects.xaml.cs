using Microsoft.Win32;
using MySqlConnector;
using Serilog;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;

namespace MyGIS
{
    /// <summary>
    /// Логика взаимодействия для Projects.xaml
    /// </summary>

    public class InfoProject
    {

        public InfoProject(int idProject, string name)
        {
            IdProject = idProject;
            Name = name;
        }

        public int IdProject { get; set; }
        public string Name { get; set; }
  
    }


    public partial class Projects : Page
    {
        List<InfoProject> infoProjects = new List<InfoProject>();
        public Projects()
        {
            InitializeComponent();
            
        }

        private async void Page_Initialized(object sender, EventArgs e)
        {
            await LoadProjectFromDb();
        }

        public async Task LoadProjectFromDb()
        {
            infoProjects.Clear();

            string command = "SELECT info.idProject, info.Name, info.DateOfCreation, info.DateOfLastEdit, image.dataOffSet, image.ProjectResizedImage FROM ProjectInformations as info join ProjectImages as image on info.idProject = image.idProject;";
            ProjectPanel.Children.Clear();

            await DbManager.OpenConnection();

            var reader = await DbManager.ExecuteCommand(command);

            await foreach (var project in GetAllProjects(reader))
            {
                int id = (int)project[0];
                string name = (string)project[1];
                DateOnly dateOfCreation = (DateOnly)project[2];
                DateOnly dateOfLastEdit = (DateOnly)project[3];
                ImageSource imageSource = project[4] as ImageSource;

                ProjectPanel.Children.Add(CreateItemOfProjects(
                    name, dateOfCreation, dateOfLastEdit, imageSource, id.ToString()));
            }

            await reader.CloseAsync();

            await DbManager.CloseConnection();

            Log.Information("Projects loaded");
        }

        private async IAsyncEnumerable<List<object>> GetAllProjects(MySqlDataReader reader)
        {
            while (await reader.ReadAsync())
            {
                int id = reader.GetInt32(0);
                string name = reader.GetString(1);
                
                DateOnly dateOfCreation = reader.GetDateOnly(2);

                DateOnly dateOfLastEdit = reader.GetDateOnly(3);

                int dataOffSet = reader.GetInt32(4);

                byte[] buffer = new byte[dataOffSet];
                reader.GetBytes(5, 0, buffer, 0, dataOffSet);


                var imageSource = ImageTools.ByteArrayToImageSource(buffer);

                List<object> list = new List<object>() { 
                    id, name, dateOfCreation,
                    dateOfLastEdit, imageSource
                };

                var info = new InfoProject(id, name);

                infoProjects.Add(info);
                
                yield return list;
            }
        }

        private Grid CreateItemOfProjects(string name, DateOnly dateOfCreation, DateOnly dateOfLastEdit, ImageSource image, string id)
        {
            Grid mainGrid = new Grid();
            mainGrid.Width = 140;
            mainGrid.Height = 240;
            mainGrid.Uid = id;
            mainGrid.Margin = new Thickness(5);
            mainGrid.MouseLeave += MainGrid_MouseLeave;
            mainGrid.MouseEnter += MainGrid_MouseEnter;
            mainGrid.MouseDown += Grid_MouseDown;

            Button deleteButton = new Button
            {
                Width = 20,
                Height = 20,
                Content = "x",
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalContentAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(100, 10, 0, 0)
            };
            deleteButton.Click += DeleteImageButton_Click;
            deleteButton.Uid = "DeleteButton" + id;
            Panel.SetZIndex(deleteButton, 3);


            Grid icon = new Grid()
            {
                Width = 140,
                Height = 140,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new ImageBrush(image)
            };


            Label labelOpenProject = new Label()
            {
                Name = "OpenText",
                Visibility = Visibility.Hidden,
                Width=60,
                Height=25,
                Margin = new Thickness(0, 0, 0, -42),
                HorizontalAlignment = HorizontalAlignment.Center,
                Content = "Открыть",
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 255)),
                FontSize = 12
        };
            

            Random random = new Random();

            Grid grid = new Grid();
            grid.Height = 100;

            Frame MFrame = new Frame();
            MFrame.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(
                (byte)random.Next(0, 255), (byte)random.Next(0, 255), (byte)random.Next(0, 255)));

            grid.VerticalAlignment = VerticalAlignment.Bottom;

            Frame frame = new Frame();
            frame.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(170, 170, 170));
            Label label = new Label();
            label.Content = "Название "+name;
            label.FontSize = 9;

            Label label1 = new Label();
            label1.Content = "Дата создания\n " + dateOfCreation;
            label1.FontSize = 9;

            Label label2 = new Label();
            label2.Content = "Дата последнего\n изменения " + dateOfLastEdit;
            label2.FontSize = 9;

            StackPanel stackPanel = new StackPanel();

            
            grid.Children.Add(frame);
            stackPanel.Children.Add(label);
            stackPanel.Children.Add(label1);
            stackPanel.Children.Add(label2);
            grid.Children.Add(stackPanel);

            mainGrid.Children.Add(MFrame);
            mainGrid.Children.Add(labelOpenProject);
            mainGrid.Children.Add(deleteButton);
            mainGrid.Children.Add(grid);
            mainGrid.Children.Add(icon);
            


            return mainGrid;
        }

        private async void DeleteImageButton_Click(object sender, RoutedEventArgs e)
        {
            Button? button = sender as Button;

            if (button is null)
                return;

            string buttonIdentify = "DeleteButton";
            string uid = button.Uid;

            int id = int.Parse(uid.Substring(buttonIdentify.Length, uid.Length - buttonIdentify.Length));

            MessageBoxResult result = MessageBox.Show("Вы уверены?", "Удалить проект", MessageBoxButton.OKCancel);

            if (result == MessageBoxResult.OK) 
            {
                await Task.Run(async () => {

                    string command = "delete from ProjectInformations where idProject = @id",
                    command1 = "delete from ProjectImages where idProject = @id";

                    List<MySqlParameter> parameters = new List<MySqlParameter>()
                    {
                        new MySqlParameter("@id", MySqlDbType.Int32)
                        {
                            Value = id
                        }
                    };

                    await DbManager.OpenConnection();

                    var reader = await DbManager.ExecuteCommand(command1, parameters);
                    await reader.CloseAsync();

                    reader = await DbManager.ExecuteCommand(command, parameters);
                    await reader.CloseAsync();

                    await DbManager.CloseConnection();
                });

                await LoadProjectFromDb();
            }
        }

        private void MainGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            Grid? grid = sender as Grid;

            if (grid is null)
                return;

            grid.Height = 240;

            DoubleAnimation gridAnimation = new DoubleAnimation();
            gridAnimation.From = grid.ActualHeight;
            gridAnimation.To = 240;
            gridAnimation.Duration = TimeSpan.FromSeconds(0.1);
            grid.BeginAnimation(Grid.HeightProperty, gridAnimation);

            
            var label = grid.Children[1] as Label;

            if (label is null)
                return;

            label.Visibility = Visibility.Hidden;

        }

        private void MainGrid_MouseEnter(object sender, MouseEventArgs e)
        {
            Grid? grid = sender as Grid;

            if (grid is null)
                return;

            DoubleAnimation gridAnimation = new DoubleAnimation();
            gridAnimation.From = grid.ActualHeight;
            gridAnimation.To = 270;
            gridAnimation.Duration = TimeSpan.FromSeconds(1);
            grid.BeginAnimation(Grid.HeightProperty, gridAnimation);

            var label = grid.Children[1] as Label;

            if (label is null)
                return;

            label.Visibility = Visibility.Visible;
            label.VerticalAlignment = VerticalAlignment.Center;
        }

        private void BtnOpenCreationWindow_Click(object sender, RoutedEventArgs e)
        {
            var window = new CreateProject();

            window.Show();
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Grid? grid = sender as Grid;

            if (grid is null)
                return;

            var id = int.Parse(grid.Uid);

            var info = infoProjects.Find((e) => e.IdProject == id);

            if (info is null)
                return;

            //var page = new ManageMap(info);

            //this.NavigationService.Navigate(page);
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            await LoadProjectFromDb();
        }

       
    }
}
