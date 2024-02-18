using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Rasters;
using Esri.ArcGISRuntime.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MyGIS
{
    class MapVM : DependencyObject, INotifyPropertyChanged
    {
        public static readonly DependencyProperty MapProperty;
        public static readonly DependencyProperty GraphicsOverlaysProperty;

        static MapVM()
        {
            MapProperty = DependencyProperty.Register("Map", typeof(Map), typeof(MapVM));
            GraphicsOverlaysProperty = DependencyProperty.Register("GraphicsOverlays", typeof(GraphicsOverlayCollection), typeof(MapVM));
        }
        public Map? Map
        {
            get { return (Map)GetValue(MapProperty); }
            set
            {
                SetValue(MapProperty, value);
                OnPropertyChanged();
            }
        }

        public GraphicsOverlayCollection? GraphicsOverlays
        {
            get { return (GraphicsOverlayCollection)GetValue(GraphicsOverlaysProperty); }
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

        public MapVM(InfoProject infoProject) 
        {
            var path = $"temp/{infoProject.Name}{infoProject.IdProject}.tif";
            var image = new Raster(path);

            RasterLayer raster = new RasterLayer(image);

            Map = new Map(new Basemap(raster));

        }
    }
}
