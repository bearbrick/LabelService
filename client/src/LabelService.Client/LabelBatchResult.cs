namespace LabelService.Client;

/// <summary>
/// 批量集合的单位，区分业务条目和拼版纸页。
/// </summary>
public enum LabelBatchUnit
{
    /// <summary>
    /// 每项对应一条业务数据，包含该条目的全部份数。
    /// </summary>
    Item,

    /// <summary>
    /// 每项对应一页拼版纸张，不对应纸上的某一个格子。
    /// </summary>
    Page,
}

/// <summary>
/// 一次生成的可打印集合，保留原始响应，补打不重新调用服务。
/// </summary>
public sealed class LabelBatchResult
{
    /// <summary>
    /// 由 SDK 创建集合。
    /// </summary>
    /// <param name="originalFile">原始服务响应，包含整批计时和追踪信息。</param>
    /// <param name="unit">集合单位。</param>
    /// <param name="items">按服务输出顺序组织的项。</param>
    internal LabelBatchResult(LabelFile originalFile, LabelBatchUnit unit, IReadOnlyList<LabelBatchItem> items)
    {
        OriginalFile = originalFile;
        Unit = unit;
        Items = items;
    }

    /// <summary>
    /// 原始整批文件，可另存留底，保留完整响应头和服务端计时。
    /// </summary>
    public LabelFile OriginalFile
    {
        get;
    }

    /// <summary>
    /// 集合按业务条目还是拼版纸页组织。
    /// </summary>
    public LabelBatchUnit Unit
    {
        get;
    }

    /// <summary>
    /// 只读的可打印项，下标从零开始，可以传整批、单项或选中的子集给 Windows 打印模块。
    /// </summary>
    public IReadOnlyList<LabelBatchItem> Items
    {
        get;
    }
}

/// <summary>
/// 一条业务标签及其份数，或一页拼版纸张；打印此对象会保留其页选择语义。
/// </summary>
public sealed class LabelBatchItem
{
    /// <summary>
    /// 由 SDK 创建可打印项。
    /// </summary>
    /// <param name="files">该项的原始文件，PNG 多份时每份一个文件。</param>
    /// <param name="itemNumber">普通业务条目序号，从一开始；拼版时为空。</param>
    /// <param name="pageNumber">拼版纸页序号，从一开始；普通条目时为空。</param>
    /// <param name="copies">该条目的份数；一页拼版纸张为一份。</param>
    /// <param name="pdfPageIndex">拼版 PDF 选择的零起点页号；其他情况为空。</param>
    internal LabelBatchItem(IReadOnlyList<LabelFile> files, int? itemNumber, int? pageNumber, int copies, int? pdfPageIndex)
    {
        Files = files;
        ItemNumber = itemNumber;
        PageNumber = pageNumber;
        Copies = copies;
        PdfPageIndex = pdfPageIndex;
    }

    /// <summary>
    /// 原始内容文件。PDF 拼版项引用完整原 PDF，选页保存在本项中，打印时应传本项而非直接打印此文件。
    /// </summary>
    public IReadOnlyList<LabelFile> Files
    {
        get;
    }

    /// <summary>
    /// 业务条目序号，从一开始；拼版纸页项为空。
    /// </summary>
    public int? ItemNumber
    {
        get;
    }

    /// <summary>
    /// 拼版纸页序号，从一开始；普通条目为空。
    /// </summary>
    public int? PageNumber
    {
        get;
    }

    /// <summary>
    /// 服务已经生成的条目份数，不是额外打印倍率；选一项会提交它原有的全部份数。
    /// </summary>
    public int Copies
    {
        get;
    }

    /// <summary>
    /// 拼版 PDF 在原文档中的零起点页号，PNG 拼版页已经是独立文件，无需页选择。
    /// </summary>
    public int? PdfPageIndex
    {
        get;
    }
}
