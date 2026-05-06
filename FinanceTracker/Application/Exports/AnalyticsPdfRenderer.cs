using Application.Analytics;
using Domain.Common;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;

namespace Application.Exports;

internal sealed class AnalyticsPdfRenderer
{
    private const int PageWidth = 1240;
    private const int PageHeight = 1754;
    private static readonly CultureInfo RuCulture = new("ru-RU");

    public byte[] Render(AnalyticsPdfReport report)
    {
        var pages = new List<byte[]>
        {
            RenderOverviewPage(report),
            RenderCalendarAndHistoryPage(report)
        };

        return ImagePdfBuilder.BuildFromJpegPages(pages, PageWidth, PageHeight);
    }

    private byte[] RenderOverviewPage(AnalyticsPdfReport report)
    {
        using var bitmap = CreatePageBitmap();
        using var graphics = CreateGraphics(bitmap);
        DrawPageBackground(graphics);

        DrawTitle(graphics, "Отчет по аналитике", $"{report.From:dd.MM.yyyy} - {report.To:dd.MM.yyyy}");
        DrawSummaryCards(graphics, report);
        DrawDonutChart(graphics, report, new Rectangle(70, 350, 500, 430));
        DrawBalanceChart(graphics, report.BalanceHistory, new Rectangle(620, 350, 550, 430));
        DrawCashFlow(graphics, report.CashFlow, new Rectangle(70, 850, 1100, 320));
        DrawRecurringSummary(graphics, report.Recurring, new Rectangle(70, 1210, 1100, 250));

        return ToJpeg(bitmap);
    }

    private byte[] RenderCalendarAndHistoryPage(AnalyticsPdfReport report)
    {
        using var bitmap = CreatePageBitmap();
        using var graphics = CreateGraphics(bitmap);
        DrawPageBackground(graphics);

        DrawTitle(graphics, "Календарь и история операций", $"Месяц: {report.CalendarMonth:MMMM yyyy}");
        DrawCalendar(graphics, report, new Rectangle(60, 140, 1120, 860));
        DrawHistory(graphics, report.HistoryItems, new Rectangle(60, 1035, 1120, 620));

        return ToJpeg(bitmap);
    }

    private static Bitmap CreatePageBitmap()
    {
        return new Bitmap(PageWidth, PageHeight);
    }

    private static Graphics CreateGraphics(Bitmap bitmap)
    {
        var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        return graphics;
    }

    private static void DrawPageBackground(Graphics graphics)
    {
        graphics.Clear(Color.FromArgb(248, 244, 236));
    }

    private static void DrawTitle(Graphics graphics, string title, string subtitle)
    {
        using var titleFont = new Font("Arial", 28, FontStyle.Bold);
        using var subtitleFont = new Font("Arial", 13, FontStyle.Regular);
        using var titleBrush = new SolidBrush(Color.FromArgb(20, 35, 45));
        using var subtitleBrush = new SolidBrush(Color.FromArgb(96, 96, 96));

        graphics.DrawString(title, titleFont, titleBrush, new PointF(60, 42));
        graphics.DrawString(subtitle, subtitleFont, subtitleBrush, new PointF(64, 88));
    }

    private static void DrawSummaryCards(Graphics graphics, AnalyticsPdfReport report)
    {
        var cards = new[]
        {
            ("Общий баланс", FormatMoney(report.Dashboard.TotalBalance, report.Dashboard.CurrencyCode)),
            ("Доходы", FormatMoney(report.Dashboard.TotalIncome, report.Dashboard.CurrencyCode)),
            ("Расходы", FormatMoney(report.Dashboard.TotalExpense, report.Dashboard.CurrencyCode)),
            ("Чистый поток", FormatMoney(report.Dashboard.Net, report.Dashboard.CurrencyCode))
        };

        for (var index = 0; index < cards.Length; index++)
        {
            var x = 60 + index * 290;
            var rect = new Rectangle(x, 140, 260, 150);
            DrawCard(graphics, rect, cards[index].Item1, cards[index].Item2, index == 3 ? $"Транзакций: {report.Dashboard.TransactionsCount}" : null);
        }
    }

    private static void DrawCard(Graphics graphics, Rectangle rect, string title, string value, string? footer)
    {
        using var cardBrush = new SolidBrush(Color.White);
        using var borderPen = new Pen(Color.FromArgb(220, 224, 230));
        using var titleFont = new Font("Arial", 12, FontStyle.Regular);
        using var valueFont = new Font("Arial", 20, FontStyle.Bold);
        using var footerFont = new Font("Arial", 11, FontStyle.Regular);
        using var titleBrush = new SolidBrush(Color.FromArgb(88, 96, 104));
        using var valueBrush = new SolidBrush(Color.FromArgb(28, 42, 62));

        graphics.FillRoundedRectangle(cardBrush, rect, 18);
        graphics.DrawRoundedRectangle(borderPen, rect, 18);
        graphics.DrawString(title, titleFont, titleBrush, rect.Left + 20, rect.Top + 18);
        graphics.DrawString(value, valueFont, valueBrush, new RectangleF(rect.Left + 20, rect.Top + 50, rect.Width - 40, 58));

        if (!string.IsNullOrWhiteSpace(footer))
        {
            graphics.DrawString(footer, footerFont, titleBrush, rect.Left + 20, rect.Bottom - 36);
        }
    }

    private static void DrawDonutChart(Graphics graphics, AnalyticsPdfReport report, Rectangle area)
    {
        DrawSectionCard(graphics, area, "Расходы по категориям");

        var categories = report.Categories.ToList();
        var total = categories.Sum(item => item.Amount);
        if (total <= 0)
        {
            DrawPlaceholder(graphics, area, "Нет данных по расходам.");
            return;
        }

        var chartRect = new Rectangle(area.Left + 30, area.Top + 80, 280, 280);
        var colors = new[]
        {
            Color.FromArgb(50, 101, 134),
            Color.FromArgb(19, 174, 135),
            Color.FromArgb(67, 59, 255),
            Color.FromArgb(255, 138, 61),
            Color.FromArgb(217, 72, 95),
            Color.FromArgb(0, 167, 196)
        };

        float startAngle = -90f;
        for (var index = 0; index < categories.Count && index < colors.Length; index++)
        {
            var item = categories[index];
            var sweep = (float)(item.Amount / total * 360m);
            using var brush = new SolidBrush(colors[index]);
            graphics.FillPie(brush, chartRect, startAngle, sweep);
            startAngle += sweep;
        }

        using var innerBrush = new SolidBrush(Color.White);
        graphics.FillEllipse(innerBrush, new Rectangle(chartRect.Left + 55, chartRect.Top + 55, 170, 170));

        using var centerTitleFont = new Font("Arial", 12, FontStyle.Regular);
        using var centerValueFont = new Font("Arial", 16, FontStyle.Bold);
        using var centerBrush = new SolidBrush(Color.FromArgb(32, 44, 60));
        graphics.DrawString("Всего", centerTitleFont, centerBrush, new RectangleF(chartRect.Left + 96, chartRect.Top + 108, 100, 24), CenterStringFormat());
        graphics.DrawString(FormatMoney(total, report.Dashboard.CurrencyCode), centerValueFont, centerBrush, new RectangleF(chartRect.Left + 70, chartRect.Top + 138, 150, 44), CenterStringFormat());

        using var legendTitleFont = new Font("Arial", 12, FontStyle.Bold);
        using var legendFont = new Font("Arial", 11, FontStyle.Regular);
        using var legendBrush = new SolidBrush(Color.FromArgb(36, 46, 56));

        graphics.DrawString("Категории", legendTitleFont, legendBrush, area.Left + 360, area.Top + 86);
        for (var index = 0; index < categories.Count && index < 8; index++)
        {
            var item = categories[index];
            var y = area.Top + 124 + index * 34;
            using var swatchBrush = new SolidBrush(colors[index % colors.Length]);
            graphics.FillEllipse(swatchBrush, area.Left + 362, y + 6, 12, 12);
            graphics.DrawString(item.CategoryName, legendFont, legendBrush, area.Left + 384, y);
            graphics.DrawString(
                $"{FormatMoney(item.Amount, item.CurrencyCode)} · {item.TransactionsCount} оп.",
                legendFont,
                legendBrush,
                area.Left + 700,
                y,
                RightStringFormat());
        }
    }

    private static void DrawBalanceChart(Graphics graphics, IReadOnlyCollection<BalanceHistoryPointDto> points, Rectangle area)
    {
        DrawSectionCard(graphics, area, "Динамика баланса");
        if (points.Count == 0)
        {
            DrawPlaceholder(graphics, area, "Нет данных по балансу.");
            return;
        }

        var plot = new Rectangle(area.Left + 35, area.Top + 85, area.Width - 70, area.Height - 135);
        var values = points.Select(item => item.Balance).ToList();
        var min = values.Min();
        var max = values.Max();
        if (min == max)
        {
            min -= 1;
            max += 1;
        }

        using var axisPen = new Pen(Color.FromArgb(210, 214, 220), 1);
        using var linePen = new Pen(Color.FromArgb(50, 101, 134), 4);
        using var fillBrush = new SolidBrush(Color.FromArgb(50, 101, 134, 40));
        using var labelFont = new Font("Arial", 10, FontStyle.Regular);
        using var labelBrush = new SolidBrush(Color.FromArgb(90, 98, 108));

        for (var i = 0; i < 4; i++)
        {
            var y = plot.Top + (plot.Height / 3f) * i;
            graphics.DrawLine(axisPen, plot.Left, y, plot.Right, y);
        }

        var pointList = points.ToList();
        var chartPoints = new List<PointF>(pointList.Count);
        for (var index = 0; index < pointList.Count; index++)
        {
            var x = plot.Left + (pointList.Count == 1 ? plot.Width / 2f : index * plot.Width / (pointList.Count - 1f));
            var normalized = (float)((pointList[index].Balance - min) / (max - min));
            var y = plot.Bottom - normalized * plot.Height;
            chartPoints.Add(new PointF(x, y));
        }

        if (chartPoints.Count > 1)
        {
            var polygon = new List<PointF>(chartPoints)
            {
                new(chartPoints[^1].X, plot.Bottom),
                new(chartPoints[0].X, plot.Bottom)
            };
            graphics.FillPolygon(fillBrush, polygon.ToArray());
            graphics.DrawLines(linePen, chartPoints.ToArray());
        }

        foreach (var point in chartPoints)
        {
            graphics.FillEllipse(Brushes.White, point.X - 6, point.Y - 6, 12, 12);
            graphics.DrawEllipse(linePen, point.X - 6, point.Y - 6, 12, 12);
        }

        graphics.DrawString("Старт", labelFont, labelBrush, plot.Left, plot.Bottom + 14);
        graphics.DrawString(pointList[0].Label, labelFont, labelBrush, plot.Left, plot.Bottom + 34);
        graphics.DrawString("Финиш", labelFont, labelBrush, plot.Right - 80, plot.Bottom + 14);
        graphics.DrawString(pointList[^1].Label, labelFont, labelBrush, plot.Right - 80, plot.Bottom + 34);

        graphics.DrawString(FormatMoney(pointList[0].Balance, pointList[0].CurrencyCode), labelFont, labelBrush, plot.Left, plot.Top - 28);
        graphics.DrawString(FormatMoney(pointList[^1].Balance, pointList[^1].CurrencyCode), labelFont, labelBrush, plot.Right - 140, plot.Top - 28);
    }

    private static void DrawCashFlow(Graphics graphics, IReadOnlyCollection<CashFlowPointDto> points, Rectangle area)
    {
        DrawSectionCard(graphics, area, "Потоки по периодам");
        if (points.Count == 0)
        {
            DrawPlaceholder(graphics, area, "Нет данных по движению средств.");
            return;
        }

        using var titleFont = new Font("Arial", 11, FontStyle.Regular);
        using var valueFont = new Font("Arial", 11, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.FromArgb(38, 46, 58));
        using var barBrush = new SolidBrush(Color.FromArgb(19, 174, 135));
        using var expenseBrush = new SolidBrush(Color.FromArgb(217, 72, 95));

        var maxValue = points.Max(item => Math.Max(item.Income, item.Expense));
        var top = area.Top + 70;
        var lineHeight = 44;

        foreach (var (point, index) in points.Take(6).Select((item, idx) => (item, idx)))
        {
            var y = top + index * lineHeight;
            graphics.DrawString(point.Label, titleFont, textBrush, area.Left + 28, y + 4);
            var incomeWidth = maxValue == 0 ? 0 : (int)(point.Income / maxValue * 280m);
            var expenseWidth = maxValue == 0 ? 0 : (int)(point.Expense / maxValue * 280m);
            graphics.FillRoundedRectangle(barBrush, new Rectangle(area.Left + 210, y, Math.Max(incomeWidth, 2), 14), 6);
            graphics.FillRoundedRectangle(expenseBrush, new Rectangle(area.Left + 210, y + 18, Math.Max(expenseWidth, 2), 14), 6);
            graphics.DrawString($"Доходы: {FormatMoney(point.Income, point.CurrencyCode)}", valueFont, textBrush, area.Left + 520, y - 1);
            graphics.DrawString($"Расходы: {FormatMoney(point.Expense, point.CurrencyCode)}", valueFont, textBrush, area.Left + 520, y + 17);
            graphics.DrawString($"Итог: {FormatMoney(point.Net, point.CurrencyCode)}", valueFont, textBrush, area.Left + 860, y + 8);
        }
    }

    private static void DrawRecurringSummary(Graphics graphics, RecurringPaymentsAnalyticsDto recurring, Rectangle area)
    {
        DrawSectionCard(graphics, area, "Повторяющиеся платежи");
        using var font = new Font("Arial", 12, FontStyle.Regular);
        using var boldFont = new Font("Arial", 12, FontStyle.Bold);
        using var brush = new SolidBrush(Color.FromArgb(38, 46, 58));

        graphics.DrawString($"Активных правил: {recurring.ActiveRulesCount}", boldFont, brush, area.Left + 28, area.Top + 78);
        graphics.DrawString($"Всего правил: {recurring.TotalRulesCount}", font, brush, area.Left + 28, area.Top + 112);
        graphics.DrawString($"Сгенерировано доходов: {FormatMoney(recurring.GeneratedIncome, recurring.CurrencyCode)}", font, brush, area.Left + 28, area.Top + 146);
        graphics.DrawString($"Сгенерировано расходов: {FormatMoney(recurring.GeneratedExpense, recurring.CurrencyCode)}", font, brush, area.Left + 28, area.Top + 180);

        var rightX = area.Left + 540;
        foreach (var (item, index) in recurring.Items.Take(4).Select((value, idx) => (value, idx)))
        {
            var y = area.Top + 78 + index * 38;
            graphics.DrawString(item.Name, boldFont, brush, rightX, y);
            graphics.DrawString(
                $"{FormatMoney(item.GeneratedAmount, item.CurrencyCode)} · {item.ExecutionsCount} оп. · следующее {FormatDate(item.NextExecutionAt)}",
                font,
                brush,
                rightX,
                y + 18);
        }
    }

    private static void DrawCalendar(Graphics graphics, AnalyticsPdfReport report, Rectangle area)
    {
        DrawSectionCard(graphics, area, "Календарь платежей");
        using var weekdayFont = new Font("Arial", 11, FontStyle.Bold);
        using var dayFont = new Font("Arial", 11, FontStyle.Bold);
        using var textFont = new Font("Arial", 9, FontStyle.Regular);
        using var strongFont = new Font("Arial", 9, FontStyle.Bold);
        using var darkBrush = new SolidBrush(Color.FromArgb(38, 46, 58));
        using var mutedBrush = new SolidBrush(Color.FromArgb(112, 120, 126));
        using var cellBrush = new SolidBrush(Color.White);
        using var cellBorder = new Pen(Color.FromArgb(224, 228, 234));
        using var recurringBrush = new SolidBrush(Color.FromArgb(230, 255, 85));
        using var recurringExpenseBrush = new SolidBrush(Color.FromArgb(67, 59, 255, 50));
        using var expenseBrush = new SolidBrush(Color.FromArgb(217, 72, 95, 35));

        var monthStart = new DateTime(report.CalendarMonth.Year, report.CalendarMonth.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(report.CalendarMonth.Year, report.CalendarMonth.Month);
        var shift = ((int)monthStart.DayOfWeek + 6) % 7;
        var weekdays = new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" };

        var gridLeft = area.Left + 20;
        var gridTop = area.Top + 74;
        var cellWidth = 154;
        var cellHeight = 118;

        for (var index = 0; index < weekdays.Length; index++)
        {
            graphics.DrawString(weekdays[index], weekdayFont, mutedBrush, gridLeft + index * cellWidth + 8, gridTop);
        }

        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(report.CalendarMonth.Year, report.CalendarMonth.Month, day);
            var position = shift + day - 1;
            var column = position % 7;
            var row = position / 7;
            var rect = new Rectangle(gridLeft + column * cellWidth, gridTop + 28 + row * cellHeight, cellWidth - 8, cellHeight - 8);

            graphics.FillRoundedRectangle(cellBrush, rect, 14);
            graphics.DrawRoundedRectangle(cellBorder, rect, 14);
            graphics.DrawString(day.ToString(RuCulture), dayFont, darkBrush, rect.Left + 10, rect.Top + 8);

            if (report.CalendarExpenses.TryGetValue(date.Date, out var expense))
            {
                var expenseRect = new Rectangle(rect.Left + 8, rect.Top + 28, rect.Width - 16, 28);
                graphics.FillRoundedRectangle(expenseBrush, expenseRect, 10);
                graphics.DrawString("Потрачено", textFont, darkBrush, expenseRect.Left + 8, expenseRect.Top + 4);
                graphics.DrawString(FormatMoney(expense.Amount, expense.CurrencyCode), strongFont, darkBrush, expenseRect.Left + 8, expenseRect.Top + 14);
            }

            if (report.CalendarRecurring.TryGetValue(date.Date, out var recurring))
            {
                foreach (var (item, index) in recurring.Take(2).Select((value, idx) => (value, idx)))
                {
                    var y = rect.Top + 60 + index * 23;
                    var itemRect = new Rectangle(rect.Left + 8, y, rect.Width - 16, 20);
                    graphics.FillRoundedRectangle(item.Type == TransactionType.Income ? recurringBrush : recurringExpenseBrush, itemRect, 8);
                    graphics.DrawString(
                        $"{item.Name}: {FormatMoney(item.Amount, item.CurrencyCode)}",
                        textFont,
                        darkBrush,
                        new RectangleF(itemRect.Left + 6, itemRect.Top + 3, itemRect.Width - 12, itemRect.Height - 6));
                }
            }
        }
    }

    private static void DrawHistory(Graphics graphics, IReadOnlyCollection<AnalyticsHistoryItem> items, Rectangle area)
    {
        DrawSectionCard(graphics, area, "История операций");
        if (items.Count == 0)
        {
            DrawPlaceholder(graphics, area, "Нет операций за выбранный период.");
            return;
        }

        using var headerFont = new Font("Arial", 11, FontStyle.Bold);
        using var rowFont = new Font("Arial", 10, FontStyle.Regular);
        using var darkBrush = new SolidBrush(Color.FromArgb(38, 46, 58));
        using var mutedBrush = new SolidBrush(Color.FromArgb(108, 116, 124));
        using var linePen = new Pen(Color.FromArgb(230, 232, 236));

        var top = area.Top + 78;
        var left = area.Left + 24;

        graphics.DrawString("Дата", headerFont, darkBrush, left, top);
        graphics.DrawString("Описание", headerFont, darkBrush, left + 120, top);
        graphics.DrawString("Счет / Категория", headerFont, darkBrush, left + 470, top);
        graphics.DrawString("Источник", headerFont, darkBrush, left + 820, top);
        graphics.DrawString("Сумма", headerFont, darkBrush, left + 980, top);

        var rowTop = top + 30;
        foreach (var (item, index) in items.Take(14).Select((value, idx) => (value, idx)))
        {
            var y = rowTop + index * 38;
            graphics.DrawLine(linePen, left, y - 8, area.Right - 24, y - 8);
            graphics.DrawString(item.Date.ToString("dd.MM.yyyy", RuCulture), rowFont, darkBrush, left, y);
            graphics.DrawString(item.Title, rowFont, darkBrush, new RectangleF(left + 120, y, 320, 32));
            graphics.DrawString($"{item.AccountName} / {item.CategoryName}", rowFont, mutedBrush, new RectangleF(left + 470, y, 310, 32));
            graphics.DrawString(item.SourceLabel, rowFont, mutedBrush, left + 820, y);
            graphics.DrawString($"{FormatMoney(item.Amount, item.CurrencyCode)} ({item.TypeLabel})", rowFont, darkBrush, left + 980, y);
        }
    }

    private static void DrawSectionCard(Graphics graphics, Rectangle area, string title)
    {
        using var backgroundBrush = new SolidBrush(Color.White);
        using var borderPen = new Pen(Color.FromArgb(224, 228, 234));
        using var titleFont = new Font("Arial", 15, FontStyle.Bold);
        using var titleBrush = new SolidBrush(Color.FromArgb(28, 42, 62));

        graphics.FillRoundedRectangle(backgroundBrush, area, 20);
        graphics.DrawRoundedRectangle(borderPen, area, 20);
        graphics.DrawString(title, titleFont, titleBrush, area.Left + 20, area.Top + 20);
    }

    private static void DrawPlaceholder(Graphics graphics, Rectangle area, string text)
    {
        using var font = new Font("Arial", 12, FontStyle.Regular);
        using var brush = new SolidBrush(Color.FromArgb(108, 116, 124));
        graphics.DrawString(text, font, brush, new RectangleF(area.Left + 24, area.Top + 80, area.Width - 48, 40));
    }

    private static StringFormat CenterStringFormat()
    {
        return new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
    }

    private static StringFormat RightStringFormat()
    {
        return new StringFormat
        {
            Alignment = StringAlignment.Far,
            LineAlignment = StringAlignment.Near
        };
    }

    private static byte[] ToJpeg(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Jpeg);
        return stream.ToArray();
    }

    private static string FormatMoney(decimal amount, string currencyCode)
    {
        return $"{amount.ToString("N2", RuCulture)} {currencyCode}";
    }

    private static string FormatDate(DateTime? value)
    {
        return value.HasValue ? value.Value.ToString("dd.MM.yyyy", RuCulture) : "-";
    }
}

internal sealed record AnalyticsPdfReport(
    DateTime From,
    DateTime To,
    DateTime CalendarMonth,
    DashboardAnalyticsDto Dashboard,
    IReadOnlyCollection<CategoryAnalyticsDto> Categories,
    IReadOnlyCollection<BalanceHistoryPointDto> BalanceHistory,
    IReadOnlyCollection<CashFlowPointDto> CashFlow,
    RecurringPaymentsAnalyticsDto Recurring,
    IReadOnlyCollection<AnalyticsHistoryItem> HistoryItems,
    IReadOnlyDictionary<DateTime, CalendarExpenseItem> CalendarExpenses,
    IReadOnlyDictionary<DateTime, IReadOnlyCollection<CalendarRecurringItem>> CalendarRecurring);

internal sealed record AnalyticsHistoryItem(
    DateTime Date,
    string Title,
    string AccountName,
    string CategoryName,
    string SourceLabel,
    string TypeLabel,
    decimal Amount,
    string CurrencyCode);

internal sealed record CalendarExpenseItem(decimal Amount, string CurrencyCode);

internal sealed record CalendarRecurringItem(string Name, TransactionType Type, decimal Amount, string CurrencyCode);

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle bounds, int radius)
    {
        using var path = CreateRoundedRectangle(bounds, radius);
        graphics.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle bounds, int radius)
    {
        using var path = CreateRoundedRectangle(bounds, radius);
        graphics.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
