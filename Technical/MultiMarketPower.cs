namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows.Media;

using ATAS.Indicators.Drawing;
using ATAS.Indicators.Technical.Properties;

using OFT.Attributes;
using OFT.Attributes.Editors;

using Utils.Common;

[Category("Order Flow")]
[DisplayName("CVD pro(multi) / Multi Market Powers")]
[Description("Cumulative delta volume pro. Allows to build multiple CVD lines for different trade sizes")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/371-multi-market-powers")]
public class MultiMarketPower : Indicator
{
	#region Fields

	private readonly ValueDataSeries _filter1Series = new("Filter1")
	{
		Color = Color.FromRgb(135, 206, 235),
		IsHidden = true,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true
	};

	private readonly ValueDataSeries _filter2Series = new("Filter2")
	{
		Color = DefaultColors.Red.Convert(),
		IsHidden = true,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true
	};

	private readonly ValueDataSeries _filter3Series = new("Filter3")
	{
		Color = DefaultColors.Green.Convert(),
		IsHidden = true,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true
	};

	private readonly ValueDataSeries _filter4Series = new("Filter4")
	{
		Color = Color.FromRgb(128, 128, 128),
		Width = 2,
		IsHidden = true,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true
	};

	private readonly ValueDataSeries _filter5Series = new("Filter5")
	{
		Color = Color.FromRgb(205, 92, 92),
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

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Resources), Name = "CumulativeTrades", GroupName = "Filters", Order = 90)]
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

	[Display(ResourceType = typeof(Resources), Name = "Enabled", GroupName = "Filter1", Order = 100)]
	public bool UseFilter1
	{
		get => _useFilter1;
		set
		{
			_useFilter1 = value;
			_filter1Series.VisualType = value ? VisualMode.Line : VisualMode.Hide;
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "LineWidth", GroupName = "Filter1", Order = 120)]
	[Range(1, 100)]
	public int LineWidth1
	{
		get => _filter1Series.Width;
		set => _filter1Series.Width = value;
	}

	[Display(ResourceType = typeof(Resources), Name = "MinimumVolume", GroupName = "Filter1", Order = 130)]
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

	[Display(ResourceType = typeof(Resources), Name = "MaximumVolume", GroupName = "Filter1", Order = 140)]
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

	[Display(ResourceType = typeof(Resources), Name = "Color", GroupName = "Filter1", Order = 150)]
	public Color Color1
	{
		get => _filter1Series.Color;
		set => _filter1Series.Color = value;
	}

	[Display(ResourceType = typeof(Resources), Name = "Enabled", GroupName = "Filter2", Order = 200)]
	public bool UseFilter2
	{
		get => _useFilter2;
		set
		{
			_useFilter2 = value;
			_filter2Series.VisualType = value ? VisualMode.Line : VisualMode.Hide;
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "LineWidth", GroupName = "Filter2", Order = 220)]
	[Range(1, 100)]
	public int LineWidth2
	{
		get => _filter2Series.Width;
		set => _filter2Series.Width = value;
	}

	[Display(ResourceType = typeof(Resources), Name = "MinimumVolume", GroupName = "Filter2", Order = 230)]
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

	[Display(ResourceType = typeof(Resources), Name = "MaximumVolume", GroupName = "Filter2", Order = 240)]
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

	[Display(ResourceType = typeof(Resources), Name = "Color", GroupName = "Filter2", Order = 250)]
	public Color Color2
	{
		get => _filter2Series.Color;
		set => _filter2Series.Color = value;
	}

	[Display(ResourceType = typeof(Resources), Name = "Enabled", GroupName = "Filter3", Order = 300)]
	public bool UseFilter3
	{
		get => _useFilter3;
		set
		{
			_useFilter3 = value;
			_filter3Series.VisualType = value ? VisualMode.Line : VisualMode.Hide;
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "LineWidth", GroupName = "Filter3", Order = 320)]
	[Range(1, 100)]
	public int LineWidth3
	{
		get => _filter3Series.Width;
		set => _filter3Series.Width = value;
	}

	[Display(ResourceType = typeof(Resources), Name = "MinimumVolume", GroupName = "Filter3", Order = 330)]
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

	[Display(ResourceType = typeof(Resources), Name = "MaximumVolume", GroupName = "Filter3", Order = 340)]
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

	[Display(ResourceType = typeof(Resources), Name = "Color", GroupName = "Filter3", Order = 350)]
	public Color Color3
	{
		get => _filter3Series.Color;
		set => _filter3Series.Color = value;
	}

	[Display(ResourceType = typeof(Resources), Name = "Enabled", GroupName = "Filter4", Order = 400)]
	public bool UseFilter4
	{
		get => _useFilter4;
		set
		{
			_useFilter4 = value;
			_filter4Series.VisualType = value ? VisualMode.Line : VisualMode.Hide;
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "LineWidth", GroupName = "Filter4", Order = 420)]
	[Range(1, 100)]
	public int LineWidth4
	{
		get => _filter4Series.Width;
		set => _filter4Series.Width = value;
	}

	[Display(ResourceType = typeof(Resources), Name = "MinimumVolume", GroupName = "Filter4", Order = 430)]
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

	[Display(ResourceType = typeof(Resources), Name = "MaximumVolume", GroupName = "Filter4", Order = 440)]
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

	[Display(ResourceType = typeof(Resources), Name = "Color", GroupName = "Filter4", Order = 450)]
	public Color Color4
	{
		get => _filter4Series.Color;
		set => _filter4Series.Color = value;
	}

	[Display(ResourceType = typeof(Resources), Name = "Enabled", GroupName = "Filter5", Order = 500)]
	public bool UseFilter5
	{
		get => _useFilter5;
		set
		{
			_useFilter5 = value;
			_filter5Series.VisualType = value ? VisualMode.Line : VisualMode.Hide;
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "LineWidth", GroupName = "Filter5", Order = 520)]
	[Range(1, 100)]
	public int LineWidth5
	{
		get => _filter5Series.Width;
		set => _filter5Series.Width = value;
	}

	[Display(ResourceType = typeof(Resources), Name = "MinimumVolume", GroupName = "Filter5", Order = 530)]
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

	[Display(ResourceType = typeof(Resources), Name = "MaximumVolume", GroupName = "Filter5", Order = 540)]
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

	[Display(ResourceType = typeof(Resources), Name = "Color", GroupName = "Filter5", Order = 550)]
	public Color Color5
	{
		get => _filter5Series.Color;
		set => _filter5Series.Color = value;
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
	}

	#endregion

	#region Protected methods

	protected override void OnRecalculate()
	{
		_bigTradesIsReceived = false;
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar == 0)
		{
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

		if (_filter1Series[bar] != 0)
			return;

		_filter1Series[bar] = _filter1Series[bar - 1];
		_filter2Series[bar] = _filter2Series[bar - 1];
		_filter3Series[bar] = _filter3Series[bar - 1];
		_filter4Series[bar] = _filter4Series[bar - 1];
		_filter5Series[bar] = _filter5Series[bar - 1];
	}

	protected override void OnCumulativeTradesResponse(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> cumulativeTrades)
	{
		if (request.RequestId != _requestId)
			return;

		ClearValues();
		var trades = cumulativeTrades.ToList();
		
		CalculateHistory(trades);

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
				var prevBarReset = _lastTrade.Time < GetCandle(CurrentBar - 1).Time;

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

		RaiseBarValueChanged(CurrentBar - 1);
		_lastTrade = trade.MemberwiseClone();
	}

	private void CalculateHistory(List<CumulativeTrade> trades)
	{
		try
		{
			if (CumulativeTrades)
			{
				for (var i = _sessionBegin; i <= CurrentBar - 1; i++)
					CalculateBarTrades(trades, i);

				foreach (var trade in _trades)
					CalculateTrade(trade, false, false);
			}
			else
			{
				var ticks = trades.SelectMany(x => x.Ticks).ToList();

				for (var i = _sessionBegin; i <= CurrentBar - 1; i++)
					CalculateBarTicks(ticks, i);

				foreach (var tick in _ticks)
					CalculateTick(tick);
			}

			RedrawChart();
		}
		catch (NullReferenceException)
		{
			//on reset exception ignored
		}
	}

	private void CalculateBarTicks(List<MarketDataArg> trades, int i)
	{
		var candle = GetCandle(i);

		var candleTrades = trades
			.Where(x => x.Time >= candle.Time && x.Time <= candle.LastTime)
			.ToList();

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
	}

	private bool IsFiltered(decimal minFilter, decimal maxFilter, decimal volume)
	{
		return volume >= minFilter && (volume <= maxFilter || maxFilter == 0);
	}

	private void CalculateBarTrades(List<CumulativeTrade> trades, int bar, bool realTime = false, bool newBar = false)
	{
		if (newBar)
			CalculateBarTrades(trades, bar - 1, true);

		if (CumulativeTrades && realTime && !newBar)
		{
			_delta1 -= _lastDelta1;
			_delta2 -= _lastDelta2;
			_delta3 -= _lastDelta3;
			_delta4 -= _lastDelta4;
			_delta5 -= _lastDelta5;
		}

		var candle = GetCandle(bar);

		var candleTrades = trades
			.Where(x => x.Time >= candle.Time && x.Time <= candle.LastTime)
			.ToList();

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

		RaiseBarValueChanged(bar);
		_lastBar = bar;
	}

	#endregion
}