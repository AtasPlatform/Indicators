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
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Displays account information on the chart including account ID, balance, blocked margin, available balance, and PnL.
/// </summary>
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000648751-account-info-display")]
[Category(IndicatorCategories.Trading)]
[DisplayName("Account Info Display")]
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
        public bool IsInitialized { get; set; }

        public decimal StartEquity { get; set; }
        public decimal PeakEquity { get; set; }

        // Phase 1: per-trade max open pnl (NOT historical)
        public decimal TradeMaxOpenPnL { get; set; }           
        public decimal LastTradeMaxOpenPnL { get; set; }       // Max OpenPnL since entry (last closed trade)
        public bool WasPositionOpen { get; set; }              // Lifecycle flag for FLAT<->OPEN transitions

        public DateTime InitializedAtUtc { get; set; }

        // Per-account config
        public bool EnableTrailingDrawdown { get; set; }
        public decimal MaxTrailingDrawdown { get; set; }
        public TrailingInitMode InitializationMode { get; set; }
        public decimal ManualStopEquity { get; set; }

        // Phase 4: EOD engine (per-account)
        public TrailingPeakUpdateMode PeakUpdateMode { get; set; }
        public TimeSpan EodTimeLocal { get; set; }

        // Marker to ensure we capture EOD only once per local day
        public DateTime LastEodCaptureDate { get; set; } // Date component only

        // Reset policy (per-account)
        public bool EnableMonthlyReset { get; set; }
        public int MonthlyResetDay { get; set; }   // 1..31 (validaremos)

        // Reset markers (per-account)
        public int LastMonthlyResetKey { get; set; } // yyyymm, e.g. 202512

        // Phase 5-2: Daily Rails (per-account)
        public bool EnableDailyLossLimit { get; set; }
        public decimal DailyLossLimit { get; set; }
        public DailyLossModeKind DailyLossMode { get; set; }
        public bool EnableDailyProfitCap { get; set; }
        public decimal DailyProfitCap { get; set; } // FromSessionStart only (equity - DailyStartEquity)

        public DailyResetModeKind DailyResetMode { get; set; }
        public TimeSpan DailyResetTimeLocal { get; set; } // used only when DailyResetMode == LocalCustomTime

        public int LastDailyResetKey { get; set; } // yyyymmdd based on trading day key (NY 17:00)
        public decimal DailyStartEquity { get; set; }
        public decimal DailyPeakEquity { get; set; }
        public decimal TradeClosedPnlBaseline { get; set; }
        public decimal LastClosedTradePnL { get; set; }

        // --- Phase 5-3.1: Session behavior metrics (per account) ---

        // Baseline to compute realized PnL "today" as (ClosedPnL - DailyClosedPnlBaseline).
        public decimal DailyClosedPnlBaseline { get; set; }

        // Counters for the current trading day (reset with the daily reset key).
        public int TradesToday { get; set; }
        public int WinsToday { get; set; }
        public int LossesToday { get; set; }

        // Current streak tracking (mutually exclusive).
        public int CurrentWinStreak { get; set; }
        public int CurrentLossStreak { get; set; }

        // --- Phase 5-3.2: soft recommendations (per account) ---

        public bool EnableSoftRecommendations { get; set; } = true;

        public int MaxTradesPerDay { get; set; } = 3;
        public int MaxConsecutiveLosses { get; set; } = 2;

    }

    private sealed class SuggestionResult
    {
        public SuggestedStatus Status { get; init; }
        public string ReasonsText { get; init; } = string.Empty;
    }

    #region Persistence DTO

    private sealed class PersistedRootV1
    {
        public int SchemaVersion { get; set; } = _schemaVersion;
        public string UpdatedAtUtc { get; set; } = DateTime.UtcNow.ToString("O");

        // Key: accountKey (AccountID or fallback)
        public Dictionary<string, PersistedAccountV1> Accounts { get; set; } = new();
    }

    private sealed class PersistedAccountV1
    {
        public PersistedConfigV1 Config { get; set; } = new();
        public PersistedRuntimeV1 Runtime { get; set; } = new();
    }

    private sealed class PersistedConfigV1
    {
        public bool EnableTrailingDrawdown { get; set; }
        public decimal MaxTrailingDrawdown { get; set; }
        public int InitializationMode { get; set; }            // enum as int
        public decimal ManualStopEquity { get; set; }

        public int PeakUpdateMode { get; set; }                // enum as int
        public string EodTimeLocal { get; set; }               // "HH:mm:ss"

        public bool EnableMonthlyReset { get; set; }
        public int MonthlyResetDay { get; set; }

        // Daily Rails (config)
        public bool EnableDailyLossLimit { get; set; }
        public decimal DailyLossLimit { get; set; }
        public int DailyLossMode { get; set; }          // enum as int

        public bool EnableDailyProfitCap { get; set; }
        public decimal DailyProfitCap { get; set; }     // from day start only

        public int DailyResetMode { get; set; }         // enum as int
        public string DailyResetTimeLocal { get; set; } // "HH:mm:ss"

        // Phase 5-3.2 (config)
        public bool? EnableSoftRecommendations { get; set; }
        public int? MaxTradesPerDay { get; set; }
        public int? MaxConsecutiveLosses { get; set; }
    }

    private sealed class PersistedRuntimeV1
    {
        public bool IsInitialized { get; set; }
        public decimal StartEquity { get; set; }
        public decimal PeakEquity { get; set; }
        public decimal MaxOpenPnL { get; set; } // legacy v0/v1; kept for backward compatibility (no longer used)

        public string InitializedAtUtc { get; set; }           // "O"
        public string LastEodCaptureDate { get; set; }         // "yyyy-MM-dd" (date only)

        public int LastMonthlyResetKey { get; set; }           // yyyymm

        // Daily Rails (runtime)
        public int LastDailyResetKey { get; set; }      // yyyymmdd key
        public decimal DailyStartEquity { get; set; }
        public decimal DailyPeakEquity { get; set; }

        // Phase 5-1 (runtime) - trade
        public decimal LastTradeMaxOpenPnL { get; set; }
        public decimal LastClosedTradePnL { get; set; }

        // Phase 5-3.1 (runtime)
        public decimal DailyClosedPnlBaseline { get; set; }
        public int TradesToday { get; set; }
        public int WinsToday { get; set; }
        public int LossesToday { get; set; }
        public int CurrentWinStreak { get; set; }
        public int CurrentLossStreak { get; set; }
    }

    #endregion

    #region Live position tracker

    private sealed class PositionSnapshot
    {
        public bool IsOpen { get; set; }
        public OrderDirections Direction { get; set; }
        public decimal Volume { get; set; }           // absolute contracts
        public decimal AvgEntryPrice { get; set; }
        public DateTime OpenTime { get; set; }
        public string SecurityCode { get; set; }
        public string AccountId { get; set; }
    }
    #endregion

    #endregion

    #region Fields

    private Rectangle? _lastPanelRect;
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

    private readonly Dictionary<string, TrailingDdState> _trailingStatesByAccount = new();
    private string _activeAccountKey;

    private bool _defaultEnableTrailingDrawdown = true;
    private TrailingInitMode _defaultInitializationMode = TrailingInitMode.AutoFromCurrentEquity;
    private decimal _defaultManualStopEquity = 0m;

    private TrailingPeakUpdateMode _defaultPeakUpdateMode = TrailingPeakUpdateMode.Realtime;

    // Bulenox default: 17:00 CT (user can change depending on local timezone)
    private TimeSpan _defaultEodTimeLocal = new(17, 0, 0);

    private bool _defaultEnableMonthlyReset = true;
    private int _defaultMonthlyResetDay = 1;

    //Daily rails
    private bool _enableDailyLossLimit;
    private decimal _dailyLossLimit;
    private DailyLossModeKind _dailyLossMode;

    private bool _enableDailyProfitCap;
    private decimal _dailyProfitCap;

    private DailyResetModeKind _dailyResetMode = DailyResetModeKind.NewYork1700;
    private TimeSpan _dailyResetTimeLocal = new TimeSpan(17, 0, 0);

    //Toggles
    private const string _rowsGroupTrailingDd = "Rows / Trailing DD";
    private const string _rowsGroupTrade = "Rows / Trade";
    private const string _rowsGroupPosition = "Rows / Position";
    private const string _rowsGroupDailyRails = "Rows / Daily Rails";
    private const string _rowsGroupSessionMetrics = "Rows / Session Metrics";
    private const string _groupSoftRecs = "Behavior / Soft Recommendations";
    private const string _rowsGroupRecommendations = "Rows / Recommendations";

    #endregion

    #region Persistence

    private const int _schemaVersion = 1;
    private const string _stateFileName = "AccountInfoDisplay.states.v1.json";

    private string _stateFilePath;

    private PersistedRootV1 _persistedRoot;

    // Dirty + throttle
    private bool _isDirty;
    private DateTime _lastSaveUtc = DateTime.MinValue;
    private readonly TimeSpan _minSaveInterval = TimeSpan.FromSeconds(10);

    // Fingerprint to skip redundant disk writes
    private string _lastSavedHash = string.Empty;

    // Json options
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    #endregion

    #region Live position tracker

    // Trade rebuild buffers (per active account+symbol)
    private PositionSnapshot _posSnapshot = new();

    #endregion

    #region Properties

    // ==============================
    // Visualization (Look & Feel)
    // ==============================

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BackGround),
        Description = nameof(Strings.LabelFillColorDescription), GroupName = nameof(Strings.Visualization), Order = 10)]
    public CrossColor BackgroundColor
    {
        get => _backgroundColor.Convert();
        set
        {
            _backgroundColor = value.Convert();
            _lastPanelRect = null;
            RedrawChart();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TextColor),
        Description = nameof(Strings.LabelTextColorDescription), GroupName = nameof(Strings.Visualization), Order = 11)]
    public CrossColor TextColor
    {
        get => _textColor.Convert();
        set
        {
            _textColor = value.Convert();
            _lastPanelRect = null;
            RedrawChart();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.PositiveColor),
        Description = nameof(Strings.PositiveColorDescription), GroupName = nameof(Strings.Visualization), Order = 12)]
    public CrossColor PositiveColor
    {
        get => _positiveColor.Convert();
        set
        {
            _positiveColor = value.Convert();
            _lastPanelRect = null;
            RedrawChart();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.NegativeColor),
        Description = nameof(Strings.NegativeColorDescription), GroupName = nameof(Strings.Visualization), Order = 13)]
    public CrossColor NegativeColor
    {
        get => _negativeColor.Convert();
        set
        {
            _negativeColor = value.Convert();
            _lastPanelRect = null;
            RedrawChart();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.NeutralColor),
        Description = nameof(Strings.NeutralColorDescription), GroupName = nameof(Strings.Visualization), Order = 14)]
    public CrossColor NeutralColor
    {
        get => _neutralColor.Convert();
        set
        {
            _neutralColor = value.Convert();
            _lastPanelRect = null;
            RedrawChart();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.FontSize),
        Description = nameof(Strings.FontSizeDescription), GroupName = nameof(Strings.Visualization), Order = 15)]
    [Range(6, 30)]
    public float FontSize
    {
        get => _font.Size;
        set
        {
            if (Math.Abs(_font.Size - value) < 0.001f)
                return;

            _font = new RenderFont("Arial", value);

            // Force full panel redraw and avoid using stale previous rect.
            _lastPanelRect = null;
            RedrawChart();
        }
    }

    // ==============================
    // Layout (Panel placement)
    // ==============================

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.HorizontalPosition),
        Description = "Panel horizontal alignment on the chart.", GroupName = nameof(Strings.LayoutGroup), Order = 20)]
    public HorizontalAlignment HorizontalPosition { get; set; } = HorizontalAlignment.Left;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.VerticalPosition),
        Description = "Panel vertical alignment on the chart.", GroupName = nameof(Strings.LayoutGroup), Order = 21)]
    public VerticalAlignment VerticalPosition { get; set; } = VerticalAlignment.Bottom;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.OffsetX),
        Description = nameof(Strings.OffsetXDescription), GroupName = nameof(Strings.LayoutGroup), Order = 22)]
    [Range(0, 1000)]
    public int OffsetX { get; set; } = 20;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.OffsetY),
        Description = nameof(Strings.OffsetYDescription), GroupName = nameof(Strings.LayoutGroup), Order = 23)]
    [Range(0, 1000)]
    public int OffsetY { get; set; } = 20;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ColumnSpacing),
        Description = nameof(Strings.ColumnSpacingDescription), GroupName = nameof(Strings.LayoutGroup), Order = 24)]
    [Range(5, 50)]
    public int ColumnSpacing { get; set; } = 15;

    // ==============================
    // Rows (Core upstream toggles)
    // Keep as-is for upstream compatibility
    // ==============================

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowAccountId),
        Description = nameof(Strings.ShowAccountIdDescription), GroupName = nameof(Strings.Settings), Order = 30)]
    public bool ShowAccountId { get; set; } = true;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowCurrency),
        Description = nameof(Strings.ShowCurrencyDescription), GroupName = nameof(Strings.Settings), Order = 31)]
    public bool ShowCurrency { get; set; } = true;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowBalance),
        Description = nameof(Strings.ShowBalanceDescription), GroupName = nameof(Strings.Settings), Order = 32)]
    public bool ShowBalance { get; set; } = true;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowAvailableBalance),
        Description = nameof(Strings.ShowAvailableBalanceDescription), GroupName = nameof(Strings.Settings), Order = 33)]
    public bool ShowAvailableBalance { get; set; } = true;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowBlockedMargin),
        Description = nameof(Strings.ShowBlockedMarginDescription), GroupName = nameof(Strings.Settings), Order = 34)]
    public bool ShowMargin { get; set; } = false;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowLeverage),
        Description = nameof(Strings.ShowLeverageDescription), GroupName = nameof(Strings.Settings), Order = 35)]
    public bool ShowLeverage { get; set; } = true;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowOpenPnL),
        Description = nameof(Strings.ShowOpenPnLDescription), GroupName = nameof(Strings.Settings), Order = 36)]
    public bool ShowOpenPnL { get; set; } = true;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowClosedPnL),
        Description = nameof(Strings.ShowClosedPnLDescription), GroupName = nameof(Strings.Settings), Order = 37)]
    public bool ShowClosedPnL { get; set; } = true;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowTotalPnL),
        Description = nameof(Strings.ShowTotalPnLDescription), GroupName = nameof(Strings.Settings), Order = 38)]
    public bool ShowTotalPnL { get; set; } = false;

    // ==============================
    // Rows (New granular toggles — literal strings)
    // ==============================

    // Trailing DD rows (Group: Rows / Trailing DD)
    [Display(Name = "Show Start Equity",
        Description = "Shows the equity captured when trailing drawdown was initialized for this account.",
        GroupName = _rowsGroupTrailingDd, Order = 41)]
    public bool ShowStartEquityRow { get; set; } = true;

    [Display(Name = "Show Peak Equity",
        Description = "Shows the highest equity recorded according to the selected peak update mode.",
        GroupName = _rowsGroupTrailingDd, Order = 42)]
    public bool ShowPeakEquityRow { get; set; } = true;

    [Display(Name = "Show Stop Equity",
        Description = "Shows the trailing stop equity (Peak Equity minus Max Trailing Drawdown).",
        GroupName = _rowsGroupTrailingDd, Order = 43)]
    public bool ShowStopEquityRow { get; set; } = true;

    [Display(Name = "Show Remaining DD",
        Description = "Shows how much equity can still be lost before the trailing stop is reached. Positive = safe, negative = breached.",
        GroupName = _rowsGroupTrailingDd, Order = 44)]
    public bool ShowRemainingDdRow { get; set; } = true;

    [Display(Name = "Show Current DD",
        Description = "Shows the current drawdown from Peak Equity. The value increases as drawdown worsens.",
        GroupName = _rowsGroupTrailingDd, Order = 45)]
    public bool ShowCurrentDdRow { get; set; } = true;

    // Trade metrics rows (Group: Rows / Trade)
    [Display(Name = "Show Trade Max Open PnL (Current)",
        Description = "Shows the maximum unrealized profit reached during the currently open trade (peak Open PnL while the position is open).",
        GroupName = _rowsGroupTrade, Order = 50)]
    public bool ShowTradeMaxOpenPnlCurrentRow { get; set; } = true;

    [Display(Name = "Show Trade Max Open PnL (Last)",
        Description = "Shows the maximum unrealized profit reached during the previous trade. The value is frozen at trade close and displayed while flat.",
        GroupName = _rowsGroupTrade, Order = 51)]
    public bool ShowTradeMaxOpenPnlLastRow { get; set; } = true;

    [Display(Name = "Show Last Closed Trade PnL",
        Description = "Shows the realized PnL of the last completed trade, calculated as the change in Closed PnL between trade open and trade close.",
        GroupName = _rowsGroupTrade, Order = 52)]
    public bool ShowLastClosedTradePnlRow { get; set; } = true;

    // Position rows (Group: Rows / Position)
    [Display(Name = "Show Position Snapshot",
        Description = "Shows position direction, quantity, and entry price while a position is open.",
        GroupName = _rowsGroupPosition, Order = 60)]
    public bool ShowPositionSnapshot { get; set; } = true;

    [Display(Name = "Show FLAT Row",
        Description = "Shows a 'FLAT' status row when there is no open position.",
        GroupName = _rowsGroupPosition, Order = 61)]
    public bool ShowFlatRow { get; set; } = true;

    // Daily rails rows (Group: Rows / Daily Rails)
    [Display(Name = "Show Daily Start Equity",
        Description = "Shows the equity captured at the daily reset time. Daily PnL is calculated from this value.",
        GroupName = _rowsGroupDailyRails, Order = 70)]
    public bool ShowDailyStartEquityRow { get; set; } = true;

    [Display(Name = "Show Daily PnL",
        Description = "Shows current daily PnL as (Equity - Daily Start Equity).",
        GroupName = _rowsGroupDailyRails, Order = 71)]
    public bool ShowDailyPnlRow { get; set; } = true;

    [Display(Name = "Show Daily Loss Base",
        Description = "Shows the equity reference used to compute the daily loss stop (daily start or intraday peak, depending on mode).",
        GroupName = _rowsGroupDailyRails, Order = 72)]
    public bool ShowDailyLossBaseRow { get; set; } = true;

    [Display(Name = "Show Daily Stop Equity",
        Description = "Shows the daily loss stop equity (Loss Base minus Daily Loss Limit).",
        GroupName = _rowsGroupDailyRails, Order = 73)]
    public bool ShowDailyStopEquityRow { get; set; } = true;

    [Display(Name = "Show Remaining Daily Loss",
        Description = "Shows how much equity can still be lost today before the daily loss stop is reached. Positive = safe, negative = breached.",
        GroupName = _rowsGroupDailyRails, Order = 74)]
    public bool ShowRemainingDailyLossRow { get; set; } = true;

    [Display(Name = "Show Daily Profit Cap",
        Description = "Shows the configured daily profit cap amount (non-trailing).",
        GroupName = _rowsGroupDailyRails, Order = 75)]
    public bool ShowDailyProfitCapRow { get; set; } = true;

    [Display(Name = "Show Remaining to Profit Cap",
        Description = "Shows how much profit remains until the daily profit cap is reached. Zero or below means the cap has been met.",
        GroupName = _rowsGroupDailyRails, Order = 76)]
    public bool ShowRemainingToProfitCapRow { get; set; } = true;

    // Session metrics rows (Group: Rows / Session Metrics)
    [Display(Name = "Show Trades Today",
        Description = "Shows the number of closed trades during the current trading day (based on the daily reset key).",
        GroupName = _rowsGroupSessionMetrics, Order = 80)]
    public bool ShowTradesTodayRow { get; set; } = true;

    [Display(Name = "Show Wins / Losses Today",
        Description = "Shows how many winning and losing trades were closed during the current trading day.",
        GroupName = _rowsGroupSessionMetrics, Order = 81)]
    public bool ShowWinsLossesTodayRow { get; set; } = true;

    [Display(Name = "Show Current Streak",
        Description = "Shows the current consecutive win/loss streak based on last closed trades (e.g., W3 or L2).",
        GroupName = _rowsGroupSessionMetrics, Order = 82)]
    public bool ShowCurrentStreakRow { get; set; } = true;

    [Display(Name = "Show Realized PnL Today",
        Description = "Shows realized PnL for the current trading day as (Closed PnL - DailyClosedPnLBaseline).",
        GroupName = _rowsGroupSessionMetrics, Order = 83)]
    public bool ShowRealizedPnlTodayRow { get; set; } = true;

    [Display(Name = "Show Suggested Status",
    Description = "Shows the display-only suggested status (OK/STOP) based on configured soft recommendation rules.",
    GroupName = _rowsGroupRecommendations, Order = 90)]
    public bool ShowSuggestedStatusRow { get; set; } = true;

    [Display(Name = "Show Stop Reasons",
        Description = "Shows which rules triggered STOP (compact list).",
        GroupName = _rowsGroupRecommendations, Order = 91)]
    public bool ShowStopReasonsRow { get; set; } = true;


    // ==============================
    // Funding / Trailing DD (Per-account defaults)
    // ==============================

    #region Trailing Drawdown Settings

    [Display(
        Name = "Enable Trailing Drawdown",
        Description = "Enable trailing drawdown tracking based on peak equity.",
        GroupName = "Funding / Trailing DD",
        Order = 100
    )]
    public bool DefaultEnableTrailingDrawdown
    {
        get => _defaultEnableTrailingDrawdown;
        set
        {
            _defaultEnableTrailingDrawdown = value;

            var state = TryGetActiveState();
            if (state != null)
                state.EnableTrailingDrawdown = value;

            TouchActiveAccountAndScheduleSave(force: false);
            RedrawChart();
        }
    }

    private decimal _maxTrailingDrawdownDefault = 2500m;

    [Display(
        Name = "Max Trailing Drawdown",
        Description = "Maximum allowed trailing drawdown from peak equity (absolute currency amount).",
        GroupName = "Funding / Trailing DD",
        Order = 101
    )]
    [Range(0, 1_000_000)]
    public decimal DefaultMaxTrailingDrawdown
    {
        get => _maxTrailingDrawdownDefault;
        set
        {
            _maxTrailingDrawdownDefault = value;

            var state = TryGetActiveState();
            if (state != null)
                state.MaxTrailingDrawdown = value;

            TouchActiveAccountAndScheduleSave(force: false);
            RedrawChart();
        }
    }

    [Display(
        Name = "Initialization Mode",
        Description = "Defines how Start/Peak equity are initialized (manual stop equity or current equity).",
        GroupName = "Funding / Trailing DD",
        Order = 102
    )]
    public TrailingInitMode DefaultInitializationMode
    {
        get => _defaultInitializationMode;
        set
        {
            _defaultInitializationMode = value;

            var state = TryGetActiveState();
            if (state != null)
                state.InitializationMode = value;

            TouchActiveAccountAndScheduleSave(force: false);
            RedrawChart();
        }
    }

    [Display(
        Name = "Manual Stop Equity",
        Description = "Bootstrap stop equity for funded accounts. Peak is derived as (Stop Equity + Max Trailing Drawdown).",
        GroupName = "Funding / Trailing DD",
        Order = 103
    )]
    [Range(-1_000_000, 10_000_000)]
    public decimal DefaultManualStopEquity
    {
        get => _defaultManualStopEquity;
        set
        {
            _defaultManualStopEquity = value;

            var state = TryGetActiveState();
            if (state != null)
                state.ManualStopEquity = value;

            TouchActiveAccountAndScheduleSave(force: false);
            RedrawChart();
        }
    }

    [Display(
        Name = "Reinitialize Now",
        Description = "Forces trailing drawdown state re-initialization on next render.",
        GroupName = "Funding / Trailing DD",
        Order = 104
    )]
    public bool ReinitializeNow
    {
        get => false;
        set
        {
            if (!value)
                return;

            _reinitializeNow = true;
            RedrawChart();
        }
    }

    private bool _reinitializeNow;

    [Display(
        Name = "Peak Update Mode",
        Description = "Realtime updates Peak Equity continuously; EndOfDay updates Peak Equity only once per day at the configured EOD time.",
        GroupName = "Funding / Trailing DD",
        Order = 105
    )]
    public TrailingPeakUpdateMode DefaultPeakUpdateMode
    {
        get => _defaultPeakUpdateMode;
        set
        {
            _defaultPeakUpdateMode = value;

            var state = TryGetActiveState();
            if (state != null)
            {
                state.PeakUpdateMode = value;
                state.LastEodCaptureDate = default;
            }

            TouchActiveAccountAndScheduleSave(force: false);
            RedrawChart();
        }
    }

    [Display(
        Name = "EOD Time (Local)",
        Description = "End-of-day time used when Peak Update Mode is EndOfDay. Uses the PC local clock (DateTime.Now).",
        GroupName = "Funding / Trailing DD",
        Order = 106
    )]
    public TimeSpan DefaultEodTimeLocal
    {
        get => _defaultEodTimeLocal;
        set
        {
            if (value < TimeSpan.Zero)
                value = TimeSpan.Zero;
            if (value >= TimeSpan.FromDays(1))
                value = TimeSpan.FromDays(1) - TimeSpan.FromSeconds(1);

            _defaultEodTimeLocal = value;

            var state = TryGetActiveState();
            if (state != null)
            {
                state.EodTimeLocal = value;
                state.LastEodCaptureDate = default;
            }

            TouchActiveAccountAndScheduleSave(force: false);
            RedrawChart();
        }
    }

    #endregion

    #region Trailing Reset Settings

    [Display(
        Name = "Enable Monthly Reset",
        Description = "Resets trailing drawdown state on a fixed day of each month (per account).",
        GroupName = "Funding / Trailing DD",
        Order = 120
    )]
    public bool DefaultEnableMonthlyReset
    {
        get => _defaultEnableMonthlyReset;
        set
        {
            _defaultEnableMonthlyReset = value;

            var state = TryGetActiveState();
            if (state != null)
                state.EnableMonthlyReset = value;

            TouchActiveAccountAndScheduleSave(force: false);
            RedrawChart();
        }
    }

    [Display(
        Name = "Monthly Reset Day",
        Description = "Day of month when trailing drawdown resets (1–31). If the month has fewer days, the last day is used.",
        GroupName = "Funding / Trailing DD",
        Order = 121
    )]
    [Range(1, 31)]
    public int DefaultMonthlyResetDay
    {
        get => _defaultMonthlyResetDay;
        set
        {
            _defaultMonthlyResetDay = value;

            var state = TryGetActiveState();
            if (state != null)
                state.MonthlyResetDay = value;

            TouchActiveAccountAndScheduleSave(force: false);
            RedrawChart();
        }
    }

    #endregion

    // ==============================
    // Daily Rails (Rules / Controls)
    // ==============================

    #region Daily Rails

    [Display(
        GroupName = "Daily Rails",
        Name = "Enable Daily Loss Limit",
        Description = "Enables the daily loss limit. When enabled, the panel shows the daily stop level and remaining daily loss.",
        Order = 200)]
    public bool EnableDailyLossLimit
    {
        get => _enableDailyLossLimit;
        set
        {
            _enableDailyLossLimit = value;

            var state = TryGetActiveState();
            if (state != null)
                state.EnableDailyLossLimit = value;

            TouchActiveAccountAndScheduleSave(force: false);
            RedrawChart();
        }
    }

    [Display(
        GroupName = "Daily Rails",
        Name = "Daily Loss Limit",
        Description = "Maximum allowed loss for the day (absolute currency amount).",
        Order = 201)]
    public decimal DailyLossLimit
    {
        get => _dailyLossLimit;
        set
        {
            _dailyLossLimit = value;

            var state = TryGetActiveState();
            if (state != null)
                state.DailyLossLimit = value;

            TouchActiveAccountAndScheduleSave(force: false);
            RedrawChart();
        }
    }

    [Display(
        GroupName = "Daily Rails",
        Name = "Daily Loss Mode",
        Description = "Selects the daily loss reference: FromSessionStart uses Daily Start Equity; FromSessionPeak uses the intraday peak equity as the loss base.",
        Order = 202)]
    public DailyLossModeKind DailyLossMode
    {
        get => _dailyLossMode;
        set
        {
            _dailyLossMode = value;

            var state = TryGetActiveState();
            if (state != null)
                state.DailyLossMode = value;

            TouchActiveAccountAndScheduleSave(force: false);
            RedrawChart();
        }
    }

    [Display(
        GroupName = "Daily Rails",
        Name = "Enable Daily Profit Cap",
        Description = "Enables the daily profit cap (non-trailing). When reached, remaining-to-cap becomes zero or negative.",
        Order = 203)]
    public bool EnableDailyProfitCap
    {
        get => _enableDailyProfitCap;
        set
        {
            _enableDailyProfitCap = value;

            var state = TryGetActiveState();
            if (state != null)
                state.EnableDailyProfitCap = value;

            TouchActiveAccountAndScheduleSave(force: false);
            RedrawChart();
        }
    }

    [Display(
        GroupName = "Daily Rails",
        Name = "Daily Profit Cap",
        Description = "Daily profit cap amount measured from Daily Start Equity (non-trailing).",
        Order = 204)]
    public decimal DailyProfitCap
    {
        get => _dailyProfitCap;
        set
        {
            _dailyProfitCap = value;

            var state = TryGetActiveState();
            if (state != null)
                state.DailyProfitCap = value;

            TouchActiveAccountAndScheduleSave(force: false);
            RedrawChart();
        }
    }

    [Display(
        GroupName = "Daily Rails",
        Name = "Daily Reset Mode",
        Description = "Defines when daily rails reset. NewYork1700 uses the America/New_York 17:00 session cut; LocalCustomTime uses the local time configured below.",
        Order = 205)]
    public DailyResetModeKind DailyResetMode
    {
        get => _dailyResetMode;
        set
        {
            _dailyResetMode = value;

            var state = TryGetActiveState();
            if (state != null)
                state.DailyResetMode = value;

            TouchActiveAccountAndScheduleSave(force: false);
            RedrawChart();
        }
    }

    [Display(
        GroupName = "Daily Rails",
        Name = "Daily Reset Time (Local)",
        Description = "Local reset time used only when Daily Reset Mode is LocalCustomTime.",
        Order = 206)]
    public TimeSpan DailyResetTimeLocal
    {
        get => _dailyResetTimeLocal;
        set
        {
            if (value < TimeSpan.Zero)
                value = TimeSpan.Zero;
            if (value >= TimeSpan.FromDays(1))
                value = TimeSpan.FromDays(1) - TimeSpan.FromSeconds(1);

            _dailyResetTimeLocal = value;

            var state = TryGetActiveState();
            if (state != null)
                state.DailyResetTimeLocal = value;

            TouchActiveAccountAndScheduleSave(force: false);
            RedrawChart();
        }
    }

    #endregion

    #region Soft Recommendations
    [Display(
    GroupName = "Daily Rails",
    Name = "Enable Soft Recommendations",
    Description = "Enables display-only soft recommendations (OK/STOP) based on session metrics and daily rails. No alerts or blocking.",
    Order = 207)]
    public bool DefaultEnableSoftRecommendations
    {
        get => _defaultEnableSoftRecommendations;
        set
        {
            _defaultEnableSoftRecommendations = value;

            var state = TryGetActiveState();
            if (state != null)
                state.EnableSoftRecommendations = value;

            TouchActiveAccountAndScheduleSave(force: false);
            RedrawChart();
        }
    }
    private bool _defaultEnableSoftRecommendations = true;

    [Display(
        GroupName = "Daily Rails",
        Name = "Max Trades Per Day",
        Description = "Suggested STOP if the number of closed trades today reaches this threshold.",
        Order = 208)]
    [Range(0, 500)]
    public int DefaultMaxTradesPerDay
    {
        get => _defaultMaxTradesPerDay;
        set
        {
            _defaultMaxTradesPerDay = value;

            var state = TryGetActiveState();
            if (state != null)
                state.MaxTradesPerDay = value;

            TouchActiveAccountAndScheduleSave(force: false);
            RedrawChart();
        }
    }
    private int _defaultMaxTradesPerDay = 3;

    [Display(
        GroupName = "Daily Rails",
        Name = "Max Consecutive Losses",
        Description = "Suggested STOP if the current loss streak reaches this threshold.",
        Order = 209)]
    [Range(0, 100)]
    public int DefaultMaxConsecutiveLosses
    {
        get => _defaultMaxConsecutiveLosses;
        set
        {
            _defaultMaxConsecutiveLosses = value;

            var state = TryGetActiveState();
            if (state != null)
                state.MaxConsecutiveLosses = value;

            TouchActiveAccountAndScheduleSave(force: false);
            RedrawChart();
        }
    }
    private int _defaultMaxConsecutiveLosses = 2;


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

    public enum TrailingPeakUpdateMode
    {
        Realtime = 0, // classic trailing: peak updates whenever equity makes a new high
        EndOfDay = 1  // EOD: peak updates only once per day at EOD time
    }

    public enum TrailingInitMode
    {
        AutoFromCurrentEquity = 0,
        ManualStopEquity = 1
    }

    public enum DailyResetModeKind
    {
        NewYork1700,
        LocalCustomTime
    }

    public enum DailyLossModeKind
    {
        FromSessionStart,
        FromSessionPeak
    }

    private enum SuggestedStatus
    {
        Ok = 0,
        Stop = 1
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

        InitPersistencePath();
        LoadStateFromDisk();

        // Ensure active portfolio state is created, then apply persisted data if present
        var portfolio = _currentPortfolio ?? TradingManager?.Portfolio;
        if (portfolio != null)
        {
            _activeAccountKey = GetAccountKey(portfolio);

            // Create state with defaults
            var state = GetOrCreateTrailingState(_activeAccountKey);

            // Apply persisted data (if any)
            ApplyLoadedStateToAccount(_activeAccountKey);

            // Sync UI from loaded state (so UI shows per-account values)
            SyncUiFromState(state);

            RedrawChart();
        }

    }

    protected override void OnDispose()
	{
		if (TradingManager != null)
		{
			TradingManager.PortfolioSelected -= OnPortfolioSelected;
		}

        try
        {
            if (!string.IsNullOrEmpty(_activeAccountKey))
            {
                PersistAccountToMemory(_activeAccountKey);
                SaveIfNeeded(force: true);
            }
        }
        catch
        {
            // ignore
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

        _activeAccountKey = GetAccountKey(portfolio);
        var state = GetOrCreateTrailingState(_activeAccountKey);

        // Phase 1: update live position snapshot (single open position assumption)
        UpdatePositionSnapshotFromTradingManager(portfolio);

        // Phase 2: equity and trailing DD state
        var equity = portfolio.Balance + portfolio.OpenPnL;
        var nowLocal = DateTime.Now;

        var prevDailyKey = state.LastDailyResetKey;

        MaybeResetForSchedule(state, portfolio, equity, nowLocal);
        MaybeResetDailyRails(state, equity, nowLocal);

        // Phase 5-3.1: detect daily reset edge and reset session metrics baselines/counters
        if (state.LastDailyResetKey != prevDailyKey && state.LastDailyResetKey != 0)
        {
            state.DailyClosedPnlBaseline = portfolio.ClosedPnL;
            state.TradesToday = 0;
            state.WinsToday = 0;
            state.LossesToday = 0;
            state.CurrentWinStreak = 0;
            state.CurrentLossStreak = 0;

            PersistAccountToMemory(_activeAccountKey);
            MarkDirty();
        }

        InitializeTrailingState(state, portfolio, equity);
        UpdateTrailingState(state, portfolio, equity, nowLocal);

        var rows = BuildRows(state, portfolio, equity); // updated signature
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

        // Clear union of old+new panel rect to avoid leftover artifacts when font/rows/position change.
        // Use an opaque pass to truly clear previous alpha-blended text, then draw the panel with the configured alpha.
        if (_lastPanelRect.HasValue)
        {
            var union = Rectangle.Union(_lastPanelRect.Value, rectangle);
            var clearColor = Color.FromArgb(255, _backgroundColor.R, _backgroundColor.G, _backgroundColor.B);
            context.FillRectangle(clearColor, union);
        }

        context.FillRectangle(_backgroundColor, rectangle);

        context.DrawRectangle(new RenderPen(Color.Gray, 1), rectangle);

        var textRect = new Rectangle(
            x + padding,
            y + padding,
            rectWidth - padding * 2,
            rectHeight - padding * 2
        );

        DrawColoredRows(context, rows, textRect, portfolio, maxLabelWidth);

        _lastPanelRect = rectangle;

        // Throttled autosave (only if dirty)
        if (_isDirty)
            SaveIfNeeded(force: false);
    }

    #endregion

    #region Private Methods

    private void OnPortfolioSelected(Portfolio portfolio)
    {
        // 1) Persist previous account (upsert + save)
        if (!string.IsNullOrEmpty(_activeAccountKey))
        {
            PersistAccountToMemory(_activeAccountKey);
            MarkDirty();
            SaveIfNeeded(force: true);
        }

        // 2) Switch
        _currentPortfolio = portfolio;
        _activeAccountKey = GetAccountKey(portfolio);
        _reinitializeNow = false;

        // 3) Ensure state exists
        var state = GetOrCreateTrailingState(_activeAccountKey);

        // 4) Load persisted data for new account if present
        ApplyLoadedStateToAccount(_activeAccountKey);

        // 5) Sync UI from the per-account state
        SyncUiFromState(state);

        _posSnapshot = new PositionSnapshot();

        RedrawChart();
    }

    private List<DisplayRow> BuildRows(TrailingDdState state, Portfolio portfolio, decimal equity)
    {
        var rows = new List<DisplayRow>();

        if (portfolio == null)
            return rows;

        // -----------------------------
        // Account / Balances / PnL
        // -----------------------------
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

        if (!state.WasPositionOpen && ShowLastClosedTradePnlRow)
            rows.Add(new DisplayRow("Last Closed Trade PnL", FormatCurrency(state.LastClosedTradePnL), numericValue: state.LastClosedTradePnL));

        if (ShowClosedPnL)
            rows.Add(new DisplayRow("Closed PnL", FormatCurrency(portfolio.ClosedPnL), numericValue: portfolio.ClosedPnL));

        if (ShowTotalPnL)
        {
            var totalPnL = portfolio.ClosedPnL + portfolio.OpenPnL;
            rows.Add(new DisplayRow("Total PnL", FormatCurrency(totalPnL), numericValue: totalPnL));
        }

        // -----------------------------
        // Trailing Drawdown (Phase 2)
        // -----------------------------
        if (state.EnableTrailingDrawdown && state.IsInitialized)
        {
            var stopEquity = state.PeakEquity - state.MaxTrailingDrawdown;
            var currentDd = state.PeakEquity - equity;     // magnitude (positive when in drawdown)
            var remainingDd = equity - stopEquity;         // positive = safe, negative = breached

            if (ShowStartEquityRow)
                rows.Add(new DisplayRow("Start Equity", FormatCurrency(state.StartEquity)));

            if (ShowPeakEquityRow)
                rows.Add(new DisplayRow("Peak Equity", FormatCurrency(state.PeakEquity)));

            if (ShowStopEquityRow)
                rows.Add(new DisplayRow("Stop Equity", FormatCurrency(stopEquity)));

            if (ShowRemainingDdRow)
                rows.Add(new DisplayRow("Remaining DD", FormatCurrency(remainingDd), numericValue: remainingDd));

            if (ShowCurrentDdRow)
            {
                // We want "bigger DD is worse" while reusing the existing color rules:
                // positive => green, negative => red. So we pass -currentDd as numericValue.
                rows.Add(new DisplayRow("Current DD", FormatCurrency(currentDd), numericValue: -currentDd));
            }

            // Per-trade metrics (Phase 5-1)
            if (state.WasPositionOpen && ShowTradeMaxOpenPnlCurrentRow)
                rows.Add(new DisplayRow("Trade Max Open PnL (Current)", FormatCurrency(state.TradeMaxOpenPnL), numericValue: state.TradeMaxOpenPnL));

            if (!state.WasPositionOpen && ShowTradeMaxOpenPnlLastRow)
                rows.Add(new DisplayRow("Trade Max Open PnL (Last)", FormatCurrency(state.LastTradeMaxOpenPnL), numericValue: state.LastTradeMaxOpenPnL));
        }

        // -----------------------------
        // Daily Rails (Phase 5-2)
        // -----------------------------
        var dailyRailsEnabled =
            (state.EnableDailyLossLimit && state.DailyLossLimit > 0m) ||
            (state.EnableDailyProfitCap && state.DailyProfitCap > 0m);

        if (dailyRailsEnabled)
        {
            var dailyPnl = equity - state.DailyStartEquity;

            if (ShowDailyStartEquityRow)
                rows.Add(new DisplayRow("Daily Start Equity", FormatCurrency(state.DailyStartEquity)));

            if (ShowDailyPnlRow)
                rows.Add(new DisplayRow("Daily PnL", FormatCurrency(dailyPnl), numericValue: dailyPnl));

            if (state.EnableDailyLossLimit && state.DailyLossLimit > 0m)
            {
                var lossBase = state.DailyLossMode == DailyLossModeKind.FromSessionPeak
                    ? state.DailyPeakEquity
                    : state.DailyStartEquity;

                var dailyStopEquity = lossBase - state.DailyLossLimit;
                var remainingLoss = equity - dailyStopEquity;

                if (ShowDailyLossBaseRow)
                    rows.Add(new DisplayRow("Daily Loss Base", FormatCurrency(lossBase)));

                if (ShowDailyStopEquityRow)
                    rows.Add(new DisplayRow("Daily Stop Equity", FormatCurrency(dailyStopEquity)));

                if (ShowRemainingDailyLossRow)
                    rows.Add(new DisplayRow("Remaining Daily Loss", FormatCurrency(remainingLoss), numericValue: remainingLoss));
            }

            if (state.EnableDailyProfitCap && state.DailyProfitCap > 0m)
            {
                // Profit cap is always from day start (non-trailing), by design.
                var profitAchieved = Math.Max(0m, dailyPnl);
                var remainingToCap = state.DailyProfitCap - profitAchieved;

                if (ShowDailyProfitCapRow)
                    rows.Add(new DisplayRow("Daily Profit Cap", FormatCurrency(state.DailyProfitCap)));

                if (ShowRemainingToProfitCapRow)
                    rows.Add(new DisplayRow("Remaining to Profit Cap", FormatCurrency(remainingToCap), numericValue: remainingToCap));
            }
        }

        // -----------------------------
        // Session Metrics (Phase 5-3.1)
        // -----------------------------
        if (ShowTradesTodayRow || ShowWinsLossesTodayRow || ShowCurrentStreakRow || ShowRealizedPnlTodayRow)
        {
            var realizedToday = portfolio.ClosedPnL - state.DailyClosedPnlBaseline;

            if (ShowTradesTodayRow)
                rows.Add(new DisplayRow("Trades Today", state.TradesToday.ToString("N0")));

            if (ShowWinsLossesTodayRow)
                rows.Add(new DisplayRow("Wins / Losses Today", $"{state.WinsToday:N0} / {state.LossesToday:N0}"));

            if (ShowCurrentStreakRow)
            {
                var streakText =
                    state.CurrentWinStreak > 0 ? $"W{state.CurrentWinStreak:N0}" :
                    state.CurrentLossStreak > 0 ? $"L{state.CurrentLossStreak:N0}" :
                    "-";

                rows.Add(new DisplayRow("Streak", streakText));
            }

            if (ShowRealizedPnlTodayRow)
                rows.Add(new DisplayRow("Realized PnL Today", FormatCurrency(realizedToday), numericValue: realizedToday));
        }

        // -----------------------------
        // Soft Recommendations (Phase 5-3.2)
        // -----------------------------
        if (ShowSuggestedStatusRow || ShowStopReasonsRow)
        {
            var suggestion = EvaluateSoftRecommendations(state, portfolio, equity);

            if (ShowSuggestedStatusRow)
            {
                var statusText = suggestion.Status == SuggestedStatus.Stop ? "STOP (Suggested)" : "OK";
                // Use numericValue to leverage existing color logic: negative => red, positive => green.
                var statusColorValue = suggestion.Status == SuggestedStatus.Stop ? -1m : 1m;
                rows.Add(new DisplayRow("Suggested Status", statusText, numericValue: statusColorValue));
            }

            if (ShowStopReasonsRow && suggestion.Status == SuggestedStatus.Stop)
                rows.Add(new DisplayRow("Stop Reasons", suggestion.ReasonsText));
        }

        // -----------------------------
        // Position Snapshot (Phase 5-1)
        // -----------------------------
        if (ShowPositionSnapshot && _posSnapshot != null && _posSnapshot.IsOpen)
        {
            rows.Add(new DisplayRow("Pos Dir", _posSnapshot.Direction.ToString()));
            rows.Add(new DisplayRow("Pos Qty", _posSnapshot.Volume.ToString("N0")));
            rows.Add(new DisplayRow("Pos Entry", _posSnapshot.AvgEntryPrice.ToString("N2")));
        }
        else if (ShowFlatRow)
        {
            rows.Add(new DisplayRow("Pos", "FLAT"));
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

            // Determine color for numeric values
            var valueColor = _textColor; // System.Drawing.Color

            if (row.ValueColorOverride.HasValue)
            {
                // CrossColor -> System.Drawing.Color
                valueColor = row.ValueColorOverride.Value.Convert();
            }
            else if (row.NumericValue.HasValue)
            {
                var v = row.NumericValue.Value;

                if (v > 0)
                    valueColor = _positiveColor;
                else if (v < 0)
                    valueColor = _negativeColor;
                else
                    valueColor = _neutralColor;
            }


            // Draw value
            var valueRect = new Rectangle(valueColumnX, currentY, textRect.Right - valueColumnX, lineHeight);
            context.FillRectangle(_backgroundColor, valueRect);
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

    private void ResetActiveAccountState(TrailingDdState state)
    {
        state.IsInitialized = false;
        state.StartEquity = 0m;
        state.PeakEquity = 0m;

        // Phase 5-1 trade runtime
        state.TradeMaxOpenPnL = 0m;
        state.LastTradeMaxOpenPnL = 0m;
        state.LastClosedTradePnL = 0m;
        state.TradeClosedPnlBaseline = 0m;
        state.WasPositionOpen = false;

        state.InitializedAtUtc = default;
        state.LastEodCaptureDate = default;

        // Phase 5-2 daily runtime
        state.LastDailyResetKey = 0;
        state.DailyStartEquity = 0m;
        state.DailyPeakEquity = 0m;

        // Phase 5-3.1 session metrics runtime
        state.DailyClosedPnlBaseline = 0m;
        state.TradesToday = 0;
        state.WinsToday = 0;
        state.LossesToday = 0;
        state.CurrentWinStreak = 0;
        state.CurrentLossStreak = 0;

        _reinitializeNow = false;
    }

    private void ResetAllAccountStates()
    {
        _trailingStatesByAccount.Clear();
        _reinitializeNow = false;
    }

    private void InitializeTrailingState(TrailingDdState state, Portfolio portfolio, decimal equity)
    {
        if (!state.EnableTrailingDrawdown)
            return;


        // One-shot init unless forced.
        if (state.IsInitialized && !_reinitializeNow)
            return;

        state.IsInitialized = true;
        state.InitializedAtUtc = DateTime.UtcNow;

        state.StartEquity = equity;

        if (state.InitializationMode == TrailingInitMode.ManualStopEquity && state.MaxTrailingDrawdown > 0m)
        {
            // Bootstrap using user-provided stop equity:
            // PeakEquity is derived so that: StopEquity = PeakEquity - MaxDD == ManualStopEquity
            state.PeakEquity = state.ManualStopEquity + state.MaxTrailingDrawdown;
        }
        else
        {
            // Default: start from current equity
            state.PeakEquity = equity;
        }

        state.TradeMaxOpenPnL = 0m;
        state.WasPositionOpen = false;
        state.LastTradeMaxOpenPnL = 0m;

        // Consume the pulse
        _reinitializeNow = false;
    }

    private void UpdateTrailingState(TrailingDdState state, Portfolio portfolio, decimal equity, DateTime nowLocal)
    {
        if (!state.EnableTrailingDrawdown || !state.IsInitialized)
            return;

        // Phase 1: per-account per-trade tracking (single open position assumption)
        var isOpen = IsActivePositionOpen(portfolio);

        // OPEN event: FLAT -> OPEN
        if (!state.WasPositionOpen && isOpen)
        {
            state.TradeMaxOpenPnL = 0m;                     // current trade starts at 0
            state.WasPositionOpen = true;
            state.TradeClosedPnlBaseline = portfolio.ClosedPnL;
        }
        // CLOSE event: OPEN -> FLAT
        else if (state.WasPositionOpen && !isOpen)
        {
            // Store the final max of the trade that just closed
            state.LastTradeMaxOpenPnL = state.TradeMaxOpenPnL;

            //Store the PnL of the trade that just closed
            state.LastClosedTradePnL = portfolio.ClosedPnL - state.TradeClosedPnlBaseline;

            // Phase 5-3.1: update per-day session metrics on trade close
            var tradePnl = state.LastClosedTradePnL;

            state.TradesToday++;

            if (tradePnl > 0m)
            {
                state.WinsToday++;
                state.CurrentWinStreak++;
                state.CurrentLossStreak = 0;
            }
            else if (tradePnl < 0m)
            {
                state.LossesToday++;
                state.CurrentLossStreak++;
                state.CurrentWinStreak = 0;
            }
            else
            {
                // Zero-PnL trade: neutral. Reset both to avoid biasing either streak.
                state.CurrentWinStreak = 0;
                state.CurrentLossStreak = 0;
            }

            PersistAccountToMemory(_activeAccountKey);
            MarkDirty();

            // Reset current-trade runtime (last trade remains visible)
            state.TradeMaxOpenPnL = 0m;
            state.WasPositionOpen = false;
        }
        // UPDATE while OPEN
        else if (state.WasPositionOpen && isOpen)
        {
            if (portfolio.OpenPnL > state.TradeMaxOpenPnL)
                state.TradeMaxOpenPnL = portfolio.OpenPnL;
        }
        // UPDATE while FLAT: do nothing (LastTradeMaxOpenPnL stays visible)

        // PeakEquity policy
        if (state.PeakUpdateMode == TrailingPeakUpdateMode.Realtime)
        {
            if (equity > state.PeakEquity)
                state.PeakEquity = equity;

            return;
        }

        // EOD mode
        MaybeCaptureEodPeak(state, equity, nowLocal);
    }


    #endregion

    #region Multi account helpers

    private string GetAccountKey(Portfolio portfolio)
    {
        // Prefer AccountID if present; fallback to a stable placeholder
        if (!string.IsNullOrWhiteSpace(portfolio?.AccountID))
            return portfolio.AccountID;

        // Worst-case fallback: single bucket to avoid exceptions
        return "DEFAULT";
    }

    private TrailingDdState GetOrCreateTrailingState(string accountKey)
    {
        if (_trailingStatesByAccount.TryGetValue(accountKey, out var state))
            return state;

        state = new TrailingDdState();

        _trailingStatesByAccount[accountKey] = state;

        if (HasPersistedAccount(accountKey))
        {
            // Populate from JSON (per-account config + runtime)
            ApplyLoadedStateToAccount(accountKey);
            return state;
        }

        // Factory defaults for a brand-new account (do NOT copy from previous account UI)
        state.EnableTrailingDrawdown = true;
        state.MaxTrailingDrawdown = 2500m;
        state.InitializationMode = TrailingInitMode.AutoFromCurrentEquity;
        state.ManualStopEquity = 0m;
        state.PeakUpdateMode = TrailingPeakUpdateMode.Realtime;
        state.EodTimeLocal = new TimeSpan(17, 0, 0);

        state.EnableMonthlyReset = true;
        state.MonthlyResetDay = 1;

        state.DailyResetMode = DailyResetModeKind.NewYork1700;
        state.DailyResetTimeLocal = new TimeSpan(17, 0, 0);
        state.EnableDailyLossLimit = false;
        state.DailyLossLimit = 0m;
        state.DailyLossMode = DailyLossModeKind.FromSessionStart;

        state.EnableDailyProfitCap = false;
        state.DailyProfitCap = 0m;

        state.EnableSoftRecommendations = true;
        state.MaxTradesPerDay = 3;
        state.MaxConsecutiveLosses = 2;

        return state;
    }

    private void SyncUiFromState(TrailingDdState state)
    {
        _maxTrailingDrawdownDefault = state.MaxTrailingDrawdown;
        _defaultInitializationMode = state.InitializationMode;
        _defaultManualStopEquity = state.ManualStopEquity;
        _defaultEnableTrailingDrawdown = state.EnableTrailingDrawdown;

        _defaultPeakUpdateMode = state.PeakUpdateMode;
        _defaultEodTimeLocal = state.EodTimeLocal;

        _defaultEnableMonthlyReset = state.EnableMonthlyReset;
        _defaultMonthlyResetDay = state.MonthlyResetDay;

        _enableDailyLossLimit = state.EnableDailyLossLimit;
        _dailyLossLimit = state.DailyLossLimit;
        _dailyLossMode = state.DailyLossMode;

        _enableDailyProfitCap = state.EnableDailyProfitCap;
        _dailyProfitCap = state.DailyProfitCap;

        _dailyResetMode = state.DailyResetMode;
        _dailyResetTimeLocal = state.DailyResetTimeLocal;

        // Soft recommendations (Phase 5-3.2)
        _defaultEnableSoftRecommendations = state.EnableSoftRecommendations;
        _defaultMaxTradesPerDay = state.MaxTradesPerDay;
        _defaultMaxConsecutiveLosses = state.MaxConsecutiveLosses;
    }

    private TrailingDdState TryGetActiveState()
    {
        if (string.IsNullOrEmpty(_activeAccountKey))
            return null;

        return _trailingStatesByAccount.TryGetValue(_activeAccountKey, out var state)
            ? state
            : null;
    }

    private void MaybeResetForSchedule(TrailingDdState state, Portfolio portfolio, decimal equity, DateTime nowLocal)
    {
        if (state == null || portfolio == null)
            return;

        // ---------------------------------------------------------------------
        // 1) Monthly reset (per account)
        // Resets the whole trailing state once per month when Day >= MonthlyResetDay.
        // If MonthlyResetDay exceeds the days in the month, the last day is used.
        // ---------------------------------------------------------------------
        if (state.EnableMonthlyReset)
        {
            var daysInMonth = DateTime.DaysInMonth(nowLocal.Year, nowLocal.Month);

            // Defensive clamp (state.MonthlyResetDay is UI-ranged, but keep it safe)
            var requestedDay = state.MonthlyResetDay;
            if (requestedDay < 1) requestedDay = 1;

            var effectiveResetDay = requestedDay > daysInMonth ? daysInMonth : requestedDay;

            var monthKey = (nowLocal.Year * 100) + nowLocal.Month; // yyyymm

            // If we're past the reset day and we haven't reset this month yet -> reset now.
            if (nowLocal.Day >= effectiveResetDay && state.LastMonthlyResetKey != monthKey)
            {
                // Only reset if trailing stop is breached (Remaining DD < 0)
                var stopEquity = state.PeakEquity - state.MaxTrailingDrawdown;
                var remainingDd = equity - stopEquity;

                if (remainingDd < 0m)
                {
                    state.LastMonthlyResetKey = monthKey;

                    ResetActiveAccountState(state);

                    TouchMonthlyResetAndScheduleSave(GetAccountKey(portfolio), force: false);
                    _reinitializeNow = false;
                }
            }
        }
    }

    private void MaybeCaptureEodPeak(TrailingDdState state, decimal equity, DateTime nowLocal)
    {
        if (state == null)
            return;

        // Capture only once per local day after configured EOD time
        if (nowLocal.TimeOfDay < state.EodTimeLocal)
            return;

        var today = nowLocal.Date;

        if (state.LastEodCaptureDate != default && state.LastEodCaptureDate.Date == today)
            return;

        // In EOD mode, PeakEquity updates only at capture time
        if (equity > state.PeakEquity)
            state.PeakEquity = equity;

        state.LastEodCaptureDate = today;

        PersistAccountToMemory(_activeAccountKey);
        MarkDirty();
    }


    #endregion

    #region Persistence helpers

    private void InitPersistencePath()
    {
        // %AppData%\Roaming\ATAS\JSON\AccountInfoDisplay.states.v1.json
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var baseDir = Path.Combine(appData, "ATAS", "JSON");

        Directory.CreateDirectory(baseDir);

        _stateFilePath = Path.Combine(baseDir, _stateFileName);
    }

    private void LoadStateFromDisk()
    {
        try
        {
            if (string.IsNullOrEmpty(_stateFilePath))
                InitPersistencePath();

            if (!File.Exists(_stateFilePath))
            {
                _persistedRoot = new PersistedRootV1();
                return;
            }

            var json = File.ReadAllText(_stateFilePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
            {
                _persistedRoot = new PersistedRootV1();
                return;
            }

            var loaded = JsonSerializer.Deserialize<PersistedRootV1>(json, _jsonOptions);
            if (loaded == null)
            {
                _persistedRoot = new PersistedRootV1();
                return;
            }

            // Basic schema guard (v1 only for now)
            if (loaded.SchemaVersion != _schemaVersion)
            {
                // For now: re-init, or you can implement migrations later.
                _persistedRoot = new PersistedRootV1();
                return;
            }

            loaded.Accounts ??= new Dictionary<string, PersistedAccountV1>();
            _persistedRoot = loaded;

            // Seed fingerprint to avoid immediate rewrite
            _lastSavedHash = ComputeSha256(json);
            _isDirty = false;
        }
        catch
        {
            // Fail-safe: never break indicator rendering due to IO/JSON issues.
            _persistedRoot = new PersistedRootV1();
        }
    }

    private void ApplyLoadedStateToAccount(string accountKey)
    {
        if (string.IsNullOrEmpty(accountKey))
            return;

        if (_persistedRoot?.Accounts == null)
            return;

        if (!_persistedRoot.Accounts.TryGetValue(accountKey, out var persisted))
            return;

        if (!_trailingStatesByAccount.TryGetValue(accountKey, out var state))
            return;

        var cfg = persisted.Config ?? new PersistedConfigV1();
        var rt = persisted.Runtime ?? new PersistedRuntimeV1();

        // --- Config ---
        state.EnableTrailingDrawdown = cfg.EnableTrailingDrawdown;
        state.MaxTrailingDrawdown = cfg.MaxTrailingDrawdown;
        state.InitializationMode = (TrailingInitMode)cfg.InitializationMode;
        state.ManualStopEquity = cfg.ManualStopEquity;

        state.PeakUpdateMode = (TrailingPeakUpdateMode)cfg.PeakUpdateMode;
        state.EodTimeLocal = ParseTimeSpanOrDefault(cfg.EodTimeLocal, state.EodTimeLocal);

        state.EnableMonthlyReset = cfg.EnableMonthlyReset;
        state.MonthlyResetDay = cfg.MonthlyResetDay;

        // --- Daily Rails Config ---
        state.EnableDailyLossLimit = cfg.EnableDailyLossLimit;
        state.DailyLossLimit = cfg.DailyLossLimit;
        state.DailyLossMode = (DailyLossModeKind)cfg.DailyLossMode;

        state.EnableDailyProfitCap = cfg.EnableDailyProfitCap;
        state.DailyProfitCap = cfg.DailyProfitCap;

        state.DailyResetMode = (DailyResetModeKind)cfg.DailyResetMode;
        state.DailyResetTimeLocal = ParseTimeSpanOrDefault(cfg.DailyResetTimeLocal, state.DailyResetTimeLocal);

        // Soft recommendations (Phase 5-3.2): keep factory defaults if older JSON doesn't have the fields
        if (cfg.EnableSoftRecommendations.HasValue)
            state.EnableSoftRecommendations = cfg.EnableSoftRecommendations.Value;

        if (cfg.MaxTradesPerDay.HasValue)
            state.MaxTradesPerDay = cfg.MaxTradesPerDay.Value;

        if (cfg.MaxConsecutiveLosses.HasValue)
            state.MaxConsecutiveLosses = cfg.MaxConsecutiveLosses.Value;


        // --- Runtime ---
        state.IsInitialized = rt.IsInitialized;
        state.StartEquity = rt.StartEquity;
        state.PeakEquity = rt.PeakEquity;
        state.WasPositionOpen = false;
        state.TradeMaxOpenPnL = 0m;
        state.LastTradeMaxOpenPnL = rt.LastTradeMaxOpenPnL;
        state.LastClosedTradePnL = rt.LastClosedTradePnL;

        state.TradeClosedPnlBaseline = 0m;

        state.InitializedAtUtc = ParseDateTimeOrDefault(rt.InitializedAtUtc, default);
        state.LastEodCaptureDate = ParseDateOrDefault(rt.LastEodCaptureDate, default);

        state.LastMonthlyResetKey = rt.LastMonthlyResetKey;

        // --- Daily Rails Runtime ---
        state.LastDailyResetKey = rt.LastDailyResetKey;
        state.DailyStartEquity = rt.DailyStartEquity;
        state.DailyPeakEquity = rt.DailyPeakEquity;

        // --- Phase 5-3.1 Runtime ---
        state.DailyClosedPnlBaseline = rt.DailyClosedPnlBaseline;
        state.TradesToday = rt.TradesToday;
        state.WinsToday = rt.WinsToday;
        state.LossesToday = rt.LossesToday;
        state.CurrentWinStreak = rt.CurrentWinStreak;
        state.CurrentLossStreak = rt.CurrentLossStreak;

        // IMPORTANT: loading should not mark dirty.
    }

    private void PersistAccountToMemory(string accountKey)
    {
        if (string.IsNullOrEmpty(accountKey))
            return;

        if (_persistedRoot == null)
            _persistedRoot = new PersistedRootV1();

        _persistedRoot.Accounts ??= new Dictionary<string, PersistedAccountV1>();

        if (!_trailingStatesByAccount.TryGetValue(accountKey, out var state))
            return;

        if (!_persistedRoot.Accounts.TryGetValue(accountKey, out var persisted))
        {
            persisted = new PersistedAccountV1();
            _persistedRoot.Accounts[accountKey] = persisted;
        }

        persisted.Config ??= new PersistedConfigV1();
        persisted.Runtime ??= new PersistedRuntimeV1();

        // --- Config ---
        persisted.Config.EnableTrailingDrawdown = state.EnableTrailingDrawdown;
        persisted.Config.MaxTrailingDrawdown = state.MaxTrailingDrawdown;
        persisted.Config.InitializationMode = (int)state.InitializationMode;
        persisted.Config.ManualStopEquity = state.ManualStopEquity;

        persisted.Config.PeakUpdateMode = (int)state.PeakUpdateMode;
        persisted.Config.EodTimeLocal = FormatTimeSpan(state.EodTimeLocal);

        persisted.Config.EnableMonthlyReset = state.EnableMonthlyReset;
        persisted.Config.MonthlyResetDay = state.MonthlyResetDay;

        // --- Daily Rails Config ---
        persisted.Config.EnableDailyLossLimit = state.EnableDailyLossLimit;
        persisted.Config.DailyLossLimit = state.DailyLossLimit;
        persisted.Config.DailyLossMode = (int)state.DailyLossMode;

        persisted.Config.EnableDailyProfitCap = state.EnableDailyProfitCap;
        persisted.Config.DailyProfitCap = state.DailyProfitCap;

        persisted.Config.DailyResetMode = (int)state.DailyResetMode;
        persisted.Config.DailyResetTimeLocal = FormatTimeSpan(state.DailyResetTimeLocal);


        // --- Phase 5-3.2 Config ---
        persisted.Config.EnableSoftRecommendations = state.EnableSoftRecommendations;
        persisted.Config.MaxTradesPerDay = state.MaxTradesPerDay;
        persisted.Config.MaxConsecutiveLosses = state.MaxConsecutiveLosses;

        // --- Runtime ---
        persisted.Runtime.IsInitialized = state.IsInitialized;
        persisted.Runtime.StartEquity = state.StartEquity;
        persisted.Runtime.PeakEquity = state.PeakEquity;

        persisted.Runtime.InitializedAtUtc = state.InitializedAtUtc == default
            ? null
            : state.InitializedAtUtc.ToString("O");

        persisted.Runtime.LastEodCaptureDate = state.LastEodCaptureDate == default
            ? null
            : state.LastEodCaptureDate.Date.ToString("yyyy-MM-dd");

        persisted.Runtime.LastMonthlyResetKey = state.LastMonthlyResetKey;

        _persistedRoot.UpdatedAtUtc = DateTime.UtcNow.ToString("O");

        // --- Daily Rails Runtime ---
        persisted.Runtime.LastDailyResetKey = state.LastDailyResetKey;
        persisted.Runtime.DailyStartEquity = state.DailyStartEquity;
        persisted.Runtime.DailyPeakEquity = state.DailyPeakEquity;

        // Phase 5-1 (runtime) - trade
        persisted.Runtime.LastTradeMaxOpenPnL = state.LastTradeMaxOpenPnL;
        persisted.Runtime.LastClosedTradePnL = state.LastClosedTradePnL;

        // --- Phase 5-3.1 Runtime ---
        persisted.Runtime.DailyClosedPnlBaseline = state.DailyClosedPnlBaseline;
        persisted.Runtime.TradesToday = state.TradesToday;
        persisted.Runtime.WinsToday = state.WinsToday;
        persisted.Runtime.LossesToday = state.LossesToday;
        persisted.Runtime.CurrentWinStreak = state.CurrentWinStreak;
        persisted.Runtime.CurrentLossStreak = state.CurrentLossStreak;

    }

    private void SaveIfNeeded(bool force)
    {
        if (_persistedRoot == null)
            return;

        if (string.IsNullOrEmpty(_stateFilePath))
            InitPersistencePath();

        if (!force)
        {
            if (!_isDirty)
                return;

            var nowUtc = DateTime.UtcNow;
            if (nowUtc - _lastSaveUtc < _minSaveInterval)
                return;
        }

        try
        {
            var json = JsonSerializer.Serialize(_persistedRoot, _jsonOptions);
            var hash = ComputeSha256(json);

            // Skip disk write if content identical
            if (!force && string.Equals(hash, _lastSavedHash, StringComparison.Ordinal))
            {
                _isDirty = false;
                return;
            }

            // Atomic-ish write: write temp then replace
            var tmp = _stateFilePath + ".tmp";
            File.WriteAllText(tmp, json, Encoding.UTF8);

            if (File.Exists(_stateFilePath))
            {
                // Replace destination atomically; no need to delete first
                File.Replace(tmp, _stateFilePath, null);
            }

            else

            {
                // First-time save
                File.Move(tmp, _stateFilePath);
            }

            _lastSavedHash = hash;
            _lastSaveUtc = DateTime.UtcNow;
            _isDirty = false;
        }
        catch
        {
            // Never break indicator due to IO errors.
            // Keep dirty true so we can retry later.
            _isDirty = true;
        }
    }

    private void MarkDirty()
    {
        _isDirty = true;
    }

    private static string ComputeSha256(string text)
    {
        if (text == null)
            return string.Empty;

        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    private static TimeSpan ParseTimeSpanOrDefault(string value, TimeSpan fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return TimeSpan.TryParse(value, out var ts) ? ts : fallback;
    }

    private static string FormatTimeSpan(TimeSpan value)
    {
        // HH:mm:ss (safe, stable)
        var h = (int)value.TotalHours;
        var m = value.Minutes;
        var s = value.Seconds;
        return $"{h:00}:{m:00}:{s:00}";
    }

    private static DateTime ParseDateTimeOrDefault(string value, DateTime fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? dt
            : fallback;
    }

    private static DateTime ParseDateOrDefault(string value, DateTime fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return DateTime.TryParseExact(value, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var dt)
            ? dt.Date
            : fallback;
    }

    private void TouchActiveAccountAndScheduleSave(bool force = false)
    {
        if (string.IsNullOrEmpty(_activeAccountKey))
            return;

        PersistAccountToMemory(_activeAccountKey);
        MarkDirty();
        SaveIfNeeded(force);
    }

    private void TouchMonthlyResetAndScheduleSave(string accountKey, bool force = false)
    {
        if (string.IsNullOrEmpty(accountKey))
            return;

        PersistAccountToMemory(accountKey);
        MarkDirty();
        SaveIfNeeded(force);
    }


    #endregion

    #region Daily Rails Helpers
    private static int ToYyyyMmDdKey(DateTime d)
    => (d.Year * 10000) + (d.Month * 100) + d.Day;

    private static DateTime EnsureLocalKind(DateTime dt)
    {
        // ATAS often provides Unspecified; treat as Local for timezone conversions.
        return dt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dt, DateTimeKind.Local)
            : dt;
    }

    private int GetDailyTradingKey(DateTime nowLocal, TrailingDdState state)
    {
        if (state.DailyResetMode == DailyResetModeKind.LocalCustomTime)
        {
            var baseDate = nowLocal.Date;
            var keyDate = nowLocal.TimeOfDay < state.DailyResetTimeLocal ? baseDate.AddDays(-1) : baseDate;
            return ToYyyyMmDdKey(keyDate);
        }

        // Default: NewYork1700 (DST-safe)
        TimeZoneInfo nyTz;
        try { nyTz = TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
        catch { nyTz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }

        var local = EnsureLocalKind(nowLocal);
        var nowNy = TimeZoneInfo.ConvertTime(local, nyTz);

        var cutoff = new TimeSpan(17, 0, 0);
        var keyDateNy = nowNy.TimeOfDay < cutoff ? nowNy.Date.AddDays(-1) : nowNy.Date;

        return ToYyyyMmDdKey(keyDateNy);
    }

    private void MaybeResetDailyRails(TrailingDdState state, decimal equity, DateTime nowLocal)
    {
        if (state == null)
            return;

        // Compute the daily trading key according to configured reset mode
        var key = GetDailyTradingKey(nowLocal, state);

        // Always maintain daily peak if we already have a session in progress
        if (state.LastDailyResetKey == key && state.LastDailyResetKey != 0)
        {
            if (equity > state.DailyPeakEquity)
                state.DailyPeakEquity = equity;

            return;
        }

        // First time today (or first initialization)
        state.LastDailyResetKey = key;
        state.DailyStartEquity = equity;
        state.DailyPeakEquity = equity;

        // -----------------------------
        // Phase 5-3.1: session metrics reset
        // (requires ClosedPnL baseline; we already have portfolio in OnRender, so set it there)
        // -----------------------------
        // NOTE: We cannot set DailyClosedPnlBaseline here because this method does not receive Portfolio.
        // We'll set it from OnRender right after calling MaybeResetDailyRails, only when key changed.


        // Persist per-account runtime so it survives restarts / account switches
        PersistAccountToMemory(_activeAccountKey);
        MarkDirty();
    }


    #endregion

    #region Live position tracker helpers
    // ===========================
    // Phase 1: Position tracker
    // ===========================

    private void UpdatePositionSnapshotFromTradingManager(Portfolio portfolio)
    {
        _posSnapshot ??= new PositionSnapshot();

        var tm = TradingManager;
        var pos = tm?.Position;
        var sec = tm?.Security;

        if (portfolio == null || pos == null || sec == null)
        {
            _posSnapshot.IsOpen = false;
            return;
        }

        // Defensive: ensure the position belongs to the currently selected portfolio/security
        if (!string.Equals(pos.AccountID, portfolio.AccountID, StringComparison.Ordinal))
        {
            _posSnapshot.IsOpen = false;
            return;
        }

        if (pos.Security == null || !string.Equals(pos.Security.Code, sec.Code, StringComparison.Ordinal))
        {
            _posSnapshot.IsOpen = false;
            return;
        }

        var isOpen = pos.IsInPosition && pos.Volume != 0m;

        if (!isOpen)
        {
            _posSnapshot = new PositionSnapshot
            {
                IsOpen = false,
                AccountId = portfolio.AccountID,
                SecurityCode = sec.Code
            };
            return;
        }

        var dir = pos.Volume > 0m ? OrderDirections.Buy : OrderDirections.Sell;

        _posSnapshot = new PositionSnapshot
        {
            IsOpen = true,
            Direction = dir,
            Volume = Math.Abs(pos.Volume),
            AvgEntryPrice = pos.AveragePrice,
            AccountId = portfolio.AccountID,
            SecurityCode = sec.Code
            // OpenTime: if needed in Phase 1, it can be approximated using MyTrades; not required for per-trade max reset.
        };
    }

    private bool IsActivePositionOpen(Portfolio portfolio)
    {
        var tm = TradingManager;
        var pos = tm?.Position;
        var sec = tm?.Security;

        if (portfolio == null || pos == null || sec == null)
            return false;

        if (!string.Equals(pos.AccountID, portfolio.AccountID, StringComparison.Ordinal))
            return false;

        if (pos.Security == null || !string.Equals(pos.Security.Code, sec.Code, StringComparison.Ordinal))
            return false;

        return pos.IsInPosition && pos.Volume != 0m;
    }


    #endregion

    private SuggestionResult EvaluateSoftRecommendations(TrailingDdState state, Portfolio portfolio, decimal equity)
    {
        if (state == null || portfolio == null)
            return new SuggestionResult { Status = SuggestedStatus.Ok };

        if (!state.EnableSoftRecommendations)
            return new SuggestionResult { Status = SuggestedStatus.Ok };

        var reasons = new List<string>();

        // 1) Trades per day
        if (state.MaxTradesPerDay > 0 && state.TradesToday >= state.MaxTradesPerDay)
            reasons.Add($"TradesToday≥{state.MaxTradesPerDay}");

        // 2) Loss streak
        if (state.MaxConsecutiveLosses > 0 && state.CurrentLossStreak >= state.MaxConsecutiveLosses)
            reasons.Add($"LossStreak≥{state.MaxConsecutiveLosses}");

        // 3) Daily loss breached (only when daily loss is enabled)
        if (state.EnableDailyLossLimit && state.DailyLossLimit > 0m)
        {
            var lossBase = state.DailyLossMode == DailyLossModeKind.FromSessionPeak
                ? state.DailyPeakEquity
                : state.DailyStartEquity;

            var dailyStopEquity = lossBase - state.DailyLossLimit;
            var remainingLoss = equity - dailyStopEquity;

            if (remainingLoss < 0m)
                reasons.Add("DailyLossBreached");
        }

        if (reasons.Count == 0)
            return new SuggestionResult { Status = SuggestedStatus.Ok };

        return new SuggestionResult
        {
            Status = SuggestedStatus.Stop,
            ReasonsText = string.Join("; ", reasons)
        };
    }

    private bool HasPersistedAccount(string accountKey)
    => _persistedRoot?.Accounts != null && _persistedRoot.Accounts.ContainsKey(accountKey);



    #endregion
}
