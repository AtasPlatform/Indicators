namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using OFT.Attributes;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

[DisplayName("CSV Levels Importer")]
[Display(Description = "Imports horizontal levels and zones from a local CSV file.")]
[HelpLink("https://docs.atas.net/")]
public class CsvLevelsImporter : Indicator
{
	private readonly List<ImportedLevel> _levels = [];
	private readonly object _levelsLock = new();

	private DateTime _lastLoadTime = DateTime.MinValue;
	private FileSystemWatcher? _watcher;
	private volatile bool _isLoading;
	private string? _lastError;

	private string _csvDirectory = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
		"ATAS",
		"CsvLevels");

	private string _csvFileName = "levels.csv";
	private int _recalcIntervalMinutes = 5;
	private bool _useFileWatcher = true;
	private int _transparency = 70;
	private bool _drawLinesOnChart = true;
	private bool _showPriceOnChart;
	private bool _showLabelsOnRight = true;
	private int _labelFontSize = 11;
	private int _labelXOffset = -80;
	private int _labelYOffset = -8;

	[Parameter]
	[Display(Name = "CSV directory", GroupName = "CSV", Order = 10)]
	public string CsvDirectory
	{
		get => _csvDirectory;
		set
		{
			_csvDirectory = value;
			ForceReload();
		}
	}

	[Parameter]
	[Display(Name = "CSV file name", GroupName = "CSV", Order = 20)]
	public string CsvFileName
	{
		get => _csvFileName;
		set
		{
			_csvFileName = value;
			ForceReload();
		}
	}

	[Parameter]
	[Display(Name = "Reload interval, minutes", GroupName = "CSV", Order = 30)]
	[Range(0, 1440)]
	public int RecalcIntervalMinutes
	{
		get => _recalcIntervalMinutes;
		set
		{
			_recalcIntervalMinutes = Math.Max(0, value);
			ForceReload();
		}
	}

	[Display(Name = "Watch file changes", GroupName = "CSV", Order = 40)]
	public bool UseFileWatcher
	{
		get => _useFileWatcher;
		set
		{
			_useFileWatcher = value;
			SetupFileWatcher();
		}
	}

	[Display(Name = "Transparency, percent", GroupName = "Drawing", Order = 100)]
	[Range(0, 100)]
	public int Transparency
	{
		get => _transparency;
		set
		{
			_transparency = Math.Clamp(value, 0, 100);
			RedrawChart();
		}
	}

	[Display(Name = "Draw levels", GroupName = "Drawing", Order = 110)]
	public bool DrawLinesOnChart
	{
		get => _drawLinesOnChart;
		set
		{
			_drawLinesOnChart = value;
			RedrawChart();
		}
	}

	[Display(Name = "Show price", GroupName = "Drawing", Order = 120)]
	public bool ShowPriceOnChart
	{
		get => _showPriceOnChart;
		set
		{
			_showPriceOnChart = value;
			RedrawChart();
		}
	}

	[Display(Name = "Show labels on right", GroupName = "Labels", Order = 200)]
	public bool ShowLabelsOnRight
	{
		get => _showLabelsOnRight;
		set
		{
			_showLabelsOnRight = value;
			RedrawChart();
		}
	}

	[Display(Name = "Font size", GroupName = "Labels", Order = 210)]
	[Range(6, 72)]
	public int LabelFontSize
	{
		get => _labelFontSize;
		set
		{
			_labelFontSize = Math.Max(6, value);
			RedrawChart();
		}
	}

	[Display(Name = "X offset", GroupName = "Labels", Order = 220)]
	public int LabelXOffset
	{
		get => _labelXOffset;
		set
		{
			_labelXOffset = value;
			RedrawChart();
		}
	}

	[Display(Name = "Y offset", GroupName = "Labels", Order = 230)]
	public int LabelYOffset
	{
		get => _labelYOffset;
		set
		{
			_labelYOffset = value;
			RedrawChart();
		}
	}

	public CsvLevelsImporter()
		: base(true)
	{
		Name = "CSV Levels Importer";
		EnableCustomDrawing = true;
		DrawAbovePrice = true;
		Panel = IndicatorDataProvider.CandlesPanel;
		SubscribeToDrawingEvents(DrawingLayouts.Final | DrawingLayouts.LatestBar);
		DataSeries[0].IsHidden = true;
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar != CurrentBar - 1 || _isLoading)
			return;

		bool empty;
		lock (_levelsLock)
			empty = _levels.Count == 0;

		if (empty)
		{
			LoadLevelsFromFile();
			return;
		}

		if (_recalcIntervalMinutes > 0 && (DateTime.Now - _lastLoadTime).TotalMinutes >= _recalcIntervalMinutes)
			LoadLevelsFromFile();
	}

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		if (!_drawLinesOnChart || ChartInfo is null)
			return;

		List<ImportedLevel> snapshot;
		lock (_levelsLock)
			snapshot = [.. _levels];

		var region = ChartInfo.PriceChartContainer.Region;
		var x1 = region.Left;
		var x2 = region.Right;

		foreach (var level in snapshot)
		{
			var drawColor = ApplyTransparency(level.Color, _transparency);
			var pen = MakePen(drawColor, level.LineWidth, level.LineType);

			if (level.Price2.HasValue)
				DrawZone(context, level, drawColor, pen, x1, x2);
			else
				DrawHorizontalLine(context, level, drawColor, pen, x1, x2);

			if (_showLabelsOnRight && !string.IsNullOrWhiteSpace(level.Note))
				DrawLabel(context, level, drawColor, x2);
		}

		if (_lastError is null)
			return;

		var font = new RenderFont("Arial", 10);
		context.DrawString($"[CSV Levels] {_lastError}", font, Color.OrangeRed, region.Left + 4, region.Top + 4);
	}

	protected override void OnDispose()
	{
		_watcher?.Dispose();
		_watcher = null;
		base.OnDispose();
	}

	private void LoadLevelsFromFile()
	{
		_isLoading = true;
		_lastError = null;

		lock (_levelsLock)
			_levels.Clear();

		RedrawChart();

		try
		{
			var filePath = Path.Combine(_csvDirectory, _csvFileName);

			if (!File.Exists(filePath))
			{
				_lastError = $"File not found: {filePath}";
				RedrawChart();
				return;
			}

			var imported = ParseCsv(ReadFileWithRetry(filePath));

			lock (_levelsLock)
			{
				_levels.Clear();
				_levels.AddRange(imported);
			}

			_lastLoadTime = DateTime.Now;
			SetupFileWatcher();
		}
		catch (Exception ex)
		{
			_lastError = ex.Message.Length > 100 ? ex.Message[..100] + "..." : ex.Message;
			LogError(ex);
		}
		finally
		{
			_isLoading = false;
			RedrawChart();
		}
	}

	private static string ReadFileWithRetry(string path, int maxAttempts = 3)
	{
		for (var attempt = 0; attempt < maxAttempts; attempt++)
		{
			try
			{
				using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
				using var reader = new StreamReader(stream, Encoding.UTF8, true);
				return reader.ReadToEnd();
			}
			catch (IOException) when (attempt + 1 < maxAttempts)
			{
				System.Threading.Thread.Sleep(100);
			}
		}

		using var fallback = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		using var fallbackReader = new StreamReader(fallback, Encoding.UTF8, true);
		return fallbackReader.ReadToEnd();
	}

	private void SetupFileWatcher()
	{
		_watcher?.Dispose();
		_watcher = null;

		if (!_useFileWatcher || !Directory.Exists(_csvDirectory))
			return;

		try
		{
			_watcher = new FileSystemWatcher(_csvDirectory, _csvFileName)
			{
				NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
				EnableRaisingEvents = true
			};

			_watcher.Changed += (_, _) => ForceReload();
			_watcher.Created += (_, _) => ForceReload();
			_watcher.Renamed += (_, _) => ForceReload();
		}
		catch (Exception ex)
		{
			_lastError = ex.Message;
			LogError(ex);
		}
	}

	private void ForceReload()
	{
		lock (_levelsLock)
			_levels.Clear();

		_lastLoadTime = DateTime.MinValue;
		_lastError = null;
		RecalculateValues();
		RedrawChart();
	}

	private void DrawHorizontalLine(RenderContext context, ImportedLevel level, Color color, RenderPen pen, int x1, int x2)
	{
		var y = ChartInfo!.GetYByPrice(level.Price, false);
		context.DrawLine(pen, x1, y, x2, y);

		if (!_showPriceOnChart)
			return;

		var font = new RenderFont("Arial", _labelFontSize);
		context.DrawString(level.Price.ToString("F2", CultureInfo.InvariantCulture), font, color, x1 + 4, y + _labelYOffset);
	}

	private void DrawZone(RenderContext context, ImportedLevel level, Color color, RenderPen pen, int x1, int x2)
	{
		var high = Math.Max(level.Price, level.Price2!.Value);
		var low = Math.Min(level.Price, level.Price2.Value);
		var yTop = ChartInfo!.GetYByPrice(high, false);
		var yBottom = ChartInfo!.GetYByPrice(low, false);
		var yMin = Math.Min(yTop, yBottom);
		var yMax = Math.Max(yTop, yBottom);
		var rect = new Rectangle(x1, yMin, Math.Max(1, x2 - x1), Math.Max(1, yMax - yMin));

		context.FillRectangle(color, rect);
		context.DrawRectangle(pen, rect);

		if (!_showPriceOnChart)
			return;

		var font = new RenderFont("Arial", _labelFontSize);
		context.DrawString(high.ToString("F2", CultureInfo.InvariantCulture), font, color, x1 + 4, yMin + _labelYOffset);
		context.DrawString(low.ToString("F2", CultureInfo.InvariantCulture), font, color, x1 + 4, yMax + _labelYOffset);
	}

	private void DrawLabel(RenderContext context, ImportedLevel level, Color color, int rightX)
	{
		var text = level.Note.Replace("\\n", "\n");
		if (string.IsNullOrWhiteSpace(text))
			return;

		var yCenter = level.Price2.HasValue
			? (ChartInfo!.GetYByPrice(Math.Max(level.Price, level.Price2.Value), false)
				+ ChartInfo.GetYByPrice(Math.Min(level.Price, level.Price2.Value), false)) / 2
			: ChartInfo!.GetYByPrice(level.Price, false);

		var x = level.TextAlignment switch
		{
			0 => rightX - 200 + _labelXOffset,
			2 => rightX - 100 + _labelXOffset,
			_ => rightX + _labelXOffset
		};

		context.DrawString(text, new RenderFont("Arial", _labelFontSize), color, x, yCenter + _labelYOffset);
	}

	private static List<ImportedLevel> ParseCsv(string csv)
	{
		var result = new List<ImportedLevel>();
		if (string.IsNullOrWhiteSpace(csv))
			return result;

		var lines = csv.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
		var separator = DetectSeparator(lines);

		foreach (var rawLine in lines)
		{
			var line = rawLine.Trim();
			if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
				continue;

			var fields = SplitLine(line, separator);
			if (fields.Count == 0)
				continue;

			if (fields[0].Trim().Equals("price", StringComparison.OrdinalIgnoreCase)
				|| fields[0].Trim().Equals("prix", StringComparison.OrdinalIgnoreCase))
				continue;

			if (!TryParseDecimal(fields.ElementAtOrDefault(0), out var price))
				continue;

			decimal? price2 = null;
			if (TryParseDecimal(fields.ElementAtOrDefault(1), out var parsedPrice2) && parsedPrice2 > 0)
				price2 = parsedPrice2;

			result.Add(new ImportedLevel
			{
				Price = price,
				Price2 = price2,
				Note = UnquoteField(fields.ElementAtOrDefault(2) ?? string.Empty),
				Color = ParseColor(UnquoteField(fields.ElementAtOrDefault(3) ?? "white")),
				LineType = ParseInt(fields.ElementAtOrDefault(4), 0),
				LineWidth = Math.Max(1, ParseInt(fields.ElementAtOrDefault(5), 1)),
				TextAlignment = ParseInt(fields.ElementAtOrDefault(6), 1)
			});
		}

		return result;
	}

	private static char DetectSeparator(IEnumerable<string> lines)
	{
		foreach (var line in lines)
		{
			var text = line.Trim();
			if (string.IsNullOrEmpty(text) || text.StartsWith('#'))
				continue;

			var semicolons = text.Count(c => c == ';');
			var commas = text.Count(c => c == ',');
			if (semicolons > commas)
				return ';';
			if (commas > semicolons)
				return ',';
		}

		return ';';
	}

	private static List<string> SplitLine(string line, char separator)
	{
		var result = new List<string>();
		var current = new StringBuilder();
		var inQuotes = false;

		foreach (var c in line)
		{
			if (c == '"')
			{
				inQuotes = !inQuotes;
				continue;
			}

			if (c == separator && !inQuotes)
			{
				result.Add(current.ToString().Trim());
				current.Clear();
				continue;
			}

			current.Append(c);
		}

		result.Add(current.ToString().Trim());
		return result;
	}

	private static string UnquoteField(string value)
		=> value.Trim().Trim('"').Trim('\'');

	private static bool TryParseDecimal(string? value, out decimal result)
	{
		result = 0;
		if (string.IsNullOrWhiteSpace(value))
			return false;

		var normalized = Regex.Replace(value.Trim(), @",(\d{1,4})$", ".$1")
			.Replace("\u00A0", string.Empty)
			.Replace(" ", string.Empty);

		return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
	}

	private static int ParseInt(string? value, int defaultValue)
		=> int.TryParse(value?.Trim(), out var parsed) ? parsed : defaultValue;

	private static Color ParseColor(string raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
			return Color.White;

		var value = raw.Trim();

		if (value.StartsWith('#'))
		{
			try
			{
				var hex = value.TrimStart('#');
				if (hex.Length == 6)
					return Color.FromArgb(
						Convert.ToInt32(hex[..2], 16),
						Convert.ToInt32(hex[2..4], 16),
						Convert.ToInt32(hex[4..6], 16));

				if (hex.Length == 8)
					return Color.FromArgb(
						Convert.ToInt32(hex[..2], 16),
						Convert.ToInt32(hex[2..4], 16),
						Convert.ToInt32(hex[4..6], 16),
						Convert.ToInt32(hex[6..8], 16));
			}
			catch
			{
				return Color.White;
			}
		}

		return value.ToLowerInvariant() switch
		{
			"red" => Color.Red,
			"green" => Color.LimeGreen,
			"blue" => Color.RoyalBlue,
			"white" => Color.White,
			"black" => Color.Black,
			"purple" => Color.MediumPurple,
			"pink" => Color.HotPink,
			"yellow" => Color.Yellow,
			"gold" => Color.Gold,
			"brown" => Color.SaddleBrown,
			"cyan" => Color.Cyan,
			"teal" => Color.Teal,
			"gray" or "grey" => Color.Gray,
			"orange" => Color.Orange,
			"lime" => Color.Lime,
			"magenta" => Color.Magenta,
			"salmon" => Color.Salmon,
			"turquoise" => Color.Turquoise,
			"violet" => Color.Violet,
			"silver" => Color.Silver,
			_ => Color.FromName(value).IsKnownColor ? Color.FromName(value) : Color.White
		};
	}

	private static RenderPen MakePen(Color color, int width, int lineType)
	{
		var dash = lineType switch
		{
			1 => DashStyle.Dash,
			2 => DashStyle.Dot,
			_ => DashStyle.Solid
		};

		return new RenderPen(color, width, dash);
	}

	private static Color ApplyTransparency(Color color, int transparencyPercent)
	{
		var alpha = 255 - (int)(255 * Math.Clamp(transparencyPercent, 0, 100) / 100.0);
		return Color.FromArgb(alpha, color.R, color.G, color.B);
	}

	private void LogError(Exception ex)
	{
		try
		{
			Directory.CreateDirectory(_csvDirectory);
			var logPath = Path.Combine(_csvDirectory, "csv_levels_errors.log");
			File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}", Encoding.UTF8);
		}
		catch
		{
			// Logging must never break chart rendering.
		}
	}

	private sealed class ImportedLevel
	{
		public decimal Price { get; init; }
		public decimal? Price2 { get; init; }
		public string Note { get; init; } = string.Empty;
		public Color Color { get; init; } = Color.White;
		public int LineType { get; init; }
		public int LineWidth { get; init; } = 1;
		public int TextAlignment { get; init; } = 1;
	}
}
