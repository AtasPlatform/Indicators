namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.Linq;

using ATAS.Indicators.Drawing;
using ATAS.Indicators.Technical.Extensions;

using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering;
using OFT.Rendering.Context;
using OFT.Rendering.Control;
using OFT.Rendering.Settings;
using OFT.Rendering.Tools;

using Utils.Common.Logging;

using Color = CrossColor;

[DisplayName("Cluster Statistic")]
[Category(IndicatorCategories.VolumeOrderFlow)]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.ClusterStatisticDescription))]
[HelpLink("https://help.atas.net/support/solutions/articles/72000602624")]
public class ClusterStatistic : Indicator
{
	#region Nested types

	public class SortedRows : SortedList<int, DataType>
	{
		#region Properties

		public int SkipIdx { get; set; } = -1;

		#endregion
	}

	public class RenderOrder : Dictionary<DataType, RenderInfo>
	{
		#region Fields

		public readonly SortedRows AvailableStrings = new();

		#endregion

		#region ctor

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
            Add(DataType.DeltaSecond, new RenderInfo(16));
            Add(DataType.PeakVolPerSec, new RenderInfo(17));
            Add(DataType.PeakDeltaPerSec, new RenderInfo(18));
        }

		#endregion

		#region Public methods

		public void SetEnabled(DataType type, bool enabled)
		{
			this[type].Enabled = enabled;
			RebuildCache();
		}

		public void UpdateOrder(DataType from, DataType to)
		{
			var fromOrder = this[from].Order;
			var toOrder = this[to].Order;

			if (fromOrder > toOrder)
			{
				foreach (var row in this.Where(row => row.Value.Order < fromOrder && row.Value.Order >= toOrder))
					row.Value.Order++;
			}
			else
			{
				foreach (var row in this.Where(row => row.Value.Order > fromOrder && row.Value.Order <= toOrder))
					row.Value.Order--;
			}

			this[from].Order = toOrder;
			RebuildCache();
		}

		#endregion

		#region Private methods

		private void RebuildCache()
		{
			AvailableStrings.Clear();

			foreach (var (type, info) in this)
			{
				if (!info.Enabled)
					continue;

				AvailableStrings.Add(info.Order, type);
			}
		}

		#endregion
	}

	public class RenderInfo(int order, bool enabled = false)
	{
		#region Properties

		public int Order { get; set; } = order;

		public bool Enabled { get; set; } = enabled;

		#endregion
	}

	private struct MaxValues
	{
		public decimal MaxAsk { get; set; }

		public decimal MaxBid { get; set; }

		public decimal MaxSessionDelta { get; set; }

		public decimal MaxDeltaPerVolume { get; set; }

		public decimal MaxSessionDeltaPerVolume { get; set; }

		public decimal MaxDelta { get; set; }

		public decimal MinDelta { get; set; }

		public decimal MaxMaxDelta { get; set; }

		public decimal MaxMinDelta { get; set; }

		public decimal MaxVolume { get; set; }

		public decimal MaxTicks { get; set; }

		public decimal MaxDuration { get; set; }

		public decimal CumVolume { get; set; }

		public decimal MaxDeltaChange { get; set; }

		public decimal MaxHeight { get; set; }

		public decimal MaxVolumeSec { get; set; }
		public decimal MaxDeltaSec { get; set; }
        public decimal MaxPeakVolPerSec { get; set; }
        public decimal MaxPeakDeltaPerSec { get; set; }
    }

	public enum DataType
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
        DeltaSecond,
        SessionVolume,
		Trades,
		Height,
		Time,
		Duration,
        PeakVolPerSec,
        PeakDeltaPerSec,
        None
	}

	#endregion

	#region Static and constants

	private const int _headerOffset = 3;

	private static readonly RenderStringFormat _tipFormat = new()
	{
		Alignment = StringAlignment.Center,
		LineAlignment = StringAlignment.Center
	};

	#endregion

	#region Fields

	private readonly ValueDataSeries _candleDurations = new("durations");
	private readonly ValueDataSeries _candleHeights = new("heights");
	private readonly ValueDataSeries _cDelta = new("cDelta");
	private readonly ValueDataSeries _cDeltaPerVol = new("DeltaPerVol");
	private readonly ValueDataSeries _cVolume = new("cVolume");
	private readonly ValueDataSeries _deltaPerVol = new("BarDeltaPerVol");
    private readonly ValueDataSeries _deltaPerSecond = new("Delta/sec");
    private readonly ValueDataSeries _peakVolPerSec = new("PeakVolPerSec");
    private readonly ValueDataSeries _peakDeltaPerSec = new("PeakDeltaPerSec");

    // --- SoT core state (historical + real-time) ---
    private List<CumulativeTrade> _allCumulativeTrades;     // historical storage
    private readonly Queue<CumulativeTrade> _liveQueue = new(); // sliding RT window

    // Sliding-window aggregates for RT
    private int _winTrades;                 // number of executions (sum of Ticks.Count)
    private decimal _winDelta;              // signed delta (buy vol - sell vol)

    // Per-live-bar peaks while the bar is forming
    private decimal _rtPeakTradesPerSec;    // trades/sec peak for the current live bar
    private decimal _rtPeakDeltaPerSec;     // delta/sec at the same window as trades/sec peak

    private readonly RenderStringFormat _stringLeftFormat = new()
	{
		Alignment = StringAlignment.Near,
		LineAlignment = StringAlignment.Center,
		Trimming = StringTrimming.EllipsisCharacter,
		FormatFlags = StringFormatFlags.NoWrap
	};

	private readonly ValueDataSeries _volPerSecond = new("VolPerSecond");
	private bool _atHeader;

	private bool _atPanel;

	private byte _bgAlpha = 255;
	private int _bgTransparency = 10;
	private bool _centerAlign;
	private decimal _cumVolume;
	private bool _fontChanged;
	private System.Drawing.Color _headerBackground = System.Drawing.Color.FromArgb(0xFF, 84, 84, 84);

	private int _headerWidth = 130;

	private int _height = 15;

	private int _lastBar = -1;
	private int _lastDeltaAlert;
	private decimal _lastDeltaValue;
	private int _lastVolumeAlert;
	private decimal _lastVolumeValue;

	private RenderPen _linePen = new(System.Drawing.Color.Transparent);
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

	private DataType _pressedString = DataType.None;
	
	private int _selectionOffset;
	private RenderPen _selectionPen = new(System.Drawing.Color.Transparent, 3);
	private int _selectionY;
	private bool _showAsk;
	private bool _showBid;
	private bool _showDelta;
	private bool _showDeltaChange;
	private bool _showDeltaPerVolume;
	private bool _showDuration;
	private bool _showHighLow;
	private bool _showMaximumDelta;
	private bool _showMinimumDelta;
	private bool _showSessionDelta;
	private bool _showSessionDeltaPerVolume;
	private bool _showSessionVolume;
	private bool _showTicks;
	private bool _showTime;
	private bool _showVolume;
	private bool _showVolumePerSecond;
    private bool _showDeltaPerSecond;
    private System.Drawing.Color _textColor;
	private string _tipText;

	[Browsable(false)]
	public RenderOrder RowsOrder = new();

    #endregion

    #region Properties

    private int StrCount => RowsOrder.AvailableStrings.Count;
	
    #region Rows

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowAsk), GroupName = nameof(Strings.Rows),
        Description = nameof(Strings.ShowAsksDescription), Order = 110)]
    public bool ShowAsk
    {
        get => _showAsk;
        set
        {
            _showAsk = value;
            RowsOrder.SetEnabled(DataType.Ask, value);
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
            RowsOrder.SetEnabled(DataType.Bid, value);
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
            RowsOrder.SetEnabled(DataType.Delta, value);
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
            RowsOrder.SetEnabled(DataType.DeltaVolume, value);
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
            RowsOrder.SetEnabled(DataType.SessionDelta, value);
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
            RowsOrder.SetEnabled(DataType.SessionDeltaVolume, value);

            if (value)
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
            RowsOrder.SetEnabled(DataType.MaxDelta, value);
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
            RowsOrder.SetEnabled(DataType.MinDelta, value);
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
            RowsOrder.SetEnabled(DataType.DeltaChange, value);
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
            RowsOrder.SetEnabled(DataType.Volume, value);
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
            RowsOrder.SetEnabled(DataType.VolumeSecond, value);
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
            RowsOrder.SetEnabled(DataType.SessionVolume, value);
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
            RowsOrder.SetEnabled(DataType.Trades, value);
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
            RowsOrder.SetEnabled(DataType.Height, value);
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
            RowsOrder.SetEnabled(DataType.Time, value);
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
            RowsOrder.SetEnabled(DataType.Duration, value);
        }
    }

    [DisplayName("Delta/sec")]
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Rows),
     Description = "Show Delta per second", Order = 197)]
    public bool ShowDeltaPerSecond
    {
        get => _showDeltaPerSecond;
        set
        {
            _showDeltaPerSecond = value;
            RowsOrder.SetEnabled(DataType.DeltaSecond, value);
        }
    }

    [DisplayName("Max Vol/sec (peak)")]
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Rows), Order = 198)]
    public bool ShowPeakVolPerSec
    {
        get => RowsOrder.TryGetValue(DataType.PeakVolPerSec, out var ri) && ri.Enabled;
        set => RowsOrder.SetEnabled(DataType.PeakVolPerSec, value);
    }

    [DisplayName("Delta at Max vol/sec")]
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Rows), Order = 199)]
    public bool ShowPeakDeltaPerSec
    {
        get => RowsOrder.TryGetValue(DataType.PeakDeltaPerSec, out var ri) && ri.Enabled;
        set => RowsOrder.SetEnabled(DataType.PeakDeltaPerSec, value);
    }

    #endregion

    #region Max vol/sec settings

    [Display(Name = "Time Window (sec)", GroupName = "Max vol/sec", Order = 201)]
    [Range(1, 600)]
    public int SotTimeWindowSec { get; set; } = 5;

    [Display(Name = "Min Trades per Window", GroupName = "Max vol/sec", Order = 202)]
    [Range(1, 100000)]
    public int SotMinTrades { get; set; } = 150;

    #endregion

    #region Colors

    [Display(ResourceType = typeof(Strings), Name = "BackGround", GroupName = nameof(Strings.Visualization),
        Description = nameof(Strings.LabelFillColorDescription), Order = 200)]
    public Color BackGroundColor { get; set; } = Color.FromArgb(120, 0, 0, 0);

    [Range(1, 10)]
    [Display(ResourceType = typeof(Strings), Name = "Transparency", GroupName = nameof(Strings.Visualization), Order = 205)]
    public int BgTransparency
    {
        get => _bgTransparency;
        set
        {
            _bgTransparency = value;
            _bgAlpha = (byte)(255 * value / 10);
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Grid), GroupName = nameof(Strings.Visualization),
        Description = nameof(Strings.GridColorDescription), Order = 210)]
    public Color GridColor
    {
        get => _linePen.Color.Convert();
        set
        {
            _linePen = new RenderPen(value.Convert());
            _selectionPen = new RenderPen(value.Convert(), 3);
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.VisibleProportion), GroupName = nameof(Strings.Visualization),
        Description = nameof(Strings.VisibleProportionDescription), Order = 220)]
    public bool VisibleProportion { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Volume), GroupName = nameof(Strings.Visualization),
        Description = nameof(Strings.VolumeColorDescription), Order = 230)]
    public Color VolumeColor { get; set; } = CrossColors.DarkGray;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AskColor), GroupName = nameof(Strings.Visualization),
        Description = nameof(Strings.AskColorDescription), Order = 240)]
    public Color AskColor { get; set; } = CrossColors.Green;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BidColor), GroupName = nameof(Strings.Visualization),
        Description = nameof(Strings.BidColorDescription), Order = 250)]
    public Color BidColor { get; set; } = CrossColors.Red;

    #endregion

    #region Text

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.Text),
        Description = nameof(Strings.LabelTextColorDescription), Order = 300)]
    public Color TextColor
    {
        get => _textColor.Convert();
        set => _textColor = value.Convert();
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Font), GroupName = nameof(Strings.Text),
        Description = nameof(Strings.FontSettingDescription), Order = 310)]
    public FontSetting Font { get; set; } = new("Arial", 9);

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.CenterAlign), GroupName = nameof(Strings.Text),
        Description = nameof(Strings.CenterAlignDescription), Order = 320)]
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

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.Headers),
        Description = nameof(Strings.HeaderBackgroundDescription), Order = 330)]
    public Color HeaderBackground
    {
        get => _headerBackground.Convert();
        set => _headerBackground = value.Convert();
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.HideRowsDescription), GroupName = nameof(Strings.Headers),
        Description = nameof(Strings.HideHeadersDescription), Order = 340)]
    public bool HideRowsDescription { get; set; }

    #endregion

    #region Volume Alert

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.VolumeAlert),
        Description = nameof(Strings.UseAlertDescription), Order = 400)]
    public bool UseVolumeAlert { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.VolumeAlert),
        Description = nameof(Strings.AlertFilterDescription), Order = 410)]
    [Range(0, int.MaxValue)]
    public decimal VolumeAlertValue { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.VolumeAlert),
        Description = nameof(Strings.AlertFileDescription), Order = 420)]
    public string VolumeAlertFile { get; set; } = "alert1";

    #endregion

    #region Delta alert

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.DeltaAlert),
        Description = nameof(Strings.UseAlertDescription), Order = 500)]
    public bool UseDeltaAlert { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.DeltaAlert),
        Description = nameof(Strings.AlertFilterDescription), Order = 510)]
    public decimal DeltaAlertValue { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.DeltaAlert),
        Description = nameof(Strings.AlertFileDescription), Order = 520)]
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
		SubscribeToDrawingEvents(DrawingLayouts.LatestBar | DrawingLayouts.Historical | DrawingLayouts.Final);

		DataSeries[0].IsHidden = true;
		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
		ShowDescription = false;
	}

	#endregion

	#region Public methods

	public override bool ProcessMouseDown(RenderControlMouseEventArgs e)
	{
		var cursor = e.Location;

		if (!Container.Region.Contains(cursor) || e.X > _headerWidth)
			return base.ProcessMouseDown(e);

		if (StrCount <= 1)
			return base.ProcessMouseDown(e);

		var height = Container.Region.Height / StrCount;

		var rowNum = Math.Max((e.Y - Container.Region.Top) / height, 0);
		rowNum = Math.Min(rowNum, StrCount - 1);

		_selectionOffset = 0;
		_selectionY = e.Y;
		_pressedString = RowsOrder.AvailableStrings.GetValueAtIndex(rowNum);
		CacheChanged();

		return true;
	}

	public override bool ProcessMouseMove(RenderControlMouseEventArgs e)
	{
		_atPanel = Container.Region.Contains(e.Location);
		_atHeader = e.X <= _headerWidth && _atPanel;

		if (_pressedString is DataType.None)
			return base.ProcessMouseMove(e);

		if (StrCount <= 1)
			return base.ProcessMouseDown(e);

		var height = Container.Region.Height / StrCount;

		var rowNum = Math.Max((e.Y - Container.Region.Top) / height, 0);
		rowNum = Math.Min(rowNum, StrCount - 1);

		var currentString = RowsOrder.AvailableStrings.GetValueAtIndex(rowNum);

		if (_pressedString != currentString)
		{
			RowsOrder.UpdateOrder(_pressedString, currentString);
			CacheChanged();

			_selectionY += (e.Y > _selectionY ? 1 : -1) * height;
		}

		_selectionOffset = _selectionY - e.Y;

		return true;
	}

	public override StdCursor GetCursor(RenderControlMouseEventArgs e)
	{
		if ((!Container.Region.Contains(e.Location) || e.X > _headerWidth) && _pressedString is DataType.None)
			return base.GetCursor(e);

		return StdCursor.Hand;
	}

	public override bool ProcessMouseUp(RenderControlMouseEventArgs e)
	{
		_pressedString = DataType.None;
		CacheChanged();
		return base.ProcessMouseUp(e);
	}

	#endregion

	#region Protected methods

	protected override void OnApplyDefaultColors()
	{
		if (ChartInfo is null)
			return;

		BidColor = ChartInfo.ColorsStore.FootprintBidColor.Convert();
		BidColor = CrossColorExtensions.FromRgb(BidColor.R, BidColor.G, BidColor.B);

		AskColor = ChartInfo.ColorsStore.FootprintAskColor.Convert();
		AskColor = CrossColorExtensions.FromRgb(AskColor.R, AskColor.G, AskColor.B);

		VolumeColor = ChartInfo.ColorsStore.PaneSeparators.Color.Convert();
		VolumeColor = CrossColorExtensions.FromRgb(VolumeColor.R, VolumeColor.G, VolumeColor.B);

		GridColor = ChartInfo.ColorsStore.Grid.Color.Convert();
		GridColor = CrossColorExtensions.FromRgb(GridColor.R, GridColor.G, GridColor.B);

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
        _deltaPerSecond[bar] = candle.Delta / candleSeconds;

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

		_deltaPerVol[bar] = candle.Volume is 0 
			? 0
			: Math.Abs(candle.Delta * 100m / candle.Volume);

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
		_maxBid = Math.Max(candle.Bid, _maxBid);

		_maxDeltaChange = Math.Max(Math.Abs(candle.Delta - prevCandle.Delta), _maxDeltaChange);

		_maxDelta = Math.Max(Math.Abs(candle.Delta), _maxDelta);

		_maxMaxDelta = Math.Max(Math.Abs(candle.MaxDelta), _maxMaxDelta);
		_maxMinDelta = Math.Max(Math.Abs(candle.MinDelta), _maxMinDelta);

		_maxVolume = Math.Max(candle.Volume, _maxVolume);

		_minDelta = Math.Min(candle.MinDelta, _minDelta);

		_maxDeltaPerVolume = candle.Volume is 0
			? 0
			: Math.Max(Math.Abs(100 * candle.Delta / candle.Volume), _maxDeltaPerVolume);

		var candleHeight = candle.High - candle.Low;
		_maxHeight = Math.Max(candleHeight, _maxHeight);
		_candleHeights[bar] = candleHeight;

		_maxTicks = Math.Max(candle.Ticks, _maxTicks);

		_candleDurations[bar] = (int)(candle.LastTime - candle.Time).TotalSeconds;
		_maxDuration = Math.Max(_candleDurations[bar], _maxDuration);

		if (Math.Abs(_cVolume[bar] - 0) > 0.000001m)
			_cDeltaPerVol[bar] = _cDelta[bar] * 
				(_cVolume[bar] is 0 
					? _cDelta[bar] 
					: 100.0m / _cVolume[bar]);

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
		if (ChartInfo is not { PriceChartContainer.BarsWidth: > 2 })
			return;

		if (LastVisibleBarNumber > CurrentBar - 1)
			return;

		if (StrCount is 0)
			return;

		if (_fontChanged)
		{
			var str = "Session Delta/Volume";
			var width = context.MeasureString(str, Font.RenderObject).Width;
			_headerWidth = width + 10;
			_fontChanged = false;
		}

		var bounds = context.ClipBounds;

		_height = Container.Region.Height / StrCount;
		var fullBarsWidth = (int)(ChartInfo.PriceChartContainer.BarsWidth + ChartInfo.PriceChartContainer.BarSpacing);
		var showHeadersText = context.MeasureString("1", Font.RenderObject).Height * 0.9 <= _height;
		var showValues = fullBarsWidth >= 30 && showHeadersText;

		try
		{
			context.SetClip(Container.Region);

			context.SetTextRenderingHint(RenderTextRenderingHint.Aliased);

			var overPixels = Container.Region.Height % StrCount;

			var y = Container.Region.Y;

			var maxX = ChartInfo.GetXByBar(LastVisibleBarNumber) + fullBarsWidth;

			var maxValues = CreateMaxValues();

			var drawHeaders = !HideRowsDescription
				|| Container.Region.Contains(MouseLocationInfo.LastPosition)
				|| _pressedString is not DataType.None;

			var selectionY = 0;

			if ((layout is DrawingLayouts.LatestBar or DrawingLayouts.Historical && _pressedString is DataType.None)
			    ||
			    (_pressedString is not DataType.None && layout is DrawingLayouts.Final))
			{
				var startBar = LastVisibleBarNumber;

				if (layout is DrawingLayouts.Historical)
					startBar = Math.Min(startBar, CurrentBar - 2);

				for (var bar = startBar; bar >= FirstVisibleBarNumber; bar--)
				{
					if (layout is DrawingLayouts.LatestBar)
					{
						if (bar < CurrentBar - 1)
							break;
					}

					var x = ChartInfo.GetXByBar(bar);

					var y1 = y;
					var candle = GetCandle(bar);

					DrawBarValues(context, maxValues, candle, x, ref y1, ref selectionY, fullBarsWidth, showValues, overPixels, bar);
					overPixels = Container.Region.Height % StrCount;
				}
			}

			if (layout is DrawingLayouts.Historical || _pressedString is not DataType.None)
				DrawValuesTable(context, fullBarsWidth, maxX);

			if ((drawHeaders && layout is DrawingLayouts.Final && (HideRowsDescription || _pressedString is not DataType.None))
			    ||
			    (layout is DrawingLayouts.Historical && !HideRowsDescription && _pressedString is DataType.None))
			{
				for (var i = 0; i < RowsOrder.AvailableStrings.Count; i++)
				{
					var type = RowsOrder.AvailableStrings.GetValueAtIndex(i);
					var rectHeight = _height + (overPixels > 0 ? 1 : 0);

					if (i == RowsOrder.AvailableStrings.SkipIdx && i != RowsOrder.AvailableStrings.Count - 1)
					{
						y += rectHeight;
						overPixels--;
						continue;
					}

					DrawHeader(type);

					if (_pressedString is not DataType.None && i == RowsOrder.AvailableStrings.Count - 1 && i != RowsOrder.AvailableStrings.SkipIdx)
						DrawHeader(_pressedString);

					y += rectHeight;
					overPixels--;

					void DrawHeader(DataType type)
					{
						var isSelected = type == _pressedString;
						var rectY = type == _pressedString ? selectionY - _selectionOffset : y;

						if (isSelected)
							rectY = Math.Max(Container.Region.Y, Math.Min(Container.Region.Bottom - rectHeight, rectY));

						var descRect = new Rectangle(0, rectY, _headerWidth, rectHeight);
						context.FillRectangle(_headerBackground, descRect);

						if (showHeadersText)
						{
							var text = GetHeader(type);

							var textRect = descRect with
							{
								X = descRect.X + _headerOffset
							};

							context.DrawString(text, Font.RenderObject, _textColor, textRect, _stringLeftFormat);
						}

						if (type == _pressedString)
						{
							var selectionRect = descRect with
							{
								X = Container.Region.X,
								Width = maxX - Container.Region.X
							};

							switch (_selectionOffset)
							{
								case < 0:
									context.FillRectangle(_headerBackground,
										new Rectangle(Container.Region.X, selectionY, selectionRect.Width, rectY - selectionY));
									context.DrawLine(_linePen, Container.Region.X, selectionY, maxX, selectionY);
									break;
								case > 0:
									context.FillRectangle(_headerBackground,
										new Rectangle(Container.Region.X, rectY + rectHeight, selectionRect.Width, selectionY - rectY));
									context.DrawLine(_linePen, Container.Region.X, selectionY + rectHeight, maxX, selectionY + rectHeight);
									break;
							}

							context.DrawRectangle(_selectionPen, selectionRect);
						}
						else if (i is not 0 && i - 1 != RowsOrder.AvailableStrings.SkipIdx)
							context.DrawLine(_linePen, Container.Region.X, rectY, maxX, rectY);
					}
				}

				var tableRect = new Rectangle(Container.Region.X, Container.Region.Y, maxX - Container.Region.X, Container.Region.Height - 1);
				context.DrawLine(_linePen, _headerWidth, Container.Region.Y, _headerWidth, Container.Region.Bottom);
				context.DrawRectangle(_linePen, tableRect);
			}

			if (_pressedString is not DataType.None)
				return;

			if (!_atPanel)
				return;

			if (layout is DrawingLayouts.Final)
			{
				if (!Container.Region.Contains(MouseLocationInfo.LastPosition))
					return;

				if ((_atHeader && showHeadersText) || (!_atHeader && showValues))
					return;

				var bar = MouseLocationInfo.BarBelowMouse;
				var rowNum = Math.Max((MouseLocationInfo.LastPosition.Y - Container.Region.Top) / _height, 0);
				rowNum = Math.Min(rowNum, StrCount - 1);

				var type = RowsOrder.AvailableStrings.GetValueAtIndex(rowNum);

				var tipColor = System.Drawing.Color.Transparent;
				var tipText = "";

				if (_atHeader)
				{
					tipText = GetHeader(type);
					tipColor = _headerBackground;
				}
				else
				{
					var candle = GetCandle(bar);
					var rate = GetRate(maxValues, type, candle, bar);

					tipColor = GetBrush(type, candle, bar, rate);
					tipText = GetValueText(type, candle, bar);
				}

				DrawToolTip(context, MouseLocationInfo.LastPosition, tipText, tipColor);
			}
		}
		catch (ArgumentOutOfRangeException)
		{
			//Chart cleared
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

	private void DrawValuesTable(RenderContext context, int barWidth, int maxX)
	{
		var x = 0;

		for (var bar = FirstVisibleBarNumber; bar <= LastVisibleBarNumber; bar++)
		{
			x = ChartInfo.GetXByBar(bar);
			context.DrawLine(_linePen, x, Container.Region.Y, x, Container.Region.Bottom);
		}

		x += barWidth;
		context.DrawLine(_linePen, x, Container.Region.Y, x, Container.Region.Bottom);

		var overPixels = Container.Region.Height % StrCount;

		var y = Container.Region.Y;

		var skipIdx = RowsOrder.AvailableStrings.SkipIdx;

		for (var i = 0; i < RowsOrder.AvailableStrings.Count; i++)
		{
			if (_pressedString is not DataType.None)
			{
				if ((_selectionOffset < 0 && i == skipIdx + 1) || (_selectionOffset > 0 && i == skipIdx))
				{
					y += _height + (overPixels > 0 ? 1 : 0);
					overPixels--;
					continue;
				}
			}

			context.DrawLine(_linePen, Container.Region.X, y, maxX, y);

			y += _height + (overPixels > 0 ? 1 : 0);
			overPixels--;
		}

		y--;
		context.DrawLine(_linePen, Container.Region.X, y, maxX, y);
	}

	private void DrawBarValues(RenderContext context, MaxValues maxValues, IndicatorCandle candle,
		int x, ref int y, ref int selectionY, int fullBarsWidth, bool showValues, int overPixelsSpace, int bar)
	{
		var overPixels = overPixelsSpace;

		for (var i = 0; i < RowsOrder.AvailableStrings.Count; i++)
		{
			var rowIndex = i;
			var type = RowsOrder.AvailableStrings.GetValueAtIndex(rowIndex);
			var isSelected = type == _pressedString;

			if (isSelected)
				selectionY = y;

			var rectHeight = _height + (overPixels > 0 ? 1 : 0);

			if (rowIndex == RowsOrder.AvailableStrings.SkipIdx && rowIndex != RowsOrder.AvailableStrings.Count - 1)
			{
				y += rectHeight;
				overPixels--;
				continue;
			}

			DrawValue(context, type, candle, maxValues, selectionY, x, y, bar, rectHeight, fullBarsWidth, showValues);

			y += rectHeight;
			overPixels--;
		}

		if (_pressedString is DataType.None)
			return;

		{
			var idx = RowsOrder.AvailableStrings.SkipIdx;
			var rectHeight = _height + (overPixels - 1 < idx ? 0 : 1);
			DrawValue(context, _pressedString, candle, maxValues, selectionY, x, y, bar, rectHeight, fullBarsWidth, showValues);
		}
	}

	private void DrawValue(RenderContext context, DataType type, IndicatorCandle candle, MaxValues maxValues,
		int selectionY, int x, int y, int bar, int rectHeight, int fullBarsWidth, bool showValues)
	{
		var rectY = type == _pressedString ? selectionY - _selectionOffset : y;

		if (type == _pressedString)
			rectY = Math.Max(Container.Region.Y, Math.Min(Container.Region.Bottom - rectHeight, rectY));

		var rect = new Rectangle(x, rectY, fullBarsWidth, rectHeight);
		var rate = GetRate(maxValues, type, candle, bar);

		var bgBrush = GetBrush(type, candle, bar, rate);

		context.FillRectangle(bgBrush, rect);

		if (showValues)
		{
			var text = GetValueText(type, candle, bar);

			var textRect = rect with
			{
				X = rect.X + _headerOffset
			};

			context.DrawString(text, Font.RenderObject, _textColor, textRect, _stringLeftFormat);
		}
	}

	private System.Drawing.Color GetBrush(DataType type, IndicatorCandle candle, int bar, decimal rate)
	{
		return type switch
		{
			DataType.Ask or DataType.Bid or DataType.Delta or DataType.DeltaVolume or DataType.DeltaSecond =>
				Blend(candle.Delta > 0 ? AskColor : BidColor, BackGroundColor, rate),

			DataType.Volume or DataType.VolumeSecond or DataType.SessionVolume or
				DataType.Trades or DataType.Height or DataType.Time or DataType.Duration or DataType.PeakVolPerSec => Blend(VolumeColor, BackGroundColor, rate),
			DataType.MaxDelta => Blend(candle.MaxDelta > 0 ?  AskColor : BidColor, BackGroundColor, rate),
			DataType.MinDelta => Blend(candle.MinDelta > 0 ?  AskColor : BidColor, BackGroundColor, rate),
            DataType.SessionDeltaVolume => Blend(_cDeltaPerVol[bar] > 0 ? AskColor : BidColor, BackGroundColor, rate),
			DataType.SessionDelta => Blend(_cDelta[bar] > 0 ? AskColor : BidColor, BackGroundColor, rate),
			DataType.DeltaChange => GetDeltaChangeBrush(candle, bar, rate),
            DataType.PeakDeltaPerSec => Blend(_peakDeltaPerSec[bar] >= 0 ? AskColor : BidColor, BackGroundColor, rate),
            DataType.None => System.Drawing.Color.Transparent,
            _ => throw new ArgumentOutOfRangeException()
		};
	}

	private decimal GetRate(MaxValues maxValues, DataType type, IndicatorCandle candle, int bar)
	{
		return type switch
		{
			DataType.Ask => GetRate(candle.Ask, maxValues.MaxAsk),
			DataType.Bid => GetRate(candle.Bid, maxValues.MaxBid),
			DataType.Delta => GetRate(Math.Abs(candle.Delta), maxValues.MaxDelta),
			DataType.DeltaVolume => candle.Volume != 0 ? GetRate(Math.Abs(candle.Delta * 100.0m / candle.Volume), maxValues.MaxDeltaPerVolume) : 0,
			DataType.SessionDelta => GetRate(Math.Abs(_cDelta[bar]), maxValues.MaxSessionDelta),
			DataType.SessionDeltaVolume => GetRate(Math.Abs(_cDeltaPerVol[bar]), maxValues.MaxSessionDeltaPerVolume),
			DataType.MaxDelta => GetRate(Math.Abs(candle.MaxDelta), maxValues.MaxMaxDelta),
			DataType.MinDelta => GetRate(Math.Abs(candle.MinDelta), maxValues.MaxMinDelta),
			DataType.DeltaChange => GetRate(Math.Abs(candle.Delta - GetCandle(Math.Max(bar - 1, 0)).Delta), maxValues.MaxDeltaChange),
			DataType.Volume => GetRate(candle.Volume, maxValues.MaxVolume),
			DataType.VolumeSecond => GetRate(_volPerSecond[bar], maxValues.MaxVolumeSec),
            DataType.DeltaSecond => GetRate(Math.Abs(_deltaPerSecond[bar]), maxValues.MaxDeltaSec),
            DataType.SessionVolume => GetRate(_cVolume[bar], maxValues.CumVolume),
			DataType.Trades => GetRate(candle.Ticks, maxValues.MaxTicks),
			DataType.Height => GetRate(_candleHeights[bar], maxValues.MaxHeight),
			DataType.Time => GetRate(_cVolume[bar], maxValues.CumVolume),
			DataType.Duration => GetRate(_candleDurations[bar], maxValues.MaxDuration),
            DataType.PeakVolPerSec => GetRate(_peakVolPerSec[bar], maxValues.MaxPeakVolPerSec),
            DataType.PeakDeltaPerSec => GetRate(Math.Abs(_peakDeltaPerSec[bar]), maxValues.MaxPeakDeltaPerSec),
            DataType.None => 0,

			_ => throw new ArgumentOutOfRangeException()
		};
	}

	private MaxValues CreateMaxValues()
	{
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
        decimal maxDeltaSec = 0m;
        decimal maxPeakVolPerSec = 0m;
        decimal maxPeakDeltaPerSec = 0m;

        if (VisibleProportion)
		{
			for (var i = FirstVisibleBarNumber; i <= LastVisibleBarNumber; i++)
			{
				var candle = GetCandle(i);
				maxDelta = Math.Max(candle.Delta, maxDelta);
				maxVolume = Math.Max(candle.Volume, maxVolume);
				minDelta = Math.Min(candle.MinDelta, minDelta);
				maxAsk = Math.Max(candle.Ask, maxAsk);
				maxBid = Math.Max(candle.Bid, maxBid);
				maxMaxDelta = Math.Max(Math.Abs(candle.MaxDelta), maxMaxDelta);
				maxMinDelta = Math.Max(Math.Abs(candle.MinDelta), maxMinDelta);
				maxSessionDelta = Math.Max(Math.Abs(_cDelta[i]), maxSessionDelta);

				if (candle.Volume is not 0)
					maxDeltaPerVolume = Math.Max(Math.Abs(100 * candle.Delta / candle.Volume), maxDeltaPerVolume);

				maxSessionDeltaPerVolume = Math.Max(Math.Abs(_cDeltaPerVol[i]), maxSessionDeltaPerVolume);
				cumVolume += candle.Volume;

                // Absolute peak for Delta/sec over the visible range
                maxDeltaSec = Math.Max(Math.Abs(_deltaPerSecond[i]), maxDeltaSec);

                maxPeakVolPerSec = Math.Max(maxPeakVolPerSec, Math.Abs(_peakVolPerSec[i]));
                maxPeakDeltaPerSec = Math.Max(maxPeakDeltaPerSec, Math.Abs(_peakDeltaPerSec[i]));

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
            for (int i = 0; i <= CurrentBar - 1; i++)
			{
                maxDeltaSec = Math.Max(Math.Abs(_deltaPerSecond[i]), maxDeltaSec);
                maxPeakVolPerSec = Math.Max(maxPeakVolPerSec, Math.Abs(_peakVolPerSec[i]));
                maxPeakDeltaPerSec = Math.Max(maxPeakDeltaPerSec, Math.Abs(_peakDeltaPerSec[i]));
            }
        }

		return new MaxValues
		{
			MaxAsk = maxAsk,
			MaxBid = maxBid,
			MaxSessionDelta = maxSessionDelta,
			MaxDeltaPerVolume = maxDeltaPerVolume,
			MaxSessionDeltaPerVolume = maxSessionDeltaPerVolume,
			MaxDelta = maxDelta,
			MinDelta = minDelta,
			MaxMaxDelta = maxMaxDelta,
			MaxMinDelta = maxMinDelta,
			MaxVolume = maxVolume,
			MaxTicks = maxTicks,
			MaxDuration = maxDuration,
			CumVolume = cumVolume,
			MaxDeltaChange = maxDeltaChange,
			MaxHeight = maxHeight,
			MaxVolumeSec = maxVolumeSec,
            MaxDeltaSec = maxDeltaSec,
            MaxPeakVolPerSec = maxPeakVolPerSec,
            MaxPeakDeltaPerSec = maxPeakDeltaPerSec
        };
	}

	private string GetValueText(DataType type, IndicatorCandle candle, int bar)
	{
		return type switch
		{
			DataType.Ask => ChartInfo.TryGetMinimizedVolumeString(candle.Ask),
			DataType.Bid => ChartInfo.TryGetMinimizedVolumeString(candle.Bid),
			DataType.Delta => ChartInfo.TryGetMinimizedVolumeString(candle.Delta),
			DataType.DeltaVolume => _deltaPerVol[bar].ToString("F") + "%",
			DataType.SessionDelta => ChartInfo.TryGetMinimizedVolumeString(_cDelta[bar]),
			DataType.SessionDeltaVolume => _cDeltaPerVol[bar].ToString("F") + "%",
			DataType.MaxDelta => ChartInfo.TryGetMinimizedVolumeString(candle.MaxDelta),
			DataType.MinDelta => ChartInfo.TryGetMinimizedVolumeString(candle.MinDelta),
			DataType.DeltaChange => ChartInfo.TryGetMinimizedVolumeString(candle.Delta - GetCandle(Math.Max(bar - 1, 0)).Delta),
			DataType.Volume => ChartInfo.TryGetMinimizedVolumeString(candle.Volume),
			DataType.VolumeSecond => ChartInfo.TryGetMinimizedVolumeString(_volPerSecond[bar]),
			DataType.SessionVolume => ChartInfo.TryGetMinimizedVolumeString(_cVolume[bar]),
			DataType.Trades => candle.Ticks.ToString(CultureInfo.InvariantCulture),
			DataType.Height => _candleHeights[bar].ToString(CultureInfo.InvariantCulture),
			DataType.Time => candle.Time.AddHours(InstrumentInfo.TimeZone).ToString("HH:mm:ss"),
			DataType.Duration => ((int)(candle.LastTime - candle.Time).TotalSeconds).ToString(),
            DataType.DeltaSecond => ChartInfo.TryGetMinimizedVolumeString(_deltaPerSecond[bar]),
            DataType.PeakVolPerSec => ChartInfo.TryGetMinimizedVolumeString(_peakVolPerSec[bar]),
            DataType.PeakDeltaPerSec => ChartInfo.TryGetMinimizedVolumeString(_peakDeltaPerSec[bar]),
            DataType.None => string.Empty,
			_ => throw new ArgumentOutOfRangeException()
		};
	}

	private void DrawToolTip(RenderContext g, Point location, string text, System.Drawing.Color bgColor)
	{
		var bounds = g.ClipBounds;
		g.ResetClip();

		const int offset = 15;

		var x = location.X;
		var y = location.Y;

		var size = g.MeasureString(text, Font.RenderObject);
		var height = size.Height + 10;
		var rect = new Rectangle(x + offset, y - height - 20, size.Width + 20, height);

		var center = rect.Y + rect.Height / 2;

		Point[] points =
		[
			new(x, y),
			new(x + offset, center - (int)(0.3 * height)),
			new(x + offset, center + (int)(0.3 * height))
		];

		g.FillPolygon(_textColor, points);

		var pen = new RenderPen(_textColor, 2);
		g.DrawRectangle(pen, rect, 2);
		g.FillRectangle(bgColor, rect);
		g.DrawString(text, Font.RenderObject, _textColor, rect, _tipFormat);

		g.SetClip(bounds);
	}

	private void CacheChanged()
	{
		if (_pressedString is DataType.None)
		{
			RowsOrder.AvailableStrings.SkipIdx = -1;
			return;
		}

		var idx = RowsOrder.AvailableStrings.IndexOfValue(_pressedString);

		if (idx is -1)
			throw new KeyNotFoundException("Type " + _pressedString + " not found at cache");

		RowsOrder.AvailableStrings.SkipIdx = idx;
	}

	private System.Drawing.Color GetDeltaChangeBrush(IndicatorCandle candle, int j, decimal rate)
	{
		var prevCandle = GetCandle(Math.Max(j - 1, 0));
		var change = candle.Delta - prevCandle.Delta;
		var rectColor = change > 0 ? AskColor : BidColor;
		return Blend(rectColor, BackGroundColor, rate);
	}

	private void FontChanged(object sender, PropertyChangedEventArgs e)
	{
		_fontChanged = true;
	}

	private string GetHeader(DataType type)
	{
		return type switch
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
            DataType.DeltaSecond => "Delta/sec",
            DataType.PeakVolPerSec => "Max Vol/sec",
            DataType.PeakDeltaPerSec => "Delta at Max vol/sec",
            DataType.None => string.Empty,

			_ => throw new ArgumentOutOfRangeException()
		};
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

    protected override void OnFinishRecalculate()
    {
        // Clear RT window state when a full recalc completes
        _liveQueue.Clear();
        _winTrades = 0;
        _winDelta = 0m;
        _rtPeakTradesPerSec = 0m;
        _rtPeakDeltaPerSec = 0m;

        if (CurrentBar <= 0)
            return;

        // Request historical cumulative trades for the loaded range
        var startTime = GetCandle(0).Time;
        var endTime = GetCandle(CurrentBar - 1).LastTime;

        // 0,0 means "no size filter" — ask for all trades
        var request = new CumulativeTradesRequest(startTime, endTime, 0, 0);
        RequestForCumulativeTrades(request);
    }

    protected override void OnCumulativeTradesResponse(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> cumulativeTrades)
    {
        // Store and rebuild the SoT peaks over history
        _allCumulativeTrades = cumulativeTrades?.ToList() ?? new List<CumulativeTrade>();
        RebuildHistoricalSoT();
    }

    // Recompute SoT peaks (trades/sec peak inside each bar and its delta/sec) using historical cumulative trades
    private void RebuildHistoricalSoT()
    {
        if (_allCumulativeTrades == null || _allCumulativeTrades.Count == 0 || CurrentBar <= 0)
            return;

        // Optional: clear Peak* series before full rebuild
        for (int i = 0; i < CurrentBar; i++)
        {
            _peakVolPerSec[i] = 0m;
            _peakDeltaPerSec[i] = 0m;
        }

        var q = new Queue<CumulativeTrade>();
        int tradeIdx = 0;

        // Sliding-window aggregates
        int winTrades = 0;
        decimal winDelta = 0m;

        // Iterate bars chronologically
        for (int bar = 0; bar < CurrentBar; bar++)
        {
            var c = GetCandle(bar);
            var barStart = c.Time;
            var barEnd = c.LastTime;

            // Reset per-bar peaks
            decimal peakTradesPerSec = 0m;
            decimal peakDeltaPerSec = 0m;

            // Move cursor to this bar's start
            while (tradeIdx < _allCumulativeTrades.Count && _allCumulativeTrades[tradeIdx].Time < barStart)
                tradeIdx++;

            int j = tradeIdx;

            // Walk through trades within this bar's time
            while (j < _allCumulativeTrades.Count && _allCumulativeTrades[j].Time <= barEnd)
            {
                var t = _allCumulativeTrades[j++];
                q.Enqueue(t);

                // Count number of executions in this bundle; fallback to 1 if null
                int ticksCount = (t.Ticks != null ? t.Ticks.Count : 1);
                winTrades += ticksCount;

                // Signed delta using Direction
                winDelta += (t.Direction == TradeDirection.Buy ? t.Volume : -t.Volume);

                // Slide the window: drop trades older than T seconds from current trade time
                var cutoff = t.Time.AddSeconds(-SotTimeWindowSec);
                while (q.Count > 0 && q.Peek().Time <= cutoff)
                {
                    var u = q.Dequeue();
                    int uTicks = (u.Ticks != null ? u.Ticks.Count : 1);
                    winTrades -= uTicks;
                    winDelta -= (u.Direction == TradeDirection.Buy ? u.Volume : -u.Volume);
                }

                // Evaluate if window meets the minimum trade count
                if (winTrades >= SotMinTrades)
                {
                    var tradesPerSec = (decimal)winTrades / SotTimeWindowSec; // SoT Vol/sec
                    var deltaPerSec = winDelta / SotTimeWindowSec;            // SoT Delta/sec (same window)

                    // Keep delta/sec from the window with the greatest trades/sec (break ties by higher vol/sec)
                    if (tradesPerSec > peakTradesPerSec)
                    {
                        peakTradesPerSec = tradesPerSec;
                        peakDeltaPerSec = deltaPerSec;
                    }
                }
            }

            // Store peak-of-bar values (0 if no window met the minimum)
            _peakVolPerSec[bar] = Math.Round(peakTradesPerSec, 0);
            _peakDeltaPerSec[bar] = Math.Round(peakDeltaPerSec, 0);
        }
    }

    // Called by the platform for each incoming cumulative trade in real-time
    protected override void OnCumulativeTrade(CumulativeTrade trade)
    {
        if (trade == null || trade.Volume <= 0)
            return;

        var bar = CurrentBar - 1;
        if (bar < 0)
            return;

        // Maintain a sliding window of last SotTimeWindowSec seconds
        _liveQueue.Enqueue(trade);

        int ticksCount = (trade.Ticks != null ? trade.Ticks.Count : 1);
        _winTrades += ticksCount;
        _winDelta += (trade.Direction == TradeDirection.Buy ? trade.Volume : -trade.Volume);

        // Remove outdated trades (older than T seconds from current trade time)
        var cutoff = trade.Time.AddSeconds(-SotTimeWindowSec);
        while (_liveQueue.Count > 0 && _liveQueue.Peek().Time <= cutoff)
        {
            var old = _liveQueue.Dequeue();
            int oldTicks = (old.Ticks != null ? old.Ticks.Count : 1);
            _winTrades -= oldTicks;
            _winDelta -= (old.Direction == TradeDirection.Buy ? old.Volume : -old.Volume);
        }

        // Only compute/update peaks if the window meets the minimum trades
        if (_winTrades >= SotMinTrades)
        {
            var tradesPerSec = (decimal)_winTrades / SotTimeWindowSec;
            var deltaPerSec = _winDelta / SotTimeWindowSec;

            if (tradesPerSec > _rtPeakTradesPerSec)
            {
                _rtPeakTradesPerSec = tradesPerSec;
                _rtPeakDeltaPerSec = deltaPerSec;
            }
        }

        // Write to Peak* series (rounded to int-like display like your UI)
        _peakVolPerSec[bar] = Math.Round(_rtPeakTradesPerSec, 0);
        _peakDeltaPerSec[bar] = Math.Round(_rtPeakDeltaPerSec, 0);

        // If bar changes, reset RT peaks for the next bar
        if (_lastBar != bar)
        {
            _rtPeakTradesPerSec = 0m;
            _rtPeakDeltaPerSec = 0m;
            _winTrades = 0;
            _winDelta = 0m;
            _liveQueue.Clear();
        }

        _lastBar = bar;
    }



    #endregion
}