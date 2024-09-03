﻿namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.Linq;

using ATAS.Indicators.Drawing;

using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Control;
using OFT.Rendering.Settings;
using OFT.Rendering.Tools;

using Utils.Common.Logging;

using Color = CrossColor;

[DisplayName("Cluster Statistic")]
[Category("Clusters, Profiles, Levels")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.ClusterStatisticDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602624")]
public class ClusterStatistic : Indicator
{
    #region Static and constants

    private const int _headerOffset = 3;
    
    #endregion

    #region Fields

    private readonly ValueDataSeries _candleDurations = new("durations");
    private readonly ValueDataSeries _candleHeights = new("heights");
    private readonly ValueDataSeries _cDelta = new("cDelta");
    private readonly ValueDataSeries _cVolume = new("cVolume");
    private readonly ValueDataSeries _cDeltaPerVol = new("DeltaPerVol");
    private readonly ValueDataSeries _deltaPerVol = new("BarDeltaPerVol");
    private readonly ValueDataSeries _volPerSecond = new("VolPerSecond");

    private readonly RenderStringFormat _stringLeftFormat = new()
    {
        Alignment = StringAlignment.Near,
        LineAlignment = StringAlignment.Center,
        Trimming = StringTrimming.EllipsisCharacter,
        FormatFlags = StringFormatFlags.NoWrap
    };

    private int _headerWidth = 130;

    private int _lastBar = -1;
    private Color _backGroundColor = Color.FromArgb(120, 0, 0, 0);
    private bool _centerAlign;
    private decimal _cumVolume;

    private int _height = 15;
    private int _lastDeltaAlert;
    private decimal _lastDeltaValue;
    private int _lastVolumeAlert;
    private decimal _lastVolumeValue;
    private decimal _maxAsk;
    private decimal _maxBid;
    private decimal _maxDelta;
    private decimal _maxDeltaChange;
    private decimal _maxDeltaPerVolume;
    private decimal _maxDuration;
    private decimal _maxHeight;
    private decimal _maxMaxDelta;
    private decimal _maxMinDelta;
    private decimal _maxSessionDelta;
    private decimal _maxSessionDeltaPerVolume;
    private decimal _maxTicks;
    private decimal _maxVolume;
    private decimal _minDelta;

    private byte _bgAlpha = 255;
    private int _bgTransparency = 10;

    private DataType _pressedString = DataType.None;
    private int _selectionOffset;

    private RenderOrder _rowsOrder = new();
    private bool _showAsk;
    private bool _showBid;
    private bool _showDelta;
    private bool _showDeltaPerVolume;
    private bool _showSessionDelta;
    private bool _showSessionDeltaPerVolume;
    private bool _showMaximumDelta;
    private bool _showMinimumDelta;
    private bool _showDeltaChange;
    private bool _showVolume;
    private bool _showVolumePerSecond;
    private bool _showSessionVolume;
    private bool _showTicks;
    private bool _showHighLow;
    private bool _showTime;
    private bool _showDuration;
    private bool _fontChanged;

    private int _strCount => _rowsOrder.AvailableStrings.Count;

    #endregion

    #region Properties

    #region Rows

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowAsk), GroupName = nameof(Strings.Rows),
    Description = nameof(Strings.ShowAsksDescription), Order = 110)]
    public bool ShowAsk
    {
        get => _showAsk;
        set
        {
            _showAsk = value;
            _rowsOrder.SetEnabled(DataType.Ask, value);
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowBid), GroupName = nameof(Strings.Rows),
        Description = nameof(Strings.ShowBidsDescription), Order = 110)]
    public bool ShowBid
    {
        get => _showBid;
        set
        {
            _showBid = value;
            _rowsOrder.SetEnabled(DataType.Bid, value);
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowDelta), GroupName = nameof(Strings.Rows),
        Description = nameof(Strings.ShowDeltaDescription), Order = 120)]
    public bool ShowDelta
    {
        get => _showDelta;
        set
        {
            _showDelta = value;
            _rowsOrder.SetEnabled(DataType.Delta, value);
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowDeltaPerVolume), GroupName = nameof(Strings.Rows),
        Description = nameof(Strings.ShowDeltaPerVolumeDescription), Order = 130)]
    public bool ShowDeltaPerVolume
    {
        get => _showDeltaPerVolume;
        set
        {
            _showDeltaPerVolume = value;
            _rowsOrder.SetEnabled(DataType.DeltaVolume, value);
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowSessionDelta), GroupName = nameof(Strings.Rows),
        Description = nameof(Strings.ShowSessionDeltaDescription), Order = 140)]
    public bool ShowSessionDelta
    {
        get => _showSessionDelta;
        set
        {
            _showSessionDelta = value;
            _rowsOrder.SetEnabled(DataType.SessionDelta, value);
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowSessionDeltaPerVolume), GroupName = nameof(Strings.Rows),
        Description = nameof(Strings.ShowSessionDeltaPerVolumeDescription), Order = 150)]
    public bool ShowSessionDeltaPerVolume
    {
        get => _showSessionDeltaPerVolume;
        set
        {
            _showSessionDeltaPerVolume = value;
            _rowsOrder.SetEnabled(DataType.SessionDeltaVolume, value);
            
            if(value)
				_headerWidth = 180;
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowMaximumDelta), GroupName = nameof(Strings.Rows),
        Description = nameof(Strings.ShowMaximumDeltaDescription), Order = 160)]
    public bool ShowMaximumDelta
    {
        get => _showMaximumDelta;
        set
        {
            _showMaximumDelta = value;
            _rowsOrder.SetEnabled(DataType.MaxDelta, value);
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowMinimumDelta), GroupName = nameof(Strings.Rows),
        Description = nameof(Strings.ShowMinimumDeltaDescription), Order = 170)]
    public bool ShowMinimumDelta
    {
        get => _showMinimumDelta;
        set
        {
            _showMinimumDelta = value;
            _rowsOrder.SetEnabled(DataType.MinDelta, value);
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowDeltaChange), GroupName = nameof(Strings.Rows),
        Description = nameof(Strings.ShowDeltaChangeDescription), Order = 175)]
    public bool ShowDeltaChange
    {
        get => _showDeltaChange;
        set
        {
            _showDeltaChange = value;
            _rowsOrder.SetEnabled(DataType.DeltaChange, value);
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowVolume), GroupName = nameof(Strings.Rows),
        Description = nameof(Strings.ShowVolumesDescription), Order = 180)]
    public bool ShowVolume
    {
        get => _showVolume;
        set
        {
            _showVolume = value;
            _rowsOrder.SetEnabled(DataType.Volume, value);
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowVolumePerSecond), GroupName = nameof(Strings.Rows),
        Description = nameof(Strings.ShowVolumePerSecondDescription), Order = 190)]
    public bool ShowVolumePerSecond
    {
        get => _showVolumePerSecond;
        set
        {
            _showVolumePerSecond = value;
            _rowsOrder.SetEnabled(DataType.VolumeSecond, value);
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowSessionVolume), GroupName = nameof(Strings.Rows),
        Description = nameof(Strings.ShowSessionVolumeDescription), Order = 191)]
    public bool ShowSessionVolume
    {
        get => _showSessionVolume;
        set
        {
            _showSessionVolume = value;
            _rowsOrder.SetEnabled(DataType.SessionVolume, value);
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowTradesCount), GroupName = nameof(Strings.Rows),
        Description = nameof(Strings.ShowTradesCountDescription), Order = 192)]
    public bool ShowTicks
    {
        get => _showTicks;
        set
        {
            _showTicks = value;
            _rowsOrder.SetEnabled(DataType.Trades, value);
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowHeight), GroupName = nameof(Strings.Rows),
        Description = nameof(Strings.ShowCandleHeightDescription), Order = 193)]
    public bool ShowHighLow
    {
        get => _showHighLow;
        set
        {
            _showHighLow = value;
            _rowsOrder.SetEnabled(DataType.Height, value);
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowTime), GroupName = nameof(Strings.Rows),
        Description = nameof(Strings.ShowCandleTimeDescription), Order = 194)]
    public bool ShowTime
    {
        get => _showTime;
        set
        {
            _showTime = value;
            _rowsOrder.SetEnabled(DataType.Time, value);
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowDuration), GroupName = nameof(Strings.Rows),
        Description = nameof(Strings.ShowCandleDurationDescription), Order = 196)]
    public bool ShowDuration
    {
        get => _showDuration;
        set
        {
            _showDuration = value;
            _rowsOrder.SetEnabled(DataType.Duration, value);
        }
    }


    #endregion

    #region Colors

    [Display(ResourceType = typeof(Strings), Name = "BackGround", GroupName = nameof(Strings.Visualization), Description = nameof(Strings.LabelFillColorDescription), Order = 200)]
    public Color BackGroundColor
    {
	    get => _backGroundColor;
	    set => _backGroundColor = value; 
    }

    [Range(1, 10)]
    [Display(ResourceType = typeof(Strings), Name = "Transparency", GroupName = nameof(Strings.Visualization), Order = 205)]
    public int BgTransparency
    {
	    get=> _bgTransparency;
	    set
	    {
		    _bgTransparency = value;
		    _bgAlpha = (byte)(255 * value / 10);
	    } 
    } 
    
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Grid), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.GridColorDescription), Order = 210)]
    public Color GridColor { get; set; } = CrossColors.Transparent;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.VisibleProportion), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.VisibleProportionDescription), Order = 220)]
    public bool VisibleProportion { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Volume), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.VolumeColorDescription), Order = 230)]
    public Color VolumeColor { get; set; } = CrossColors.DarkGray;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AskColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.AskColorDescription), Order = 240)]
    public Color AskColor { get; set; } = CrossColors.Green;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BidColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.BidColorDescription), Order = 250)]
    public Color BidColor { get; set; } = CrossColors.Red;

    #endregion

    #region Text

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.Text), Description = nameof(Strings.LabelTextColorDescription), Order = 300)]
    public Color TextColor { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Font), GroupName = nameof(Strings.Text), Description = nameof(Strings.FontSettingDescription), Order = 310)]
    public FontSetting Font { get; set; } = new("Arial", 9);

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.CenterAlign), GroupName = nameof(Strings.Text), Description = nameof(Strings.CenterAlignDescription), Order = 320)]
    public bool CenterAlign
    {
        get => _centerAlign;
        set
        {
            _centerAlign = value;
            _stringLeftFormat.Alignment = value ? StringAlignment.Center : StringAlignment.Near;
        }
    }

    #endregion

    #region Headers

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.Headers), Description = nameof(Strings.HeaderBackgroundDescription), Order = 330)]
    public Color HeaderBackground { get; set; } = CrossColor.FromArgb(0xFF, 84, 84, 84);

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.HideRowsDescription), GroupName = nameof(Strings.Headers), Description = nameof(Strings.HideHeadersDescription), Order = 340)]
    public bool HideRowsDescription { get; set; }

    #endregion

    #region Volume Alert

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.VolumeAlert), Description = nameof(Strings.UseAlertDescription), Order = 400)]
    public bool UseVolumeAlert { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.VolumeAlert), Description = nameof(Strings.AlertFilterDescription), Order = 410)]
    [Range(0, int.MaxValue)]
    public decimal VolumeAlertValue { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.VolumeAlert), Description = nameof(Strings.AlertFileDescription), Order = 420)]
    public string VolumeAlertFile { get; set; } = "alert1";

    #endregion

    #region Delta alert

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.DeltaAlert), Description = nameof(Strings.UseAlertDescription), Order = 500)]
    public bool UseDeltaAlert { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.DeltaAlert), Description = nameof(Strings.AlertFilterDescription), Order = 510)]
    public decimal DeltaAlertValue { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.DeltaAlert), Description = nameof(Strings.AlertFileDescription), Order = 520)]
    public string DeltaAlertFile { get; set; } = "alert1";

    #endregion

    #endregion

    #region ctor

    public ClusterStatistic()
        : base(true)
    {
        DenyToChangePanel = true;
        Panel = IndicatorDataProvider.NewPanel;
        EnableCustomDrawing = true;
        ShowDelta = ShowSessionDelta = ShowVolume = true;
        SubscribeToDrawingEvents(DrawingLayouts.LatestBar | DrawingLayouts.Final);

        DataSeries[0].IsHidden = true;
        ((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
        ShowDescription = false;
    }
	
    #endregion

    public override bool ProcessMouseDown(RenderControlMouseEventArgs e)
    {
	    var cursor = e.Location;

        if(!Container.Region.Contains(cursor) || e.X > _headerWidth)
	        return base.ProcessMouseDown(e);

        var stringsCnt = _rowsOrder.AvailableStrings.Count;

        if(stringsCnt <= 1)
	        return base.ProcessMouseDown(e);

        var height = Container.Region.Height / stringsCnt;
        _selectionOffset = (e.Y - Container.Region.Top) % height;

        var rowNum = Math.Max((e.Y - Container.Region.Top) / height, 0);
        rowNum = Math.Min(rowNum, stringsCnt - 1);
        
        _pressedString = _rowsOrder.AvailableStrings.GetValueAtIndex(rowNum);

        return true;
    }

    public override bool ProcessMouseMove(RenderControlMouseEventArgs e)
    {
        if(_pressedString is DataType.None)
			return base.ProcessMouseMove(e);

		var stringsCnt = _rowsOrder.AvailableStrings.Count;

		if (stringsCnt <= 1)
			return base.ProcessMouseDown(e);

        var height = Container.Region.Height / stringsCnt;
        
        var rowNum = Math.Max((e.Y - Container.Region.Top) / height, 0);
        rowNum = Math.Min(rowNum, stringsCnt - 1);

        var currentString = _rowsOrder.AvailableStrings.GetValueAtIndex(rowNum);

        if (_pressedString != currentString)
	        _rowsOrder.UpdateOrder(_pressedString, currentString);
        
        return true;
    }
	
    public override bool ProcessMouseUp(RenderControlMouseEventArgs e)
    {
        _pressedString = DataType.None;
		return base.ProcessMouseUp(e);
    }
	
    #region Protected methods

    protected override void OnInitialize()
    {
        ((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
		Font.PropertyChanged += FontChanged;
        base.OnInitialize();
    }

    protected override void OnApplyDefaultColors()
    {
        if (ChartInfo is null)
            return;

        BidColor = ChartInfo.ColorsStore.FootprintBidColor.Convert();
        BidColor = Color.FromRgb(BidColor.R, BidColor.G, BidColor.B);

        AskColor = ChartInfo.ColorsStore.FootprintAskColor.Convert();
        AskColor = Color.FromRgb(AskColor.R, AskColor.G, AskColor.B);

        VolumeColor = ChartInfo.ColorsStore.PaneSeparators.Color.Convert();
        VolumeColor = Color.FromRgb(VolumeColor.R, VolumeColor.G, VolumeColor.B);
        
        GridColor = ChartInfo.ColorsStore.Grid.Color.Convert();
        GridColor = Color.FromRgb(GridColor.R, GridColor.G, GridColor.B);

        HeaderBackground = DefaultColors.Gray.Convert();
        TextColor = CrossColors.White;

        BackGroundColor = ChartInfo.ColorsStore.BaseBackgroundColor.Convert();
        BackGroundColor = Color.FromArgb(128, BackGroundColor.R, BackGroundColor.G, BackGroundColor.B);
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        var candle = GetCandle(bar);

        var candleSeconds = Convert.ToDecimal((candle.LastTime - candle.Time).TotalSeconds);

        if (candleSeconds is 0)
            candleSeconds = 1;

        _volPerSecond[bar] = candle.Volume / candleSeconds;

        if (bar == 0)
        {
            _cumVolume = 0;
            _maxVolume = 0;
            _maxDelta = 0;
            _maxMaxDelta = 0;
            _maxMinDelta = 0;
            _maxDeltaChange = 0;
            _minDelta = decimal.MaxValue;
            _maxHeight = 0;
            _maxTicks = 0;
            _maxDuration = 0;
            _maxSessionDelta = 0;
            _maxDeltaPerVolume = 0;
            _maxSessionDeltaPerVolume = 0;
            _maxBid = _maxAsk = 0;
            _cDelta[bar] = candle.Delta;
            return;
        }

        _deltaPerVol[bar] = Math.Abs(candle.Delta * 100m / candle.Volume);

        var prevCandle = GetCandle(bar - 1);

        if (IsNewSession(bar))
        {
            _cVolume[bar] = _cumVolume = candle.Volume;
            _cDelta[bar] = candle.Delta;
        }
        else
        {
            _cumVolume = _cVolume[bar] = _cVolume[bar - 1] + candle.Volume;
            _cDelta[bar] = _cDelta[bar - 1] + candle.Delta;
        }

        _maxSessionDelta = Math.Max(Math.Abs(_cDelta[bar]), _maxSessionDelta);

        _maxAsk = Math.Max(candle.Ask, _maxAsk);
        _maxBid = Math.Max(candle.Ask, _maxBid);

        _maxDeltaChange = Math.Max(Math.Abs(candle.Delta - prevCandle.Delta), _maxDeltaChange);

        _maxDelta = Math.Max(Math.Abs(candle.Delta), _maxDelta);

        _maxMaxDelta = Math.Max(Math.Abs(candle.MaxDelta), _maxMaxDelta);
        _maxMinDelta = Math.Max(Math.Abs(candle.MinDelta), _maxMinDelta);

        _maxVolume = Math.Max(candle.Volume, _maxVolume);

        _minDelta = Math.Min(candle.MinDelta, _minDelta);


        _maxDeltaPerVolume = candle.Volume != 0
            ? Math.Max(Math.Abs(100 * candle.Delta / candle.Volume), _minDelta)
            : 0;

        var candleHeight = candle.High - candle.Low;
        _maxHeight = Math.Max(candleHeight, _maxHeight);
        _candleHeights[bar] = candleHeight;

        _maxTicks = Math.Max(candle.Ticks, _maxTicks);

        _candleDurations[bar] = (int)(candle.LastTime - candle.Time).TotalSeconds;
        _maxDuration = Math.Max(_candleDurations[bar], _maxDuration);

        if (Math.Abs(_cVolume[bar] - 0) > 0.000001m)
            _cDeltaPerVol[bar] = 100.0m * _cDelta[bar] / _cVolume[bar];

        _maxSessionDeltaPerVolume = Math.Max(Math.Abs(_cDeltaPerVol[bar]), _maxSessionDeltaPerVolume);

        if (_lastBar != bar)
            _lastVolumeValue = _lastDeltaValue = 0m;

        if (bar == CurrentBar - 1)
        {
            if (UseDeltaAlert && _lastDeltaAlert != bar)
            {
                if ((_lastDeltaValue < DeltaAlertValue && candle.Delta >= DeltaAlertValue)
                 || (_lastDeltaValue > DeltaAlertValue && candle.Delta <= DeltaAlertValue))
                {
                    AddAlert(DeltaAlertFile, $"Cluster statistic delta alert: {candle.Delta}");
                    _lastDeltaAlert = bar;
                }
            }

            if (UseVolumeAlert && _lastVolumeAlert != bar)
            {
                if (_lastVolumeValue < VolumeAlertValue && candle.Volume >= VolumeAlertValue)
                {
                    AddAlert(VolumeAlertFile, $"Cluster statistic volume alert: {candle.Volume}");
                    _lastVolumeAlert = bar;
                }
            }
        }

        _lastVolumeValue = candle.Volume;
        _lastDeltaValue = candle.Delta;
        _lastBar = bar;
    }

    protected override void OnRender(RenderContext context, DrawingLayouts layout)
    {
        if (ChartInfo is not { PriceChartContainer: { BarsWidth: > 2 } })
            return;

        if (LastVisibleBarNumber > CurrentBar - 1)
            return;
		
        if (_strCount is 0)
            return;

        if (_fontChanged)
        {
	        var str = "Session Delta/Volume";
	        var width = context.MeasureString(str, Font.RenderObject).Width;
	        _headerWidth = width + 10;
	        _fontChanged = false;
        }

        var bounds = context.ClipBounds;

        try
        {
            var renderField = new Rectangle(Container.Region.X, Container.Region.Y, Container.Region.Width,
                Container.Region.Height);
            context.SetClip(renderField);

            context.SetTextRenderingHint(RenderTextRenderingHint.Aliased);

            _height = Container.Region.Height / _strCount;
            var overPixels = Container.Region.Height % _strCount;

            var y = Container.Region.Y;

            var firstY = y;
            var linePen = new RenderPen(GridColor.Convert());
            var maxX = 0;
            var lastX = 0;

            var fullBarsWidth = ChartInfo.GetXByBar(1) - ChartInfo.GetXByBar(0);
            var showHeadersText = context.MeasureString("1", Font.RenderObject).Height * 0.8 <= _height;
            var showValues = fullBarsWidth >= 30 && showHeadersText;
            var textColor = TextColor.Convert();

            decimal maxVolumeSec;
            var maxDelta = 0m;
            var maxAsk = 0m;
            var maxBid = 0m;
            var maxMaxDelta = 0m;
            var maxMinDelta = 0m;
            var maxVolume = 0m;
            var cumVolume = 0m;
            var maxDeltaChange = 0m;
            var maxSessionDelta = 0m;
            var maxSessionDeltaPerVolume = 0m;
            var maxDeltaPerVolume = 0m;
            var minDelta = decimal.MaxValue;
            var maxHeight = 0m;
            var maxTicks = 0m;
            var maxDuration = 0m;

            if (VisibleProportion)
            {
                for (var i = FirstVisibleBarNumber; i <= LastVisibleBarNumber; i++)
                {
                    var candle = GetCandle(i);
                    maxDelta = Math.Max(candle.Delta, maxDelta);
                    maxVolume = Math.Max(candle.Volume, maxVolume);
                    minDelta = Math.Min(candle.MinDelta, minDelta);
                    maxAsk = Math.Max(candle.Ask, maxAsk);
                    maxBid = Math.Max(candle.Ask, maxBid);
                    maxMaxDelta = Math.Max(Math.Abs(candle.MaxDelta), maxMaxDelta);
                    maxMinDelta = Math.Max(Math.Abs(candle.MinDelta), maxMinDelta);
                    maxSessionDelta = Math.Max(Math.Abs(_cDelta[i]), maxSessionDelta);

                    if (candle.Volume is not 0)
                        maxDeltaPerVolume = Math.Max(Math.Abs(100 * candle.Delta / candle.Volume), maxDeltaPerVolume);

                    maxSessionDeltaPerVolume = Math.Max(Math.Abs(_cDeltaPerVol[i]), maxSessionDeltaPerVolume);
                    cumVolume += candle.Volume;

                    if (i == 0)
                        continue;

                    var prevCandle = GetCandle(i - 1);
                    maxDeltaChange = Math.Max(Math.Abs(candle.Delta - prevCandle.Delta), maxDeltaChange);
                    maxHeight = Math.Max(candle.High - candle.Low, maxHeight);
                    maxTicks = Math.Max(candle.Ticks, maxTicks);
                    maxDuration = Math.Max(_candleDurations[i], maxDuration);
                }

                maxVolumeSec = _volPerSecond.MAX(LastVisibleBarNumber - FirstVisibleBarNumber, LastVisibleBarNumber);
            }
            else
            {
                maxAsk = _maxAsk;
                maxBid = _maxBid;
                maxSessionDelta = _maxSessionDelta;
                maxDeltaPerVolume = _maxDeltaPerVolume;
                maxSessionDeltaPerVolume = _maxSessionDeltaPerVolume;
                maxDelta = _maxDelta;
                minDelta = _minDelta;
                maxMaxDelta = _maxMaxDelta;
                maxMinDelta = _maxMinDelta;
                maxVolume = _maxVolume;
                maxTicks = _maxTicks;
                maxDuration = _maxDuration;
                cumVolume = _cumVolume;
                maxDeltaChange = _maxDeltaChange;
                maxHeight = _maxHeight;
                maxVolumeSec = _volPerSecond.MAX(CurrentBar - 1, CurrentBar - 1);
            }

            var drawHeaders = !HideRowsDescription 
	            || (MouseLocationInfo.LastPosition.Y >= Container.Region.Y && MouseLocationInfo.LastPosition.Y <= Container.Region.Bottom)
	            || _pressedString is not DataType.None;

            for (var j = LastVisibleBarNumber; j >= FirstVisibleBarNumber; j--)
            {
                var x = ChartInfo.GetXByBar(j) + 3;

                if(drawHeaders && x + fullBarsWidth < _headerWidth)
                    continue;

                maxX = Math.Max(x, maxX);

                var y1 = y;
                var candle = GetCandle(j);

                foreach (var (_, type) in _rowsOrder.AvailableStrings)
                {
	                var rectY = type == _pressedString ? ChartInfo.MouseLocationInfo.LastPosition.Y - _selectionOffset : y1;

	                var rectHeight = _height + (overPixels > 0 ? 1 : 0);
	                var rect = new Rectangle(x, rectY, fullBarsWidth, rectHeight);

	                var rate = type switch
	                {
		                DataType.Ask => GetRate(candle.Ask, maxAsk),
		                DataType.Bid => GetRate(candle.Bid, maxBid),
		                DataType.Delta => GetRate(Math.Abs(candle.Delta), maxDelta),
		                DataType.DeltaVolume => candle.Volume != 0 ? GetRate(Math.Abs(candle.Delta * 100.0m / candle.Volume), maxDeltaPerVolume) : 0,
		                DataType.SessionDelta => GetRate(_deltaPerVol[j], maxSessionDelta),
		                DataType.SessionDeltaVolume => GetRate(Math.Abs(_cDelta[j]), maxSessionDeltaPerVolume),
		                DataType.MaxDelta => GetRate(Math.Abs(candle.MaxDelta), maxMaxDelta),
		                DataType.MinDelta => GetRate(Math.Abs(candle.MinDelta), maxMinDelta),
		                DataType.DeltaChange => GetRate(Math.Abs(candle.Delta - GetCandle(Math.Max(j - 1, 0)).Delta), maxDeltaChange),
		                DataType.Volume => GetRate(candle.Volume, maxVolume),
		                DataType.VolumeSecond => GetRate(_volPerSecond[j], maxVolumeSec),
		                DataType.SessionVolume => GetRate(_cVolume[j], cumVolume),
		                DataType.Trades => GetRate(candle.Ticks, maxTicks),
		                DataType.Height => GetRate(_candleHeights[j], maxHeight),
		                DataType.Time => GetRate(_cVolume[j], cumVolume),
		                DataType.Duration => GetRate(_candleDurations[j], maxDuration),
		                DataType.None => 0,

		                _ => throw new ArgumentOutOfRangeException()
	                };

					var bgBrush = type switch
	                {
		                DataType.Ask or DataType.Bid or DataType.Delta or DataType.DeltaVolume =>
			                Blend(candle.Delta > 0 ? AskColor : BidColor, _backGroundColor, rate),

		                DataType.MaxDelta or DataType.MinDelta or DataType.Volume or DataType.VolumeSecond or DataType.SessionVolume or
			                DataType.Trades or DataType.Height or DataType.Time or DataType.Duration => Blend(VolumeColor, _backGroundColor, rate),

		                DataType.SessionDeltaVolume => Blend(_cDeltaPerVol[j] > 0 ? AskColor : BidColor, _backGroundColor, rate),
		                DataType.SessionDelta => Blend(_cDelta[j] > 0 ? AskColor : BidColor, _backGroundColor, rate),
		                DataType.DeltaChange => GetDeltaChangeBrush(candle, j, rate),
		                DataType.None => System.Drawing.Color.Transparent,
		                _ => throw new ArgumentOutOfRangeException()
	                };

	                context.FillRectangle(bgBrush, rect);

	                if (showValues)
	                {
		                var text = type switch
		                {
			                DataType.Ask => ChartInfo.TryGetMinimizedVolumeString(candle.Ask),
			                DataType.Bid => ChartInfo.TryGetMinimizedVolumeString(candle.Bid),
			                DataType.Delta => ChartInfo.TryGetMinimizedVolumeString(candle.Delta),
			                DataType.DeltaVolume => _deltaPerVol[j].ToString("F") + "%",
			                DataType.SessionDelta => ChartInfo.TryGetMinimizedVolumeString(_cDelta[j]),
			                DataType.SessionDeltaVolume => _cDeltaPerVol[j].ToString("F") + "%",
			                DataType.MaxDelta => ChartInfo.TryGetMinimizedVolumeString(candle.MaxDelta),
			                DataType.MinDelta => ChartInfo.TryGetMinimizedVolumeString(candle.MinDelta),
			                DataType.DeltaChange => ChartInfo.TryGetMinimizedVolumeString(candle.Delta - GetCandle(Math.Max(j - 1, 0)).Delta),
			                DataType.Volume => ChartInfo.TryGetMinimizedVolumeString(candle.Volume),
			                DataType.VolumeSecond => ChartInfo.TryGetMinimizedVolumeString(_volPerSecond[j]),
			                DataType.SessionVolume => ChartInfo.TryGetMinimizedVolumeString(_cVolume[j]),
			                DataType.Trades => candle.Ticks.ToString(CultureInfo.InvariantCulture),
			                DataType.Height => _candleHeights[j].ToString(CultureInfo.InvariantCulture),
			                DataType.Time => candle.Time.AddHours(InstrumentInfo.TimeZone).ToString("HH:mm:ss"),
			                DataType.Duration => ((int)(candle.LastTime - candle.Time).TotalSeconds).ToString(),
			                DataType.None => string.Empty,
			                _ => throw new ArgumentOutOfRangeException()
		                };

		                rect.X += _headerOffset;
		                context.DrawString(text, Font.RenderObject, textColor, rect, _stringLeftFormat);
                    }

	                y1 += rectHeight;
	                overPixels--;
                }
				
                context.DrawLine(linePen, x, y1 - 1, x + fullBarsWidth, y1 - 1);
                lastX = x + fullBarsWidth;
                context.DrawLine(linePen, lastX, Container.Region.Bottom, lastX, Container.Region.Y);
                overPixels = Container.Region.Height % _strCount;
            }

            maxX += fullBarsWidth;

            var headBgBrush = HeaderBackground.Convert();
			
            foreach (var (_, type) in _rowsOrder.AvailableStrings)
            {
				var rectY = type == _pressedString ? ChartInfo.MouseLocationInfo.LastPosition.Y - _selectionOffset : y;
                var rectHeight = _height + (overPixels > 0 ? 1 : 0);

                if (drawHeaders)
                {
	                var descRect = new Rectangle(0, rectY, _headerWidth, rectHeight);
	                context.FillRectangle(headBgBrush, descRect);

	                if (showHeadersText)
	                {
		                var text = type switch
		                {
			                DataType.Ask => "Ask",
			                DataType.Bid => "Bid",
			                DataType.Delta => "Delta",
			                DataType.DeltaVolume => "Delta/Volume",
			                DataType.SessionDelta => "Session Delta",
			                DataType.SessionDeltaVolume => "Session Delta/Volume",
			                DataType.MaxDelta => "Max.Delta",
			                DataType.MinDelta => "Min.Delta",
			                DataType.DeltaChange => "Delta Change",
			                DataType.Volume => "Volume",
			                DataType.VolumeSecond => "Volume/sec",
			                DataType.SessionVolume => "Session Volume",
			                DataType.Trades => "Trades",
			                DataType.Height => "Height",
			                DataType.Time => "Time",
			                DataType.Duration => "Duration",
			                DataType.None => string.Empty,

			                _ => throw new ArgumentOutOfRangeException()
		                };

                        descRect.X += _headerOffset;
		                context.DrawString(text, Font.RenderObject, textColor, descRect, _stringLeftFormat);
	                }
                }

                if (drawHeaders)
	                maxX = Math.Max(maxX, _headerWidth);

                context.DrawLine(linePen, Container.Region.X, y, maxX, y);

                y += rectHeight;
	            overPixels--;
            }

            if (drawHeaders)
            {
	            context.DrawLine(linePen, Container.Region.X, Container.Region.Y, Container.Region.X, Container.Region.Bottom);
	            context.DrawLine(linePen, _headerWidth, Container.Region.Y, _headerWidth, Container.Region.Bottom);
            }
			
            context.DrawLine(linePen, 0, Container.Region.Bottom - 1, maxX, Container.Region.Bottom - 1);
            context.DrawLine(linePen, 0, firstY - y, maxX, firstY - y);
        }
        catch (ArgumentOutOfRangeException)
        {
            //Chart cleared
            return;
        }
        catch (Exception e)
        {
            this.LogError("Cluster statistic rendering error ", e);
            throw;
        }
        finally
        {
            context.SetTextRenderingHint(RenderTextRenderingHint.AntiAlias);
            context.SetClip(bounds);
        }
    }

    #endregion

    #region Private methods

    private System.Drawing.Color GetDeltaChangeBrush(IndicatorCandle candle, int j, decimal rate)
    {
	    var prevCandle = GetCandle(Math.Max(j - 1, 0));
	    var change = candle.Delta - prevCandle.Delta;
	    var rectColor = change > 0 ? AskColor : BidColor;
	    return Blend(rectColor, _backGroundColor, rate);
    }

    private void FontChanged(object sender, PropertyChangedEventArgs e)
    {
	    _fontChanged = true;
    }

    private decimal GetRate(decimal value, decimal maximumValue)
    {
        if (maximumValue == 0)
            return 10;

        var rate = value * 100.0m / (maximumValue * 0.6m);

        if (rate < 10)
            rate = 10;

        if (rate > 100)
            return 100;

        return rate;
    }

    private System.Drawing.Color Blend(Color color, Color backColor, decimal amount)
    {
        var r = (byte)(color.R + (backColor.R - color.R) * (1 - amount * 0.01m));
        var g = (byte)(color.G + (backColor.G - color.G) * (1 - amount * 0.01m));
        var b = (byte)(color.B + (backColor.B - color.B) * (1 - amount * 0.01m));
        return System.Drawing.Color.FromArgb(_bgAlpha, r, g, b);
    }

    #endregion

    private class RenderOrder : Dictionary<DataType, RenderInfo>
    {
	    public SortedList<int, DataType> AvailableStrings = new();

	    public RenderOrder()
	    {
		    Add(DataType.Ask, new RenderInfo(0));
		    Add(DataType.Bid, new RenderInfo(1));
		    Add(DataType.Delta, new RenderInfo(2));
		    Add(DataType.DeltaVolume, new RenderInfo(3));
		    Add(DataType.SessionDelta, new RenderInfo(4));
		    Add(DataType.SessionDeltaVolume, new RenderInfo(5));
		    Add(DataType.MaxDelta, new RenderInfo(6));
		    Add(DataType.MinDelta, new RenderInfo(7));
		    Add(DataType.DeltaChange, new RenderInfo(8));
		    Add(DataType.Volume, new RenderInfo(9));
		    Add(DataType.VolumeSecond, new RenderInfo(10));
		    Add(DataType.SessionVolume, new RenderInfo(11));
		    Add(DataType.Trades, new RenderInfo(12));
		    Add(DataType.Height, new RenderInfo(13));
		    Add(DataType.Time, new RenderInfo(14));
		    Add(DataType.Duration, new RenderInfo(15));
        }

	    public void SetEnabled(DataType type, bool enabled)
	    {
            this[type].Enabled = enabled;
            RebuildCache();
	    }

	    private void RebuildCache()
	    {
		    AvailableStrings.Clear();

		    foreach (var (type, info) in this)
		    {
			    if(!info.Enabled)
                    continue;

                AvailableStrings.Add(info.Order, type);
		    }
	    }

	    public void UpdateOrder(DataType from, DataType to)
	    {
		    var fromOrder = this[from].Order;
		    var toOrder = this[to].Order;
			
		    if (fromOrder > toOrder)
		    {
			    foreach (KeyValuePair<DataType, RenderInfo> row in this.Where(row => row.Value.Order < fromOrder && row.Value.Order >= toOrder))
				    row.Value.Order++;
		    }
		    else
		    {
			    foreach (KeyValuePair<DataType, RenderInfo> row in this.Where(row => row.Value.Order > fromOrder && row.Value.Order <= toOrder))
				    row.Value.Order--;
            }

		    this[from].Order = toOrder;
            RebuildCache();
        }
    }
	
    private class RenderInfo(int order, bool enabled = false)
    {
		public int Order { get; set; } = order;

		public bool Enabled { get; set; } = enabled;
    }

    private enum DataType
    {
        Ask,
        Bid,
        Delta,
        DeltaVolume,
        SessionDelta,
        SessionDeltaVolume,
        MaxDelta,
        MinDelta,
        DeltaChange,
        Volume,
        VolumeSecond,
        SessionVolume,
        Trades,
        Height,
        Time,
        Duration,
        None
    }
}