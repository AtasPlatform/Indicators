namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

using ATAS.Indicators.Drawing;

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

    private readonly ValueDataSeries _spreadSeries = new("SpreadSeries", "Smart Money Spread")
    {
        VisualType = VisualMode.Histogram,
        Width = 3,
        UseMinimizedModeIfEnabled = true,
        ShowZeroValue = true // Importante para ver el eje 0
    };

    public enum ViewMode { Lines, SmartMoneySpread }
    private ViewMode _viewMode = ViewMode.Lines;
	public enum SessionMode
    {
        None,
        DefaultSession,
        CustomSession
    }

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
	private decimal _maxVolume1 = 5;
	private decimal _maxVolume2 = 10;
	private decimal _maxVolume3 = 20;
	private decimal _maxVolume4 = 40;
	private decimal _maxVolume5;
	private decimal _minVolume1;
	private decimal _minVolume2 = 6;
	private decimal _minVolume3 = 11;
	private decimal _minVolume4 = 21;
	private decimal _minVolume5 = 41;

	private int _requestId;
	private int _sessionBegin;

	private List<MarketDataArg> _ticks = new();
	private List<CumulativeTrade> _trades = new();

	private bool _useFilter1 = true;
	private bool _useFilter2 = true;
	private bool _useFilter3 = true;
	private bool _useFilter4 = true;
	private bool _useFilter5 = true;

    private SessionMode _sessionMode = SessionMode.DefaultSession;
    private TimeSpan _customSessionStart = new(9, 30, 0); // example
    private int _sessionsBack = 1;
    private DateTime _historyStartTime = DateTime.MinValue;
    private DateTime _historyEndTime = DateTime.MinValue;
    private bool _pendingRealtimeReplay;

    private CrossColor _spreadPositiveColor = CrossColor.FromArgb(255, 0, 255, 0); // Lime
    private CrossColor _spreadNegativeColor = CrossColor.FromArgb(255, 255, 0, 0); // Red

    #endregion

    #region Properties

    [Display(GroupName = "General", Name = "View Mode", Order = 5)]
    public ViewMode Mode
    {
        get => _viewMode;
        set
        {
            _viewMode = value;
            UpdateVisibility();
            RecalculateValues();
        }
    }

    [Display(GroupName = "General", Name = "Spread Positive Color", Order = 6)]
    public CrossColor SpreadPositiveColor
    {
        get => _spreadPositiveColor;
        set { _spreadPositiveColor = value; RedrawChart(); }
    }

    [Display(GroupName = "General", Name = "Spread Negative Color", Order = 7)]
    public CrossColor SpreadNegativeColor
    {
        get => _spreadNegativeColor;
        set { _spreadNegativeColor = value; RedrawChart(); }
    }

    [Display(GroupName = "Session", Name = "Session Mode", Order = 10)]
    public SessionMode SessionPowerMode
    {
        get => _sessionMode;
        set { _sessionMode = value; RecalculateValues(); }
    }

    [Display(GroupName = "Session", Name = "Custom Session Start", Order = 20)]
    public TimeSpan CustomSessionStart
    {
        get => _customSessionStart;
        set { _customSessionStart = value; if (_sessionMode == SessionMode.CustomSession) RecalculateValues(); }
    }

    [Range(1, 50)]
    [Display(GroupName = "Session", Name = "Sessions Back", Order = 30)]
    public int SessionsBack
    {
        get => _sessionsBack;
        set { _sessionsBack = value; RecalculateValues(); }
    }

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
            UpdateVisibility();
        }
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimumVolume), GroupName = nameof(Strings.Filter1), Description = nameof(Strings.MinVolumeFilterCommonDescription), Order = 130)]
	[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
	[Range(0, 100000000)]
	public decimal MinVolume1
	{
		get => _minVolume1;
		set
		{
			_minVolume1 = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaximumVolume), GroupName = nameof(Strings.Filter1), Description = nameof(Strings.MaxVolumeFilterCommonDescription), Order = 140)]
	[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
	[Range(0.0000001, 100000000)]
	public decimal MaxVolume1
	{
		get => _maxVolume1;
		set
		{
			_maxVolume1 = value;
			RecalculateValues();
		}
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
            UpdateVisibility();
        }
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimumVolume), GroupName = nameof(Strings.Filter2), Description = nameof(Strings.MinVolumeFilterCommonDescription), Order = 230)]
	[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
	[Range(0, 100000000)]
	public decimal MinVolume2
	{
		get => _minVolume2;
		set
		{
			_minVolume2 = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaximumVolume), GroupName = nameof(Strings.Filter2), Description = nameof(Strings.MaxVolumeFilterCommonDescription), Order = 240)]
	[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
	[Range(0, 100000000)]
	public decimal MaxVolume2
	{
		get => _maxVolume2;
		set
		{
			_maxVolume2 = value;
			RecalculateValues();
		}
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
            UpdateVisibility();
        }
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimumVolume), GroupName = nameof(Strings.Filter3), Description = nameof(Strings.MinVolumeFilterCommonDescription), Order = 330)]
	[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
	[Range(0, 100000000)]
	public decimal MinVolume3
	{
		get => _minVolume3;
		set
		{
			_minVolume3 = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaximumVolume), GroupName = nameof(Strings.Filter3), Description = nameof(Strings.MaxVolumeFilterCommonDescription), Order = 340)]
	[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
	[Range(0, 100000000)]
	public decimal MaxVolume3
	{
		get => _maxVolume3;
		set
		{
			_maxVolume3 = value;
			RecalculateValues();
		}
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
            UpdateVisibility();
        }
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimumVolume), GroupName = nameof(Strings.Filter4), Description = nameof(Strings.MinVolumeFilterCommonDescription), Order = 430)]
	[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
	[Range(0, 100000000)]
	public decimal MinVolume4
	{
		get => _minVolume4;
		set
		{
			_minVolume4 = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaximumVolume), GroupName = nameof(Strings.Filter4), Description = nameof(Strings.MaxVolumeFilterCommonDescription), Order = 440)]
	[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
	[Range(0, 100000000)]
	public decimal MaxVolume4
	{
		get => _maxVolume4;
		set
		{
			_maxVolume4 = value;
			RecalculateValues();
		}
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
			UpdateVisibility();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimumVolume), GroupName = nameof(Strings.Filter5), Description = nameof(Strings.MinVolumeFilterCommonDescription), Order = 530)]
	[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
	[Range(0, 100000000)]
	public decimal MinVolume5
	{
		get => _minVolume5;
		set
		{
			_minVolume5 = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaximumVolume), GroupName = nameof(Strings.Filter5), Description = nameof(Strings.MaxVolumeFilterCommonDescription), Order = 540)]
	[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
	[Range(0, 100000000)]
	public decimal MaxVolume5
	{
		get => _maxVolume5;
		set
		{
			_maxVolume5 = value;
			RecalculateValues();
		}
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
		Panel = IndicatorDataProvider.NewPanel;
		DenyToChangePanel = true;

		DataSeries[0] = _filter1Series;
		DataSeries.Add(_filter2Series);
		DataSeries.Add(_filter3Series);
		DataSeries.Add(_filter4Series);
		DataSeries.Add(_filter5Series);

        DataSeries.Add(_spreadSeries);

        UpdateVisibility();
    }

	#endregion

	#region Protected methods
	
	protected override void OnCalculate(int bar, decimal value)
	{
		if (!_bigTradesIsReceived || bar != CurrentBar - 1)
			return;

		DataSeries.ForEach(ds =>
		{
			if (ds is ValueDataSeries vds && vds[bar] is 0)
				vds[bar] = vds[bar - 1];
		});
	}
	
	protected override void OnFinishRecalculate()
	{
		UpdateVisibility();

        _bigTradesIsReceived = false;

        _ticks.Clear();
		_trades.Clear();

        _sessionBegin = FindSessionBeginBar();

        _historyStartTime = GetCandle(_sessionBegin).Time;

        // Use the last fully formed bar time window to avoid "moving end" issues.
        var lastBar = Math.Max(0, CurrentBar - 2);
        _historyEndTime = GetCandle(lastBar).LastTime;

        _pendingRealtimeReplay = true;

        var request = new CumulativeTradesRequest(_historyStartTime, _historyEndTime, 0, 0);
        _requestId = request.RequestId;
		RequestForCumulativeTrades(request);
    }
	
	protected override void OnCumulativeTradesResponse(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> cumulativeTrades)
	{
		if (request.RequestId != _requestId)
			return;

		ClearValues();

        _bigTradesIsReceived = true;

        var trades = cumulativeTrades?.ToList() ?? new List<CumulativeTrade>();
		CalculateHistory(trades);

        if (_pendingRealtimeReplay)
        {
            ReplayBufferedRealtimeAfterHistory();
            _pendingRealtimeReplay = false;
        }

        RedrawChart();
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
        {
            _lastBar = CurrentBar - 1;

            if (IsSessionStart(_lastBar))
                ResetSessionState(_lastBar);
        }

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
        {
            _lastBar = CurrentBar - 1;

            if (IsSessionStart(_lastBar))
                ResetSessionState(_lastBar);
        }

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
        {
            _lastBar = CurrentBar - 1;

            if (IsSessionStart(_lastBar))
                ResetSessionState(_lastBar);
        }

        CalculateTrade(trade, true, newBar);
	}

	#endregion

	#region Private methods

	private void ClearValues()
	{
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

				if (_lastTrade.Volume >= _minVolume1 && (_lastTrade.Volume <= _maxVolume1 || _maxVolume1 == 0))
				{
					_delta1 -= lastVolume;

					if (prevBarReset)
						_filter1Series[CurrentBar - 2] -= lastVolume;
				}

				if (_lastTrade.Volume >= _minVolume2 && (_lastTrade.Volume <= _maxVolume2 || _maxVolume2 == 0))
				{
					if (prevBarReset)
						_filter2Series[CurrentBar - 2] -= lastVolume;

					_delta2 -= lastVolume;
				}

				if (_lastTrade.Volume >= _minVolume3 && (_lastTrade.Volume <= _maxVolume3 || _maxVolume3 == 0))
				{
					if (prevBarReset)
						_filter3Series[CurrentBar - 2] -= lastVolume;

					_delta3 -= lastVolume;
				}

				if (_lastTrade.Volume >= _minVolume4 && (_lastTrade.Volume <= _maxVolume4 || _maxVolume4 == 0))
				{
					if (prevBarReset)
						_filter4Series[CurrentBar - 2] -= lastVolume;

					_delta4 -= lastVolume;
				}

				if (_lastTrade.Volume >= _minVolume5 && (_lastTrade.Volume <= _maxVolume5 || _maxVolume5 == 0))
				{
					if (prevBarReset)
						_filter5Series[CurrentBar - 2] -= lastVolume;

					_delta5 -= lastVolume;
				}
			}
		}

		var volume = trade.Volume;
		var deltaVolume = volume * (trade.Direction == TradeDirection.Buy ? 1 : -1);

		if (volume >= _minVolume1 && (volume <= _maxVolume1 || _maxVolume1 == 0))
			_delta1 += deltaVolume;

		if (volume >= _minVolume2 && (volume <= _maxVolume2 || _maxVolume2 == 0))
			_delta2 += deltaVolume;

		if (volume >= _minVolume3 && (volume <= _maxVolume3 || _maxVolume3 == 0))
			_delta3 += deltaVolume;

		if (volume >= _minVolume4 && (volume <= _maxVolume4 || _maxVolume4 == 0))
			_delta4 += deltaVolume;

		if (volume >= _minVolume5 && (volume <= _maxVolume5 || _maxVolume5 == 0))
			_delta5 += deltaVolume;

		_filter1Series[CurrentBar - 1] = _delta1;
		_filter2Series[CurrentBar - 1] = _delta2;
		_filter3Series[CurrentBar - 1] = _delta3;
		_filter4Series[CurrentBar - 1] = _delta4;
		_filter5Series[CurrentBar - 1] = _delta5;

        // Smart Money (4+5) - Dumb Money (1+2)
        var smartMoney = _delta4 + _delta5;
        var dumbMoney = _delta1 + _delta2;
        var spread = smartMoney - dumbMoney;

        _spreadSeries[CurrentBar - 1] = spread;
        _spreadSeries.Colors[CurrentBar - 1] = spread >= 0 ? SpreadPositiveColor.Convert() : SpreadNegativeColor.Convert();

        RaiseBarValueChanged(CurrentBar - 1);
		_lastTrade = trade.MemberwiseClone();
	}

	private void CalculateHistory(List<CumulativeTrade> trades)
	{
		try
		{
			if(trades.Count is 0)
				return;
			
			var searchIdx = 0;

            if (CumulativeTrades)
			{
				trades = trades.OrderBy(t => t.Time).ToList();
                
				for (var i = _sessionBegin; i <= CurrentBar - 1; i++)
                {
                    if (IsSessionStart(i))
                        ResetSessionState(i);

                    CalculateBarTrades(trades, i, ref searchIdx);
                }

			}
			else
			{
				var ticks = trades
					.SelectMany(x => x.Ticks)
					.OrderBy(t=>t.Time)
					.ToList();

				for (var i = _sessionBegin; i <= CurrentBar - 1; i++)
                {
                    if (IsSessionStart(i))
                        ResetSessionState(i);

                    CalculateBarTicks(ticks, i, ref searchIdx);
                }

			}

			RedrawChart();
		}
		catch (NullReferenceException)
		{
			//on reset exception ignored
		}
	}

    private void CalculateBarTicks(List<MarketDataArg> trades, int barIndex, ref int searchIdx)
    {
        var candle = GetCandle(barIndex);

        var candleTrades = new List<MarketDataArg>();

        for (var idx = searchIdx; idx < trades.Count; idx++)
        {
            var trade = trades[idx];

            if (trade.Direction is TradeDirection.Between)
                continue;

            // If trade is after this candle, stop here and keep cursor at the first non-consumed index.
            if (trade.Time > candle.LastTime)
            {
                searchIdx = idx;
                break;
            }

            // If trade is before this candle, advance cursor and keep scanning.
            if (trade.Time < candle.Time)
            {
                searchIdx = idx;
                continue;
            }

            // Trade belongs to this candle.
            candleTrades.Add(trade);
            searchIdx = idx;
        }

        foreach (var tick in candleTrades)
        {
            var deltaVolume = tick.Volume * (tick.Direction is TradeDirection.Buy ? 1 : -1);

            if (IsFiltered(MinVolume1, MaxVolume1, tick.Volume))
                _delta1 += deltaVolume;

            if (IsFiltered(MinVolume2, MaxVolume2, tick.Volume))
                _delta2 += deltaVolume;

            if (IsFiltered(MinVolume3, MaxVolume3, tick.Volume))
                _delta3 += deltaVolume;

            if (IsFiltered(MinVolume4, MaxVolume4, tick.Volume))
                _delta4 += deltaVolume;

            if (IsFiltered(MinVolume5, MaxVolume5, tick.Volume))
                _delta5 += deltaVolume;
        }

        _filter1Series[barIndex] = _delta1;
        _filter2Series[barIndex] = _delta2;
        _filter3Series[barIndex] = _delta3;
        _filter4Series[barIndex] = _delta4;
        _filter5Series[barIndex] = _delta5;

        var smartMoney = _delta4 + _delta5;
        var dumbMoney = _delta1 + _delta2;
        var spread = smartMoney - dumbMoney;

        _spreadSeries[barIndex] = spread;
        _spreadSeries.Colors[CurrentBar - 1] = spread >= 0 ? SpreadPositiveColor.Convert() : SpreadNegativeColor.Convert();

        RaiseBarValueChanged(barIndex);
    }

    private void CalculateTick(MarketDataArg tick)
	{
		var deltaVolume = tick.Volume * (tick.Direction is TradeDirection.Buy ? 1 : -1);

		if (IsFiltered(MinVolume1, MaxVolume1, tick.Volume))
			_delta1 += deltaVolume;

		if (IsFiltered(MinVolume2, MaxVolume2, tick.Volume))
			_delta2 += deltaVolume;

		if (IsFiltered(MinVolume3, MaxVolume3, tick.Volume))
			_delta3 += deltaVolume;

		if (IsFiltered(MinVolume4, MaxVolume4, tick.Volume))
			_delta4 += deltaVolume;

		if (IsFiltered(MinVolume5, MaxVolume5, tick.Volume))
			_delta5 += deltaVolume;

		_filter1Series[^1] = _delta1;
		_filter2Series[^1] = _delta2;
		_filter3Series[^1] = _delta3;
		_filter4Series[^1] = _delta4;
		_filter5Series[^1] = _delta5;

        // Smart Money (4+5) - Dumb Money (1+2)
        var smartMoney = _delta4 + _delta5;
        var dumbMoney = _delta1 + _delta2;
        var spread = smartMoney - dumbMoney;

        _spreadSeries[CurrentBar - 1] = spread;
        _spreadSeries.Colors[CurrentBar - 1] = spread >= 0 ? SpreadPositiveColor.Convert() : SpreadNegativeColor.Convert();
    }

	private bool IsFiltered(decimal minFilter, decimal maxFilter, decimal volume)
	{
		return volume >= minFilter && (volume <= maxFilter || maxFilter == 0);
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

		var candleTrades = new List<CumulativeTrade>();

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

			candleTrades.Add(trade);
		}

        _lastDelta1 = candleTrades
			.Where(x => x.Volume >= _minVolume1 && (x.Volume <= _maxVolume1 || _maxVolume1 == 0))
			.Sum(x => x.Volume * (x.Direction == TradeDirection.Buy ? 1 : -1));

		_delta1 += _lastDelta1;

		_filter1Series[bar] = _delta1;

		_lastDelta2 = candleTrades
			.Where(x => x.Volume >= _minVolume2 && (x.Volume <= _maxVolume2 || _maxVolume2 == 0))
			.Sum(x => x.Volume * (x.Direction == TradeDirection.Buy ? 1 : -1));

		_delta2 += _lastDelta2;

		_filter2Series[bar] = _delta2;

		_lastDelta3 = candleTrades
			.Where(x => x.Volume >= _minVolume3 && (x.Volume <= _maxVolume3 || _maxVolume3 == 0))
			.Sum(x => x.Volume * (x.Direction == TradeDirection.Buy ? 1 : -1));

		_delta3 += _lastDelta3;

		_filter3Series[bar] = _delta3;

		_lastDelta4 = candleTrades
			.Where(x => x.Volume >= _minVolume4 && (x.Volume <= _maxVolume4 || _maxVolume4 == 0))
			.Sum(x => x.Volume * (x.Direction == TradeDirection.Buy ? 1 : -1));

		_delta4 += _lastDelta4;

		_filter4Series[bar] = _delta4;

		_lastDelta5 = candleTrades
			.Where(x => x.Volume >= _minVolume5 && (x.Volume <= _maxVolume5 || _maxVolume5 == 0))
			.Sum(x => x.Volume * (x.Direction == TradeDirection.Buy ? 1 : -1));

		_delta5 += _lastDelta5;

		_filter5Series[bar] = _delta5;

        // Smart Money (4+5) - Dumb Money (1+2)
        var smartMoney = _delta4 + _delta5;
        var dumbMoney = _delta1 + _delta2;
        var spread = smartMoney - dumbMoney;

        _spreadSeries[bar] = spread;
        _spreadSeries.Colors[CurrentBar - 1] = spread >= 0 ? SpreadPositiveColor.Convert() : SpreadNegativeColor.Convert();

        RaiseBarValueChanged(bar);
		_lastBar = bar;
	}

    private void UpdateVisibility()
    {
        if (_viewMode == ViewMode.SmartMoneySpread)
        {
            // Ocultar líneas individuales, mostrar Spread
            _filter1Series.VisualType = VisualMode.Hide;
            _filter2Series.VisualType = VisualMode.Hide;
            _filter3Series.VisualType = VisualMode.Hide;
            _filter4Series.VisualType = VisualMode.Hide;
            _filter5Series.VisualType = VisualMode.Hide;
            _spreadSeries.VisualType = VisualMode.Histogram;
        }
        else
        {
            // Restaurar visualización basada en los checkboxes individuales
            _filter1Series.VisualType = _useFilter1 ? VisualMode.Line : VisualMode.Hide;
            _filter2Series.VisualType = _useFilter2 ? VisualMode.Line : VisualMode.Hide;
            _filter3Series.VisualType = _useFilter3 ? VisualMode.Line : VisualMode.Hide;
            _filter4Series.VisualType = _useFilter4 ? VisualMode.Line : VisualMode.Hide;
            _filter5Series.VisualType = _useFilter5 ? VisualMode.Line : VisualMode.Hide;
            _spreadSeries.VisualType = VisualMode.Hide;
        }
    }

    private bool IsSessionStart(int bar)
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
                var prev = GetCandle(bar - 1);

                // Use the same timezone normalization pattern as CumulativeDelta
                var tz = InstrumentInfo.TimeZone;
                return prev.Time.AddHours(tz).TimeOfDay < _customSessionStart
                    && candle.Time.AddHours(tz).TimeOfDay >= _customSessionStart;

            default:
                return false;
        }
    }

    private int FindSessionBeginBar()
    {
        var totalBars = CurrentBar - 1;
        if (totalBars < 0)
            return 0;

        // If SessionMode.None, start from bar 0 (or allow SessionsBack to still work: typically no)
        if (_sessionMode == SessionMode.None)
            return 0;

        var found = 0;

        for (var i = totalBars; i >= 0; i--)
        {
            if (!IsSessionStart(i))
                continue;

            found++;
            if (found >= _sessionsBack)
                return i;
        }

        // Not enough sessions in history; fall back to earliest bar
        return 0;
    }

    private void ResetSessionState(int bar)
    {
        // Reset running deltas
        _delta1 = _delta2 = _delta3 = _delta4 = _delta5 = 0m;

        // Also reset “last per bar” deltas to avoid carry in CalculateBarTrades realtime branch
        _lastDelta1 = _lastDelta2 = _lastDelta3 = _lastDelta4 = _lastDelta5 = 0m;

        // Initialize series values at session start (optional but helps avoid gaps)
        _filter1Series[bar] = 0m;
        _filter2Series[bar] = 0m;
        _filter3Series[bar] = 0m;
        _filter4Series[bar] = 0m;
        _filter5Series[bar] = 0m;
        _spreadSeries[bar] = 0m;
    }

    private void ReplayBufferedRealtimeAfterHistory()
    {
        // Replay cumulative trades that arrived AFTER the historical request end
        foreach (var ct in _trades)
        {
            if (ct.Time > _historyEndTime)
                CalculateTrade(ct, isUpdate: false, newBar: false);
        }

        // Replay ticks that arrived AFTER the historical request end
        foreach (var t in _ticks)
        {
            if (t.Time > _historyEndTime)
                CalculateTick(t);
        }

        _trades.Clear();
        _ticks.Clear();
    }




    #endregion
}