namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;

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
	private RenderPen _borderPen = new(Color.Gray, 1);
	private RenderFont _font = new("Arial", 11);
	private const int Padding = 10;

	private Portfolio _currentPortfolio;

	// Per-trade max open PnL. Updated from position events (not from rendering), guarded by _tradeLock.
	private readonly object _tradeLock = new();
	private Position _trackedPosition;
	private int _tradeSide; // sign of the tracked trade: 1 long, -1 short, 0 flat
	private decimal _currentTradeMaxOpenPnl;
	private decimal? _lastTradeMaxOpenPnl;

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
		set
		{
			if (Math.Abs(_font.Size - value) < 0.01f) return;
			_font = new RenderFont("Arial", value);
		}
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

	[Display(Name = "Show max open PnL", GroupName = "Settings",
		Description = "Show the maximum open PnL reached during the current trade.")]
	public bool ShowMaxOpenPnL { get; set; } = false;

	[Display(Name = "Show last trade max open PnL", GroupName = "Settings",
		Description = "Keep the maximum open PnL of the last closed trade visible while flat.")]
	public bool ShowLastTradeMaxOpenPnL { get; set; } = false;

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
		_currentPortfolio = TradingManager?.Portfolio;

		if (TradingManager != null)
			TradingManager.SecuritySelected += OnSecuritySelected;

		ResetTradeTracking();
	}

	protected override void OnPortfolioSelected(Portfolio portfolio)
	{
		_currentPortfolio = portfolio;
		ResetTradeTracking();
		RedrawChart();
	}

	protected override void OnDispose()
	{
		if (TradingManager != null)
			TradingManager.SecuritySelected -= OnSecuritySelected;

		AttachPosition(null);
	}

	protected override void OnPositionChanged(Position position)
	{
		if (!IsChartPosition(position))
			return;

		AttachPosition(position);
		UpdateTrade(position);
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		// No calculation needed
	}

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		if (ChartInfo == null || Container == null)
			return;

		// Get current portfolio
		var portfolio = _currentPortfolio ?? TradingManager?.Portfolio;
		if (portfolio == null)
			return;

		// Build display text
		var lines = BuildLines(portfolio);
		if (lines.Count == 0)
			return;

		// Calculate proper dimensions for table layout
		var lineHeight = context.MeasureString("A", _font).Height;

		var maxLabelWidth = 0;
		var maxValueWidth = 0;

		foreach (var line in lines)
		{
			var labelWidth = (int)context.MeasureString(line.Label, _font).Width;
			var valueWidth = (int)context.MeasureString(line.Value, _font).Width;
			
			if (labelWidth > maxLabelWidth)
				maxLabelWidth = labelWidth;
			if (valueWidth > maxValueWidth)
				maxValueWidth = valueWidth;
		}

		var rectWidth = maxLabelWidth + ColumnSpacing + maxValueWidth + Padding * 2;
		var rectHeight = lines.Count * lineHeight + Padding * 2;

		// Calculate position
		var x = CalculateXPosition(rectWidth);
		var y = CalculateYPosition(rectHeight);

		// Draw background
		var rectangle = new Rectangle(x, y, rectWidth, rectHeight);
		context.FillRectangle(_backgroundColor, rectangle);

		// Draw border
		context.DrawRectangle(_borderPen, rectangle);

		// Draw text
		var textRect = new Rectangle(x + Padding, y + Padding, rectWidth - Padding * 2, rectHeight - Padding * 2);
		DrawColoredText(context, lines, textRect, maxLabelWidth);
	}

	#endregion

	#region Private Methods

	private void OnSecuritySelected(Security security)
	{
		ResetTradeTracking();
		RedrawChart();
	}

	private void OnTrackedPositionPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		// UnrealizedPnL raises PropertyChanged on every recalculation, so every peak is seen,
		// including the ones that happen between two rendered frames.
		if (e.PropertyName is nameof(Position.UnrealizedPnL) or nameof(Position.Volume) or nameof(Position.IsInPosition) or null or "")
			UpdateTrade((Position)sender);
	}

	private bool IsChartPosition(Position position)
	{
		var portfolio = _currentPortfolio ?? TradingManager?.Portfolio;
		var security = TradingManager?.Security;

		return position != null && portfolio != null && security != null
			&& string.Equals(position.AccountID, portfolio.AccountID, StringComparison.Ordinal)
			&& position.Security != null
			&& string.Equals(position.Security.Code, security.Code, StringComparison.Ordinal);
	}

	private void AttachPosition(Position position)
	{
		lock (_tradeLock)
		{
			if (ReferenceEquals(_trackedPosition, position))
				return;

			if (_trackedPosition != null)
				_trackedPosition.PropertyChanged -= OnTrackedPositionPropertyChanged;

			_trackedPosition = position;

			if (_trackedPosition != null)
				_trackedPosition.PropertyChanged += OnTrackedPositionPropertyChanged;
		}
	}

	private void ResetTradeTracking()
	{
		lock (_tradeLock)
		{
			_tradeSide = 0;
			_currentTradeMaxOpenPnl = 0m;
			_lastTradeMaxOpenPnl = null;
		}

		var position = TradingManager?.Position;
		var tracked = IsChartPosition(position) ? position : null;
		AttachPosition(tracked);

		if (tracked != null)
			UpdateTrade(tracked);
	}

	private void UpdateTrade(Position position)
	{
		// The position's own PnL is used, not Portfolio.OpenPnL, which includes other positions of the account.
		var side = position.IsInPosition ? Math.Sign(position.Volume) : 0;
		var pnl = position.UnrealizedPnL;
		var changed = false;

		lock (_tradeLock)
		{
			if (side != _tradeSide)
			{
				// Close (side -> 0) or reversal (side -> -side) ends the current trade.
				if (_tradeSide != 0)
					_lastTradeMaxOpenPnl = _currentTradeMaxOpenPnl;

				// Opening or reversing starts a new trade.
				if (side != 0)
					_currentTradeMaxOpenPnl = pnl;

				_tradeSide = side;
				changed = true;
			}
			else if (side != 0 && pnl > _currentTradeMaxOpenPnl)
			{
				_currentTradeMaxOpenPnl = pnl;
				changed = true;
			}
		}

		if (changed && (ShowMaxOpenPnL || ShowLastTradeMaxOpenPnL))
			RedrawChart();
	}

	private sealed record DisplayLine(string Label, string Value, decimal? RawForColoring);

	private List<DisplayLine> BuildLines(Portfolio p)
	{
		var lines = new List<DisplayLine>();

		if (ShowAccountId)
			lines.Add(new("Account", p.AccountID, null));

		if (ShowCurrency && p.Currency.HasValue)
			lines.Add(new("Currency", p.Currency.Value.ToString(), null));

		if (ShowBalance)
			lines.Add(new("Balance", FormatCurrency(p.Balance), null));

		if (ShowAvailableBalance && p.BalanceAvailable.HasValue)
			lines.Add(new("Available", FormatCurrency(p.BalanceAvailable.Value), null));

		if (ShowMargin)
			lines.Add(new("Blocked Margin", FormatCurrency(p.BlockedMargin), null));

		if (ShowLeverage && p.Leverage != 1)
			lines.Add(new("Leverage", $"{p.Leverage:F2}x", null));

		if (ShowOpenPnL)
			lines.Add(new("Open PnL", FormatCurrency(p.OpenPnL), p.OpenPnL));

		if (ShowMaxOpenPnL || ShowLastTradeMaxOpenPnL)
		{
			int side;
			decimal currentMax;
			decimal? lastMax;

			lock (_tradeLock)
			{
				side = _tradeSide;
				currentMax = _currentTradeMaxOpenPnl;
				lastMax = _lastTradeMaxOpenPnl;
			}

			if (ShowMaxOpenPnL && side != 0)
				lines.Add(new("Max open PnL", FormatCurrency(currentMax), currentMax));

			if (ShowLastTradeMaxOpenPnL && side == 0 && lastMax.HasValue)
				lines.Add(new("Last trade max PnL", FormatCurrency(lastMax.Value), lastMax.Value));
		}

		if (ShowClosedPnL)
			lines.Add(new("Closed PnL", FormatCurrency(p.ClosedPnL), p.ClosedPnL));

		if (ShowTotalPnL)
			// Session-level total: OpenPnL + ClosedPnL (current session) - NOT
			// Portfolio.TotalPnL, which uses TotalClosedPnL (cumulative across sessions).
			lines.Add(new("Total PnL", FormatCurrency(p.ClosedPnL + p.OpenPnL), p.ClosedPnL + p.OpenPnL));

		return lines;
	}

	private System.Drawing.Color ColorFor(decimal? raw)
		=> raw is null ? _textColor
		 : raw > 0m ? _positiveColor
		 : raw < 0m ? _negativeColor
					   : _neutralColor;

	private void DrawColoredText(RenderContext context, List<DisplayLine> lines,
		Rectangle textRect, int maxLabelWidth)
	{
		var lineHeight = context.MeasureString("A", _font).Height;

		// Calculate value column position
		var valueColumnX = textRect.X + maxLabelWidth + ColumnSpacing;
		var currentY = textRect.Y;

		// Draw lines
		foreach (var line in lines)
		{
			context.DrawString(line.Label, _font, _textColor, textRect.X, currentY);
			context.DrawString(line.Value, _font, ColorFor(line.RawForColoring), valueColumnX, currentY);
			currentY += lineHeight;
		}
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
