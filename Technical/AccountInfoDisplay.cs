namespace ATAS.Indicators.Technical;

using ATAS.DataFeedsCore;
using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;

/// <summary>
/// Displays account information on the chart including account ID, balance, blocked margin, available balance, and PnL.
/// </summary>
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000648751-account-info-display")]
[Category(IndicatorCategories.Trading)]
[DisplayName("Account Info Display Modif")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.AccountInfoDisplayDescription))]
public class AccountInfoDisplay : Indicator
{
    #region class
    private sealed class DisplayRow
    {
        public string Label { get; }
        public string ValueText { get; }

        // Not used yet in Phase 0 (kept for Phase 1/2).
        public decimal? NumericValue { get; }
        public CrossColor? ValueColorOverride { get; }

        public DisplayRow(string label, string valueText, decimal? numericValue = null, CrossColor? valueColorOverride = null)
        {
            Label = label ?? string.Empty;
            ValueText = valueText ?? string.Empty;
            NumericValue = numericValue;
            ValueColorOverride = valueColorOverride;
        }
    }

    private sealed class TrailingDdState
    {
        public decimal StartEquity { get; set; }
        public decimal PeakEquity { get; set; }
        public decimal MaxOpenPnL { get; set; }
        public DateTime InitializedAtUtc { get; set; }

        public bool IsInitialized { get; set; }
    }
    #endregion

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

    private TrailingDdState _trailingState = new();

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

    #region Trailing Drawdown Settings

    public enum TrailingInitMode
    {
        AutoFromCurrentEquity = 0,
        ManualStopEquity = 1
    }

    [Display(
        Name = "Enable Trailing Drawdown",
        Description = "Enable trailing drawdown tracking based on peak equity.",
        GroupName = "Funding / Trailing DD",
        Order = 10
    )]
    public bool EnableTrailingDrawdown { get; set; } = true;

    [Display(
        Name = "Max Trailing Drawdown",
        Description = "Maximum allowed trailing drawdown from peak equity (absolute currency amount).",
        GroupName = "Funding / Trailing DD",
        Order = 11
    )]
    [Range(0, 1_000_000)]
    public decimal MaxTrailingDrawdown { get; set; } = 2500m;

    [Display(
        Name = "Initialization Mode",
        Description = "How the trailing drawdown state is initialized.",
        GroupName = "Funding / Trailing DD",
        Order = 12
    )]
    public TrailingInitMode InitializationMode { get; set; } = TrailingInitMode.AutoFromCurrentEquity;

    [Display(
        Name = "Manual Stop Equity",
        Description = "Current liquidation/stop equity level (bootstrap). Peak is derived as Stop + Max DD.",
        GroupName = "Funding / Trailing DD",
        Order = 13
    )]
    [Range(-1_000_000, 10_000_000)]
    public decimal ManualStopEquity { get; set; } = 0m;

    [Display(
     Name = "Reinitialize Now",
     Description = "Forces trailing drawdown state re-initialization on next render.",
     GroupName = "Funding / Trailing DD",
     Order = 14
 )]
    public bool ReinitializeNow
    {
        get => false; // always show unchecked (button-like)
        set
        {
            if (!value)
                return;

            _reinitializeNow = true;
            RedrawChart();
        }
    }

    private bool _reinitializeNow;

    #endregion

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

        var portfolio = _currentPortfolio ?? TradingManager?.Portfolio;
        if (portfolio == null)
            return;

        // Phase 2: equity and trailing DD state
        var equity = portfolio.Balance + portfolio.OpenPnL;

        InitializeTrailingState(portfolio, equity);
        UpdateTrailingState(portfolio, equity);

        var rows = BuildRows(portfolio, equity); // updated signature
        if (rows == null || rows.Count == 0)
            return;

        var lineHeight = context.MeasureString("A", _font).Height;

        var maxLabelWidth = 0;
        var maxValueWidth = 0;

        foreach (var row in rows)
        {
            var labelWidth = (int)context.MeasureString(row.Label, _font).Width;
            var valueWidth = (int)context.MeasureString(row.ValueText, _font).Width;

            if (labelWidth > maxLabelWidth)
                maxLabelWidth = labelWidth;

            if (valueWidth > maxValueWidth)
                maxValueWidth = valueWidth;
        }

        var padding = 10;
        var rectWidth = maxLabelWidth + ColumnSpacing + maxValueWidth + padding * 2;
        var rectHeight = rows.Count * lineHeight + padding * 2;

        var x = CalculateXPosition(rectWidth);
        var y = CalculateYPosition(rectHeight);

        var rectangle = new Rectangle(x, y, rectWidth, rectHeight);
        context.FillRectangle(_backgroundColor, rectangle);

        context.DrawRectangle(new RenderPen(Color.Gray, 1), rectangle);

        var textRect = new Rectangle(
            x + padding,
            y + padding,
            rectWidth - padding * 2,
            rectHeight - padding * 2
        );

        DrawColoredRows(context, rows, textRect, portfolio, maxLabelWidth);
    }

    #endregion

    #region Private Methods

    private void OnPortfolioSelected(Portfolio portfolio)
	{
		_currentPortfolio = portfolio;
        ResetTrailingState();
        RedrawChart();
	}

    private List<DisplayRow> BuildRows(Portfolio portfolio, decimal equity)
    {
        var rows = new List<DisplayRow>();

        if (portfolio == null)
            return rows;

        // --- Existing rows (unchanged) ---
        if (ShowAccountId)
            rows.Add(new DisplayRow("Account", portfolio.AccountID));

        if (ShowCurrency && portfolio.Currency.HasValue)
            rows.Add(new DisplayRow("Currency", portfolio.Currency.Value.ToString()));

        if (ShowBalance)
            rows.Add(new DisplayRow("Balance", FormatCurrency(portfolio.Balance)));

        if (ShowAvailableBalance && portfolio.BalanceAvailable.HasValue)
            rows.Add(new DisplayRow("Available", FormatCurrency(portfolio.BalanceAvailable.Value)));

        if (ShowMargin)
            rows.Add(new DisplayRow("Blocked Margin", FormatCurrency(portfolio.BlockedMargin)));

        if (ShowLeverage && portfolio.Leverage != 1)
            rows.Add(new DisplayRow("Leverage", $"{portfolio.Leverage:F2}x"));

        if (ShowOpenPnL)
            rows.Add(new DisplayRow("Open PnL", FormatCurrency(portfolio.OpenPnL), numericValue: portfolio.OpenPnL));

        if (ShowClosedPnL)
            rows.Add(new DisplayRow("Closed PnL", FormatCurrency(portfolio.ClosedPnL), numericValue: portfolio.ClosedPnL));

        if (ShowTotalPnL)
        {
            var totalPnL = portfolio.ClosedPnL + portfolio.OpenPnL;
            rows.Add(new DisplayRow("Total PnL", FormatCurrency(totalPnL), numericValue: totalPnL));
        }

        // --- Trailing Drawdown block (Phase 2) ---
        if (EnableTrailingDrawdown && _trailingState.IsInitialized)
        {
            var stopEquity = _trailingState.PeakEquity - MaxTrailingDrawdown;
            var currentDd = _trailingState.PeakEquity - equity;
            var remainingDd = equity - stopEquity;

            rows.Add(new DisplayRow("Equity", FormatCurrency(equity)));
            rows.Add(new DisplayRow("Start Equity", FormatCurrency(_trailingState.StartEquity)));
            rows.Add(new DisplayRow("Peak Equity", FormatCurrency(_trailingState.PeakEquity)));
            rows.Add(new DisplayRow("Stop Equity", FormatCurrency(stopEquity)));

            // Remaining DD: positive => safe (green), negative => breached (red)
            rows.Add(new DisplayRow("Remaining DD", FormatCurrency(remainingDd), numericValue: remainingDd));

            // Current DD: show as positive magnitude but color should reflect "bad when larger".
            // We keep numericValue as negative magnitude so existing color rules (pos=green, neg=red) still work.
            rows.Add(new DisplayRow("Current DD", FormatCurrency(currentDd), numericValue: -currentDd));

            rows.Add(new DisplayRow("Max Open PnL", FormatCurrency(_trailingState.MaxOpenPnL), numericValue: _trailingState.MaxOpenPnL));
        }

        return rows;
    }

    private void DrawColoredRows(RenderContext context, List<DisplayRow> rows, Rectangle textRect, Portfolio portfolio, int maxLabelWidth)
    {
        var lineHeight = context.MeasureString("A", _font).Height;

        // Calculate value column position
        var valueColumnX = textRect.X + maxLabelWidth + ColumnSpacing;
        var currentY = textRect.Y;

        foreach (var row in rows)
        {
            var label = row?.Label ?? string.Empty;
            var valueStr = row?.ValueText ?? string.Empty;

            // Draw label
            context.DrawString(label, _font, _textColor, textRect.X, currentY);

            // Determine color for numeric values (PnL/DD rows are provided via NumericValue)
            var valueColor = _textColor;

            if (row.NumericValue.HasValue)
            {
                var value = row.NumericValue.Value;
                valueColor = value > 0
                    ? _positiveColor
                    : (value < 0 ? _negativeColor : _neutralColor);
            }

            // Draw value
            context.DrawString(valueStr, _font, valueColor, valueColumnX, currentY);

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

    #region Trailing Drawdown Helpers

    private void ResetTrailingState()
    {
        _trailingState = new TrailingDdState();
        _reinitializeNow = false;
    }

    private void InitializeTrailingState(Portfolio portfolio, decimal equity)
    {
        if (!EnableTrailingDrawdown)
            return;


        // One-shot init unless forced.
        if (_trailingState.IsInitialized && !_reinitializeNow)
            return;

        _trailingState.IsInitialized = true;
        _trailingState.InitializedAtUtc = DateTime.UtcNow;

        _trailingState.StartEquity = equity;

        if (InitializationMode == TrailingInitMode.ManualStopEquity && MaxTrailingDrawdown > 0m)
        {
            // Bootstrap using user-provided stop equity:
            // PeakEquity is derived so that: StopEquity = PeakEquity - MaxDD == ManualStopEquity
            _trailingState.PeakEquity = ManualStopEquity + MaxTrailingDrawdown;
        }
        else
        {
            // Default: start from current equity
            _trailingState.PeakEquity = equity;
        }

        _trailingState.MaxOpenPnL = portfolio.OpenPnL;

        // Consume the pulse
        _reinitializeNow = false;
    }

    private void UpdateTrailingState(Portfolio portfolio, decimal equity)
    {
        if (!EnableTrailingDrawdown || !_trailingState.IsInitialized)
            return;

        if (equity > _trailingState.PeakEquity)
            _trailingState.PeakEquity = equity;

        if (portfolio.OpenPnL > _trailingState.MaxOpenPnL)
            _trailingState.MaxOpenPnL = portfolio.OpenPnL;
    }

    #endregion

    #endregion
}
