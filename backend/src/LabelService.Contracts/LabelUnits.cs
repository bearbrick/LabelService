namespace LabelService.Contracts;

/// <summary>
/// 毫米、磅和打印点之间的换算。前端（frontend/src/model/units.ts）必须用同样的公式。
/// </summary>
public static class LabelUnits
{
    /// <summary>每英寸毫米数。</summary>
    public const double MmPerInch = 25.4;

    /// <summary>每英寸磅数。</summary>
    public const double PointsPerInch = 72;

    /// <summary>
    /// 毫米换算成打印点：点数 = 毫米 ÷ 25.4 × DPI。203dpi 下 1mm ≈ 8 点，300dpi 下 1mm ≈ 11.8 点。
    /// </summary>
    /// <param name="mm">毫米。</param>
    /// <param name="dpi">分辨率。</param>
    /// <returns>打印点数，不取整。</returns>
    public static double MmToDots(double mm, int dpi) => mm / MmPerInch * dpi;

    /// <summary>
    /// 磅换算成毫米：1pt = 1/72 英寸 ≈ 0.353mm。
    /// </summary>
    /// <param name="points">磅。</param>
    /// <returns>毫米。</returns>
    public static double PointsToMm(double points) => points * MmPerInch / PointsPerInch;

    /// <summary>
    /// 磅换算成打印点。
    /// </summary>
    /// <param name="points">磅。</param>
    /// <param name="dpi">分辨率。</param>
    /// <returns>打印点数，不取整。</returns>
    public static double PointsToDots(double points, int dpi) => points / PointsPerInch * dpi;
}
