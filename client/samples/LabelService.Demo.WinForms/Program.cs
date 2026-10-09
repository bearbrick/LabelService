using System.Windows.Forms;

namespace LabelService.Demo;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
#if NET
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
#endif
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
