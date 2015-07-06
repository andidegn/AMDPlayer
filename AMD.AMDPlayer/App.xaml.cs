using Microsoft.Shell;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace AMD.AMDPlayer {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, ISingleInstanceApp {

        public const Boolean DEBUG = true;


        private const string Unique = "My_Unique_Application_String";
        [STAThread]
        public static void Main(String[] args) {
            if (Keyboard.IsKeyDown(Key.LeftShift) || SingleInstance<App>.InitializeAsFirstInstance(Unique) || args.Length < 1) {
                var application = new App();

                application.InitializeComponent();
                application.Run();

                // Allow single instance code to perform cleanup operations
                SingleInstance<App>.Cleanup();
            }
        }

        [STAThread]
        protected override void OnStartup(StartupEventArgs e) {
            if (e.Args != null && e.Args.Count() > 0) {
                this.Properties["FileName"] = e.Args[0];
            }
            base.OnStartup(e);
        }

        public static event EventHandler ExternelApplicationLauncy;
        bool ISingleInstanceApp.SignalExternalCommandLineArgs(IList<string> args) {
            if (args != null && args.Count() > 1 && ExternelApplicationLauncy != null) {
                this.Properties["FileName"] = args[1]; // args[0] contains the path of the application
                ExternelApplicationLauncy(this, new EventArgs());
            }
            return true;
        }
        
        private void Application_Exit(object sender, ExitEventArgs e) {
            AMD.AMDPlayer.Properties.Settings.Default.Save();
        }
    }
}
