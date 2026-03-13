using System.Globalization;
using System.Text;

namespace CalendarSeedSqlBuilder;

public static class Program
{
    private const int WORKING_DAY_TYPE = 1;
    private const int NON_WORKING_DAY_TYPE = 2;
    private const string INPUT_DATE_FORMAT = "dd.MM.yyyy";
    private const string SQL_DATE_FORMAT = "yyyy-MM-dd";
    private const string OUTPUT_FILE = "result.sql";

    private static void Main(string[] args)
    {
        var filePath = GetFilePath(args);

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"File not found: {filePath}");
            return;
        }

        var workingDays = ParseWorkingDaysFromFile(filePath);

        if (workingDays.Count == 0)
        {
            Console.WriteLine("No valid dates found in file.");
            return;
        }

        var minYear = workingDays.Min(d => d.Year);
        var maxYear = workingDays.Max(d => d.Year);

        var allDates = GenerateAllDatesInRange(minYear, maxYear);
        var monthlyGroups = allDates.GroupBy(d => new { d.Year, d.Month })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Month);

        var sqlBuilder = new StringBuilder();

        foreach (var group in monthlyGroups)
        {
            var monthSql = GenerateMonthInsert(group.Key.Year, group.Key.Month, group, workingDays);
            sqlBuilder.AppendLine(monthSql);
            sqlBuilder.AppendLine();
        }

        try
        {
            File.WriteAllText(OUTPUT_FILE, sqlBuilder.ToString());
            Console.WriteLine($"SQL script successfully written to {OUTPUT_FILE}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing to file: {ex.Message}");
        }
    }

    private static string GetFilePath(string[] args)
    {
        return args.Length > 0 ? args[0] : "Dates.txt";
    }

    private static HashSet<DateTime> ParseWorkingDaysFromFile(string filePath)
    {
        var fileContent = File.ReadAllText(filePath);
        var dateStrings = fileContent.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        var dates = new HashSet<DateTime>();

        foreach (var dateString in dateStrings)
        {
            var trimmed = dateString.Trim();

            if (TryParseDate(trimmed, out var date))
            {
                dates.Add(date);
            }
            else
            {
                Console.WriteLine($"Warning: Skipping invalid date format '{trimmed}'.");
            }
        }

        return dates;
    }

    private static bool TryParseDate(string input, out DateTime date)
    {
        return DateTime.TryParseExact(input, INPUT_DATE_FORMAT, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static IEnumerable<DateTime> GenerateAllDatesInRange(int startYear, int endYear)
    {
        var startDate = new DateTime(startYear, 1, 1);
        var endDate = new DateTime(endYear, 12, 31);
        var current = startDate;

        while (current <= endDate)
        {
            yield return current;
            current = current.AddDays(1);
        }
    }

    private static string GenerateMonthInsert(int year, int month, IEnumerable<DateTime> daysInMonth, HashSet<DateTime> workingDays)
    {
        var monthName = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        var sql = new StringBuilder();

        sql.AppendLine($"-- {monthName}");
        sql.AppendLine("insert into exchange_calendar_days (date, type, updated_at)");
        sql.AppendLine("values");

        var sortedDays = daysInMonth.OrderBy(d => d).ToList();

        for (var i = 0; i < sortedDays.Count; i++)
        {
            var date = sortedDays[i];
            var type = workingDays.Contains(date) ? WORKING_DAY_TYPE : NON_WORKING_DAY_TYPE;
            var formattedDate = date.ToString(SQL_DATE_FORMAT);

            sql.Append($"('{formattedDate}', {type}, now())");

            if (i < sortedDays.Count - 1)
            {
                sql.AppendLine(",");
            }
            else
            {
                sql.AppendLine();
            }
        }

        sql.AppendLine("on conflict (date) do update");
        sql.AppendLine("set type = excluded.type, updated_at = excluded.updated_at;");

        return sql.ToString();
    }
}