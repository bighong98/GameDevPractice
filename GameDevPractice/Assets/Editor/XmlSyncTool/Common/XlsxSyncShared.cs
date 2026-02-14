#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using NPOI.SS.UserModel;

public static class XlsxSyncShared
{
    public const int ExcelCellMaxTextLength = 32767;

    public static void WriteHeaderRow(ISheet sheet, IReadOnlyList<string> headers)
    {
        var header = sheet.CreateRow(0);
        for (int i = 0; i < headers.Count; i++)
        {
            header.CreateCell(i, CellType.String).SetCellValue(headers[i]);
        }
    }

    public static bool TryBuildHeaderMap(ISheet sheet, IReadOnlyList<string> requiredHeaders, out Dictionary<string, int> headerMap)
    {
        headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var headerRow = sheet.GetRow(0);
        if (headerRow == null)
        {
            return false;
        }

        for (int i = 0; i < headerRow.LastCellNum; i++)
        {
            var value = headerRow.GetCell(i)?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!headerMap.ContainsKey(value))
            {
                headerMap.Add(value, i);
            }
        }

        for (int i = 0; i < requiredHeaders.Count; i++)
        {
            if (!headerMap.ContainsKey(requiredHeaders[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static string ReadCellString(IRow row, Dictionary<string, int> headerMap, string key, DataFormatter formatter)
    {
        if (!headerMap.TryGetValue(key, out var index))
        {
            return string.Empty;
        }

        var cell = row.GetCell(index);
        if (cell == null)
        {
            return string.Empty;
        }

        return formatter.FormatCellValue(cell) ?? string.Empty;
    }

    public static int ReadCellInt(IRow row, Dictionary<string, int> headerMap, string key, DataFormatter formatter)
    {
        var raw = ReadCellString(row, headerMap, key, formatter);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return Convert.ToInt32(number);
        }

        return 0;
    }

    public static float ReadCellFloat(IRow row, Dictionary<string, int> headerMap, string key, DataFormatter formatter)
    {
        var raw = ReadCellString(row, headerMap, key, formatter);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0f;
        }

        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return 0f;
    }
}
#endif
