using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.Symbology;

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.Rasters;
using System.Windows.Input;
using System.Windows;
using Esri.ArcGISRuntime.ArcGISServices;
using System.Collections.ObjectModel;
using System.Drawing;

namespace MyGIS
{
    internal class MapViewModel : DependencyObject, INotifyPropertyChanged
    {
        public static readonly DependencyProperty MapProperty;
        public static readonly DependencyProperty GraphicsOverlaysProperty;

        static MapViewModel()
        {
            MapProperty = DependencyProperty.Register("Map", typeof(Map), typeof(MapViewModel));
            GraphicsOverlaysProperty = DependencyProperty.Register("GraphicsOverlays", typeof(GraphicsOverlayCollection), typeof(MapViewModel));
        }

        string path;
        InfoProject info;
        ObservableCollection<PointsMapBinding>? pointsMaps;

        public MapPoint CenterMap { get; set; } = new MapPoint(1500, 1500, SpatialReferences.Wgs84);

        public MapViewModel()
        {

        }

        public MapViewModel(InfoProject infoProject, ObservableCollection<PointsMapBinding>? pointsMaps)
        {
            path = $"temp/{infoProject.Name}{infoProject.IdProject}.tif";

            info = infoProject;

            this.pointsMaps = pointsMaps;

        }

        public RasterLayer RasterLayer { get; set; }

        public async Task AddImageLayer()
        {
            var image = new Raster(path);

            RasterLayer = new RasterLayer(image);

            await RasterLayer.LoadAsync();

            await SetupMap(RasterLayer);

            await CreateGraphics();
        }

        public void SetPointsBindings(ObservableCollection<PointsMapBinding>? pointsMaps)
        {
            SpatialReference spatialReference = RasterLayer.FullExtent.SpatialReference;
            Envelope fullExtent = RasterLayer.FullExtent;

            // Ваши координаты (например, центр экстента)
            double centerX = (fullExtent.XMin + fullExtent.XMax) / 2.0;
            double centerY = (fullExtent.YMin + fullExtent.YMax) / 2.0;

            // Привязка координат
            CenterMap = new MapPoint(centerX, centerY, spatialReference);
        }

        //(49, 56) (78.5760942765971, 64.015213867277) Label "левый вверх",
        //  (49.5, 56) (1920.35387205418, 69.5963586484211) Label "правый вверх",
        //  (49, 55.66) (65.3467140546254, 2252.75415887265) Label "левый низ",
        //  (49.5, 55.66) (1924.07463524161, 2262.67619403913) Label "правый низ"


        private async Task SetupMap(RasterLayer raster)
        {
            await Task.Run(() =>
            {
                this.Map = new Map(new Basemap(raster));
            });
            
        }

        private async Task CreateGraphics()
        {
            await Task.Run(() => {

                GraphicsOverlayCollection overlays = new GraphicsOverlayCollection();

                this.GraphicsOverlays = overlays;


            });
        }

        public Map? Map
        {
            get { return (Map) GetValue(MapProperty); }
            set
            {
                SetValue(MapProperty, value);
                OnPropertyChanged();
            }
        }
        
        public GraphicsOverlayCollection? GraphicsOverlays
        {
            get { return (GraphicsOverlayCollection) GetValue(GraphicsOverlaysProperty); }
            set
            {
                SetValue(GraphicsOverlaysProperty, value);
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}
