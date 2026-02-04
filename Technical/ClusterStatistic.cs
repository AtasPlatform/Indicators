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
		public Action OnChanged;

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

			OnChanged?.Invoke();
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
		SessionVolume,
		Trades,
		Height,
		Time,
		Duration,
		None
	}

	public enum SessionMode
	{
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.None))]
		None,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Default))]
		DefaultSession,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.CustomSession))]
		CustomSession
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
	private readonly ValueDataSeries _deltaChange = new("deltaChange");
	private readonly ValueDataSeries _deltaPerVol = new("BarDeltaPerVol");

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
	private FontSetting _font;
	private bool _layoutChanged = true;
	private System.Drawing.Color _headerBackground = System.Drawing.Color.FromArgb(0xFF, 84, 84, 84);

	private int _headerWidth = 130;

	private int _height = 15;

	private int _lastBar = -1;
	private int _lastAskAlert;
	private decimal _lastAskValue;
	private int _lastBidAlert;
	private decimal _lastBidValue;
	private int _lastDeltaAlert;
	private decimal _lastDeltaValue;
	private int _lastDeltaPerVolumeAlert;
	private decimal _lastDeltaPerVolumeValue;
	private int _lastSessionDeltaAlert;
	private decimal _lastSessionDeltaValue;
	private int _lastSessionDeltaPerVolumeAlert;
	private decimal _lastSessionDeltaPerVolumeValue;
	private int _lastMaxDeltaAlert;
	private decimal _lastMaxDeltaValue;
	private int _lastMinDeltaAlert;
	private decimal _lastMinDeltaValue;
	private int _lastDeltaChangeAlert;
	private decimal _lastDeltaChangeValue;
	private int _lastVolumeAlert;
	private decimal _lastVolumeValue;
	private int _lastVolumePerSecondAlert;
	private decimal _lastVolumePerSecondValue;
	private int _lastSessionVolumeAlert;
	private decimal _lastSessionVolumeValue;
	private int _lastTradesAlert;
	private decimal _lastTradesValue;
	private int _lastHeightAlert;
	private decimal _lastHeightValue;

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
	private System.Drawing.Color _textColor;
	private int _fontHeight;
	private SessionMode _sessionMode = SessionMode.DefaultSession;

	[Browsable(false)]
	public RenderOrder RowsOrder = new();
    private FilterTimeSpan _customSessionStart;

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

    #endregion

    #region Session

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SessionDeltaMode), GroupName = nameof(Strings.Session),
        Description = nameof(Strings.SessionModeDescription), Order = 100)]
    public SessionMode SessionCumMode
    {
        get => _sessionMode;
        set
        {
            _sessionMode = value;
            CustomSessionStart.Enabled = value == SessionMode.CustomSession;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.CustomSessionStart), GroupName = nameof(Strings.Session),
        Description = nameof(Strings.CustomSessionStartDescription), Order = 110)]
    public FilterTimeSpan CustomSessionStart
	{
		get => _customSessionStart;
		set => SetTrackedProperty(ref _customSessionStart, value, propName =>
		{
			if (propName == nameof(FilterTimeSpan.Value) && _sessionMode == SessionMode.CustomSession)
				RecalculateValues();
		});
    }

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
    public FontSetting Font
    {
        get => _font;
        set => SetTrackedProperty(ref _font, value, OnFontPropertyChanged);
    }

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

    #region Ask Alert

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.AskAlert),
        Description = nameof(Strings.UseAlertDescription), Order = 600)]
    public bool UseAskAlert { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.AskAlert),
        Description = nameof(Strings.AlertFilterDescription), Order = 610)]
    [Range(0, int.MaxValue)]
    public decimal AskAlertValue { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.AskAlert),
        Description = nameof(Strings.AlertFileDescription), Order = 620)]
    public string AskAlertFile { get; set; } = "alert1";

    #endregion

    #region Bid Alert

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.BidAlert),
        Description = nameof(Strings.UseAlertDescription), Order = 700)]
    public bool UseBidAlert { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.BidAlert),
        Description = nameof(Strings.AlertFilterDescription), Order = 710)]
    [Range(0, int.MaxValue)]
    public decimal BidAlertValue { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.BidAlert),
        Description = nameof(Strings.AlertFileDescription), Order = 720)]
    public string BidAlertFile { get; set; } = "alert1";

    #endregion

    #region Delta Per Volume Alert

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.DeltaPerVolumeAlert),
        Description = nameof(Strings.UseAlertDescription), Order = 800)]
    public bool UseDeltaPerVolumeAlert { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.DeltaPerVolumeAlert),
        Description = nameof(Strings.AlertFilterDescription), Order = 810)]
    [Range(0, 100)]
    public decimal DeltaPerVolumeAlertValue { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.DeltaPerVolumeAlert),
        Description = nameof(Strings.AlertFileDescription), Order = 820)]
    public string DeltaPerVolumeAlertFile { get; set; } = "alert1";

    #endregion

    #region Session Delta Alert

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.SessionDeltaAlert),
        Description = nameof(Strings.UseAlertDescription), Order = 900)]
    public bool UseSessionDeltaAlert { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.SessionDeltaAlert),
        Description = nameof(Strings.AlertFilterDescription), Order = 910)]
    public decimal SessionDeltaAlertValue { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.SessionDeltaAlert),
        Description = nameof(Strings.AlertFileDescription), Order = 920)]
    public string SessionDeltaAlertFile { get; set; } = "alert1";

    #endregion

    #region Session Delta Per Volume Alert

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.SessionDeltaPerVolumeAlert),
        Description = nameof(Strings.UseAlertDescription), Order = 1000)]
    public bool UseSessionDeltaPerVolumeAlert { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.SessionDeltaPerVolumeAlert),
        Description = nameof(Strings.AlertFilterDescription), Order = 1010)]
    public decimal SessionDeltaPerVolumeAlertValue { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.SessionDeltaPerVolumeAlert),
        Description = nameof(Strings.AlertFileDescription), Order = 1020)]
    public string SessionDeltaPerVolumeAlertFile { get; set; } = "alert1";

    #endregion

    #region Max Delta Alert

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.MaxDeltaAlert),
        Description = nameof(Strings.UseAlertDescription), Order = 1100)]
    public bool UseMaxDeltaAlert { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.MaxDeltaAlert),
        Description = nameof(Strings.AlertFilterDescription), Order = 1110)]
    public decimal MaxDeltaAlertValue { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.MaxDeltaAlert),
        Description = nameof(Strings.AlertFileDescription), Order = 1120)]
    public string MaxDeltaAlertFile { get; set; } = "alert1";

    #endregion

    #region Min Delta Alert

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.MinDeltaAlert),
        Description = nameof(Strings.UseAlertDescription), Order = 1200)]
    public bool UseMinDeltaAlert { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.MinDeltaAlert),
        Description = nameof(Strings.AlertFilterDescription), Order = 1210)]
    public decimal MinDeltaAlertValue { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.MinDeltaAlert),
        Description = nameof(Strings.AlertFileDescription), Order = 1220)]
    public string MinDeltaAlertFile { get; set; } = "alert1";

    #endregion

    #region Delta Change Alert

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.DeltaChangeAlert),
        Description = nameof(Strings.UseAlertDescription), Order = 1300)]
    public bool UseDeltaChangeAlert { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.DeltaChangeAlert),
        Description = nameof(Strings.AlertFilterDescription), Order = 1310)]
    public decimal DeltaChangeAlertValue { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.DeltaChangeAlert),
        Description = nameof(Strings.AlertFileDescription), Order = 1320)]
    public string DeltaChangeAlertFile { get; set; } = "alert1";

    #endregion

    #region Volume Per Second Alert

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.VolumePerSecondAlert),
        Description = nameof(Strings.UseAlertDescription), Order = 1400)]
    public bool UseVolumePerSecondAlert { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.VolumePerSecondAlert),
        Description = nameof(Strings.AlertFilterDescription), Order = 1410)]
    [Range(0, int.MaxValue)]
    public decimal VolumePerSecondAlertValue { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.VolumePerSecondAlert),
        Description = nameof(Strings.AlertFileDescription), Order = 1420)]
    public string VolumePerSecondAlertFile { get; set; } = "alert1";

    #endregion

    #region Session Volume Alert

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.SessionVolumeAlert),
        Description = nameof(Strings.UseAlertDescription), Order = 1500)]
    public bool UseSessionVolumeAlert { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.SessionVolumeAlert),
        Description = nameof(Strings.AlertFilterDescription), Order = 1510)]
    [Range(0, int.MaxValue)]
    public decimal SessionVolumeAlertValue { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.SessionVolumeAlert),
        Description = nameof(Strings.AlertFileDescription), Order = 1520)]
    public string SessionVolumeAlertFile { get; set; } = "alert1";

    #endregion

    #region Trades Alert

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.TradesAlert),
        Description = nameof(Strings.UseAlertDescription), Order = 1600)]
    public bool UseTradesAlert { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.TradesAlert),
        Description = nameof(Strings.AlertFilterDescription), Order = 1610)]
    [Range(0, int.MaxValue)]
    public decimal TradesAlertValue { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.TradesAlert),
        Description = nameof(Strings.AlertFileDescription), Order = 1620)]
    public string TradesAlertFile { get; set; } = "alert1";

    #endregion

    #region Height Alert

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.HeightAlert),
        Description = nameof(Strings.UseAlertDescription), Order = 1700)]
    public bool UseHeightAlert { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.HeightAlert),
        Description = nameof(Strings.AlertFilterDescription), Order = 1710)]
    [Range(0, int.MaxValue)]
    public decimal HeightAlertValue { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.HeightAlert),
        Description = nameof(Strings.AlertFileDescription), Order = 1720)]
    public string HeightAlertFile { get; set; } = "alert1";

    #endregion

    #endregion

    #region ctor

    public ClusterStatistic()
		: base(true)
	{
		DenyToChangePanel = true;
		Panel = IndicatorDataProvider.NewPanel;
		EnableCustomDrawing = true;
		RowsOrder.OnChanged = () => _layoutChanged = true;
		ShowDelta = ShowSessionDelta = ShowVolume = true;
		SubscribeToDrawingEvents(DrawingLayouts.LatestBar | DrawingLayouts.Historical | DrawingLayouts.Final);

		DataSeries[0].IsHidden = true;
		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
		ShowDescription = false;

		Font = new FontSetting("Arial", 9);
		CustomSessionStart = new(false);
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
		HeaderBackground = DefaultColors.Gray.Convert();
		TextColor = CrossColors.White;

		if (ChartInfo is null)
			return;

		static Color WithoutAlpha(Color c) => CrossColorExtensions.FromRgb(c.R, c.G, c.B);

		BidColor = WithoutAlpha(ChartInfo.ColorsStore.FootprintBidColor.Convert());
		AskColor = WithoutAlpha(ChartInfo.ColorsStore.FootprintAskColor.Convert());
		VolumeColor = WithoutAlpha(ChartInfo.ColorsStore.PaneSeparators.Color.Convert());
		GridColor = WithoutAlpha(ChartInfo.ColorsStore.Grid.Color.Convert());

		var bg = ChartInfo.ColorsStore.BaseBackgroundColor;
		BackGroundColor = Color.FromArgb(128, bg.R, bg.G, bg.B);
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
			_cVolume[bar] = _cumVolume = candle.Volume;
			_cDelta[bar] = candle.Delta;
			_deltaPerVol[bar] = candle.Volume is 0
				? 0
				: Math.Abs(candle.Delta * 100m / candle.Volume);
			_cDeltaPerVol[bar] = candle.Volume is 0
				? 0
				: candle.Delta * 100m / candle.Volume;
			_deltaChange[bar] = 0; // No previous candle
			_candleHeights[bar] = candle.High - candle.Low;
			_candleDurations[bar] = (int)(candle.LastTime - candle.Time).TotalSeconds;

			_maxVolume = candle.Volume;
			_maxDelta = Math.Abs(candle.Delta);
			_maxMaxDelta = Math.Abs(candle.MaxDelta);
			_maxMinDelta = Math.Abs(candle.MinDelta);
			_maxDeltaChange = 0;
			_minDelta = candle.MinDelta;
			_maxHeight = _candleHeights[bar];
			_maxTicks = candle.Ticks;
			_maxDuration = _candleDurations[bar];
			_maxSessionDelta = Math.Abs(candle.Delta);
			_maxDeltaPerVolume = _deltaPerVol[bar];
			_maxSessionDeltaPerVolume = Math.Abs(_cDeltaPerVol[bar]);
			_maxAsk = candle.Ask;
			_maxBid = candle.Bid;
			return;
		}

		_deltaPerVol[bar] = candle.Volume is 0 
			? 0
			: Math.Abs(candle.Delta * 100m / candle.Volume);

		var prevCandle = GetCandle(bar - 1);

		if (CheckStartBar(bar))
		{
			_cVolume[bar] = _cumVolume = candle.Volume;
			_cDelta[bar] = candle.Delta;
			_deltaChange[bar] = 0; // No previous candle in this session
			_maxSessionDelta = 0;
			_maxSessionDeltaPerVolume = 0;

			// Reset last session values to avoid false alerts when new session starts
			_lastSessionDeltaValue = 0m;
			_lastSessionDeltaPerVolumeValue = 0m;
			_lastSessionVolumeValue = 0m;
			_lastDeltaChangeValue = 0m;
		}
		else
		{
			_cumVolume = _cVolume[bar] = _cVolume[bar - 1] + candle.Volume;
			_cDelta[bar] = _cDelta[bar - 1] + candle.Delta;
			_deltaChange[bar] = candle.Delta - prevCandle.Delta;
		}

		_maxSessionDelta = Math.Max(Math.Abs(_cDelta[bar]), _maxSessionDelta);

		_maxAsk = Math.Max(candle.Ask, _maxAsk);
		_maxBid = Math.Max(candle.Bid, _maxBid);

		_maxDeltaChange = Math.Max(Math.Abs(_deltaChange[bar]), _maxDeltaChange);

		_maxDelta = Math.Max(Math.Abs(candle.Delta), _maxDelta);

		_maxMaxDelta = Math.Max(Math.Abs(candle.MaxDelta), _maxMaxDelta);
		_maxMinDelta = Math.Max(Math.Abs(candle.MinDelta), _maxMinDelta);

		_maxVolume = Math.Max(candle.Volume, _maxVolume);

		_minDelta = Math.Min(candle.MinDelta, _minDelta);

		if (candle.Volume is not 0)
			_maxDeltaPerVolume = Math.Max(Math.Abs(100 * candle.Delta / candle.Volume), _maxDeltaPerVolume);

		var candleHeight = candle.High - candle.Low;
		_maxHeight = Math.Max(candleHeight, _maxHeight);
		_candleHeights[bar] = candleHeight;

		_maxTicks = Math.Max(candle.Ticks, _maxTicks);

		_candleDurations[bar] = (int)(candle.LastTime - candle.Time).TotalSeconds;
		_maxDuration = Math.Max(_candleDurations[bar], _maxDuration);

		if (_cVolume[bar] is not 0)
			_cDeltaPerVol[bar] = _cDelta[bar] * 100.0m / _cVolume[bar];

		_maxSessionDeltaPerVolume = Math.Max(Math.Abs(_cDeltaPerVol[bar]), _maxSessionDeltaPerVolume);

		if (_lastBar != bar)
		{
			_lastAskValue = 0m;
			_lastBidValue = 0m;
			_lastDeltaValue = 0m;
			_lastDeltaPerVolumeValue = 0m;
			_lastMaxDeltaValue = 0m;
			_lastMinDeltaValue = 0m;
			_lastDeltaChangeValue = 0m;
			_lastVolumeValue = 0m;
			_lastVolumePerSecondValue = 0m;
			_lastTradesValue = 0m;
			_lastHeightValue = 0m;

			// Session values are cumulative and should not be reset to 0 on new bar,
			// otherwise alerts would falsely trigger due to "crossing" from 0
		}

		if (bar == CurrentBar - 1)
		{
			// Ask Alert (exceeding)
			if (UseAskAlert && _lastAskAlert != bar)
			{
				if (_lastAskValue < AskAlertValue && candle.Ask >= AskAlertValue)
				{
					AddAlert(AskAlertFile, $"Cluster statistic ask alert: {candle.Ask}");
					_lastAskAlert = bar;
				}
			}

			// Bid Alert (exceeding)
			if (UseBidAlert && _lastBidAlert != bar)
			{
				if (_lastBidValue < BidAlertValue && candle.Bid >= BidAlertValue)
				{
					AddAlert(BidAlertFile, $"Cluster statistic bid alert: {candle.Bid}");
					_lastBidAlert = bar;
				}
			}

			// Delta Alert (crossing)
			if (UseDeltaAlert && _lastDeltaAlert != bar)
			{
				if ((_lastDeltaValue < DeltaAlertValue && candle.Delta >= DeltaAlertValue)
				    || (_lastDeltaValue > DeltaAlertValue && candle.Delta <= DeltaAlertValue))
				{
					AddAlert(DeltaAlertFile, $"Cluster statistic delta alert: {candle.Delta}");
					_lastDeltaAlert = bar;
				}
			}

			// Delta Per Volume Alert (crossing)
			if (UseDeltaPerVolumeAlert && _lastDeltaPerVolumeAlert != bar)
			{
				var deltaPerVol = _deltaPerVol[bar];
				if ((_lastDeltaPerVolumeValue < DeltaPerVolumeAlertValue && deltaPerVol >= DeltaPerVolumeAlertValue)
				    || (_lastDeltaPerVolumeValue > DeltaPerVolumeAlertValue && deltaPerVol <= DeltaPerVolumeAlertValue))
				{
					AddAlert(DeltaPerVolumeAlertFile, $"Cluster statistic delta/volume alert: {deltaPerVol:F2}%");
					_lastDeltaPerVolumeAlert = bar;
				}
			}

			// Session Delta Alert (crossing)
			if (UseSessionDeltaAlert && _lastSessionDeltaAlert != bar)
			{
				var sessionDelta = _cDelta[bar];
				if ((_lastSessionDeltaValue < SessionDeltaAlertValue && sessionDelta >= SessionDeltaAlertValue)
				    || (_lastSessionDeltaValue > SessionDeltaAlertValue && sessionDelta <= SessionDeltaAlertValue))
				{
					AddAlert(SessionDeltaAlertFile, $"Cluster statistic session delta alert: {sessionDelta}");
					_lastSessionDeltaAlert = bar;
				}
			}

			// Session Delta Per Volume Alert (crossing)
			if (UseSessionDeltaPerVolumeAlert && _lastSessionDeltaPerVolumeAlert != bar)
			{
				var sessionDeltaPerVol = _cDeltaPerVol[bar];
				if ((_lastSessionDeltaPerVolumeValue < SessionDeltaPerVolumeAlertValue && sessionDeltaPerVol >= SessionDeltaPerVolumeAlertValue)
				    || (_lastSessionDeltaPerVolumeValue > SessionDeltaPerVolumeAlertValue && sessionDeltaPerVol <= SessionDeltaPerVolumeAlertValue))
				{
					AddAlert(SessionDeltaPerVolumeAlertFile, $"Cluster statistic session delta/volume alert: {sessionDeltaPerVol:F2}%");
					_lastSessionDeltaPerVolumeAlert = bar;
				}
			}

			// Max Delta Alert (crossing)
			if (UseMaxDeltaAlert && _lastMaxDeltaAlert != bar)
			{
				if ((_lastMaxDeltaValue < MaxDeltaAlertValue && candle.MaxDelta >= MaxDeltaAlertValue)
				    || (_lastMaxDeltaValue > MaxDeltaAlertValue && candle.MaxDelta <= MaxDeltaAlertValue))
				{
					AddAlert(MaxDeltaAlertFile, $"Cluster statistic max delta alert: {candle.MaxDelta}");
					_lastMaxDeltaAlert = bar;
				}
			}

			// Min Delta Alert (crossing)
			if (UseMinDeltaAlert && _lastMinDeltaAlert != bar)
			{
				if ((_lastMinDeltaValue < MinDeltaAlertValue && candle.MinDelta >= MinDeltaAlertValue)
				    || (_lastMinDeltaValue > MinDeltaAlertValue && candle.MinDelta <= MinDeltaAlertValue))
				{
					AddAlert(MinDeltaAlertFile, $"Cluster statistic min delta alert: {candle.MinDelta}");
					_lastMinDeltaAlert = bar;
				}
			}

			// Delta Change Alert (crossing)
			if (UseDeltaChangeAlert && _lastDeltaChangeAlert != bar)
			{

                var deltaChange = _deltaChange[bar];

                if ((_lastDeltaChangeValue < DeltaChangeAlertValue && deltaChange >= DeltaChangeAlertValue)
				    || (_lastDeltaChangeValue > DeltaChangeAlertValue && deltaChange <= DeltaChangeAlertValue))
				{
					AddAlert(DeltaChangeAlertFile, $"Cluster statistic delta change alert: {deltaChange}");
					_lastDeltaChangeAlert = bar;
				}
			}

			// Volume Alert (exceeding)
			if (UseVolumeAlert && _lastVolumeAlert != bar)
			{
				if (_lastVolumeValue < VolumeAlertValue && candle.Volume >= VolumeAlertValue)
				{
					AddAlert(VolumeAlertFile, $"Cluster statistic volume alert: {candle.Volume}");
					_lastVolumeAlert = bar;
				}
			}

			// Volume Per Second Alert (exceeding)
			if (UseVolumePerSecondAlert && _lastVolumePerSecondAlert != bar)
			{
				var volPerSec = _volPerSecond[bar];
				if (_lastVolumePerSecondValue < VolumePerSecondAlertValue && volPerSec >= VolumePerSecondAlertValue)
				{
					AddAlert(VolumePerSecondAlertFile, $"Cluster statistic volume/sec alert: {volPerSec:F2}");
					_lastVolumePerSecondAlert = bar;
				}
			}

			// Session Volume Alert (exceeding)
			if (UseSessionVolumeAlert && _lastSessionVolumeAlert != bar)
			{
				var sessionVol = _cVolume[bar];
				if (_lastSessionVolumeValue < SessionVolumeAlertValue && sessionVol >= SessionVolumeAlertValue)
				{
					AddAlert(SessionVolumeAlertFile, $"Cluster statistic session volume alert: {sessionVol}");
					_lastSessionVolumeAlert = bar;
				}
			}

			// Trades Alert (exceeding)
			if (UseTradesAlert && _lastTradesAlert != bar)
			{
				if (_lastTradesValue < TradesAlertValue && candle.Ticks >= TradesAlertValue)
				{
					AddAlert(TradesAlertFile, $"Cluster statistic trades alert: {candle.Ticks}");
					_lastTradesAlert = bar;
				}
			}

			// Height Alert (exceeding)
			if (UseHeightAlert && _lastHeightAlert != bar)
			{
				var height = _candleHeights[bar];
				if (_lastHeightValue < HeightAlertValue && height >= HeightAlertValue)
				{
					AddAlert(HeightAlertFile, $"Cluster statistic height alert: {height}");
					_lastHeightAlert = bar;
				}
			}
		}

		// Update last values for next comparison
		_lastAskValue = candle.Ask;
		_lastBidValue = candle.Bid;
		_lastDeltaValue = candle.Delta;
		_lastDeltaPerVolumeValue = _deltaPerVol[bar];
		_lastSessionDeltaValue = _cDelta[bar];
		_lastSessionDeltaPerVolumeValue = _cDeltaPerVol[bar];
		_lastMaxDeltaValue = candle.MaxDelta;
		_lastMinDeltaValue = candle.MinDelta;
		_lastDeltaChangeValue = _deltaChange[bar];
        _lastVolumeValue = candle.Volume;
		_lastVolumePerSecondValue = _volPerSecond[bar];
		_lastSessionVolumeValue = _cVolume[bar];
		_lastTradesValue = candle.Ticks;
		_lastHeightValue = _candleHeights[bar];
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

		var bounds = context.ClipBounds;

		_height = Container.Region.Height / StrCount;

		if (_layoutChanged)
		{
			var maxWidth = 0;

			foreach (var type in RowsOrder.AvailableStrings.Values)
			{
				var size = context.MeasureString(GetHeader(type), Font.RenderObject);

				if (size.Width > maxWidth)
				{
					maxWidth = size.Width;
					_fontHeight = size.Height;
				}
			}

			_headerWidth = maxWidth + 10;
			_layoutChanged = false;
		}

		var fullBarsWidth = (int)(ChartInfo.PriceChartContainer.BarsWidth + ChartInfo.PriceChartContainer.BarSpacing);
		var showHeadersText = _fontHeight * 0.9 <= _height;
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
			DataType.Ask or DataType.Bid or DataType.Delta or DataType.DeltaVolume =>
				Blend(candle.Delta > 0 ? AskColor : BidColor, BackGroundColor, rate),

			DataType.Volume or DataType.VolumeSecond or DataType.SessionVolume or
				DataType.Trades or DataType.Height or DataType.Time or DataType.Duration => Blend(VolumeColor, BackGroundColor, rate),
			DataType.MaxDelta => Blend(candle.MaxDelta > 0 ?  AskColor : BidColor, BackGroundColor, rate),
			DataType.MinDelta => Blend(candle.MinDelta > 0 ?  AskColor : BidColor, BackGroundColor, rate),
            DataType.SessionDeltaVolume => Blend(_cDeltaPerVol[bar] > 0 ? AskColor : BidColor, BackGroundColor, rate),
			DataType.SessionDelta => Blend(_cDelta[bar] > 0 ? AskColor : BidColor, BackGroundColor, rate),
			DataType.DeltaChange => GetDeltaChangeBrush(bar, rate),
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
			DataType.DeltaChange => GetRate(Math.Abs(_deltaChange[bar]), maxValues.MaxDeltaChange),
			DataType.Volume => GetRate(candle.Volume, maxValues.MaxVolume),
			DataType.VolumeSecond => GetRate(_volPerSecond[bar], maxValues.MaxVolumeSec),
			DataType.SessionVolume => GetRate(_cVolume[bar], maxValues.CumVolume),
			DataType.Trades => GetRate(candle.Ticks, maxValues.MaxTicks),
			DataType.Height => GetRate(_candleHeights[bar], maxValues.MaxHeight),
			DataType.Time => GetRate(_cVolume[bar], maxValues.CumVolume),
			DataType.Duration => GetRate(_candleDurations[bar], maxValues.MaxDuration),
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

				maxDeltaChange = Math.Max(Math.Abs(_deltaChange[i]), maxDeltaChange);
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
			MaxVolumeSec = maxVolumeSec
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
			DataType.DeltaChange => ChartInfo.TryGetMinimizedVolumeString(_deltaChange[bar]),
			DataType.Volume => ChartInfo.TryGetMinimizedVolumeString(candle.Volume),
			DataType.VolumeSecond => ChartInfo.TryGetMinimizedVolumeString(_volPerSecond[bar]),
			DataType.SessionVolume => ChartInfo.TryGetMinimizedVolumeString(_cVolume[bar]),
			DataType.Trades => candle.Ticks.ToString(CultureInfo.InvariantCulture),
			DataType.Height => _candleHeights[bar].ToString(CultureInfo.InvariantCulture),
			DataType.Time => candle.Time.AddHours(InstrumentInfo.TimeZone).ToString("HH:mm:ss"),
			DataType.Duration => ((int)(candle.LastTime - candle.Time).TotalSeconds).ToString(),
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

	private void OnFontPropertyChanged(string propertyName)
	{
		_layoutChanged = true;
	}

	private System.Drawing.Color GetDeltaChangeBrush(int bar, decimal rate)
	{
		var change = _deltaChange[bar];
		var rectColor = change > 0 ? AskColor : BidColor;
		return Blend(rectColor, BackGroundColor, rate);
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

	private bool CheckStartBar(int bar)
	{
		switch (_sessionMode)
		{
			case SessionMode.None:
				return bar == 0;
			case SessionMode.DefaultSession:
				return IsNewSession(bar);
			case SessionMode.CustomSession:
				if (bar == 0)
					return true;

				var candle = GetCandle(bar);
				var prevCandle = GetCandle(bar - 1);

				return prevCandle.Time.AddHours(InstrumentInfo.TimeZone).TimeOfDay < CustomSessionStart.Value
					&& candle.Time.AddHours(InstrumentInfo.TimeZone).TimeOfDay >= CustomSessionStart.Value;
			default:
				return false;
		}
	}

	#endregion
}