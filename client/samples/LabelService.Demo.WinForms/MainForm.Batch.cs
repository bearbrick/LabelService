using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using LabelService.Client;

namespace LabelService.Demo;

/// <summary>
/// 主窗口的批量数据：一行一张标签，列是模板的全部字段。
/// 每条标签的数据都由调用方给全（演示程序在这里扮演调用方），SDK 原样发给服务端；不用公共字段，也不在客户端推算递增的值。
/// </summary>
internal sealed partial class MainForm
{
    /// <summary>
    /// 读取字段后批量数据默认放几行（复制单张数据的值）。
    /// </summary>
    private const int DefaultBatchRows = 3;

    /// <summary>
    /// 批量数据的列是按哪个模板建的；换了模板时清掉旧行重新放默认行。
    /// </summary>
    private string batchTemplate = "";

    /// <summary>
    /// 正在成批改表格（重建列、粘贴、加删很多行），这期间不逐行刷新按钮文字和调用代码，改完统一刷新一次。
    /// </summary>
    private bool batchUpdating;

    /// <summary>
    /// 绑定批量数据的按钮和表格事件。
    /// </summary>
    private void WireBatchEvents()
    {
        addRow.Click += (_, _) => AddBatchRow();
        removeRows.Click += (_, _) => RemoveBatchRows();
        pasteRows.Click += (_, _) => PasteBatchText(Clipboard.GetText());
        clearRows.Click += (_, _) =>
        {
            batchRows.Rows.Clear();
            BatchChanged();
        };
        batchRows.KeyDown += (_, e) =>
        {
            // 表格自己的 Ctrl+V 只能粘贴到一个格子里：换成按行列粘贴 Excel 复制的多行
            if (e.Control && e.KeyCode == Keys.V && !batchRows.IsCurrentCellInEditMode)
            {
                PasteBatchText(Clipboard.GetText());
                e.Handled = true;
            }
        };
        batchRows.RowsAdded += (_, _) => BatchChanged();
        batchRows.RowsRemoved += (_, _) => BatchChanged();
        batchRows.CellValueChanged += (_, _) => BatchChanged();
        batchRows.RowPostPaint += (_, e) =>
        {
            // 行头写序号，和服务端错误明细里的“第几条”对得上
            var bounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, batchRows.RowHeadersWidth - Px(4), e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, (e.RowIndex + 1).ToString(CultureInfo.InvariantCulture), batchRows.Font, bounds, SystemColors.GrayText, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        };
    }

    /// <summary>
    /// 读取字段后按字段重建批量数据的列。还是同一个模板时按字段 key 保留已有的行；换了模板或还没有行时，复制单张数据放 3 行。
    /// </summary>
    /// <param name="fieldList">模板的字段定义。</param>
    /// <param name="code">模板编码。</param>
    private void RebuildBatchColumns(IEnumerable<TemplateField> fieldList, string code)
    {
        var keep = string.Equals(batchTemplate, code, StringComparison.OrdinalIgnoreCase)
            ? batchRows.Rows.Cast<DataGridViewRow>().Select(RowValues).ToList()
            : new List<Dictionary<string, string>>();
        batchUpdating = true;
        batchRows.Rows.Clear();
        batchRows.Columns.Clear();
        foreach (var field in fieldList)
        {
            batchRows.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = field.Key,
                HeaderText = field.Required ? field.Key + " *" : field.Key,
                ToolTipText = $"{field.Name ?? field.Key} · {field.Type}{(field.Required ? " · 必填" : "")}",
                Tag = field,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                MinimumWidth = Px(60),
            });
        }

        if (keep.Count == 0)
        {
            var single = SingleValues();
            keep.AddRange(Enumerable.Repeat(single, DefaultBatchRows));
        }

        foreach (var values in keep)
        {
            AddRowValues(values);
        }

        batchTemplate = code;
        batchUpdating = false;
        BatchChanged();
    }

    /// <summary>
    /// 加一行：复制最后一行（没有行时复制单张数据），并滚到新行。
    /// </summary>
    private void AddBatchRow()
    {
        if (batchRows.Columns.Count == 0)
        {
            status.Text = "先读取字段，批量数据的列是模板的字段";
            return;
        }

        batchUpdating = true;
        AddRowValues(batchRows.Rows.Count > 0 ? RowValues(batchRows.Rows[batchRows.Rows.Count - 1]) : SingleValues());
        batchUpdating = false;
        BatchChanged();
        batchRows.CurrentCell = batchRows.Rows[batchRows.Rows.Count - 1].Cells[0];
    }

    /// <summary>
    /// 删除选中的格子或行所在的行。
    /// </summary>
    private void RemoveBatchRows()
    {
        var indexes = batchRows.SelectedCells.Cast<DataGridViewCell>().Select(cell => cell.RowIndex)
            .Concat(batchRows.SelectedRows.Cast<DataGridViewRow>().Select(row => row.Index))
            .Where(index => index >= 0).Distinct().OrderByDescending(index => index).ToList();
        batchUpdating = true;
        foreach (var index in indexes)
        {
            batchRows.Rows.RemoveAt(index);
        }

        batchUpdating = false;
        BatchChanged();
    }

    /// <summary>
    /// 粘贴 Excel 复制的多行（一行一张、制表符分隔）。第一行的格子都是字段 key 或名称时当作表头：替换现有数据，按表头对列；
    /// 否则从当前格子开始往右往下填，行不够时加行，超出最后一列的内容不要。
    /// </summary>
    /// <param name="text">剪贴板里的文字。</param>
    private void PasteBatchText(string text)
    {
        if (batchRows.Columns.Count == 0)
        {
            status.Text = "先读取字段，再粘贴批量数据";
            return;
        }

        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        while (lines.Count > 0 && lines[lines.Count - 1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var cells = lines.Select(line => line.Split('\t')).ToList();
        if (cells.Count == 0)
        {
            status.Text = "剪贴板里没有文字";
            return;
        }

        batchRows.EndEdit();
        var header = cells[0].Select(name => FindBatchColumn(name.Trim())).ToArray();
        var hasHeader = header.Any(index => index >= 0) && cells[0].Select((name, i) => name.Trim().Length == 0 || header[i] >= 0).All(ok => ok);
        int[] columns;
        int startRow;
        batchUpdating = true;
        if (hasHeader)
        {
            columns = header;
            cells.RemoveAt(0);
            startRow = 0;
            batchRows.Rows.Clear();
        }
        else
        {
            var start = batchRows.CurrentCell?.ColumnIndex ?? 0;
            columns = Enumerable.Range(start, cells.Max(row => row.Length)).Select(index => index < batchRows.Columns.Count ? index : -1).ToArray();
            startRow = batchRows.CurrentCell?.RowIndex ?? batchRows.Rows.Count;
        }

        for (var i = 0; i < cells.Count; i++)
        {
            var rowIndex = startRow + i;
            while (batchRows.Rows.Count <= rowIndex)
            {
                batchRows.Rows.Add();
            }

            for (var j = 0; j < cells[i].Length && j < columns.Length; j++)
            {
                if (columns[j] >= 0)
                {
                    batchRows.Rows[rowIndex].Cells[columns[j]].Value = cells[i][j].Trim();
                }
            }
        }

        batchUpdating = false;
        BatchChanged();
        status.Text = $"已粘贴 {cells.Count} 行{(hasHeader ? "（按表头对列，替换了原来的数据）" : "")}";
    }

    /// <summary>
    /// 按字段 key 或中文名称找批量数据的列。
    /// </summary>
    /// <param name="name">表头文字。</param>
    /// <returns>列序号；找不到时为 -1。</returns>
    private int FindBatchColumn(string name)
    {
        foreach (DataGridViewColumn column in batchRows.Columns)
        {
            if (string.Equals(column.Name, name, StringComparison.OrdinalIgnoreCase)
                || (column.Tag is TemplateField field && field.Name is { Length: > 0 } fieldName && fieldName == name))
            {
                return column.Index;
            }
        }

        return -1;
    }

    /// <summary>
    /// 批量数据每一行填了值的字段，一行是一条。
    /// </summary>
    /// <param name="endEdit">先结束正在编辑的格子（生成时要；刷新调用代码时不要，避免打断输入）。</param>
    /// <returns>每条的字段 key、类型和去掉首尾空白的值。</returns>
    private List<List<(string Key, string Type, string Value)>> BatchValues(bool endEdit = true)
    {
        if (endEdit)
        {
            batchRows.EndEdit();
        }

        return batchRows.Rows.Cast<DataGridViewRow>()
            .Select(row => batchRows.Columns.Cast<DataGridViewColumn>()
                .Select(column => (column.Name, column.Tag is TemplateField field ? field.Type : "string", (row.Cells[column.Index].Value as string ?? "").Trim()))
                .Where(value => value.Item3.Length > 0)
                .ToList())
            .ToList();
    }

    /// <summary>
    /// 行数或内容变了：必填为空的格子标红，按钮和页签写上行数，刷新调用代码。成批修改时等改完再刷新。
    /// </summary>
    private void BatchChanged()
    {
        if (batchUpdating)
        {
            return;
        }

        foreach (DataGridViewColumn column in batchRows.Columns)
        {
            var required = column.Tag is TemplateField { Required: true };
            foreach (DataGridViewRow row in batchRows.Rows)
            {
                var cell = row.Cells[column.Index];
                cell.Style.BackColor = required && string.IsNullOrWhiteSpace(cell.Value as string) ? Color.FromArgb(0xFD, 0xF1, 0xF2) : SystemColors.Window;
            }
        }

        renderBatch.Text = $"批量生成 {batchRows.Rows.Count} 条";
        batchTab.Text = $"批量数据 ({batchRows.Rows.Count})";
        UpdateCode();
    }

    /// <summary>
    /// 批量数据加一行。
    /// </summary>
    /// <param name="values">字段 key → 值；没有的字段留空。</param>
    private void AddRowValues(IReadOnlyDictionary<string, string> values)
    {
        var index = batchRows.Rows.Add();
        foreach (DataGridViewColumn column in batchRows.Columns)
        {
            batchRows.Rows[index].Cells[column.Index].Value = values.TryGetValue(column.Name, out var value) ? value : "";
        }
    }

    /// <summary>
    /// 批量数据一行的值。
    /// </summary>
    /// <param name="row">行。</param>
    /// <returns>字段 key → 值。</returns>
    private Dictionary<string, string> RowValues(DataGridViewRow row) =>
        batchRows.Columns.Cast<DataGridViewColumn>().ToDictionary(column => column.Name, column => row.Cells[column.Index].Value as string ?? "");

    /// <summary>
    /// 单张数据表格里的值（包括空的）。
    /// </summary>
    /// <returns>字段 key → 值。</returns>
    private Dictionary<string, string> SingleValues() =>
        fields.Rows.Cast<DataGridViewRow>().ToDictionary(row => Cell(row, "key"), row => Cell(row, "value"));

    /// <summary>
    /// 把批量数据改成 <paramref name="count"/> 行：多了删掉末尾的，少了复制最后一行（自检用）。
    /// </summary>
    /// <param name="count">行数。</param>
    private void SetBatchRowCount(int count)
    {
        batchUpdating = true;
        while (batchRows.Rows.Count > count)
        {
            batchRows.Rows.RemoveAt(batchRows.Rows.Count - 1);
        }

        while (batchRows.Rows.Count < count)
        {
            AddRowValues(batchRows.Rows.Count > 0 ? RowValues(batchRows.Rows[batchRows.Rows.Count - 1]) : SingleValues());
        }

        batchUpdating = false;
        BatchChanged();
    }
}
