using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace MyGIS
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Esri.ArcGISRuntime.ArcGISRuntimeEnvironment.ApiKey = "AAPK6df06588d284403c83222175745d8f5afrKcDqFmGty2gVEvotjrzUMn9RRY6M0L_CBSlVrIAgoJDvEM59uzMp_6KBKP4yDT";

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File("logs/db.txt")
                .CreateLogger();
        }

        public static string Wrap(string input)
        {
            return $"'{input}'";
        }
    }
}
