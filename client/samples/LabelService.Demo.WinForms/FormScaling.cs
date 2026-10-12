using System.Drawing;
using System.Windows.Forms;

namespace LabelService.Demo;

/// <summary>
/// 演示程序的窗口按屏幕缩放（150% 的屏幕上控件放大 1.5 倍）：控件尺寸都按 100% 缩放时的像素写，摆控件前后各调用一次。
/// </summary>
internal static class FormScaling
{
    /// <summary>
    /// 按钮最窄多少像素（100% 缩放时），和 WinForms 按钮的默认宽度一样。
    /// </summary>
    private const int MinimumButtonWidth = 75;

    /// <summary>
    /// 开始摆控件前调用：暂停布局，按屏幕 DPI 自动缩放。
    /// </summary>
    /// <param name="form">窗口。</param>
    public static void Begin(Form form)
    {
        form.SuspendLayout();
        form.AutoScaleDimensions = new SizeF(96F, 96F);
        form.AutoScaleMode = AutoScaleMode.Dpi;
    }

    /// <summary>
    /// 摆完控件后调用：按钮按文字定大小，再恢复布局（这时按屏幕缩放）。
    /// </summary>
    /// <param name="form">窗口。</param>
    /// <remarks>
    /// 自动大小的按钮默认只会变大不会变小，而新建按钮时的默认高度已经按屏幕放大过，窗口缩放时再放大一次就太高了（150% 时 51 像素）。
    /// 改成按文字定大小，最窄 75 像素（也跟着缩放）。
    /// </remarks>
    public static void End(Form form)
    {
        foreach (var button in Descendants(form).OfType<Button>().Where(button => button.AutoSize))
        {
            button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.MinimumSize = new Size(MinimumButtonWidth, 0);
        }

        form.ResumeLayout(false);
        form.PerformLayout();
    }

    /// <summary>
    /// 控件的全部后代，深度优先。
    /// </summary>
    /// <param name="control">起始控件。</param>
    /// <returns>后代控件（不含起始控件）。</returns>
    private static IEnumerable<Control> Descendants(Control control) =>
        control.Controls.Cast<Control>().SelectMany(child => new[] { child }.Concat(Descendants(child)));
}
