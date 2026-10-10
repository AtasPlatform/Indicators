namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

using ATAS.Indicators.Drawing;

using Newtonsoft.Json;

using OFT.Attributes;
using OFT.Attributes.Editors;
using OFT.Localization;
using Utils.Common;

[Category(IndicatorCategories.VolumeOrderFlow)]
[DisplayName("CVD pro(multi) / Multi Market Powers")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.MultiMarketPowerDescription))]
[HelpLink("https://help.atas.net/support/solutions/articles/72000602434")]
public class MultiMarketPower : Indicator
{
	#region Fields

	private readonly ValueDataSeries _filter1Series = new("Filter1Series", "Filter1")
	{
		Color = CrossColor.FromArgb(255, 135, 206, 235),
		IsHidden = true,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true
	};

	private readonly ValueDataSeries _filter2Series = new("Filter2Series", "Filter2")
	{
		Color = DefaultColors.Red.Convert(),
		IsHidden = true,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true
	};

	private readonly ValueDataSeries _filter3Series = new("Filter3Series", "Filter3")
	{
		Color = DefaultColors.Green.Convert(),
		IsHidden = true,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true
	};

	private readonly ValueDataSeries _filter4Series = new("Filter4Series", "Filter4")
	{
		Color = CrossColor.FromArgb(255, 128, 128, 128),
		Width = 2,
		IsHidden = true,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true
	};

	private readonly ValueDataSeries _filter5Series = new("Filter5Series", "Filter5")
	{
		Color = CrossColor.FromArgb(255, 205, 92, 92),
		Width = 2,
		IsHidden = true,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true
	};

	private bool _bigTradesIsReceived;
	private bool _cumulativeTrades = true;
	private decimal _delta1;
	private decimal _delta2;
	private decimal _delta3;
	private decimal _delta4;
	private decimal _delta5;
	private int _lastBar = -1;
	private decimal _lastDelta1;
	private decimal _lastDelta2;
	private decimal _lastDelta3;
	private decimal _lastDelta4;
	private decimal _lastDelta5;
	private CumulativeTrade _lastTrade;
	private object _locker = new();
	private VolumeFilter _maxVolume1;
	private VolumeFilter _maxVolume2;
	private VolumeFilter _maxVolume3;
	private VolumeFilter _maxVolume4;
	private VolumeFilter _maxVolume5;
	private VolumeFilter _minVolume1;
	private VolumeFilter _minVolume2;
	private VolumeFilter _minVolume3;
	private VolumeFilter _minVolume4;
	private VolumeFilter _minVolume5;

	private int _requestId;
	private int _sessionBegin;

	private List<MarketDataArg> _ticks = new();
	private List<CumulativeTrade> _trades = new();

	private bool _useFilter1 = true;
	private bool _useFilter2 = true;
	private bool _useFilter3 = true;
	private bool _useFilter4 = true;
	private bool _useFilter5 = true;

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.CumulativeTrades), GroupName = nameof(Strings.Filters), Description = nameof(Strings.CumulativeTradesModeDescription), Order = 90)]
	[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
	public bool CumulativeTrades
	{
		get => _cumulativeTrades;
		set
		{
			_cumulativeTrades = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.Filter1), Description = nameof(Strings.UseFilterDescription), Order = 100)]
	public bool UseFilter1
	{
		get => _useFilter1;
		set
		{
			_useFilter1 = value;
			_filter1Series.VisualType = value ? VisualMode.Line : VisualMode.Hide;
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimumVolume), GroupName = nameof(Strings.Filter1), Description = nameof(Strings.MinVolumeFilterCommonDescription), Order = 130)]
	[JsonIgnore]
	public VolumeFilter MinimumVolume1
	{
		get => _minVolume1;
		set => SetTrackedProperty(ref _minVolume1, value, OnFilterChanged);
	}

	[Browsable(false)]
	public decimal MinVolume1
	{
		get => _minVolume1.Value;
		set => _minVolume1.Value = value;
	}

	[Browsable(false)]
	public string? MinVolume1Money
	{
		get => _minVolume1.MoneyScalar;
		set => _minVolume1.MoneyScalar = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaximumVolume), GroupName = nameof(Strings.Filter1), Description = nameof(Strings.MaxVolumeFilterCommonDescription), Order = 140)]
	[JsonIgnore]
	public VolumeFilter MaximumVolume1
	{
		get => _maxVolume1;
		set => SetTrackedProperty(ref _maxVolume1, value, OnFilterChanged);
	}

	[Browsable(false)]
	public decimal MaxVolume1
	{
		get => _maxVolume1.Value;
		set => _maxVolume1.Value = value;
	}

	[Browsable(false)]
	public string? MaxVolume1Money
	{
		get => _maxVolume1.MoneyScalar;
		set => _maxVolume1.MoneyScalar = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.Filter1), Description = nameof(Strings.LineColorDescription), Order = 150)]
	public CrossColor Color1
	{
		get => _filter1Series.Color;
		set => _filter1Series.Color = value;
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineWidth), GroupName = nameof(Strings.Filter1), Description = nameof(Strings.LineWidthDescription), Order = 160)]
    [Range(1, 100)]
    public int LineWidth1
    {
        get => _filter1Series.Width;
        set => _filter1Series.Width = value;
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.Filter2), Description = nameof(Strings.UseFilterDescription), Order = 200)]
	public bool UseFilter2
	{
		get => _useFilter2;
		set
		{
			_useFilter2 = value;
			_filter2Series.VisualType = value ? VisualMode.Line : VisualMode.Hide;
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimumVolume), GroupName = nameof(Strings.Filter2), Description = nameof(Strings.MinVolumeFilterCommonDescription), Order = 230)]
	[JsonIgnore]
	public VolumeFilter MinimumVolume2
	{
		get => _minVolume2;
		set => SetTrackedProperty(ref _minVolume2, value, OnFilterChanged);
	}

	[Browsable(false)]
	public decimal MinVolume2
	{
		get => _minVolume2.Value;
		set => _minVolume2.Value = value;
	}

	[Browsable(false)]
	public string? MinVolume2Money
	{
		get => _minVolume2.MoneyScalar;
		set => _minVolume2.MoneyScalar = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaximumVolume), GroupName = nameof(Strings.Filter2), Description = nameof(Strings.MaxVolumeFilterCommonDescription), Order = 240)]
	[JsonIgnore]
	public VolumeFilter MaximumVolume2
	{
		get => _maxVolume2;
		set => SetTrackedProperty(ref _maxVolume2, value, OnFilterChanged);
	}

	[Browsable(false)]
	public decimal MaxVolume2
	{
		get => _maxVolume2.Value;
		set => _maxVolume2.Value = value;
	}

	[Browsable(false)]
	public string? MaxVolume2Money
	{
		get => _maxVolume2.MoneyScalar;
		set => _maxVolume2.MoneyScalar = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.Filter2), Description = nameof(Strings.LineColorDescription), Order = 250)]
	public CrossColor Color2
	{
		get => _filter2Series.Color;
		set => _filter2Series.Color = value;
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineWidth), GroupName = nameof(Strings.Filter2), Description = nameof(Strings.LineWidthDescription), Order = 260)]
    [Range(1, 100)]
    public int LineWidth2
    {
        get => _filter2Series.Width;
        set => _filter2Series.Width = value;
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.Filter3), Description = nameof(Strings.UseFilterDescription), Order = 300)]
	public bool UseFilter3
	{
		get => _useFilter3;
		set
		{
			_useFilter3 = value;
			_filter3Series.VisualType = value ? VisualMode.Line : VisualMode.Hide;
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimumVolume), GroupName = nameof(Strings.Filter3), Description = nameof(Strings.MinVolumeFilterCommonDescription), Order = 330)]
	[JsonIgnore]
	public VolumeFilter MinimumVolume3
	{
		get => _minVolume3;
		set => SetTrackedProperty(ref _minVolume3, value, OnFilterChanged);
	}

	[Browsable(false)]
	public decimal MinVolume3
	{
		get => _minVolume3.Value;
		set => _minVolume3.Value = value;
	}

	[Browsable(false)]
	public string? MinVolume3Money
	{
		get => _minVolume3.MoneyScalar;
		set => _minVolume3.MoneyScalar = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaximumVolume), GroupName = nameof(Strings.Filter3), Description = nameof(Strings.MaxVolumeFilterCommonDescription), Order = 340)]
	[JsonIgnore]
	public VolumeFilter MaximumVolume3
	{
		get => _maxVolume3;
		set => SetTrackedProperty(ref _maxVolume3, value, OnFilterChanged);
	}

	[Browsable(false)]
	public decimal MaxVolume3
	{
		get => _maxVolume3.Value;
		set => _maxVolume3.Value = value;
	}

	[Browsable(false)]
	public string? MaxVolume3Money
	{
		get => _maxVolume3.MoneyScalar;
		set => _maxVolume3.MoneyScalar = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.Filter3), Description = nameof(Strings.LineColorDescription), Order = 350)]
	public CrossColor Color3
	{
		get => _filter3Series.Color;
		set => _filter3Series.Color = value;
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineWidth), GroupName = nameof(Strings.Filter3), Description = nameof(Strings.LineWidthDescription), Order = 360)]
    [Range(1, 100)]
    public int LineWidth3
    {
        get => _filter3Series.Width;
        set => _filter3Series.Width = value;
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.Filter4), Description = nameof(Strings.UseFilterDescription), Order = 400)]
	public bool UseFilter4
	{
		get => _useFilter4;
		set
		{
			_useFilter4 = value;
			_filter4Series.VisualType = value ? VisualMode.Line : VisualMode.Hide;
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimumVolume), GroupName = nameof(Strings.Filter4), Description = nameof(Strings.MinVolumeFilterCommonDescription), Order = 430)]
	[JsonIgnore]
	public VolumeFilter MinimumVolume4
	{
		get => _minVolume4;
		set => SetTrackedProperty(ref _minVolume4, value, OnFilterChanged);
	}

	[Browsable(false)]
	public decimal MinVolume4
	{
		get => _minVolume4.Value;
		set => _minVolume4.Value = value;
	}

	[Browsable(false)]
	public string? MinVolume4Money
	{
		get => _minVolume4.MoneyScalar;
		set => _minVolume4.MoneyScalar = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaximumVolume), GroupName = nameof(Strings.Filter4), Description = nameof(Strings.MaxVolumeFilterCommonDescription), Order = 440)]
	[JsonIgnore]
	public VolumeFilter MaximumVolume4
	{
		get => _maxVolume4;
		set => SetTrackedProperty(ref _maxVolume4, value, OnFilterChanged);
	}

	[Browsable(false)]
	public decimal MaxVolume4
	{
		get => _maxVolume4.Value;
		set => _maxVolume4.Value = value;
	}

	[Browsable(false)]
	public string? MaxVolume4Money
	{
		get => _maxVolume4.MoneyScalar;
		set => _maxVolume4.MoneyScalar = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.Filter4), Description = nameof(Strings.LineColorDescription), Order = 450)]
	public CrossColor Color4
	{
		get => _filter4Series.Color;
		set => _filter4Series.Color = value;
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineWidth), GroupName = nameof(Strings.Filter4), Description = nameof(Strings.LineWidthDescription), Order = 460)]
    [Range(1, 100)]
    public int LineWidth4
    {
        get => _filter4Series.Width;
        set => _filter4Series.Width = value;
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.Filter5), Description = nameof(Strings.UseFilterDescription), Order = 500)]
	public bool UseFilter5
	{
		get => _useFilter5;
		set
		{
			_useFilter5 = value;
			_filter5Series.VisualType = value ? VisualMode.Line : VisualMode.Hide;
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimumVolume), GroupName = nameof(Strings.Filter5), Description = nameof(Strings.MinVolumeFilterCommonDescription), Order = 530)]
	[JsonIgnore]
	public VolumeFilter MinimumVolume5
	{
		get => _minVolume5;
		set => SetTrackedProperty(ref _minVolume5, value, OnFilterChanged);
	}

	[Browsable(false)]
	public decimal MinVolume5
	{
		get => _minVolume5.Value;
		set => _minVolume5.Value = value;
	}

	[Browsable(false)]
	public string? MinVolume5Money
	{
		get => _minVolume5.MoneyScalar;
		set => _minVolume5.MoneyScalar = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaximumVolume), GroupName = nameof(Strings.Filter5), Description = nameof(Strings.MaxVolumeFilterCommonDescription), Order = 540)]
	[JsonIgnore]
	public VolumeFilter MaximumVolume5
	{
		get => _maxVolume5;
		set => SetTrackedProperty(ref _maxVolume5, value, OnFilterChanged);
	}

	[Browsable(false)]
	public decimal MaxVolume5
	{
		get => _maxVolume5.Value;
		set => _maxVolume5.Value = value;
	}

	[Browsable(false)]
	public string? MaxVolume5Money
	{
		get => _maxVolume5.MoneyScalar;
		set => _maxVolume5.MoneyScalar = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.Filter5), Description = nameof(Strings.LineColorDescription), Order = 550)]
	public CrossColor Color5
	{
		get => _filter5Series.Color;
		set => _filter5Series.Color = value;
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineWidth), GroupName = nameof(Strings.Filter5), Description = nameof(Strings.LineWidthDescription), Order = 560)]
    [Range(1, 100)]
    public int LineWidth5
    {
        get => _filter5Series.Width;
        set => _filter5Series.Width = value;
    }

    #endregion

    #region ctor

    public MultiMarketPower()
		: base(true)
	{
		// PLAT-5080: the trade volume filters may be set in money; each is persisted as the old number plus a money scalar
		MinimumVolume1 = new VolumeFilter(false) { Value = 0 };
		MaximumVolume1 = new VolumeFilter(false) { Value = 5 };
		MinimumVolume2 = new VolumeFilter(false) { Value = 6 };
		MaximumVolume2 = new VolumeFilter(false) { Value = 10 };
		MinimumVolume3 = new VolumeFilter(false) { Value = 11 };
		MaximumVolume3 = new VolumeFilter(false) { Value = 20 };
		MinimumVolume4 = new VolumeFilter(false) { Value = 21 };
		MaximumVolume4 = new VolumeFilter(false) { Value = 40 };
		MinimumVolume5 = new VolumeFilter(false) { Value = 41 };
		MaximumVolume5 = new VolumeFilter(false) { Value = 0 };

		Panel = IndicatorDataProvider.NewPanel;
		DenyToChangePanel = true;

		DataSeries[0] = _filter1Series;
		DataSeries.Add(_filter2Series);
		DataSeries.Add(_filter3Series);
		DataSeries.Add(_filter4Series);
		DataSeries.Add(_filter5Series);
	}

	#endregion

	#region Protected methods
	
	// money filters select other trades when the rates or the display currency change
	protected override void OnValuationChanged()
	{
		if (HasMoneyFilters())
			DoActionInGuiThread(RecalculateValues);
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		if (!_bigTradesIsReceived || bar != CurrentBar - 1 || bar == 0)
			return;

		DataSeries.ForEach(ds =>
		{
			if (ds is ValueDataSeries vds && vds[bar] is 0)
				vds[bar] = vds[bar - 1];
		});
	}
	
	protected override void OnFinishRecalculate()
	{
		_bigTradesIsReceived = false;

        _ticks.Clear();
		_trades.Clear();
		var totalBars = CurrentBar - 1;
		_sessionBegin = totalBars;
		_lastBar = totalBars;

		for (var i = totalBars; i >= 0; i--)
		{
			if (!IsNewSession(i))
				continue;

			_sessionBegin = i;
			break;
		}

		var request = new CumulativeTradesRequest(GetCandle(_sessionBegin).Time);
		_requestId = request.RequestId;
		RequestForCumulativeTrades(request);
    }
	
	protected override void OnCumulativeTradesResponse(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> cumulativeTrades)
	{
		if (request.RequestId != _requestId)
			return;

		ClearValues();
		CalculateHistory(cumulativeTrades);

		_bigTradesIsReceived = true;
	}

	protected override void OnNewTrade(MarketDataArg trade)
	{
		if (CumulativeTrades || ChartInfo is null)
			return;

		if (!_bigTradesIsReceived)
		{
			_ticks.Add(trade);
			return;
		}

		var newBar = _lastBar < CurrentBar - 1;

		if (newBar)
			_lastBar = CurrentBar - 1;

		CalculateTick(trade);
	}

	protected override void OnCumulativeTrade(CumulativeTrade trade)
	{
		if (!CumulativeTrades)
			return;

		if (!_bigTradesIsReceived)
		{
			_trades.Add(trade);
			return;
		}

		var newBar = _lastBar < CurrentBar - 1;

		if (newBar)
			_lastBar = CurrentBar - 1;

		CalculateTrade(trade, false, newBar);
	}

	protected override void OnUpdateCumulativeTrade(CumulativeTrade trade)
	{
		if (!CumulativeTrades)
			return;

		if (!_bigTradesIsReceived)
		{
			if (_trades.Count != 0)
				_trades[^1] = trade;
			return;
		}

		var newBar = _lastBar < CurrentBar - 1;

		if (newBar)
			_lastBar = CurrentBar - 1;

		CalculateTrade(trade, true, newBar);
	}

	#endregion

	#region Private methods

	private void ClearValues()
	{
		_bigTradesIsReceived = false;
		DataSeries.ForEach(x => x.Clear());
		_delta1 = _delta2 = _delta3 = _delta4 = _delta5 = 0;
	}

	private void CalculateTrade(CumulativeTrade trade, bool isUpdate, bool newBar)
	{
		if (isUpdate && _lastTrade != null)
		{
			if (_lastTrade.IsEqual(trade))
			{
				var prevBarReset = _lastTrade.Time < GetCandle(CurrentBar - 1).Time && newBar;

				var lastVolume = _lastTrade.Volume * (_lastTrade.Direction == TradeDirection.Buy ? 1 : -1);

				if (IsFiltered(1, _lastTrade.Volume, _lastTrade.FirstPrice))
				{
					_delta1 -= lastVolume;

					if (prevBarReset)
						_filter1Series[CurrentBar - 2] -= lastVolume;
				}

				if (IsFiltered(2, _lastTrade.Volume, _lastTrade.FirstPrice))
				{
					if (prevBarReset)
						_filter2Series[CurrentBar - 2] -= lastVolume;

					_delta2 -= lastVolume;
				}

				if (IsFiltered(3, _lastTrade.Volume, _lastTrade.FirstPrice))
				{
					if (prevBarReset)
						_filter3Series[CurrentBar - 2] -= lastVolume;

					_delta3 -= lastVolume;
				}

				if (IsFiltered(4, _lastTrade.Volume, _lastTrade.FirstPrice))
				{
					if (prevBarReset)
						_filter4Series[CurrentBar - 2] -= lastVolume;

					_delta4 -= lastVolume;
				}

				if (IsFiltered(5, _lastTrade.Volume, _lastTrade.FirstPrice))
				{
					if (prevBarReset)
						_filter5Series[CurrentBar - 2] -= lastVolume;

					_delta5 -= lastVolume;
				}
			}
		}

		var volume = trade.Volume;
		var deltaVolume = volume * (trade.Direction == TradeDirection.Buy ? 1 : -1);

		if (IsFiltered(1, volume, trade.FirstPrice))
			_delta1 += deltaVolume;

		if (IsFiltered(2, volume, trade.FirstPrice))
			_delta2 += deltaVolume;

		if (IsFiltered(3, volume, trade.FirstPrice))
			_delta3 += deltaVolume;

		if (IsFiltered(4, volume, trade.FirstPrice))
			_delta4 += deltaVolume;

		if (IsFiltered(5, volume, trade.FirstPrice))
			_delta5 += deltaVolume;

		_filter1Series[CurrentBar - 1] = _delta1;
		_filter2Series[CurrentBar - 1] = _delta2;
		_filter3Series[CurrentBar - 1] = _delta3;
		_filter4Series[CurrentBar - 1] = _delta4;
		_filter5Series[CurrentBar - 1] = _delta5;

		RaiseBarValueChanged(CurrentBar - 1);
		_lastTrade = trade.MemberwiseClone();
	}

	private void CalculateHistory(IEnumerable<CumulativeTrade> trades)
	{
		List<CumulativeTrade> orderedTrades = null;
		List<MarketDataArg> orderedTicks = null;

		try
		{
			var searchIdx = 0;

			if (CumulativeTrades)
			{
				orderedTrades = trades.OrderBy(t => t.Time).ToList();

				if (orderedTrades.Count is 0)
					return;

				for (var i = _sessionBegin; i <= CurrentBar - 1; i++)
					CalculateBarTrades(orderedTrades, i, ref searchIdx);

				foreach (var trade in _trades)
					CalculateTrade(trade, false, false);
			}
			else
			{
				orderedTicks = trades
					.SelectMany(x => x.Ticks)
					.OrderBy(t => t.Time)
					.ToList();

				if (orderedTicks.Count is 0)
					return;

				for (var i = _sessionBegin; i <= CurrentBar - 1; i++)
					CalculateBarTicks(orderedTicks, i, ref searchIdx);

				foreach (var tick in _ticks)
					CalculateTick(tick);
			}

			RedrawChart();
		}
		catch (NullReferenceException)
		{
			//on reset exception ignored
		}
		finally
		{
			orderedTrades?.Clear();
			orderedTicks?.Clear();
			_trades.Clear();
			_ticks.Clear();
		}
	}

	private void CalculateBarTicks(List<MarketDataArg> trades, int i, ref int searchIdx)
	{
		var candle = GetCandle(i);

		for (var bar = searchIdx; bar < trades.Count; bar++)
		{
			var tick = trades[bar];
			searchIdx = bar;

			if (tick.Direction is TradeDirection.Between)
				continue;

			if (tick.Time > candle.LastTime)
				break;

			if (tick.Time < candle.Time)
				continue;

			var deltaVolume = tick.Volume * (tick.Direction is TradeDirection.Buy ? 1 : -1);

			if (IsFiltered(1, tick.Volume, tick.Price))
				_delta1 += deltaVolume;

			if (IsFiltered(2, tick.Volume, tick.Price))
				_delta2 += deltaVolume;

			if (IsFiltered(3, tick.Volume, tick.Price))
				_delta3 += deltaVolume;

			if (IsFiltered(4, tick.Volume, tick.Price))
				_delta4 += deltaVolume;

			if (IsFiltered(5, tick.Volume, tick.Price))
				_delta5 += deltaVolume;
		}

		_filter1Series[i] = _delta1;
		_filter2Series[i] = _delta2;
		_filter3Series[i] = _delta3;
		_filter4Series[i] = _delta4;
		_filter5Series[i] = _delta5;

		RaiseBarValueChanged(i);
	}

	private void CalculateTick(MarketDataArg tick)
	{
		var deltaVolume = tick.Volume * (tick.Direction is TradeDirection.Buy ? 1 : -1);

		if (IsFiltered(1, tick.Volume, tick.Price))
			_delta1 += deltaVolume;

		if (IsFiltered(2, tick.Volume, tick.Price))
			_delta2 += deltaVolume;

		if (IsFiltered(3, tick.Volume, tick.Price))
			_delta3 += deltaVolume;

		if (IsFiltered(4, tick.Volume, tick.Price))
			_delta4 += deltaVolume;

		if (IsFiltered(5, tick.Volume, tick.Price))
			_delta5 += deltaVolume;

		_filter1Series[^1] = _delta1;
		_filter2Series[^1] = _delta2;
		_filter3Series[^1] = _delta3;
		_filter4Series[^1] = _delta4;
		_filter5Series[^1] = _delta5;
	}

	// a money threshold is compared at the trade's price; a zero maximum means no limit
	private bool IsFiltered(int filter, decimal volume, decimal price)
	{
		var (min, max) = filter switch
		{
			1 => (_minVolume1, _maxVolume1),
			2 => (_minVolume2, _maxVolume2),
			3 => (_minVolume3, _maxVolume3),
			4 => (_minVolume4, _maxVolume4),
			_ => (_minVolume5, _maxVolume5)
		};

		return min.Compare(volume, price) >= 0 && (!max.HasThreshold() || max.Compare(volume, price) <= 0);
	}

	private void OnFilterChanged(string property)
	{
		// binding the instrument valuation is not an edit
		if (property != nameof(VolumeFilter.Valuation))
			RecalculateValues();
	}

	private bool HasMoneyFilters()
	{
		return new[] { _minVolume1, _maxVolume1, _minVolume2, _maxVolume2, _minVolume3, _maxVolume3, _minVolume4, _maxVolume4, _minVolume5, _maxVolume5 }
			.Any(f => f.IsMoney);
	}

	private void CalculateBarTrades(List<CumulativeTrade> trades, int bar, ref int searchIdx, bool realTime = false, bool newBar = false)
	{
		if (CumulativeTrades && realTime && !newBar)
		{
			_delta1 -= _lastDelta1;
			_delta2 -= _lastDelta2;
			_delta3 -= _lastDelta3;
			_delta4 -= _lastDelta4;
			_delta5 -= _lastDelta5;
		}

		var candle = GetCandle(bar);

		_lastDelta1 = 0;
		_lastDelta2 = 0;
		_lastDelta3 = 0;
		_lastDelta4 = 0;
		_lastDelta5 = 0;

		for (var i = searchIdx; i < trades.Count; i++)
		{
			var trade = trades[i];

			if (trade.Direction is TradeDirection.Between)
				continue;

			if (trade.Time > candle.LastTime)
			{
				searchIdx = i;
				break;
			}

			if (trade.Time < candle.Time)
				continue;

			var deltaVolume = trade.Volume * (trade.Direction == TradeDirection.Buy ? 1 : -1);

			if (IsFiltered(1, trade.Volume, trade.FirstPrice))
				_lastDelta1 += deltaVolume;

			if (IsFiltered(2, trade.Volume, trade.FirstPrice))
				_lastDelta2 += deltaVolume;

			if (IsFiltered(3, trade.Volume, trade.FirstPrice))
				_lastDelta3 += deltaVolume;

			if (IsFiltered(4, trade.Volume, trade.FirstPrice))
				_lastDelta4 += deltaVolume;

			if (IsFiltered(5, trade.Volume, trade.FirstPrice))
				_lastDelta5 += deltaVolume;
		}

		_delta1 += _lastDelta1;
		_filter1Series[bar] = _delta1;

		_delta2 += _lastDelta2;
		_filter2Series[bar] = _delta2;

		_delta3 += _lastDelta3;
		_filter3Series[bar] = _delta3;

		_delta4 += _lastDelta4;
		_filter4Series[bar] = _delta4;

		_delta5 += _lastDelta5;
		_filter5Series[bar] = _delta5;

		RaiseBarValueChanged(bar);
		_lastBar = bar;
	}

	#endregion
}