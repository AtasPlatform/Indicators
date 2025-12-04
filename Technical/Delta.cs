namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;

using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using OFT.Rendering.Tools;

using CrossColor = CrossColor;

[Category(IndicatorCategories.VolumeOrderFlow)]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.DeltaDescription))]
[HelpLink("https://help.atas.net/support/solutions/articles/72000602362")]
public class Delta : Indicator
{
    #region Nested types

    [Serializable]
    public enum BarDirection
    {
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Any))]
        Any = 0,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Bullish))]
        Bullish = 1,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Bearlish))]
        Bearlish = 2
    }

    [Serializable]
    public enum DeltaType
    {
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Any))]
        Any = 0,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Positive))]
        Positive = 1,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Negative))]
        Negative = 2
    }

    [Serializable]
    public enum DeltaVisualMode
    {
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Candles))]
        Candles = 0,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.HighLow))]
        HighLow = 1,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Histogram))]
        Histogram = 2,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Bars))]
        Bars = 3
    }

    public enum Location
    {
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Up))]
        Up,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Middle))]
        Middle,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Down))]
        Down
    }

    [Serializable]
    public enum AverageMode
    {
        [Display(Name = "SMA")]
        Sma = 0,
        [Display(Name = "EMA")]
        Ema = 1
    }

    public enum AverageColorMode
    {
        [Display(Name = "Fixed")]
        Fixed,
        [Display(Name = "By Slope")]
        Slope,
        [Display(Name = "By Zero Cross")]
        ZeroCross
    }

    #endregion

    #region Fields

    private readonly CandleDataSeries _candles = new("Candles", "Delta candles")
    {
        DownCandleColor = Color.Red.Convert(),
        UpCandleColor = Color.Green.Convert(),
        IsHidden = true,
        ShowCurrentValue = false,
        UseMinimizedModeIfEnabled = true,
        ResetAlertsOnNewBar = true
    };

    private readonly ValueDataSeries _currentValues = new("CurrentValues", "Current Values")
    {
        IsHidden = true,
        VisualType = VisualMode.OnlyValueOnAxis,
        ShowZeroValue = false,
        UseMinimizedModeIfEnabled = true,
        IgnoredByAlerts = true
    };

    private readonly ValueDataSeries _delta = new("DeltaId", "Delta")
    {
        Color = Color.Red.Convert(),
        VisualType = VisualMode.Hide,
        ShowZeroValue = false,
        ShowCurrentValue = false,
        IsHidden = true,
        UseMinimizedModeIfEnabled = true,
        ResetAlertsOnNewBar = true
    };

    private readonly ValueDataSeries _diapasonHigh = new("DiapasonHigh", "Delta range high")
    {
        Color = CrossColor.FromArgb(128, 128, 128, 128),
        ShowZeroValue = false,
        ShowCurrentValue = false,
        VisualType = VisualMode.Hide,
        IsHidden = true,
        UseMinimizedModeIfEnabled = true,
        IgnoredByAlerts = true
    };

    private readonly ValueDataSeries _diapasonLow = new("DiapasonLow", "Delta range low")
    {
        Color = CrossColor.FromArgb(128, 128, 128, 128),
        ShowZeroValue = false,
        ShowCurrentValue = false,
        VisualType = VisualMode.Hide,
        IsHidden = true,
        UseMinimizedModeIfEnabled = true,
        IgnoredByAlerts = true
    };

    private readonly Dictionary<int, bool> _divergenceBars = new();

    private readonly CandleDataSeries _divergenceCandles = new("DivergenceCandles", "Divergence candles")
    {
        IsHidden = false,
        ShowCurrentValue = false,
        UseMinimizedModeIfEnabled = true,
        Visible = false
    };

    private readonly CandleDataSeries _divergenceDownCandles = new("DivergenceDownCandles", "Divergence down candles")
    {
        IsHidden = false,
        ShowCurrentValue = false,
        UseMinimizedModeIfEnabled = true,
        Visible = false
    };

    private readonly CandleDataSeries _downCandles = new("DownCandles", "Delta candles")
    {
        DownCandleColor = Color.Green.Convert(),
        UpCandleColor = Color.Red.Convert(),
        IsHidden = true,
        ShowCurrentValue = false,
        UseMinimizedModeIfEnabled = true,
        ResetAlertsOnNewBar = true
    };

    private BarDirection _barDirection;
    private DeltaType _deltaType;
    private Color _downColor = Color.Red;

    private ValueDataSeries _downSeries = new("DownSeries", Strings.Down)
    {
        VisualType = VisualMode.Hide,
        IsHidden = true,
        UseMinimizedModeIfEnabled = true,
        IgnoredByAlerts = true
    };

    private decimal _filter;

    private Color _fontColor;

    private RenderStringFormat _format = new()
    {
        Alignment = StringAlignment.Center,
        LineAlignment = StringAlignment.Center
    };

    private int _lastBar;
    private int _lastBarPositiveAlert;
    private int _lastBarNegativeAlert;
    private bool _minimizedMode;
    private DeltaVisualMode _mode = DeltaVisualMode.Candles;

    private Color _neutralColor = Color.Gray;
    private decimal _prevDeltaValue;
    private bool _showCurrentValues = true;

    private Color _upColor = Color.Green;

    private ValueDataSeries _upSeries = new("UpSeries", Strings.Up)
    {
        Color = Color.Green.Convert(),
        VisualType = VisualMode.Hide,
        IsHidden = true,
        UseMinimizedModeIfEnabled = true,
        IgnoredByAlerts = true
    };

    // Pens
    private RenderPen _penUpMinor, _penUpMajor, _penDnMinor, _penDnMajor;

    private RenderPen BuildPen(ValueDataSeries series)
    {
        return new PenSettings
        {
            Color = series.Color,
            Width = series.Width,
            LineDashStyle = series.LineDashStyle
        }.RenderObject;
    }

    private void RebuildThresholdPens()
    {
        _penUpMinor = BuildPen(_upMinor);
        _penUpMajor = BuildPen(_upMajor);
        _penDnMinor = BuildPen(_dnMinor);
        _penDnMajor = BuildPen(_dnMajor);
    }

    //UI Strings
    private const string UiGroupVisualSignals = "Visual signals in price panel";
    private const string UiGroupFixedThreshold = "Fixed Threshold";
    private const string UiGroupDynamicThreshold = "Dynamic Threshold";

    // --- Average Delta Fields ---
    private readonly ValueDataSeries _avgSeries = new("AverageSeries", "Average Delta")
    {
        VisualType = VisualMode.Hide, // Hidden by default
        Color = Color.Cyan.Convert(),
        Width = 2,
        IgnoredByAlerts = true,
        UseMinimizedModeIfEnabled = true
    };

    private readonly SMA _sma = new() { Period = 10 };
    private readonly EMA _ema = new() { Period = 10 };

    private Color _avgSlopeUpColor = Color.Cyan;
    private Color _avgSlopeDownColor = Color.Magenta;

    #endregion

    #region Fields (price signals)

    private readonly ValueDataSeries _priceSignalUp = new("PriceSignalUp", "Price Signal Up")
    {
        VisualType = VisualMode.Hide,
        IsHidden = true,
        UseMinimizedModeIfEnabled = true,
        IgnoredByAlerts = true
    };

    private readonly ValueDataSeries _priceSignalDown = new("PriceSignalDown", "Price Signal Down")
    {
        VisualType = VisualMode.Hide,
        IsHidden = true,
        UseMinimizedModeIfEnabled = true,
        IgnoredByAlerts = true
    };

    private bool _visualEnabled = true;
    private int _priceSignalOffsetTicks = 2;
    private int _priceSignalSize = 10;
    private Color _priceSignalUpColor = Color.Lime;
    private Color _priceSignalDownColor = Color.Fuchsia;

    #endregion

    #region Fields (threshold lines)

    private readonly ValueDataSeries _upMajor = new("UpMajor", "Up Major") { VisualType = VisualMode.Line, ShowCurrentValue = false, UseMinimizedModeIfEnabled = true };
    private readonly ValueDataSeries _upMinor = new("UpMinor", "Up Minor") { VisualType = VisualMode.Line, ShowCurrentValue = false, UseMinimizedModeIfEnabled = true };
    private readonly ValueDataSeries _dnMinor = new("DnMinor", "Down Minor") { VisualType = VisualMode.Line, ShowCurrentValue = false, UseMinimizedModeIfEnabled = true };
    private readonly ValueDataSeries _dnMajor = new("DnMajor", "Down Major") { VisualType = VisualMode.Line, ShowCurrentValue = false, UseMinimizedModeIfEnabled = true };

    private void SetupThresholdPens()
    {
        // Solid majors
        _upMajor.Color = CrossColor.FromArgb(255, 169, 169, 169); // DarkGray
        _upMajor.Width = 1;
        _upMajor.LineDashStyle = LineDashStyle.Solid;

        _dnMajor.Color = CrossColor.FromArgb(255, 169, 169, 169); // DarkGray
        _dnMajor.Width = 1;
        _dnMajor.LineDashStyle = LineDashStyle.Solid;

        // Dotted minors
        _upMinor.Color = CrossColor.FromArgb(255, 105, 105, 105); // DimGray
        _upMinor.Width = 1;
        _upMinor.LineDashStyle = LineDashStyle.Dot;

        _dnMinor.Color = CrossColor.FromArgb(255, 105, 105, 105); // DimGray
        _dnMinor.Width = 1;
        _dnMinor.LineDashStyle = LineDashStyle.Dot;

        RebuildThresholdPens();
    }

    #endregion

    #region Fields (dynamic thresholds)
    // Gate: how many useful samples are needed to compute mean/std reliably (Welford)
    private int _samplesForMeanStd = 1;

    // Per-side readiness flags (updated each bar)
    private bool _posReady;
    private bool _negReady;

    // Last computed dynamic thresholds (cached for the current bar)
    private decimal _dynPosMinor, _dynPosMajor;
    private decimal _dynNegMinor, _dynNegMajor;

    private readonly List<bool> _posReadyByBar = new();
    private readonly List<bool> _negReadyByBar = new();

    private void EnsureReadyListsSize(int bar)
    {
        while (_posReadyByBar.Count <= bar) _posReadyByBar.Add(false);
        while (_negReadyByBar.Count <= bar) _negReadyByBar.Add(false);
    }

    // ---- Welford accumulators per side (cumulative, no removals) ----
    private struct WelfordAcc
    {
        public int Count;
        public decimal Mean;
        public decimal M2;

        public void Add(decimal x)
        {
            Count++;
            var delta = x - Mean;
            Mean += delta / Count;
            var delta2 = x - Mean;
            M2 += delta * delta2;
        }

        public decimal Std()
        {
            if (Count <= 1) return 0m;
            var var = M2 / (Count - 1);
            return (decimal)Math.Sqrt((double)var);
        }

        public void Reset() { Count = 0; Mean = 0; M2 = 0; }
    }

    private WelfordAcc _posAcc; // samples > 0 from MaxDelta
    private WelfordAcc _negAcc; // samples < 0 from MinDelta

    private void ResetDynamicState()
    {
        _posAcc.Reset();
        _negAcc.Reset();
        _posReadyByBar.Clear();
        _negReadyByBar.Clear();
    }

    #endregion

    #region Properties

    #region Visualization

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.VisualMode), GroupName = nameof(Strings.Visualization),
            Description = nameof(Strings.VisualModeDescription), Order = 10)]
    public DeltaVisualMode Mode
    {
        get => _mode;
        set
        {
            _mode = value;

            if (_mode == DeltaVisualMode.Histogram)
            {
                _delta.VisualType = VisualMode.Histogram;
                _diapasonHigh.VisualType = VisualMode.Hide;
                _diapasonLow.VisualType = VisualMode.Hide;
                _candles.Visible = _downCandles.Visible = false;
                _divergenceCandles.Visible = _divergenceDownCandles.Visible = false;
            }
            else if (_mode == DeltaVisualMode.HighLow)
            {
                _delta.VisualType = VisualMode.Histogram;
                _diapasonHigh.VisualType = VisualMode.Histogram;
                _diapasonLow.VisualType = VisualMode.Histogram;
                _candles.Visible = _downCandles.Visible = false;
                _divergenceCandles.Visible = _divergenceDownCandles.Visible = false;
            }
            else if (_mode == DeltaVisualMode.Candles)
            {
                _delta.VisualType = VisualMode.Hide;
                _diapasonHigh.VisualType = VisualMode.Hide;
                _diapasonLow.VisualType = VisualMode.Hide;
                _candles.Visible = _downCandles.Visible = true;
                _candles.Mode = _downCandles.Mode = CandleVisualMode.Candles;
                _divergenceCandles.Mode = _divergenceDownCandles.Mode = CandleVisualMode.Candles;
            }
            else
            {
                _delta.VisualType = VisualMode.Hide;
                _diapasonHigh.VisualType = VisualMode.Hide;
                _diapasonLow.VisualType = VisualMode.Hide;
                _candles.Visible = _downCandles.Visible = true;
                _candles.Mode = _downCandles.Mode = CandleVisualMode.Bars;
                _divergenceCandles.Mode = _divergenceDownCandles.Mode = CandleVisualMode.Bars;
            }

            RaisePropertyChanged("Mode");
            RecalculateValues();

            ApplyDivergenceColorsToCurrentMode();
            UpdateDivergenceCandlesVisibility();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimizedMode), GroupName = nameof(Strings.Visualization),
        Description = nameof(Strings.HistogramMinimizedModeDescription), Order = 20)]

    public bool MinimizedMode
    {
        get => _minimizedMode;
        set
        {
            _minimizedMode = value;
            RaisePropertyChanged("MinimizedMode");
            RecalculateValues();
            UpdateDivergenceCandlesVisibility();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowCurrentValue), GroupName = nameof(Strings.Visualization),
        Description = nameof(Strings.ShowCurrentValueDescription), Order = 30)]
    public bool ShowCurrentValues
    {
        get => _showCurrentValues;
        set
        {
            _showCurrentValues = value;
            _currentValues.ShowCurrentValue = value;
        }
    }

    // --- Threshold source & parameters ---
    [DisplayName("Show Threshold lines")]
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Visualization),
        Description = "Show horizontal threshold lines in the Delta panel", Order = 40)]
    public bool ShowThresholdLines
    {
        get => _showThresholdLines;
        set
        {
            if (_showThresholdLines == value) return;
            _showThresholdLines = value;
            UpdateThresholdSeries();

            // ensure dynamic thresholds/price signals are in sync with current UI
            RecalculateValues();
            RecalculateVisualSignals();
            RedrawChart();
        }
    }
    private bool _showThresholdLines = true;

    [DisplayName("Threshold source")]
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Visualization), Order = 50)]
    public ThresholdSource Thresholds
    {
        get => _thresholds;
        set
        {
            if (_thresholds == value) return;
            _thresholds = value;

            RecalculateValues();
            RecalculateVisualSignals();
            RedrawChart();
        }
    }
    private ThresholdSource _thresholds = ThresholdSource.Fixed;

    // Fixed levels
    [DisplayName("Upper major")]
    [Display(GroupName = UiGroupFixedThreshold, Description = "Upper major threshold", Order = 51)]
    [Range(0, int.MaxValue)]
    [DisplayFormat(DataFormatString = "F0")]
    public decimal UpMajorLevel
    {
        get => _upMajorLevel;
        set
        {
            if (_upMajorLevel == value) return;
            _upMajorLevel = value;
            UpdateThresholdSeries();
            RecalculateVisualSignals();
        }
    }
    private decimal _upMajorLevel = 500m;

    [DisplayName("Upper minor")]
    [Display(GroupName = UiGroupFixedThreshold, Description = "Upper minor threshold", Order = 52)]
    [Range(0, int.MaxValue)]
    [DisplayFormat(DataFormatString = "F0")]
    public decimal UpMinorLevel
    {
        get => _upMinorLevel;
        set
        {
            if (_upMinorLevel == value) return;
            _upMinorLevel = value;
            UpdateThresholdSeries();
            RecalculateVisualSignals();
        }
    }
    private decimal _upMinorLevel = 250m;

    [DisplayName("Lower minor")]
    [Display(GroupName = UiGroupFixedThreshold, Description = "Lower minor threshold", Order = 53)]
    [Range(int.MinValue, 0)]
    [DisplayFormat(DataFormatString = "F0")]
    public decimal DownMinorLevel
    {
        get => _downMinorLevel;
        set
        {
            if (_downMinorLevel == value) return;
            _downMinorLevel = value;
            UpdateThresholdSeries();
            RecalculateVisualSignals();
        }
    }
    private decimal _downMinorLevel = -250m;

    [DisplayName("Lower major")]
    [Display(GroupName = UiGroupFixedThreshold, Description = "Lower major threshold", Order = 54)]
    [Range(int.MinValue, 0)]
    [DisplayFormat(DataFormatString = "F0")]
    public decimal DownMajorLevel
    {
        get => _downMajorLevel;
        set
        {
            if (_downMajorLevel == value) return;
            _downMajorLevel = value;
            UpdateThresholdSeries();
            RecalculateVisualSignals();
        }
    }
    private decimal _downMajorLevel = -500m;

    // ---- Dynamic levels ----

    // === Session window (time-of-day anchored, daily reset) ===
    public enum SessionWindowMode { RTH, Full24h }

    private SessionWindowMode _sessionMode = SessionWindowMode.RTH;
    [DisplayName("Session Window Mode")]
    [Display(GroupName = UiGroupDynamicThreshold, Order = 55)]
    public SessionWindowMode SessionMode
    {
        get => _sessionMode;
        set
        {
            if (_sessionMode == value) return;
            _sessionMode = value;
            RecalculateValues();
            RecalculateVisualSignals();
            RedrawChart();
        }
    }

    [DisplayName("RTH Start (HH:mm)")]
    [Display(GroupName = UiGroupDynamicThreshold, Order = 56)]
    public TimeSpan RthStart { get; set; } = new TimeSpan(9, 30, 0);

    [DisplayName("RTH End (HH:mm)")]
    [Display(GroupName = UiGroupDynamicThreshold, Order = 57)]
    public TimeSpan RthEnd { get; set; } = new TimeSpan(16, 0, 0);


    // Multiplier k for mean +- k*std (per side)
    private decimal _stdMultiplier = 1.0m;

    [DisplayName("Std Multiplier (k)")]
    [Display(GroupName = UiGroupDynamicThreshold, Description = "Thresholds use mean +- k*std (cumulative since session anchor).", Order = 58)]
    [Range(typeof(decimal), "0", "10")]
    [DisplayFormat(DataFormatString = "F2")]
    public decimal StdMultiplier
    {
        get => _stdMultiplier;
        set
        {
            if (value < 0) value = 0;
            if (_stdMultiplier == value) return;
            _stdMultiplier = value;
            RecalculateValues();
            RecalculateVisualSignals();
            RedrawChart();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BullishColor), GroupName = nameof(Strings.Drawing),
        Description = nameof(Strings.PositiveValueColorDescription), Order = 60)]
    public CrossColor UpColor
    {
        get => _upColor.Convert();
        set
        {
            _upColor = value.Convert();
            _candles.UpCandleColor = value;
            _upSeries.Color = value;
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BearlishColor), GroupName = nameof(Strings.Drawing),
        Description = nameof(Strings.NegativeValueColorDescription), Order = 70)]
    public CrossColor DownColor
    {
        get => _downColor.Convert();
        set
        {
            _downColor = value.Convert();
            _candles.DownCandleColor = value;
            _downCandles.UpCandleColor = value;
            _downSeries.Color = value;
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.NeutralBorderColor), GroupName = nameof(Strings.Drawing),
        Description = nameof(Strings.NeutralValueDescription), Order = 80)]
    public CrossColor NeutralColor
    {
        get => _neutralColor.Convert();
        set
        {
            _neutralColor = value.Convert();
            _candles.BorderColor = _downCandles.BorderColor = value;
            _diapasonHigh.Color = _diapasonLow.Color = value;
        }
    }

    #endregion

    #region Filters

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BarsDirection), GroupName = nameof(Strings.Filters),
        Description = nameof(Strings.BarDirectionDescription), Order = 100)]
    public BarDirection BarsDirection
    {
        get => _barDirection;
        set
        {
            _barDirection = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.DeltaType), GroupName = nameof(Strings.Filters),
        Description = nameof(Strings.DeltaTypeDescription), Order = 110)]
    public DeltaType DeltaTypes
    {
        get => _deltaType;
        set
        {
            _deltaType = value;
            RecalculateValues();
        }
    }

    [Parameter]
    [Range(0, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.Filters),
        Description = nameof(Strings.MinDeltaVolumeFilterCommonDescription), Order = 120)]
    public decimal Filter
    {
        get => _filter;
        set
        {
            _filter = value;
            RecalculateValues();
        }
    }

    #endregion

    #region Divergence

    private Indicators.FilterColor _divergenceBarsFilter = new(true) { Enabled = false, Value = CrossColor.FromArgb(255, 255, 165, 0) };

    [Display(ResourceType = typeof(Strings), Name = "DivergenceDots", GroupName = nameof(Strings.Divergence),
        Description = nameof(Strings.BarDirVsDeltaDivergenceDescription), Order = 130)]
    public bool ShowDivergence { get; set; }

    [Display(ResourceType = typeof(Strings), Name = "DivergenceBars", GroupName = nameof(Strings.Divergence), Order = 135)]
    public Indicators.FilterColor DivergenceBarsFilter
    {
        get => _divergenceBarsFilter;
        set
        {
            if (_divergenceBarsFilter == value)
                return;

            if (_divergenceBarsFilter != null)
                _divergenceBarsFilter.PropertyChanged -= OnDivergenceFilterChanged;

            _divergenceBarsFilter = value;

            if (_divergenceBarsFilter != null)
                _divergenceBarsFilter.PropertyChanged += OnDivergenceFilterChanged;

            RaisePropertyChanged(nameof(DivergenceBarsFilter));
        }
    }

    #endregion

    #region Absorption

    private readonly CandleDataSeries _absorptionCandles = new("AbsorptionDotsCandles", "Absorption Dots")
    {
        UpCandleColor = Color.Green.Convert(),
        DownCandleColor = Color.Red.Convert(),
        BorderColor = CrossColor.FromArgb(0, 0, 0, 0),
        IsHidden = false,
        UseMinimizedModeIfEnabled = true,
        ShowCurrentValue = false
    };

    private FilterInt _absorption = new(true) { Enabled = false, Value = 250 };

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Absorption), GroupName = nameof(Strings.Absorption),
        Description = "AbsorptionThresholdDesc", Order = 140)]
    [Range(0, int.MaxValue)]
    public FilterInt Absorption
    {
        get => _absorption;
        set
        {
            if (_absorption == value)
                return;

            if (_absorption != null)
                _absorption.PropertyChanged -= OnAbsorptionFilterChanged;

            _absorption = value;

            if (_absorption != null)
                _absorption.PropertyChanged += OnAbsorptionFilterChanged;

            RaisePropertyChanged(nameof(Absorption));
        }
    }

    // Backward compatibility properties (hidden from UI)
    [Browsable(false)]
    public bool ShowAbsorptionDots
    {
        get => Absorption.Enabled;
        set => Absorption.Enabled = value;
    }

    [Browsable(false)]
    public int AbsorptionDeltaThreshold
    {
        get => Absorption.Value;
        set => Absorption.Value = value;
    }

    #endregion

    #region Volume

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Show), GroupName = nameof(Strings.VolumeLabel), Order = 200,
        Description = nameof(Strings.VolumeLabelDescription))]
    public bool ShowVolume { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.VolumeLabel),
        Description = nameof(Strings.LabelTextColorDescription), Order = 210)]
    public CrossColor FontColor
    {
        get => _fontColor.Convert();
        set => _fontColor = value.Convert();
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Location), GroupName = nameof(Strings.VolumeLabel),
        Description = nameof(Strings.LabelLocationDescription), Order = 220)]
    public Location VolLocation { get; set; } = Location.Middle;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Font), GroupName = nameof(Strings.VolumeLabel),
        Description = nameof(Strings.FontSettingDescription), Order = 230)]
    public FontSetting Font { get; set; } = new("Arial", 10);

    #endregion

    #region Visual signals in price
    // --- Price Signals on price panel ---
    [DisplayName("Price signal Offset ticks")]
    [Display(GroupName = UiGroupVisualSignals, Description = "Distance from High/Low in ticks", Order = 250)]
    [Range(0, 50)]
    public int PriceSignalOffsetTicks
    {
        get => _priceSignalOffsetTicks;
        set { _priceSignalOffsetTicks = value; RedrawChart(); }
    }

    [DisplayName("Price Signal Size")]
    [Display(GroupName = UiGroupVisualSignals, Description = "Signal size in pixels", Order = 260)]
    [Range(6, 24)]
    public int PriceSignalSize
    {
        get => _priceSignalSize;
        set { _priceSignalSize = value; RedrawChart(); }
    }

    [DisplayName("Price Signal up color")]
    [Display(GroupName = UiGroupVisualSignals, Description = "Up signal color", Order = 280)]
    public CrossColor PriceSignalUpColor
    {
        get => _priceSignalUpColor.Convert();
        set { _priceSignalUpColor = value.Convert(); RedrawChart(); }
    }

    [DisplayName("Price Signal down color")]
    [Display(GroupName = UiGroupVisualSignals, Description = "Down signal color", Order = 290)]
    public CrossColor PriceSignalDownColor
    {
        get => _priceSignalDownColor.Convert();
        set { _priceSignalDownColor = value.Convert(); RedrawChart(); }
    }

    [DisplayName("Show Visual Alerts")]
    [Display(GroupName = UiGroupVisualSignals, Description = "Draw visual signals on price when delta crosses thresholds", Order = 300)]
    public bool VisualEnabled
    {
        get => _visualEnabled;
        set
        {
            _visualEnabled = value;
            RedrawChart();
        }
    }

    private ThresholdLevel _visualUpLevel = ThresholdLevel.Major;
    [DisplayName("Visual Up Thresholds")]
    [Display(GroupName = UiGroupVisualSignals, Order = 310)]
    public ThresholdLevel VisualUpLevel
    {
        get => _visualUpLevel;
        set
        {
            _visualUpLevel = value;
            RecalculateValues();
            RedrawChart();
        }
    }

    private ThresholdLevel _visualDownLevel = ThresholdLevel.Major;
    [DisplayName("Visual Down Thresholds")]
    [Display(GroupName = UiGroupVisualSignals, Order = 315)]
    public ThresholdLevel VisualDownLevel
    {
        get => _visualDownLevel;
        set
        {
            _visualDownLevel = value;
            RecalculateValues();
            RedrawChart();
        }
    }
    #endregion

    #region Alerts

    [Browsable(false)]
    public Filter UpAlert { get; set; } = new()
    { Enabled = false, Value = 0 };

    [Browsable(false)]
    public Filter DownAlert { get; set; } = new()
    { Enabled = false, Value = 0 };

    [Browsable(false)]
    public bool UseAlerts
    {
        get => UpAlert.Enabled;
        set => UpAlert.Enabled = value;
    }

    [Browsable(false)]
    public decimal AlertFilter
    {
        get => UpAlert.Value;
        set => UpAlert.Value = value;
    }

    [Browsable(false)]
    public bool UseNegativeAlerts
    {
        get => DownAlert.Enabled;
        set => DownAlert.Enabled = value;
    }

    [Browsable(false)]
    public decimal NegativeAlertFilter
    {
        get => DownAlert.Value;
        set => DownAlert.Value = value;
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.Alerts),
    Description = nameof(Strings.AlertFileDescription), Order = 320)]
    public string AlertFile { get; set; } = "alert1";

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.FontColor), GroupName = nameof(Strings.Alerts),
    Description = nameof(Strings.AlertTextColorDescription), Order = 330)]
    public CrossColor AlertForeColor { get; set; } = CrossColor.FromArgb(255, 247, 249, 249);

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BackGround), GroupName = nameof(Strings.Alerts),
    Description = nameof(Strings.AlertFillColorDescription), Order = 340)]
    public CrossColor AlertBGColor { get; set; } = CrossColor.FromArgb(255, 75, 72, 72);

    // === New enums for unified alerting ===
    public enum ThresholdSource
    {
        Fixed = 0,
        DynamicSigned = 1 // compute pos/neg thresholds from same-sign samples only
    }
    public enum ThresholdLevel { Major = 0, Minor = 1 }

    // === Channel toggles ===
    [DisplayName("Audio Alerts")]
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Alerts), Order = 351)]
    public bool AudioEnabled { get; set; } = true;

    [DisplayName("Audio Up Thresholds")]
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Alerts), Order = 362)]
    public ThresholdLevel AudioUpLevel { get; set; } = ThresholdLevel.Major;

    [DisplayName("Audio Down Thresholds")]
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Alerts), Order = 363)]
    public ThresholdLevel AudioDownLevel { get; set; } = ThresholdLevel.Major;

    // === Bar-close audio policy ===
    [DisplayName("Audio at bar close only")]
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Alerts), Order = 364)]
    public bool AudioAtBarCloseOnly { get; set; } = true;

    #endregion

    #region Average Delta

    [Display(Name = "Show Average", GroupName = "Average", Order = 400)]
    public bool ShowAverage
    {
        get => _showAverage;
        set
        {
            _showAverage = value;
            _avgSeries.VisualType = value ? VisualMode.Line : VisualMode.Hide;
            RaisePropertyChanged(nameof(ShowAverage));
            RecalculateValues();
        }
    }
    private bool _showAverage;

    [Display(Name = "Period", GroupName = "Average", Order = 410)]
    [Range(1, 1000)]
    public int AveragePeriod
    {
        get => _sma.Period;
        set
        {
            _sma.Period = value;
            _ema.Period = value;
            RecalculateValues();
        }
    }

    [Display(Name = "Calculation Mode", GroupName = "Average", Order = 420)]
    public AverageMode AvgMode
    {
        get => _avgMode;
        set
        {
            _avgMode = value;
            RecalculateValues();
        }
    }
    private AverageMode _avgMode = AverageMode.Sma;

    [Display(Name = "Color Mode", GroupName = "Average", Order = 425)]
    public AverageColorMode AvgColorMode
    {
        get => _avgColorMode;
        set
        {
            _avgColorMode = value;
            RecalculateValues();
        }
    }
    private AverageColorMode _avgColorMode = AverageColorMode.Fixed;

    [Display(Name = "Base Color", GroupName = "Average", Order = 430)]
    public CrossColor AverageColor
    {
        get => _avgSeries.Color;
        set => _avgSeries.Color = value;
    }

    [Display(Name = "Slope Up Color", GroupName = "Average", Order = 431)]
    public CrossColor AvgSlopeUpColor
    {
        get => _avgSlopeUpColor.Convert();
        set
        {
            _avgSlopeUpColor = value.Convert(); // Convierte UI -> System.Drawing.Color
            RecalculateValues();
        }
    }

    [Display(Name = "Slope Down Color", GroupName = "Average", Order = 432)]
    public CrossColor AvgSlopeDownColor
    {
        get => _avgSlopeDownColor.Convert();
        set
        {
            _avgSlopeDownColor = value.Convert(); // Convierte UI -> System.Drawing.Color
            RecalculateValues();
        }
    }

    [Display(Name = "Width", GroupName = "Average", Order = 440)]
    [Range(1, 10)]
    public int AverageWidth
    {
        get => _avgSeries.Width;
        set => _avgSeries.Width = value;
    }

    #endregion

    #endregion

    #region ctor

    public Delta()
            : base(true)
    {
        EnableCustomDrawing = true;
        SubscribeToDrawingEvents(DrawingLayouts.Final);
        FontColor = Color.Blue.Convert();

        Panel = IndicatorDataProvider.NewPanel;
        DataSeries[0] = _delta;

        DataSeries.Insert(0, _diapasonHigh);
        DataSeries.Insert(1, _diapasonLow);
        DataSeries.Add(_candles);

        DataSeries.Add(_upSeries);
        DataSeries.Add(_downSeries);
        DataSeries.Add(_currentValues);
        DataSeries.Add(_downCandles);
        DataSeries.Add(_divergenceCandles);
        DataSeries.Add(_divergenceDownCandles);

        // price signals + absorption
        DataSeries.Add(_priceSignalUp);
        DataSeries.Add(_priceSignalDown);
        DataSeries.Add(_absorptionCandles);

        // threshold lines
        DataSeries.Add(_upMajor);
        DataSeries.Add(_upMinor);
        DataSeries.Add(_dnMinor);
        DataSeries.Add(_dnMajor);

        // average delta
        DataSeries.Add(_avgSeries);

        SetupThresholdPens();
        UpdateThresholdSeries(repaint: false);

        UpAlert.PropertyChanged += (sender, e) => _lastBarPositiveAlert = 0;
        DownAlert.PropertyChanged += (sender, e) => _lastBarNegativeAlert = 0;
        _divergenceBarsFilter.PropertyChanged += OnDivergenceFilterChanged;
        _absorption.PropertyChanged += OnAbsorptionFilterChanged;

        UpdateDivergenceCandlesVisibility();

        if (DivergenceBarsFilter?.Enabled == true)
            RecalculateValues();
    }

    #endregion

    #region Protected methods

    protected override void OnApplyDefaultColors()
    {
        if (ChartInfo is null)
            return;

        UpColor = ChartInfo.ColorsStore.UpCandleColor.Convert();
        DownColor = ChartInfo.ColorsStore.DownCandleColor.Convert();
        NeutralColor = ChartInfo.ColorsStore.BarBorderPen.Color.Convert();
        FontColor = ChartInfo.ColorsStore.FootprintMaximumVolumeTextColor.Convert();

        UpdateDivergenceCandlesVisibility();
        RebuildThresholdPens();
    }

    protected override void OnRender(RenderContext context, DrawingLayouts layout)
    {
        if (ChartInfo is null || InstrumentInfo is null)
            return;

        // Divergence dots on price panel
        if (ShowDivergence)
        {
            for (var i = FirstVisibleBarNumber; i <= LastVisibleBarNumber; i++)
            {
                try
                {
                    if (_upSeries[i] == 0 && _downSeries[i] == 0)
                        continue;

                    var candle = GetCandle(i);
                    var x = ChartInfo.PriceChartContainer.GetXByBar(i, false);

                    if (_upSeries[i] != 0)
                    {
                        var yPrice = ChartInfo.PriceChartContainer.GetYByPrice(candle.Low, false) + 10;
                        if (yPrice >= ChartInfo.PriceChartContainer.Region.Top &&
                            yPrice <= ChartInfo.PriceChartContainer.Region.Bottom)
                        {
                            var rect = new Rectangle(x - 5, yPrice - 4, 8, 8);
                            context.FillEllipse(_upColor, rect);
                        }
                    }

                    if (_downSeries[i] != 0)
                    {
                        var yPrice = ChartInfo.PriceChartContainer.GetYByPrice(candle.High, false) - 10;
                        if (yPrice >= ChartInfo.PriceChartContainer.Region.Top &&
                            yPrice <= ChartInfo.PriceChartContainer.Region.Bottom)
                        {
                            var rect = new Rectangle(x - 5, yPrice - 4, 8, 8);
                            context.FillEllipse(_downColor, rect);
                        }
                    }
                }
                catch (OverflowException)
                {
                    return;
                }
            }
        }

        // Price signals (triangles) - only on price panel
        if (VisualEnabled)
        {
            var priceRc = ChartInfo.PriceChartContainer.Region;
            var half = _priceSignalSize / 2;

            for (var i = FirstVisibleBarNumber; i <= LastVisibleBarNumber; i++)
            {
                var x = ChartInfo.PriceChartContainer.GetXByBar(i, false);

                // Up triangle
                var upPrice = _priceSignalUp[i];
                if (upPrice > 0)
                {
                    var yPx = ChartInfo.PriceChartContainer.GetYByPrice(upPrice, false);
                    if (yPx >= priceRc.Top && yPx <= priceRc.Bottom)
                    {
                        var p1 = new System.Drawing.Point(x, yPx - half);
                        var p2 = new System.Drawing.Point(x - half, yPx + half);
                        var p3 = new System.Drawing.Point(x + half, yPx + half);
                        context.FillPolygon(_priceSignalUpColor, new[] { p1, p2, p3 });
                    }
                }

                // Down triangle
                var dnPrice = _priceSignalDown[i];
                if (dnPrice > 0)
                {
                    var yPx = ChartInfo.PriceChartContainer.GetYByPrice(dnPrice, false);
                    if (yPx >= priceRc.Top && yPx <= priceRc.Bottom)
                    {
                        var p1 = new System.Drawing.Point(x, yPx + half);
                        var p2 = new System.Drawing.Point(x - half, yPx - half);
                        var p3 = new System.Drawing.Point(x + half, yPx - half);
                        context.FillPolygon(_priceSignalDownColor, new[] { p1, p2, p3 });
                    }
                }
            }
        }

        // Volume labels on delta panel (same as original)
        if (!ShowVolume || ChartInfo.ChartVisualMode != ChartVisualModes.Clusters || Panel == IndicatorDataProvider.CandlesPanel)
            return;

        var minWidth = GetMinWidth(context, FirstVisibleBarNumber, LastVisibleBarNumber);
        var barWidth = ChartInfo.GetXByBar(1) - ChartInfo.GetXByBar(0);

        if (minWidth > barWidth)
            return;

        var strHeight = context.MeasureString("0", Font.RenderObject).Height;

        var y = VolLocation switch
        {
            Location.Up => Container.Region.Y,
            Location.Down => Container.Region.Bottom - strHeight,
            _ => Container.Region.Y + (Container.Region.Bottom - Container.Region.Y) / 2
        };

        for (var i = FirstVisibleBarNumber; i <= LastVisibleBarNumber; i++)
        {
            decimal value;

            if (MinimizedMode)
            {
                value = _candles[i].Close > 0
                ? _candles[i].Close
                : -_downCandles[i].Close;
            }
            else
                value = _candles[i].Close;

            var renderText = ChartInfo.TryGetMinimizedVolumeString(value);

            var strRect = new Rectangle(ChartInfo.GetXByBar(i),
                y,
                barWidth,
                strHeight);

            context.DrawString(renderText, Font.RenderObject, _fontColor, strRect, _format);
        }
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        if (bar == 0)
        {
            DataSeries.ForEach(x => x.Clear());
            _upSeries.Clear();
            _downSeries.Clear();
            _divergenceBars.Clear();
            ResetDynamicState();
            _lastBar = -1;
        }

        // clear price signal by default
        _priceSignalUp[bar] = 0;
        _priceSignalDown[bar] = 0;

        var candle = GetCandle(bar);
        var deltaValue = candle.Delta;
        var absDelta = Math.Abs(deltaValue);
        var maxDelta = candle.MaxDelta;
        var minDelta = candle.MinDelta;

		if (maxDelta == minDelta)
		{
			if(maxDelta > 0)
				minDelta = 0;
			else
				maxDelta = 0;
        }

        var isUnderFilter = absDelta < _filter;

        if (_barDirection == BarDirection.Bullish)
        {
            if (candle.Close < candle.Open)
                isUnderFilter = true;
        }
        else if (_barDirection == BarDirection.Bearlish)
        {
            if (candle.Close > candle.Open)
                isUnderFilter = true;
        }

        if (_deltaType == DeltaType.Negative && deltaValue > 0)
            isUnderFilter = true;

        if (_deltaType == DeltaType.Positive && deltaValue < 0)
            isUnderFilter = true;

        if (isUnderFilter)
        {
            deltaValue = 0;
            absDelta = 0;
            minDelta = maxDelta = 0;
        }

        _delta[bar] = MinimizedMode ? absDelta : deltaValue;

        if (MinimizedMode)
        {
            var high = Math.Abs(maxDelta);
            var low = Math.Abs(minDelta);
            _diapasonLow[bar] = Math.Min(Math.Min(high, low), absDelta);
            _diapasonHigh[bar] = Math.Max(high, low);

            if (deltaValue >= 0)
            {
                var currentCandle = _candles[bar];
                currentCandle.Open = deltaValue > 0 ? 0 : absDelta;
                currentCandle.Close = deltaValue > 0 ? absDelta : 0;
                currentCandle.High = _diapasonHigh[bar];
                currentCandle.Low = _diapasonLow[bar];
                _downCandles[bar] = new Candle();
            }
            else
            {
                var currentCandle = _downCandles[bar];
                currentCandle.Open = 0;
                currentCandle.Close = absDelta;
                currentCandle.High = _diapasonHigh[bar];
                currentCandle.Low = _diapasonLow[bar];
                _candles[bar] = new Candle();
            }
        }
        else
        {
            _diapasonLow[bar] = minDelta;
            _diapasonHigh[bar] = maxDelta;

            _candles[bar].Open = 0;
            _candles[bar].Close = deltaValue;
            _candles[bar].High = maxDelta;
            _candles[bar].Low = minDelta;
        }

        var hasDivergence = false;

        if (MinimizedMode)
        {
            if (candle.Close > candle.Open && deltaValue < 0)
            {
                _downSeries[bar] = _downCandles[bar].High;
                hasDivergence = true;
            }
            else if (candle.Close < candle.Open && deltaValue > 0)
            {
                _upSeries[bar] = _candles[bar].High;
                hasDivergence = true;
            }
            else
            {
                _upSeries[bar] = 0;
                _downSeries[bar] = 0;
            }
        }
        else
        {
            if (candle.Close > candle.Open && _candles[bar].Close < _candles[bar].Open)
            {
                _downSeries[bar] = _candles[bar].High;
                hasDivergence = true;
            }
            else
                _downSeries[bar] = 0;

            if (candle.Close < candle.Open && _candles[bar].Close > _candles[bar].Open)
            {
                _upSeries[bar] = _candles[bar].High;
                hasDivergence = true;
            }
            else
                _upSeries[bar] = 0;
        }

        _divergenceBars[bar] = hasDivergence;

        if (hasDivergence && DivergenceBarsFilter != null && DivergenceBarsFilter.Enabled &&
            (_mode == DeltaVisualMode.Candles || _mode == DeltaVisualMode.Bars))
        {
            if (MinimizedMode)
            {
                if (deltaValue >= 0)
                {
                    _divergenceCandles[bar] = _candles[bar];
                    _divergenceDownCandles[bar] = new Candle();
                }
                else
                {
                    _divergenceDownCandles[bar] = _downCandles[bar];
                    _divergenceCandles[bar] = new Candle();
                }
            }
            else
            {
                _divergenceCandles[bar] = _candles[bar];
                _divergenceDownCandles[bar] = new Candle();
            }
        }
        else
        {
            _divergenceCandles[bar] = new Candle();
            _divergenceDownCandles[bar] = new Candle();
        }

        _delta.Colors[bar] = deltaValue > 0 ? _upColor : _downColor;

        if (DivergenceBarsFilter != null && DivergenceBarsFilter.Enabled &&
            (_mode == DeltaVisualMode.Histogram || _mode == DeltaVisualMode.HighLow))
        {
            if (hasDivergence)
            {
                var divergenceColor = DivergenceBarsFilter.Value.Convert();
                _delta.Colors[bar] = divergenceColor;
            }
        }

        // --- session-anchor resets (daily, time-of-day) ---
        if (IsSessionStart(bar) && Thresholds == ThresholdSource.DynamicSigned)
        {
            // Reset accumulators AND cut all threshold polylines at the bar before the start
            ResetDynamicState();
            CutAllThresholdsAt(bar - 1);
        }

        var inside = InSession(candle.Time);

        // ===================== DYNAMIC (NO LOOK-AHEAD) =====================
        // 1) For bar 'b', we compute thresholds FROM STATE UP TO b-1
        //    (i.e., we do NOT include current bar extremes yet).
        // 2) After drawing/writing for 'b', we feed Welford with bar 'b'
        //    so it is available for 'b+1'.
        // ==================================================================

        // --- Readiness by side based on PREVIOUS state ---
        _posReady = _posAcc.Count >= _samplesForMeanStd;
        _negReady = _negAcc.Count >= _samplesForMeanStd;
        EnsureReadyListsSize(bar);
        _posReadyByBar[bar] = _posReady && inside;
        _negReadyByBar[bar] = _negReady && inside;

        // --- Compute dynamic thresholds (signed) from PREVIOUS state ---
        _dynPosMinor = _dynPosMajor = 0m;
        _dynNegMinor = _dynNegMajor = 0m;

        if (inside && Thresholds == ThresholdSource.DynamicSigned)
        {
            // Positive side (>= 0): mean and std of MaxDelta magnitudes
            if (_posReady)
            {
                var m = _posAcc.Mean;            // >= 0
                var s = _posAcc.Std();
                var k = _stdMultiplier;
                _dynPosMinor = m;                // mean
                _dynPosMajor = m + k * s;        // mean + k*std
            }

            // Negative side (<= 0): signed thresholds from |MinDelta|
            if (_negReady)
            {
                var mAbs = _negAcc.Mean;         // mean(|MinDelta|)
                var sAbs = _negAcc.Std();
                var k = _stdMultiplier;

                // NEGATIVE signed thresholds (downside): -(mean k*std) with the major more negative
                _dynNegMinor = -mAbs;            // -mean
                _dynNegMajor = -(mAbs + k * sAbs);
            }

            // write series for pickers/history only while inside session
            _upMinor[bar] = _dynPosMinor;
            _upMajor[bar] = _dynPosMajor;
            _dnMinor[bar] = _dynNegMinor;
            _dnMajor[bar] = _dynNegMajor;
        }

        // Outside session: cut the four lines and skip writing values this bar
        if (Thresholds == ThresholdSource.DynamicSigned && !inside)
        {
            CutAllThresholdsAt(bar - 1);
        }

        else if (Thresholds == ThresholdSource.DynamicSigned)
        {
            // Show/hide per readiness for this bar
            if (!_posReady) CutUpThresholdsAt(bar - 1);
            if (!_negReady) CutDownThresholdsAt(bar - 1);
        }

        // --- Show threshold lines (panel) ---
        if (ShowThresholdLines)
        {
            if (Thresholds == ThresholdSource.Fixed)
            {
                _upMajor[bar] = UpMajorLevel;
                _upMinor[bar] = UpMinorLevel;
                _dnMinor[bar] = DownMinorLevel;
                _dnMajor[bar] = DownMajorLevel;

                _upMajor.VisualType = _upMinor.VisualType = _dnMinor.VisualType = _dnMajor.VisualType = VisualMode.Line;
            }

            else // DynamicSigned
            {
                // Values above already written using PREVIOUS state.
                _upMajor.VisualType = _upMinor.VisualType = _dnMinor.VisualType = _dnMajor.VisualType = VisualMode.Line;

            }
        }
        else
        {
            _upMajor.VisualType = _upMinor.VisualType = _dnMinor.VisualType = _dnMajor.VisualType = VisualMode.Hide;
        }

        // --- Visual price signals use thresholds computed for THIS bar from PREVIOUS state
        if (VisualEnabled)
        {
            var visualUpTh = PickUpThreshold(bar, VisualUpLevel);
            var visualDownTh = PickDownThreshold(bar, VisualDownLevel);

            if (deltaValue >= visualUpTh)
                _priceSignalUp[bar] = GetCandle(bar).High + (InstrumentInfo?.TickSize ?? 1m) * _priceSignalOffsetTicks;

            if (deltaValue <= visualDownTh)
                _priceSignalDown[bar] = GetCandle(bar).Low - (InstrumentInfo?.TickSize ?? 1m) * _priceSignalOffsetTicks;
        }


        // --- Audio (edge or close confirmation) ---
        if (AudioEnabled)
        {
            var audioUpTh = PickUpThreshold(bar, AudioUpLevel);
            var audioDownTh = PickDownThreshold(bar, AudioDownLevel);

            // Case 1: Instant audio (intra-bar cross)
            if (!AudioAtBarCloseOnly)
            {
                if (_prevDeltaValue < audioUpTh && deltaValue >= audioUpTh && _lastBarPositiveAlert != bar)
                {
                    _lastBarPositiveAlert = bar;
                    TryAddAlert($"Delta >= {audioUpTh} (UP)");
                }

                if (_prevDeltaValue > audioDownTh && deltaValue <= audioDownTh && _lastBarNegativeAlert != bar)
                {
                    _lastBarNegativeAlert = bar;
                    TryAddAlert($"Delta <= {audioDownTh} (DOWN)");
                }
            }
            // Case 2: Audio only at confirmed bar close
            else
            {
                // Run when we are calculating the previous bar (already closed)
                if (bar == CurrentBar - 2)
                {
                    var prevBarDelta = _delta[bar];
                    if (prevBarDelta >= audioUpTh && _lastBarPositiveAlert != bar)
                    {
                        _lastBarPositiveAlert = bar;
                        TryAddAlert($"Delta CLOSE >= {audioUpTh} (UP)");
                    }
                    else if (prevBarDelta <= audioDownTh && _lastBarNegativeAlert != bar)
                    {
                        _lastBarNegativeAlert = bar;
                        TryAddAlert($"Delta CLOSE <= {audioDownTh} (DOWN)");
                    }
                }
            }
        }

        // Update _prevDeltaValue ONCE at the end of the unified block
        _prevDeltaValue = deltaValue;

        // ===================== FEED WELFORD AFTER DRAWING (NO LOOK-AHEAD) =====================
        int barToFeed = bar - 1;

        if (barToFeed >= 0 && _lastBar != barToFeed)
        {
            var prevCandle = GetCandle(barToFeed);

            if (InSession(prevCandle.Time))
            {
                if (prevCandle.MaxDelta > 0)// positive side uses MaxDelta
                    _posAcc.Add(prevCandle.MaxDelta);

                if (prevCandle.MinDelta < 0)// negative side uses |MinDelta|
                    _negAcc.Add(Math.Abs(prevCandle.MinDelta));
            }

            _lastBar = barToFeed;
        }


        // Absorption dots in delta panel
        if (Absorption.Enabled)
        {
            decimal deltaOpen, deltaClose, deltaHigh, deltaLow;

            if (MinimizedMode)
            {
                deltaClose = _delta[bar];
                deltaHigh = _diapasonHigh[bar];
                deltaLow = _diapasonLow[bar];
                deltaOpen = 0;
            }
            else
            {
                deltaOpen = _candles[bar].Open;
                deltaClose = _candles[bar].Close;
                deltaHigh = _candles[bar].High;
                deltaLow = _candles[bar].Low;
            }

            decimal upperTail, lowerTail;

            if (deltaClose > deltaOpen)
            {
                upperTail = deltaHigh - deltaClose;
                lowerTail = deltaOpen - deltaLow;
            }
            else
            {
                upperTail = deltaHigh - deltaOpen;
                lowerTail = deltaClose - deltaLow;
            }

            if (upperTail > lowerTail && upperTail > Absorption.Value)
            {
                var c = new Candle();
                var center = deltaHigh - upperTail / 2;
                c.Open = center + 10;
                c.Close = center - 10;
                c.High = center + 12;
                c.Low = center - 12;
                _absorptionCandles[bar] = c;
            }
            else if (lowerTail > upperTail && lowerTail > Absorption.Value)
            {
                var c = new Candle();
                var center = deltaLow + lowerTail / 2;
                c.Open = center - 10;
                c.Close = center + 10;
                c.High = center + 12m;
                c.Low = center - 12m;
                _absorptionCandles[bar] = c;
            }
            else
                _absorptionCandles[bar] = new Candle();
        }
        else
            _absorptionCandles[bar] = new Candle();

        // Current values on axis
        if (ShowCurrentValues)
        {
            _currentValues[bar] = MinimizedMode ? absDelta : deltaValue;

            _currentValues.Colors[bar] = deltaValue > 0
                ? _upColor
                : deltaValue < 0
                    ? _downColor
                    : _neutralColor;
        }

        // --- Average Delta Calculation & Coloring ---
        var smaVal = _sma.Calculate(bar, deltaValue);
        var emaVal = _ema.Calculate(bar, deltaValue);
        var avgVal = (AvgMode == AverageMode.Sma) ? smaVal : emaVal;

        if (ShowAverage)
        {
            _avgSeries[bar] = avgVal;

            if (AvgColorMode == AverageColorMode.Fixed)
            {
                _avgSeries.Colors[bar] = _avgSeries.Color.Convert();
            }
            else if (AvgColorMode == AverageColorMode.ZeroCross)
            {
                _avgSeries.Colors[bar] = (avgVal >= 0) ? _avgSlopeUpColor : _avgSlopeDownColor;
            }
            else if (AvgColorMode == AverageColorMode.Slope)
            {
                if (bar > 0)
                {
                    var prevAvg = _avgSeries[bar - 1];
                    _avgSeries.Colors[bar] = (avgVal >= prevAvg) ? _avgSlopeUpColor : _avgSlopeDownColor;
                }
                else
                {
                    _avgSeries.Colors[bar] = _avgSeries.Color.Convert();
                }
            }
        }
        else
        {
            _avgSeries[bar] = 0;
        }
    }

    #endregion

    #region Private methods


    private int GetMinWidth(RenderContext context, int startBar, int endBar)
    {
        var maxLength = 0;

        for (var i = startBar; i <= endBar; i++)
        {
            decimal value;

            if (MinimizedMode)
            {
                value = _candles[i].Close > _candles[i].Open
                    ? _candles[i].Close
                    : -_candles[i].Open;
            }
            else
                value = _candles[i].Close;

            var length = $"{value:0.#####}".Length;

            if (length > maxLength)
                maxLength = length;
        }

        var sampleStr = "";

        for (var i = 0; i < maxLength; i++)
            sampleStr += '0';

        return context.MeasureString(sampleStr, Font.RenderObject).Width;
    }

    private (decimal up, decimal down) GetVisualThresholds(int bar)
    {
        var up = PickUpThreshold(bar, VisualUpLevel);
        var down = PickDownThreshold(bar, VisualDownLevel);
        return (up, down);
    }

    private (decimal up, decimal down) GetAudioThresholds(int bar)
    {
        var up = PickUpThreshold(bar, AudioUpLevel);
        var down = PickDownThreshold(bar, AudioDownLevel);
        return (up, down);
    }

    // --- Threshold pickers ---
    private decimal PickUpThreshold(int bar, ThresholdLevel level)
    {
        var t = GetCandle(bar).Time;
        if (!InSession(t)) return decimal.MaxValue; // disable outside session

        if (Thresholds == ThresholdSource.Fixed)
            return (level == ThresholdLevel.Major) ? UpMajorLevel : UpMinorLevel;

        if (bar < 0 || bar >= _posReadyByBar.Count || !_posReadyByBar[bar])
            return decimal.MaxValue;

        return (level == ThresholdLevel.Major) ? _upMajor[bar] : _upMinor[bar];
    }

    private decimal PickDownThreshold(int bar, ThresholdLevel level)
    {
        var t = GetCandle(bar).Time;
        if (!InSession(t)) return decimal.MinValue; // disable outside session

        if (Thresholds == ThresholdSource.Fixed)
            return (level == ThresholdLevel.Major) ? DownMajorLevel : DownMinorLevel;

        if (bar < 0 || bar >= _negReadyByBar.Count || !_negReadyByBar[bar])
            return decimal.MinValue;

        return (level == ThresholdLevel.Major) ? _dnMajor[bar] : _dnMinor[bar];
    }

    // --- Stable-safe AddAlert wrapper (some builds have different overloads) ---
    private void TryAddAlert(string msg)
    {
        // Prefer the 5-arg overload (full UI control)
        try
        {
            var instrument = InstrumentInfo?.Instrument ?? string.Empty;
            AddAlert(AlertFile, instrument, msg, AlertBGColor, AlertForeColor);
            return;
        }
        catch
        {
            // Fallback to 2-arg overload available on ExtendedIndicator
            // (plays sound file + shows message)
            AddAlert(AlertFile, msg);
        }
    }

    // --- Thresholds: visibility + full refill of all series ---
    private void UpdateThresholdSeries(bool repaint = true)
    {
        var vis = ShowThresholdLines ? VisualMode.Line : VisualMode.Hide;

        _upMajor.VisualType = vis;
        _upMinor.VisualType = vis;
        _dnMinor.VisualType = vis;
        _dnMajor.VisualType = vis;

        // Full refill using the current fixed values ONLY if Thresholds = Fixed
        if (ShowThresholdLines && _thresholds == ThresholdSource.Fixed)
        {
            var last = Math.Max(0, CurrentBar - 1);
            for (var i = 0; i <= last; i++)
            {
                _upMajor[i] = _upMajorLevel;
                _upMinor[i] = _upMinorLevel;
                _dnMinor[i] = _downMinorLevel;
                _dnMajor[i] = _downMajorLevel;
            }
        }

        RebuildThresholdPens();
        if (repaint)
            RedrawChart();
    }

    // --- Recalculate visual signals (price markers) after threshold changes ---
    private void RecalculateVisualSignals()
    {
        if (!VisualEnabled)
            return;

        var tick = InstrumentInfo?.TickSize ?? 1m;
        var offset = tick * _priceSignalOffsetTicks;

        for (var bar = 0; bar < CurrentBar; bar++)
        {
            _priceSignalUp[bar] = 0;
            _priceSignalDown[bar] = 0;

            var deltaValue = _delta[bar];

            var visualUpTh = PickUpThreshold(bar, VisualUpLevel);
            var visualDownTh = PickDownThreshold(bar, VisualDownLevel);

            if (deltaValue >= visualUpTh)
                _priceSignalUp[bar] = GetCandle(bar).High + offset;

            if (deltaValue <= visualDownTh)
                _priceSignalDown[bar] = GetCandle(bar).Low - offset;
        }

        RedrawChart();
    }
    #region Dynamic threshold private methods

    private static bool TryGetPositiveExtremeSample(IndicatorCandle candle, out decimal sample)
    {
        var x = candle.MaxDelta;
        if (x > 0) { sample = x; return true; }
        sample = 0; return false;
    }

    private static bool TryGetNegativeMagnitudeExtremeSample(IndicatorCandle candle, out decimal sampleAbs)
    {
        var x = candle.MinDelta;
        if (x < 0) { sampleAbs = Math.Abs(x); return true; }
        sampleAbs = 0; return false;
    }

    // Returns true if bar timestamp (adjusted to instrument TZ) is inside the session window
    private bool InSession(DateTime exchangeTimeUtc)
    {
        if (SessionMode == SessionWindowMode.Full24h)
            return true;

        var tLocal = exchangeTimeUtc.AddHours(InstrumentInfo.TimeZone).TimeOfDay; // <-- same idea as Initial Balance
        // RTH without midnight crossing (09:30-16:00)
        return tLocal >= RthStart && tLocal <= RthEnd;
    }

    // Detects the first bar inside a new session to reset accumulators
    private bool IsSessionStart(int bar)
    {
        if (bar == 0) return true;

        var prevUtc = GetCandle(bar - 1).Time;
        var currUtc = GetCandle(bar).Time;

        var prevIn = InSession(prevUtc);
        var currIn = InSession(currUtc);

        if (!prevIn && currIn) return true;

        // Day change while still in window: re-anchor at first RTH bar of the new day
        if (currIn && prevUtc.Date != currUtc.Date)
        {
            var tLocal = currUtc.AddHours(InstrumentInfo.TimeZone).TimeOfDay;
            if (tLocal >= RthStart && tLocal <= RthEnd) return true;
        }

        return false;
    }

    // --- Helpers to cut ValueDataSeries ---
    private void CutAllThresholdsAt(int bar /* inclusive end */)
    {
        var b = Math.Max(0, bar);
        _upMajor.SetPointOfEndLine(b);
        _upMinor.SetPointOfEndLine(b);
        _dnMinor.SetPointOfEndLine(b);
        _dnMajor.SetPointOfEndLine(b);
    }

    private void CutUpThresholdsAt(int bar)
    {
        var b = Math.Max(0, bar);
        _upMajor.SetPointOfEndLine(b);
        _upMinor.SetPointOfEndLine(b);
    }

    private void CutDownThresholdsAt(int bar)
    {
        var b = Math.Max(0, bar);
        _dnMajor.SetPointOfEndLine(b);
        _dnMinor.SetPointOfEndLine(b);
    }


    #endregion

    #endregion

    #region Event handlers

    private void OnDivergenceFilterChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Indicators.FilterColor.Enabled))
            ApplyDivergenceColorsToCurrentMode();

        UpdateDivergenceCandlesVisibility();

        RecalculateValues();

        RedrawChart();
    }

    private void OnAbsorptionFilterChanged(object sender, PropertyChangedEventArgs e)
    {
        RecalculateValues();
        RedrawChart();
    }

    private void ApplyDivergenceColorsToCurrentMode()
    {
        if (_divergenceBarsFilter?.Enabled != true)
        {
            for (var i = 0; i < CurrentBar; i++)
            {
                var actualDelta = GetCandle(i).Delta;
                var defaultColor = actualDelta > 0 ? _upColor : _downColor;
                _delta.Colors[i] = defaultColor;
            }

            return;
        }

        var divergenceColor = _divergenceBarsFilter.Value.Convert();

        for (var i = 0; i < CurrentBar; i++)
        {
            if (_divergenceBars.TryGetValue(i, out var hasDivergence) && hasDivergence)
            {
                if (_mode == DeltaVisualMode.Histogram || _mode == DeltaVisualMode.HighLow)
                    _delta.Colors[i] = divergenceColor;
            }
            else
            {
                var actualDelta = GetCandle(i).Delta;
                var defaultColor = actualDelta > 0 ? _upColor : _downColor;
                _delta.Colors[i] = defaultColor;
            }
        }
    }

    private void UpdateDivergenceCandlesVisibility()
    {
        if (DivergenceBarsFilter != null && (_mode == DeltaVisualMode.Candles || _mode == DeltaVisualMode.Bars))
        {
            var divergenceColor = DivergenceBarsFilter.Value;
            _divergenceCandles.Visible = DivergenceBarsFilter.Enabled;
            _divergenceCandles.UpCandleColor = divergenceColor;
            _divergenceCandles.DownCandleColor = divergenceColor;
            _divergenceCandles.BorderColor = divergenceColor;
            _divergenceCandles.Mode = _mode == DeltaVisualMode.Candles ? CandleVisualMode.Candles : CandleVisualMode.Bars;

            _divergenceDownCandles.Visible = DivergenceBarsFilter.Enabled && MinimizedMode;
            _divergenceDownCandles.UpCandleColor = divergenceColor;
            _divergenceDownCandles.DownCandleColor = divergenceColor;
            _divergenceDownCandles.BorderColor = divergenceColor;
            _divergenceDownCandles.Mode = _mode == DeltaVisualMode.Candles ? CandleVisualMode.Candles : CandleVisualMode.Bars;
        }
        else
        {
            _divergenceCandles.Visible = false;
            _divergenceDownCandles.Visible = false;
        }
    }

    #endregion
}