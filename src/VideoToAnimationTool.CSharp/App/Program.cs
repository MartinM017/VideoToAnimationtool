using System;
using System.Windows;

namespace VideoToAnimationTool.App
{
    public static class Program
    {
        [STAThread]
        public static int Main()
        {
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            var window = new MainWindow();
            app.Run(window);
            return 0;
        }
    }
}
