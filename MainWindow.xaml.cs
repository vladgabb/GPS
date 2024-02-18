using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Rasters;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks.Offline;
using Esri.ArcGISRuntime.UI;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PropertyChanged;

namespace MyGIS
{
    

    [AddINotifyPropertyChangedInterface]
    public class EditableKeyValuePair
    {
        public string Key { get; set; }
        public object? Value { get; set; }
    }

    public class Layer
    {
        private string _id;
        private string _name;
        List<string> header;
        List<Type> typesHeader;
        List<object> data;

        public Layer()
        {
            _id = string.Empty;
            _name = string.Empty;
            header = new List<string>();
            typesHeader = new List<Type>();
            data = new List<object>();

        }

        public Layer(string id, string name, List<string> header, List<Type> typesHeader, List<object> data)
        {
            Id = id;
            Name = name;
            this.Header = header;
            this.TypesHeader = typesHeader;
            this.Data = data;
        }

        public string Id { get => _id; set => _id = value; }
        public int LastRowId { get; set; }
        public string Name { get => _name; set => _name = value; }
        public List<string> Header { get => header; set => header = value; }
        public List<Type> TypesHeader { get => typesHeader; set => typesHeader = value; }
        public List<object> Data { get => data; set => data = value; }
        public GraphicsOverlay GraphicsOverlay { get; set; }
    }

    enum TypeOperation
    {
        None = 0b00000000,
        Identify = 0b00000001,
        InfoAttributes = 0b00000010,
        Info   = 0b00000100,
        Clear  = 0b00001000,
        Move   = 0b00010000,
        Rotate = 0b00100000,
        Resize = 0b01000000
    }

    enum TypeGraphic
    {
        None = 0b0000,
        Rectangle = 0b0001,
        Polygon = 0b0010,
        Line = 0b0100,
        Ellipse = 0b1000
    }


    public partial class MainWindow : Window
    {
        InfoProject _info = new InfoProject(4, "4 project");

        System.Drawing.Image imageMap;

        ObservableCollection<PointsMapBinding>? pointsMapBinding;
        double CurrentScale = 128_000;

        TypeOperation Operation { get; set; } = TypeOperation.None;
        TypeGraphic TypeGraphic { get; set; } = TypeGraphic.None;
        Graphic? movableGraphic = null;
        Graphic? selectedGraphic = null;
        MapPoint LastMousePosition;
        int numberOfAddedPoints = 0;
        string Splitter = "$";

        ObservableCollection<Layer> Layers = new ObservableCollection<Layer>();
        List<string>? visibleLayerNames = new List<string>();
        Layer? SelectedLayer = null;
        Layer? CurrentEditLayer = null;


        private Map _map;
        private GraphicsOverlayCollection graphicsOverlays = new GraphicsOverlayCollection();
        RasterLayer rasterLayer;
        SpatialReference? spatialReference;

        public MainWindow()
        {
            InitializeComponent();
            
            SetMap(_info);

            SetGraphicsOverlayCollection();

            AddDrawingOverlayToGraphicsOverlays();

            this.Loaded += MainWindow_Loaded;

            StackLayers.ItemsSource = Layers;

            MapView.MouseLeftButtonDown += MapView_MouseLeftButtonDown;
            MapView.MouseDoubleClick += MapView_MouseDoubleClick;
            MapView.MouseMove += MapView_MouseMove;
            MapView.MouseRightButtonDown += MapView_MouseRightButtonDown;
        }

        private void MapView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (movableGraphic != null)
                movableGraphic.IsSelected = false;
            movableGraphic = null;

        }

        private async void MapView_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (CurrentEditLayer is null)
                return;

            if (movableGraphic != null)
            {
                movableGraphic.IsSelected = false;

                IDictionary<string, object?> attributes = movableGraphic.Attributes;

                if (CurrentEditLayer.Name == $"{_info.Name}{_info.IdProject}")
                {
                    int? indexCorner = (int?)attributes["corner"];
                    if (indexCorner is null)
                        return;

                    Envelope? envelope = rasterLayer.FullExtent;

                    if (envelope == null)
                        return;

                    MapPoint mapPoint = (MapPoint)movableGraphic.Geometry;

                    pointsMapBinding[indexCorner.Value].PixelX = mapPoint.X;
                    pointsMapBinding[indexCorner.Value].PixelY = mapPoint.Y;

                    // mapPoint.X - envelope.XMin
                    // mapPoint.Y - envelope.YMin

                    string value = App.Wrap(JsonSerializer.Serialize(pointsMapBinding));

                    await DbManager.OpenConnection();

                    string command = $"update project_images set PointsBinding = {value} where idProject = {_info.IdProject};";

                    MySqlDataReader reader = await DbManager.ExecuteCommand(command);

                    await reader.CloseAsync();

                    await DbManager.CloseConnection();
                }
                else
                {
                    try
                    {
                        string idHeader = CurrentEditLayer.Header[0];
                        int IdRow = (int)attributes[idHeader];

                        string value = App.Wrap(movableGraphic.Geometry.ToJson() + Splitter + movableGraphic.Symbol.ToJson());

                        await DbManager.OpenConnection();

                        string command = $"update {CurrentEditLayer.Name} set graphic = {value} where {CurrentEditLayer.Header[0]} = {IdRow};";

                        MySqlDataReader reader = await DbManager.ExecuteCommand(command);

                        await reader.CloseAsync();

                        await DbManager.CloseConnection();
                    }
                    catch { }
                }



                movableGraphic = null;
            }
            
            GraphicsOverlay? drawing = graphicsOverlays["DrawingGraphicOverlay"];
            GraphicsOverlay? editOverlay = graphicsOverlays[CurrentEditLayer.Name];

            if (drawing.Graphics.Count < 2 || TypeGraphic == TypeGraphic.None)
                return;

            List<MapPoint> points = new List<MapPoint>();
            foreach (var graphic in drawing.Graphics)
            {
                points.Add((MapPoint)graphic.Geometry);
            }
            Graphic? shape = null;
            Dictionary<string, object?> attribures = new Dictionary<string, object?>();

            string idColumn = CurrentEditLayer.Header[0];
            int id = CurrentEditLayer.LastRowId + 1;
            attribures[idColumn] = id;

            for (int column = 1; column < CurrentEditLayer.Header.Count-1;  column++)
            {
                attribures[CurrentEditLayer.Header[column]] = null;
            }
            

            if (TypeGraphic == TypeGraphic.Rectangle)
            {
                MapPoint a = points[0],
                    b = points[1];

                MapPoint point1 = new MapPoint(b.X, a.Y),
                    point2 = new MapPoint(a.X, b.Y);
                points.Insert(1, point1);
                points.Add(point2);

                Esri.ArcGISRuntime.Geometry.Polygon multipoint = new Esri.ArcGISRuntime.Geometry.Polygon(points);

                var lineSymbol = new SimpleLineSymbol(SimpleLineSymbolStyle.ShortDot, System.Drawing.Color.Black, 3);
                var fillSymbol = new SimpleFillSymbol(SimpleFillSymbolStyle.DiagonalCross, System.Drawing.Color.Black, lineSymbol);

                shape = new Graphic(multipoint, attribures, fillSymbol);
            }

            if (TypeGraphic == TypeGraphic.Line)
            {

                Esri.ArcGISRuntime.Geometry.Polyline multipoint = new Esri.ArcGISRuntime.Geometry.Polyline(points);
                var lineSymbol = new SimpleLineSymbol(SimpleLineSymbolStyle.Dash, System.Drawing.Color.Black, 5);

                shape = new Graphic(multipoint, attribures, lineSymbol);
            }

            if (TypeGraphic == TypeGraphic.Polygon)
            {
                Esri.ArcGISRuntime.Geometry.Polygon multipoint = new Esri.ArcGISRuntime.Geometry.Polygon(points);

                var lineSymbol = new SimpleLineSymbol(SimpleLineSymbolStyle.LongDash, System.Drawing.Color.Black, 3);
                var fillSymbol = new SimpleFillSymbol(SimpleFillSymbolStyle.BackwardDiagonal, System.Drawing.Color.Black, lineSymbol);

                shape = new Graphic(multipoint, attribures, fillSymbol);
            }

            if (TypeGraphic == TypeGraphic.Ellipse)
            {
                double a = Math.Abs(points[1].X - points[0].X),
                    b = Math.Abs(points[1].Y - points[0].Y);

                double rotationAngle = Math.Atan2(points[1].X - points[0].X, points[1].Y - points[0].Y) == double.NaN ? 0 : Math.Atan2(a, b);

                double temp;
                if (a > b)
                {
                    temp = a;
                    a = b;
                    b = temp;
                }

                var ellipseArcSegment = new EllipticArcSegment(points[0], 0, b, a / b, 0, 2 * Math.PI, spatialReference);
                
                var list = new List<Segment>()
                { (Segment)ellipseArcSegment};

                SimpleLineSymbol simpleLine = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.Gray, 1);
                SimpleFillSymbol polygonFillSymbol = new SimpleFillSymbol(SimpleFillSymbolStyle.DiagonalCross, System.Drawing.Color.Black, simpleLine);

                var ellipse = new Esri.ArcGISRuntime.Geometry.Polygon(list);
                //Esri.ArcGISRuntime.Geometry.Geometry geo = ellipse.Rotate(rotationAngle * 180 / Math.PI);

                shape = new Graphic(ellipse, attribures, polygonFillSymbol);
            }

            drawing.Graphics.Clear();
            numberOfAddedPoints = 0;

            if (shape is null)
                return;

            editOverlay.Graphics.Add(shape);
            CurrentEditLayer.LastRowId++;

            string nameTable = CurrentEditLayer.Name;
            string headers = "";
            string values = "";

            headers = string.Join(", ", CurrentEditLayer.Header);

            headers = headers.Replace($"{CurrentEditLayer.Header[0]}, ", "");

            for (int i = 1; i < CurrentEditLayer.Header.Count - 1; i++)
                values += "NULL, ";

            values += App.Wrap(shape.Geometry.ToJson() + Splitter + shape.Symbol.ToJson());

            await Task.Run(async () =>
            {
                await DbManager.OpenConnection();

                string command = $"insert into {nameTable} ({headers}) values ({values});";

                MySqlDataReader reader = await DbManager.ExecuteCommand(command);

                await reader.CloseAsync();

                await DbManager.CloseConnection();
            });

        }

        private double GetGeoX(double x)
        {
            return pointsMapBinding[0].GeoX + x * (pointsMapBinding[1].GeoX - pointsMapBinding[0].GeoX);
        }

        private double GetGeoY(double y)
        {
            return pointsMapBinding[3].GeoY + y * (pointsMapBinding[0].GeoY - pointsMapBinding[3].GeoY);
        }

        private void MapView_MouseMove(object sender, MouseEventArgs e)
        {
            var screenPoint = e.GetPosition(MapView);

            MapPoint? mapMousePosition = MapView.ScreenToLocation(screenPoint);

            if (mapMousePosition == null)
                return;

            double x = (mapMousePosition.X - pointsMapBinding[0].PixelX) / (pointsMapBinding[1].PixelX - pointsMapBinding[0].PixelX),
                y = (mapMousePosition.Y - pointsMapBinding[3].PixelY) / (pointsMapBinding[0].PixelY - pointsMapBinding[3].PixelY);

            double geoX = GetGeoX(x),
                geoY = GetGeoY(y);

            GeoCoordsTextBlock.Text = $"Координаты {geoX}; {geoY};";
            
            if (movableGraphic != null)
            {
                var geometry = movableGraphic.Geometry;
                MapPoint MapLocation = MapView.ScreenToLocation(screenPoint);

                double offSetX = MapLocation.X - LastMousePosition.X,
                    offSetY = MapLocation.Y - LastMousePosition.Y;

                LastMousePosition = MapLocation;
                var newGeometry = GeometryEngine.Move(geometry, offSetX, offSetY);

                CurrentEditLayer.GraphicsOverlay.Graphics.First((
                    el)=> el == movableGraphic
                ).Geometry = newGeometry;
            }
        }

        private async void MapView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            movableGraphic = null;
            //SelectedLayer = null;

            var screenPoint = e.GetPosition(MapView);

            foreach (var graphicsOverlay in graphicsOverlays)
            {
                if (graphicsOverlay is null) continue;

                foreach (var graphic in graphicsOverlay.Graphics)
                {
                    if (graphic is null) continue;

                    graphic.IsSelected = false;
                }
            }

            if (Operation == TypeOperation.Identify)
            {
                var result = await MapView.IdentifyGraphicsOverlaysAsync(screenPoint, 10, false);

                if (result is null || result.Count <= 0)
                    return;


                var layer = result.First((el) => el.GraphicsOverlay.IsVisible);

                if (layer is null) return;

                layer.Graphics[0].IsSelected = true;
            }

            if (Operation == TypeOperation.InfoAttributes)
            {
                var result = await MapView.IdentifyGraphicsOverlaysAsync(screenPoint, 10, false);

                if (result is null || result.Count <= 0)
                    return;


                var layer = result.First((el) => el.GraphicsOverlay.IsVisible);

                if (layer is null) return;

                Layer VisibleLayer = Layers.First((el) => el.GraphicsOverlay == layer.GraphicsOverlay);
                SelectedLayer = VisibleLayer;

                NameOfRowEditotTable.Text = $"Таблица {VisibleLayer.Name}";

                selectedGraphic = layer.Graphics[0];
                selectedGraphic.IsSelected = true;

                ObservableCollection<EditableKeyValuePair> list = new ObservableCollection<EditableKeyValuePair> ();

                foreach (KeyValuePair<string, object?> pair in selectedGraphic.Attributes)
                {
                    
                    list.Add(new EditableKeyValuePair()
                    {
                        Key = pair.Key,
                        Value = pair.Value
                    });
                }

                DataGridOfRowEditor.ItemsSource = list;
            }
            if (Operation == TypeOperation.Move)
            {

                if (CurrentEditLayer is null)
                    return;

                var result = await MapView.IdentifyGraphicsOverlayAsync(CurrentEditLayer.GraphicsOverlay, screenPoint, 10, false);

                if (result is null || result.Graphics.Count <= 0)
                    return;

                var graphic = result.Graphics[0];
                graphic.IsSelected = true;

                movableGraphic = graphic;

                LastMousePosition = MapView.ScreenToLocation(screenPoint);
            }

            if (CurrentEditLayer is null)
                return;
            GraphicsOverlay? drawing = graphicsOverlays["DrawingGraphicOverlay"];
            if (CurrentEditLayer.Name == $"{_info.Name}{_info.IdProject}")
                return;

            GraphicsOverlay? editOverlay = graphicsOverlays[CurrentEditLayer.Name];


            if (Operation == TypeOperation.Info)
            {
                var result = await MapView.IdentifyGraphicsOverlaysAsync(screenPoint, 10, false);

                if (result is null || result.Count <= 0)
                    return;


                var layer = result.First((el) => el.GraphicsOverlay.IsVisible);

                if (layer is null) return;

                Graphic figure = layer.Graphics[0];
                figure.IsSelected = true;

                Esri.ArcGISRuntime.Geometry.Geometry? _geo = figure.Geometry;
                double area = _geo.Area(),
                    length = _geo.Length();

                double XMin = pointsMapBinding[3].PixelX,
                    YMin = pointsMapBinding[3].PixelY,
                    LatitudesMin = pointsMapBinding[3].GeoX,
                    LongitudeMin = pointsMapBinding[3].GeoY;

                double XMax = pointsMapBinding[1].PixelX,
                    YMax = pointsMapBinding[1].PixelY,
                    LatitudesMax = pointsMapBinding[1].GeoX,
                    LongitudeMax = pointsMapBinding[1].GeoY;

                double geodesicArea = Math.Abs(area * (LatitudesMax - LatitudesMin)
                    / (XMax - XMin) * (LongitudeMax - LongitudeMin) / (YMax - YMin) * 111 * 111) / 5;

                double lengthOfOblast = Math.Pow((XMax-XMin)* (XMax - XMin) + (YMax-YMin)* (YMax - YMin), 0.5);

                double geodesicLength = length * Math.Pow((LatitudesMax - LatitudesMin)* (LatitudesMax - LatitudesMin) + (LongitudeMax - LongitudeMin)* (LongitudeMax - LongitudeMin), 0.5) / lengthOfOblast * 111 * 1.41 / 5;

                MessageBox.Show(geodesicArea.ToString(), geodesicLength.ToString());
            }

            if (Operation == TypeOperation.Clear)
            {
                if (CurrentEditLayer is null)
                    return;

                var result = await MapView.IdentifyGraphicsOverlayAsync(CurrentEditLayer.GraphicsOverlay, screenPoint, 10, false);

                if (result is null || result.Graphics.Count <= 0)
                    return;

                var graphic = result.Graphics[0];

                string idColumn = CurrentEditLayer.Header[0];
                int? id = (int?) graphic.Attributes[idColumn];

                graphicsOverlays[CurrentEditLayer.Name].Graphics.Remove(graphic);

                if (id is null)
                    return;

                await DbManager.OpenConnection();

                string command = $"delete from {CurrentEditLayer.Name} where {idColumn} = {id};";

                MySqlDataReader reader = await DbManager.ExecuteCommand(command);

                await reader.CloseAsync();

                await DbManager.CloseConnection();
            }

         
            if (TypeGraphic == TypeGraphic.Rectangle)
            {
                

                if (numberOfAddedPoints < 2)
                {
                    MapPoint? mapPoint = MapView.ScreenToLocation(screenPoint);

                    var marker = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Cross, System.Drawing.Color.DarkViolet, 5);

                    Graphic point = new Graphic(mapPoint, marker);

                    drawing.Graphics.Add(point);
                    numberOfAddedPoints++;
                }
            }

            if (TypeGraphic == TypeGraphic.Line)
            {

                if (numberOfAddedPoints < 2)
                {
                    MapPoint? mapPoint = MapView.ScreenToLocation(screenPoint);

                    var marker = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Cross, System.Drawing.Color.DarkViolet, 5);

                    Graphic point = new Graphic(mapPoint, marker);

                    drawing.Graphics.Add(point);
                    numberOfAddedPoints++;
                }
            }

            if (TypeGraphic == TypeGraphic.Polygon)
            {

                MapPoint? mapPoint = MapView.ScreenToLocation(screenPoint);

                var marker = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Cross, System.Drawing.Color.DarkViolet, 5);

                Graphic point = new Graphic(mapPoint, marker);

                drawing.Graphics.Add(point);
                numberOfAddedPoints++;

            }

            if (TypeGraphic == TypeGraphic.Ellipse)
            {

                if (numberOfAddedPoints < 2)
                {
                    MapPoint? mapPoint = MapView.ScreenToLocation(screenPoint);

                    var marker = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Cross, System.Drawing.Color.DarkViolet, 5);

                    Graphic point = new Graphic(mapPoint, marker);

                    drawing.Graphics.Add(point);
                    numberOfAddedPoints++;
                }
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await GetProjectInformation();

            await GetAllLayers();

            await rasterLayer.LoadAsync();

            await Task.Run(()=> {
                _map.Basemap = new Basemap(rasterLayer);
                spatialReference = rasterLayer.FullExtent.SpatialReference;
            });

            await DrawPointsBinding(pointsMapBinding);
        }

        private void SetMap(InfoProject infoProject)
        {
            _map = new Map();

            MapView.Map = _map;
        }

        private void SetGraphicsOverlayCollection()
        {
            MapView.GraphicsOverlays = graphicsOverlays;
        }

        private void AddDrawingOverlayToGraphicsOverlays()
        {
            GraphicsOverlay drawingOverlay = new GraphicsOverlay() { 
                Id = "DrawingGraphicOverlay"
            };
            graphicsOverlays.Add(drawingOverlay);
        }

        private async Task GetAllLayers()
        {

            SetDefaultImageLayer($"{_info.Name}{_info.IdProject}");

            visibleLayerNames.Add($"{_info.Name}{_info.IdProject}");

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
                var nameTable = reader.GetString(0);
                Layers.Add(new Layer() { 
                    Id = nameTable+_info.IdProject,
                    Name = nameTable
                });
                visibleLayerNames.Add(nameTable);
            }

            await reader.CloseAsync();

            for (int i=1; i < Layers.Count; i++) 
            {
                var layer = Layers[i];

                await GetAllFromLayer(layer);
            }

            await DbManager.CloseConnection();

            TextBoxForCurrentVisibleLayers.Text = string.Join(", ", visibleLayerNames.ToArray());
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


                        imageMap.Save(@"local/" + _info.Name + "" + _info.IdProject + ".tif", System.Drawing.Imaging.ImageFormat.Tiff);
                    }

                }
            }
            catch { }

            await reader.CloseAsync();

            await Task.Run(() =>
            {
                var path = @"local/" + _info.Name + "" + _info.IdProject + ".tif";
                var image = new Raster(path);

                rasterLayer = new RasterLayer(image);

            });

            

            await DbManager.CloseConnection();
        }

        private async Task GetAllFromLayer(Layer layer)
        {
            string nameOfLayer = layer.Name;

            string command = $"select * from {nameOfLayer};";

            var reader = await DbManager.ExecuteCommand(command);

            List<string> header = new List<string>();
            List<Type> typesTable = new List<Type>();
            List<object> data = new List<object>();
            GraphicsOverlay graphicsOverlay = new GraphicsOverlay() { Id = nameOfLayer };

            int countColumns = reader.FieldCount;

            for (int i = 0; i < countColumns; i++)
            {
                string _head = reader.GetName(i);
                Type type = reader.GetFieldType(i);

                header.Add(_head);
                typesTable.Add(type);
            }

            int row = 0;
            while (await reader.ReadAsync())
            {
                for (int i = 0; i < countColumns; i++)
                {
                    data.Add(reader.GetValue(i));
                }

                if (data[(row+1)*countColumns - 1] == null)
                    continue;

                string JsonGeometrySymbol = (string)data[(row + 1) * countColumns - 1];

                try
                {
                    string[] para = JsonGeometrySymbol.Split(Splitter);

                    Esri.ArcGISRuntime.Geometry.Geometry geometry = Esri.ArcGISRuntime.Geometry.Geometry.FromJson(para[0]);

                    Symbol symbol = Symbol.FromJson(para[1]);

                    Dictionary<string, object?> attribures = new Dictionary<string, object?>();

                    for (int column = 0; column < countColumns-1; column++)
                    {
                        object? attribute = data[row * countColumns + column];

                        if (attribute is System.DBNull)
                            attribute = null;

                        attribures[header[column]] = attribute;
                    }

                    graphicsOverlay.Graphics.Add(new Graphic(geometry, attribures, symbol));
                } catch { }

                row++;
            }

            int index = (row - 1) * countColumns;

            layer.LastRowId = index < 0 ? -1 : (int)data[index];
            layer.Header = header;
            layer.TypesHeader = typesTable;
            layer.Data = data;
            layer.GraphicsOverlay = graphicsOverlay;

            graphicsOverlays.Add(graphicsOverlay);

            await reader.CloseAsync();

        }

        private void SetDefaultImageLayer(string name)
        {
            
            Layers.Clear();

            Layers.Add(
                new Layer()
                {
                    Id = $"basemap{_info.IdProject}",
                    Name = name
                }
            );

        }

        private void VisibilityLayerButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button.Uid == $"basemap{_info.IdProject}")
            {
                rasterLayer.IsVisible = !rasterLayer.IsVisible;
            }
            else
            {
                Layer layer = Layers.First((el) => el.Id == button.Uid);

                if (layer.GraphicsOverlay.IsVisible)
                {
                    HideGraphicOverlay(layer);
                }
                else
                {
                    ShowGraphicOverlay(layer);
                }
            }

            string row = string.Join(", ", visibleLayerNames);
            TextBoxForCurrentVisibleLayers.Text = row;
        }

        private void EditLayerButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            

            Layer? editLayer = Layers.FirstOrDefault((layer) => layer.Id == button.Uid);

            if (editLayer is null)
                return;

            if (CurrentEditLayer == editLayer)
            {
                CurrentEditLayerTextBlock.Text = "";
                CurrentEditLayer = null;
                return;
            }

            for (int i=1; i < Layers.Count; i++)
            {
                HideGraphicOverlay(Layers[i]);
            }
            

            CurrentEditLayer = editLayer;
            ShowGraphicOverlay(CurrentEditLayer);
            CurrentEditLayerTextBlock.Text = CurrentEditLayer.Name;

            TextBoxForCurrentVisibleLayers.Text = CurrentEditLayer.Name;
        }

        private void ShowGraphicOverlay(Layer layer)
        {
            layer.GraphicsOverlay.IsVisible = true;

            visibleLayerNames.Add(layer.Name);
        }
        private void HideGraphicOverlay(Layer layer)
        {
            layer.GraphicsOverlay.IsVisible = false;

            visibleLayerNames.Remove(layer.Name);
        }

        private async Task DrawPointsBinding(ObservableCollection<PointsMapBinding>? mapBindings)
        {


            await Task.Run(()=>
            {
                if (mapBindings is null)
                    return;

                Envelope? envelope = rasterLayer.FullExtent;

                if (envelope == null)
                    return;

                MapPoint leftTop = new MapPoint(mapBindings[0].PixelX + envelope.XMin, mapBindings[0].PixelY + envelope.YMin, spatialReference);

                MapPoint rightTop = new MapPoint(mapBindings[1].PixelX + envelope.XMin, mapBindings[1].PixelY + envelope.YMin, spatialReference);

                MapPoint rightBottom = new MapPoint(mapBindings[2].PixelX + envelope.XMin, mapBindings[2].PixelY + envelope.YMin, spatialReference);

                MapPoint leftBottom = new MapPoint(mapBindings[3].PixelX + envelope.XMin, mapBindings[3].PixelY + envelope.YMin, spatialReference);

                GraphicsOverlay graphicsMapBindingsOverlay = new GraphicsOverlay();
                graphicsMapBindingsOverlay.Id = $"PointsMapBinding{_info.IdProject}";

                Dictionary<string, object?> CornerInfo1 = new Dictionary<string, object?>();
                CornerInfo1["corner"] = 0;
                CornerInfo1["PixelX"] = pointsMapBinding[0].PixelX;
                CornerInfo1["PixelY"] = pointsMapBinding[0].PixelY;
                CornerInfo1["GeoX"] = pointsMapBinding[0].GeoX;
                CornerInfo1["GeoY"] = pointsMapBinding[0].GeoY;

                Dictionary<string, object?> CornerInfo2 = new Dictionary<string, object?>();
                CornerInfo2["corner"] = 1;
                CornerInfo2["PixelX"] = pointsMapBinding[1].PixelX;
                CornerInfo2["PixelY"] = pointsMapBinding[1].PixelY;
                CornerInfo2["GeoX"] = pointsMapBinding[1].GeoX;
                CornerInfo2["GeoY"] = pointsMapBinding[1].GeoY;

                Dictionary<string, object?> CornerInfo3 = new Dictionary<string, object?>();
                CornerInfo3["corner"] = 2;
                CornerInfo3["PixelX"] = pointsMapBinding[2].PixelX;
                CornerInfo3["PixelY"] = pointsMapBinding[2].PixelY;
                CornerInfo3["GeoX"] = pointsMapBinding[2].GeoX;
                CornerInfo3["GeoY"] = pointsMapBinding[2].GeoY;

                Dictionary<string, object?> CornerInfo4 = new Dictionary<string, object?>();
                CornerInfo4["corner"] = 3;
                CornerInfo4["PixelX"] = pointsMapBinding[3].PixelX;
                CornerInfo4["PixelY"] = pointsMapBinding[3].PixelY;
                CornerInfo4["GeoX"] = pointsMapBinding[3].GeoX;
                CornerInfo4["GeoY"] = pointsMapBinding[3].GeoY;

                var diamondSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Triangle, System.Drawing.Color.Salmon, 8);

                List<Graphic> graphics = new List<Graphic>()
                {
                    new Graphic(leftTop,CornerInfo1, diamondSymbol),
                    new Graphic(rightTop,CornerInfo2, diamondSymbol),
                    new Graphic(rightBottom,CornerInfo3, diamondSymbol),
                    new Graphic(leftBottom,CornerInfo4, diamondSymbol),
                };


                graphicsMapBindingsOverlay.Graphics.AddRange(graphics);

                Layers[0].GraphicsOverlay = graphicsMapBindingsOverlay;

                graphicsOverlays.Add(graphicsMapBindingsOverlay);

            });
        }

        private void IdentifyButton_Click(object sender, RoutedEventArgs e)
        {
            Operation = TypeOperation.Identify;
            TypeGraphic = TypeGraphic.None;
            HideInfoAttributes();
            ClearOverlay();
            SetTextAction("Identify");
        }

        private void InfoAttributesButton_Click(object sender, RoutedEventArgs e)
        {
            Operation = TypeOperation.InfoAttributes;
            TypeGraphic = TypeGraphic.None;
            ClearOverlay();

            
            NameOfRowEditotTable.Text = "";
            DataGridOfRowEditor.ItemsSource = new List<object>();
            LastGridColumn.Width = new GridLength(67, GridUnitType.Star);
            
            SetTextAction("Info Attributes");
        }

        private void InfoButton_Click(object sender, RoutedEventArgs e)
        {
            Operation = TypeOperation.Info;
            TypeGraphic = TypeGraphic.None;
            HideInfoAttributes();
            ClearOverlay();
            SetTextAction("Info");
        }

        private void ClearGraphicButton_Click(object sender, RoutedEventArgs e)
        {
            Operation = TypeOperation.Clear;
            TypeGraphic = TypeGraphic.None;
            HideInfoAttributes();
            ClearOverlay();
            SetTextAction("Clear");
        }

        private void DrawRectangleButton_Click(object sender, RoutedEventArgs e)
        {
            TypeGraphic = TypeGraphic.Rectangle;
            Operation = TypeOperation.None;

            ClearOverlay();
            numberOfAddedPoints = 0;
            HideInfoAttributes();
            SetTextAction("Draw Polygon");
        }

        private void DrawPolygon_Click(object sender, RoutedEventArgs e)
        {
            TypeGraphic = TypeGraphic.Polygon;
            Operation = TypeOperation.None;

            ClearOverlay();
            numberOfAddedPoints = 0;
            HideInfoAttributes();

            SetTextAction("Draw Polygon");
        }

        private void DrawLineButton_Click(object sender, RoutedEventArgs e)
        {
            TypeGraphic = TypeGraphic.Line;
            Operation = TypeOperation.None;

            ClearOverlay();
            numberOfAddedPoints = 0;

            HideInfoAttributes();
            SetTextAction("Draw Line");
        }

        private void DrawEllipseButton_Click(object sender, RoutedEventArgs e)
        {
            TypeGraphic = TypeGraphic.Ellipse;
            Operation = TypeOperation.None;

            ClearOverlay();
            numberOfAddedPoints = 0;

            HideInfoAttributes();
            SetTextAction("Draw Ellipse");
        }

        private void MoveGraphicButton_Click(object sender, RoutedEventArgs e)
        {
            Operation = TypeOperation.Move;
            TypeGraphic = TypeGraphic.None;
            HideInfoAttributes();
            ClearOverlay();
            SetTextAction("Move");
        }

        private void HideInfoAttributes()
        {
            SelectedLayer = null;
            selectedGraphic = null;
            LastGridColumn.Width = new GridLength(0, GridUnitType.Star);
        }

        private void OpenSQLWindow(object sender, RoutedEventArgs e)
        {
            TypeGraphic = TypeGraphic.None;
            HideInfoAttributes();
            ClearOverlay();

            Window window = new MySQLWindow(Layers);
            window.Show();
        }

        private void ClearOverlay(string name = "DrawingGraphicOverlay")
        {
            GraphicsOverlay? overlay = graphicsOverlays[name];
            overlay.Graphics.Clear();
        }

        private void SetTextAction(string action)
        {
            DetailAction.Visibility = Visibility.Visible;
            DetailAction.Text = $"Действие {action}";
        }

        private async void SaveAttributeInformationOfGraphic_Click(object sender, RoutedEventArgs e)
        {
            ItemCollection item = DataGridOfRowEditor.Items;

            if (item.Count == 0 || SelectedLayer is null)
                return;
            EditableKeyValuePair? rowId = item[0] as EditableKeyValuePair;


            string nameTable = SelectedLayer.Name;
            string condition = SelectedLayer.Header[0] + " = " + rowId.Value;
            List<string> expressions = new List<string>();

            for (int column=1; column<item.Count; column++)
            {
                EditableKeyValuePair? row = item[column] as EditableKeyValuePair;

                if (row.Value is null)
                    continue;

                string value = App.Wrap(row.Value.ToString());

                expressions.Add(row.Key + " = " + value);
            }
            string setColums = string.Join(", ", expressions.ToArray());

            string command = $"update {nameTable} set {setColums} where {condition};";

            try
            {

                await DbManager.OpenConnection();

                MySqlDataReader reader = await DbManager.ExecuteCommand(command);

                await reader.CloseAsync();

                await DbManager.CloseConnection();

                for (int column = 1; column < item.Count; column++)
                {
                    EditableKeyValuePair? row = item[column] as EditableKeyValuePair;

                    selectedGraphic.Attributes[row.Key] = row.Value;
                }

                MessageBox.Show("Ok");
            } catch
            {
                MessageBox.Show("Wrong data");
            }
        }

        private async void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentScale *= 2;

            await MapView.SetViewpointScaleAsync(CurrentScale);
        }

        private async void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentScale = Math.Max(CurrentScale / 2, 125);

            await MapView.SetViewpointScaleAsync(CurrentScale);
        }
    }
}
