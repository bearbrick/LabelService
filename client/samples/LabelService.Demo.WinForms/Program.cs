using System.Windows.Forms;

namespace LabelService.Demo;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
#if NET
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
#endif
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var form = new MainForm();
        if (args.Length is 2 or 3 && args[0] == "--smoke")
        {
            // 自检时不弹“未处理的异常”对话框（会一直等人点），直接记下来退出
            Application.ThreadException += (_, e) =>
            {
                System.IO.File.AppendAllText(System.IO.Path.Combine(args[1], "smoke.txt"), "自检出错：" + e.Exception + Environment.NewLine);
                Environment.Exit(3);
            };
            form.RunSmokeOnShown(args[1], args.Length == 3 ? args[2] : null);
        }

        Application.Run(form);
    }
}
