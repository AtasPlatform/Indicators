namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Text;

using ATAS.DataFeedsCore;

using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

/// <summary>
/// Displays account information on the chart including account ID, balance, blocked margin, available balance, and PnL.
/// </summary>
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000648751-account-info-display")]
[Category(IndicatorCategories.Trading)]
[DisplayName("Account Info Display")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.AccountInfoDisplayDescription))]
public class AccountInfoDisplay : Indicator
{
	#region Fields

	private Color _backgroundColor = Color.FromArgb(200, 20, 25, 35);
	private Color _textColor = Color.FromArgb(220, 220, 220);
	private Color _positiveColor = Color.FromArgb(0, 230, 118);
	private Color _negativeColor = Color.FromArgb(255, 82, 82);
	private Color _neutralColor = Color.FromArgb(150, 150, 150);
	private RenderFont _font = new("Arial", 11);
	private RenderStringFormat _stringFormat = new()
	{
		LineAlignment = StringAlignment.Near,
		Alignment = StringAlignment.Near
	};

	private Portfolio _currentPortfolio;

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BackGround),
		Description = nameof(Strings.LabelFillColorDescription), GroupName = nameof(Strings.Visualization))]
	public CrossColor BackgroundColor
	{
		get => _backgroundColor.Convert();
		set => _backgroundColor = value.Convert();
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TextColor),
		Description = nameof(Strings.LabelTextColorDescription), GroupName = nameof(Strings.Visualization))]
	public CrossColor TextColor
	{
		get => _textColor.Convert();
		set => _textColor = value.Convert();
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.PositiveColor),
		Description = nameof(Strings.PositiveColorDescription), GroupName = nameof(Strings.Visualization))]
	public CrossColor PositiveColor
	{
		get => _positiveColor.Convert();
		set => _positiveColor = value.Convert();
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.NegativeColor),
		Description = nameof(Strings.NegativeColorDescription), GroupName = nameof(Strings.Visualization))]
	public CrossColor NegativeColor
	{
		get => _negativeColor.Convert();
		set => _negativeColor = value.Convert();
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.NeutralColor),
		Description = nameof(Strings.NeutralColorDescription), GroupName = nameof(Strings.Visualization))]
	public CrossColor NeutralColor
	{
		get => _neutralColor.Convert();
		set => _neutralColor = value.Convert();
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.FontSize),
		Description = nameof(Strings.FontSizeDescription), GroupName = nameof(Strings.Visualization))]
	[Range(6, 30)]
	public float FontSize
	{
		get => _font.Size;
		set => _font = new RenderFont("Arial", value);
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowAccountId),
		Description = nameof(Strings.ShowAccountIdDescription), GroupName = nameof(Strings.Settings))]
	public bool ShowAccountId { get; set; } = true;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowCurrency),
		Description = nameof(Strings.ShowCurrencyDescription), GroupName = nameof(Strings.Settings))]
	public bool ShowCurrency { get; set; } = true;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowBalance),
		Description = nameof(Strings.ShowBalanceDescription), GroupName = nameof(Strings.Settings))]
	public bool ShowBalance { get; set; } = true;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowAvailableBalance),
		Description = nameof(Strings.ShowAvailableBalanceDescription), GroupName = nameof(Strings.Settings))]
	public bool ShowAvailableBalance { get; set; } = true;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowBlockedMargin),
		Description = nameof(Strings.ShowBlockedMarginDescription), GroupName = nameof(Strings.Settings))]
	public bool ShowMargin { get; set; } = false;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowLeverage),
		Description = nameof(Strings.ShowLeverageDescription), GroupName = nameof(Strings.Settings))]
	public bool ShowLeverage { get; set; } = true;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowOpenPnL),
		Description = nameof(Strings.ShowOpenPnLDescription), GroupName = nameof(Strings.Settings))]
	public bool ShowOpenPnL { get; set; } = true;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowClosedPnL),
		Description = nameof(Strings.ShowClosedPnLDescription), GroupName = nameof(Strings.Settings))]
	public bool ShowClosedPnL { get; set; } = true;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowTotalPnL),
		Description = nameof(Strings.ShowTotalPnLDescription), GroupName = nameof(Strings.Settings))]
	public bool ShowTotalPnL { get; set; } = false;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.HorizontalPosition),
		GroupName = nameof(Strings.LayoutGroup))]
	public HorizontalAlignment HorizontalPosition { get; set; } = HorizontalAlignment.Left;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.VerticalPosition),
		GroupName = nameof(Strings.LayoutGroup))]
	public VerticalAlignment VerticalPosition { get; set; } = VerticalAlignment.Bottom;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.OffsetX),
		Description = nameof(Strings.OffsetXDescription), GroupName = nameof(Strings.LayoutGroup))]
	[Range(0, 1000)]
	public int OffsetX { get; set; } = 20;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.OffsetY),
		Description = nameof(Strings.OffsetYDescription), GroupName = nameof(Strings.LayoutGroup))]
	[Range(0, 1000)]
	public int OffsetY { get; set; } = 20;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ColumnSpacing),
		Description = nameof(Strings.ColumnSpacingDescription), GroupName = nameof(Strings.LayoutGroup))]
	[Range(5, 50)]
	public int ColumnSpacing { get; set; } = 15;

	#endregion

	#region Enums

	public enum HorizontalAlignment
	{
		Left,
		Center,
		Right
	}

	public enum VerticalAlignment
	{
		Top,
		Middle,
		Bottom
	}

	#endregion

	#region ctor

	public AccountInfoDisplay()
		: base(true)
	{
		DenyToChangePanel = true;
		EnableCustomDrawing = true;
		SubscribeToDrawingEvents(DrawingLayouts.Final);
		DataSeries[0].IsHidden = true;
		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
	}

	#endregion

	#region Protected Methods

	protected override void OnInitialize()
	{
		if (TradingManager != null)
		{
			TradingManager.PortfolioSelected += OnPortfolioSelected;
			_currentPortfolio = TradingManager.Portfolio;
		}
	}

	protected override void OnDispose()
	{
		if (TradingManager != null)
		{
			TradingManager.PortfolioSelected -= OnPortfolioSelected;
		}
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		// No calculation needed
	}

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		if (ChartInfo == null || Container?.Region == null)
			return;

		// Get current portfolio
		var portfolio = _currentPortfolio ?? TradingManager?.Portfolio;
		if (portfolio == null)
			return;

		// Build display text
		var text = BuildDisplayText(portfolio);
		if (string.IsNullOrEmpty(text))
			return;

		// Calculate proper dimensions for table layout
		var lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
		var lineHeight = context.MeasureString("A", _font).Height;

		var maxLabelWidth = 0;
		var maxValueWidth = 0;

		foreach (var line in lines)
		{
			var parts = line.Split('|');
			if (parts.Length == 2)
			{
				var labelWidth = (int)context.MeasureString(parts[0], _font).Width;
				var valueWidth = (int)context.MeasureString(parts[1], _font).Width;

				if (labelWidth > maxLabelWidth)
					maxLabelWidth = labelWidth;
				if (valueWidth > maxValueWidth)
					maxValueWidth = valueWidth;
			}
		}

		var padding = 10;
		var rectWidth = maxLabelWidth + ColumnSpacing + maxValueWidth + padding * 2;
		var rectHeight = lines.Length * lineHeight + padding * 2;

		// Calculate position
		var x = CalculateXPosition(rectWidth);
		var y = CalculateYPosition(rectHeight);

		// Draw background
		var rectangle = new Rectangle(x, y, rectWidth, rectHeight);
		context.FillRectangle(_backgroundColor, rectangle);

		// Draw border
		context.DrawRectangle(new RenderPen(Color.Gray, 1), rectangle);

		// Draw text
		var textRect = new Rectangle(x + padding, y + padding, rectWidth - padding * 2, rectHeight - padding * 2);
		DrawColoredText(context, text, textRect, portfolio, maxLabelWidth);
	}

	#endregion

	#region Private Methods

	private void OnPortfolioSelected(Portfolio portfolio)
	{
		_currentPortfolio = portfolio;
		RedrawChart();
	}

	private string BuildDisplayText(Portfolio portfolio)
	{
		var sb = new StringBuilder();

		if (ShowAccountId)
			sb.AppendLine($"Account|{portfolio.AccountID}");

		if (ShowCurrency && portfolio.Currency.HasValue)
			sb.AppendLine($"Currency|{portfolio.Currency}");

		if (ShowBalance)
			sb.AppendLine($"Balance|{FormatCurrency(portfolio.Balance)}");

		if (ShowAvailableBalance && portfolio.BalanceAvailable.HasValue)
			sb.AppendLine($"Available|{FormatCurrency(portfolio.BalanceAvailable.Value)}");

		if (ShowMargin)
			sb.AppendLine($"Blocked Margin|{FormatCurrency(portfolio.BlockedMargin)}");

		if (ShowLeverage && portfolio.Leverage != 1)
			sb.AppendLine($"Leverage|{portfolio.Leverage:F2}x");

		if (ShowOpenPnL)
			sb.AppendLine($"Open PnL|{FormatCurrency(portfolio.OpenPnL)}");

		if (ShowClosedPnL)
			sb.AppendLine($"Closed PnL|{FormatCurrency(portfolio.ClosedPnL)}");

		if (ShowTotalPnL)
			sb.AppendLine($"Total PnL|{FormatCurrency(portfolio.ClosedPnL + portfolio.OpenPnL)}");

		return sb.ToString().TrimEnd();
	}

	private void DrawColoredText(RenderContext context, string text, Rectangle textRect, Portfolio portfolio, int maxLabelWidth)
	{
		var lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
		var lineHeight = context.MeasureString("A", _font).Height;

		// Calculate value column position
		var valueColumnX = textRect.X + maxLabelWidth + ColumnSpacing;
		var currentY = textRect.Y;

		// Draw lines
		foreach (var line in lines)
		{
			var parts = line.Split('|');
			if (parts.Length == 2)
			{
				var label = parts[0];
				var valueStr = parts[1];

				// Draw label
				context.DrawString(label, _font, _textColor, textRect.X, currentY);

				// Determine color for value
				var valueColor = _textColor;
				if (line.Contains("PnL"))
				{
					var value = ExtractNumericValue(valueStr);
					valueColor = value > 0 ? _positiveColor : (value < 0 ? _negativeColor : _neutralColor);
				}

				// Draw value
				context.DrawString(valueStr, _font, valueColor, valueColumnX, currentY);
			}
			else
			{
				// Fallback for malformed lines
				context.DrawString(line, _font, _textColor, textRect.X, currentY);
			}

			currentY += lineHeight;
		}
	}

	private decimal ExtractNumericValue(string valueStr)
	{
		// Remove currency symbols and try to parse
		var cleanStr = valueStr.Replace(",", "").Trim();
		if (decimal.TryParse(cleanStr, out var result))
			return result;
		return 0;
	}

	private string FormatCurrency(decimal value)
	{
		return value.ToString("N2");
	}

	private int CalculateXPosition(int width)
	{
		return HorizontalPosition switch
		{
			HorizontalAlignment.Left => OffsetX,
			HorizontalAlignment.Center => (Container.Region.Width - width) / 2,
			HorizontalAlignment.Right => Container.Region.Width - width - OffsetX,
			_ => OffsetX
		};
	}

	private int CalculateYPosition(int height)
	{
		return VerticalPosition switch
		{
			VerticalAlignment.Top => OffsetY,
			VerticalAlignment.Middle => (Container.Region.Height - height) / 2,
			VerticalAlignment.Bottom => Container.Region.Height - height - OffsetY,
			_ => OffsetY
		};
	}

	#endregion
}
