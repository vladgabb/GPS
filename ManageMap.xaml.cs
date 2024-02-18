using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.UI.Controls;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MyGIS
{
    /// <summary>
    /// Логика взаимодействия для ManageMap.xaml
    /// </summary>
    public partial class ManageMap : Page
    {
        InfoProject _info = new InfoProject(4, "4 project");

        System.Drawing.Image imageMap;

        ObservableCollection<PointsMapBinding>? pointsMapBinding;

        MapVM? mapViewModel;

        double CurrentScale = 128_000;

        List<string> namesOfLayers = new List<string>();

        List<List<string>> headers = new List<List<string>>();
        List<List<object>> dataLayers = new List<List<object>>();
        List<List<Type>> typesHeaders = new List<List<Type>>();

        Point ScreenCenter;

        public ManageMap()
        {
            InitializeComponent();

            mapViewModel = new MapVM(_info);
            Binding bindingMap = new Binding();
            Binding bindingGraphicsOverlays = new Binding();

            bindingMap.Source = mapViewModel;
            bindingMap.Path = new PropertyPath(MapVM.MapProperty);

            bindingGraphicsOverlays.Source = mapViewModel;
            bindingGraphicsOverlays.Path = new PropertyPath(MapVM.GraphicsOverlaysProperty);

            MainMapView.SetBinding(MapView.MapProperty, bindingMap);
            MainMapView.SetBinding(MapView.GraphicsOverlaysProperty, bindingGraphicsOverlays);
        }

        private async Task SetMapBinding()
        {
            //mapViewModel = new MapViewModel(_info, pointsMapBinding);
            //Binding bindingMap = new Binding();
            //Binding bindingGraphicsOverlays = new Binding();

            //bindingMap.Source = mapViewModel;
            //bindingMap.Path = new PropertyPath(MapViewModel.MapProperty);

            //bindingGraphicsOverlays.Source = mapViewModel;
            //bindingGraphicsOverlays.Path = new PropertyPath(MapViewModel.GraphicsOverlaysProperty);

            //await Task.Run(() =>
            //{

            //    MainMapView.SetBinding(MapView.MapProperty, bindingMap);
            //    MainMapView.SetBinding(MapView.GraphicsOverlaysProperty, bindingGraphicsOverlays);
            //});
        }

        private async void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            //var extent = mapViewModel.RasterLayer.FullExtent;

            //CurrentScale = Math.Max(CurrentScale / 2, 125);
            

            //var center = GetCenterVisibleArea();

            //await MainMapView.SetViewpointAsync(new Viewpoint(center, CurrentScale), TimeSpan.FromSeconds(0.25), Esri.ArcGISRuntime.UI.AnimationCurve.EaseOutQuad);
        }

        private async void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentScale *= 2;

            var center = GetCenterVisibleArea();

            await MainMapView.SetViewpointAsync(new Viewpoint(center, CurrentScale), TimeSpan.FromSeconds(0.5), Esri.ArcGISRuntime.UI.AnimationCurve.EaseInQuad);
        }

        private MapPoint? GetCenterVisibleArea()
        {
            var viewPoint = MainMapView.GetCurrentViewpoint(ViewpointType.BoundingGeometry);

            var center = viewPoint.TargetGeometry.Extent.GetCenter();

            return center;
        }

        private async Task GetAllLayers()
        {
            await DbManager.OpenConnection();

            string command = "select nameTableOfLayer from project_layers where idProject = @id;";

            List<MySqlParameter> mySqlParameters = new List<MySqlParameter>() { 
                new MySqlParameter("@id", MySqlDbType.Int32)
                {
                    Value = _info.IdProject
                },
            };

            var reader = await DbManager.ExecuteCommand(command, mySqlParameters);

            // read all data
            while (await reader.ReadAsync())
            {
                namesOfLayers.Add(reader.GetString(0));
            }

            await reader.CloseAsync();

            headers.Clear();
            typesHeaders.Clear();
            dataLayers.Clear();

            foreach (var nameLayer in namesOfLayers)
            {
                await GetAllFromLayer(nameLayer);
            }

            await DbManager.CloseConnection();
        }

        private async void MainMapView_Initialized(object sender, EventArgs e)
        {
            await SetMapBinding();

            //await mapViewModel.AddImageLayer();
        }

        private async void ManageMapPage_Loaded(object sender, RoutedEventArgs e)
        {
            await GetProjectInformation();

            await GetAllLayers();
        }

        private async Task GetProjectInformation()
        {
            await DbManager.OpenConnection();

            string command = "select dataOffSet, ProjectImage, PointsBinding from project_images where idProject = @id;";

            List<MySqlParameter> parameters = new List<MySqlParameter>() {
                new MySqlParameter("@id", MySqlDbType.Int32)
                {
                    Value = _info.IdProject
                }

            };

            var reader = await DbManager.ExecuteCommand(command, parameters);

            try
            {

                while (await reader.ReadAsync())
                {
                    int dataOffSet = reader.GetInt32(0);

                    byte[] buffer = new byte[dataOffSet];
                    reader.GetBytes(1, 0, buffer, 0, dataOffSet);

                    var jsonStringPointsBinding = reader.GetString(2);

                    pointsMapBinding = JsonSerializer.Deserialize(jsonStringPointsBinding, typeof(ObservableCollection<PointsMapBinding>))
                        as ObservableCollection<PointsMapBinding>;

                    using (MemoryStream stream = new MemoryStream())
                    {
                        stream.Write(buffer, 0, buffer.Length);

                        imageMap = System.Drawing.Image.FromStream(stream);

                        imageMap.Save($"temp/{_info.Name}{_info.IdProject}.tif", System.Drawing.Imaging.ImageFormat.Tiff);
                    }

                }
            }
            catch { }

            await reader.CloseAsync();

            await DbManager.CloseConnection();
        }

        private async Task GetAllFromLayer(string nameOfLayer)
        {
            string command = $"select * from {nameOfLayer};";

            var reader = await DbManager.ExecuteCommand(command);

            List<string> header = new List<string>();
            List<Type> typesTable = new List<Type>();
            List<object> data = new List<object>();

            int countColumns = reader.FieldCount;
            
            for (int i=0; i < countColumns; i++)
            {
                string _head = reader.GetName(i);
                Type type = reader.GetFieldType(i);

                header.Add(_head);
                typesTable.Add(type);
            }

            while (await reader.ReadAsync())
            {
                for (int i = 0; i < countColumns; i++)
                {
                    data.Add(reader.GetValue(i));
                }
            }

            headers.Add(header);
            typesHeaders.Add(typesTable);
            dataLayers.Add(data);

            await reader.CloseAsync();

        }
    
        private void AddUIRowsLayers()
        {
            foreach (var nameLayer in namesOfLayers)
            {
                StackPanel row = new StackPanel()
                {
                    Margin = new Thickness(0, 5, 0, 5),
                    Orientation = Orientation.Horizontal
                };

                TextBlock textBlock = new TextBlock()
                {
                    Width = 50,
                    Height = 20,
                    MaxHeight = 75,
                    FontSize = 8,
                    Text = nameLayer
                };

                   
                Button showButton = new Button()
                {
                    Width = 20,
                    Height = 20,
                    Content = new Image().Source = new BitmapImage(new Uri("/images/Eye.png", UriKind.Relative))
                };

                Button selectButton = new Button()
                {
                    Width = 20,
                    Height = 20,
                    Content = new Image().Source = new BitmapImage(new Uri("/images/Select All.png", UriKind.Relative))
                };

                Button editButton = new Button()
                {
                    Width = 20,
                    Height = 20,
                    Content = new Image().Source = new BitmapImage(new Uri("/images/Edit.png", UriKind.Relative))
                };

                StackPanel rowButtons = new StackPanel()
                {
                    Margin = new Thickness(0, 3, 0, 3),
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Top,
                    Height = 20
                };


                rowButtons.Children.Add(showButton);
                rowButtons.Children.Add(selectButton);
                rowButtons.Children.Add(editButton);

                row.Children.Add(textBlock);
                row.Children.Add(rowButtons);

                StackLayers.Children.Add(row);
            }
            
        }
    }

    
}
