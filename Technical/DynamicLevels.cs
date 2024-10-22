namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

using ATAS.Indicators.Drawing;

using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering;

using Utils.Common.Logging;

[DisplayName("Dynamic Levels")]
[Category(IndicatorCategories.ClustersProfilesLevels)]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.DynamicLevelsDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602380")]
public class DynamicLevels : Indicator
{
	#region Nested types

	#region Candle

	public class DynamicCandle
	{
		#region Nested types

		private class PriceInfo
		{
			#region Properties

			public decimal Price { get; }

			public decimal Volume { get; set; }

			public decimal Value { get; set; }

			#endregion

			#region ctor

			public PriceInfo(decimal price)
			{
				Price = price;
			}

			#endregion
		}

		#endregion

		#region Fields

		private readonly SortedList<decimal, PriceInfo> _allPrice = new();

		private decimal _cachedVah;
		private decimal _cachedVal;
		private decimal _cachedVol;
		private decimal _maxPrice;

		private long _cacheTs;

		private PriceInfo _maxPriceInfo = new(0);

		public MiddleClusterType Type = MiddleClusterType.Volume;

		#endregion

		#region Properties

		public decimal MaxValue { get; set; }

		public decimal TrueMaxValue { get; set; }

		public decimal MaxValuePrice { get; set; }

		public decimal High { get; set; }

		public decimal Low { get; set; }

		public decimal Open { get; set; }

		public decimal Close { get; set; }

		public decimal Volume { get; set; }

		#endregion

		#region Public methods

		public void AddCandle(IndicatorCandle candle, decimal tickSize)
		{
			if (Open == 0)
				Open = candle.Open;
			Close = candle.Close;

			for (var price = candle.High; price >= candle.Low; price -= tickSize)
			{
				var info = candle.GetPriceVolumeInfo(price);

				if (info == null)
					continue;

				if (price > High)
					High = price;

				if (price < Low || Low == 0)
					Low = price;

				Volume += info.Volume;

				if (!_allPrice.TryGetValue(price, out var priceInfo))
				{
					priceInfo = new PriceInfo(price);
					_allPrice.Add(price, priceInfo);
				}

				priceInfo.Volume += info.Volume;

				if (priceInfo.Volume > _maxPriceInfo.Value)
				{
					_maxPrice = price;
					_maxPriceInfo = priceInfo;
				}

				switch (Type)
				{
					case MiddleClusterType.Bid:
					{
						priceInfo.Value += info.Bid;
						break;
					}
					case MiddleClusterType.Ask:
					{
						priceInfo.Value += info.Ask;
						break;
					}
					case MiddleClusterType.Delta:
					{
						priceInfo.Value += info.Ask - info.Bid;
						break;
					}
					case MiddleClusterType.Volume:
					{
						priceInfo.Value += info.Volume;
						break;
					}
					case MiddleClusterType.Tick:
					{
						priceInfo.Value += info.Ticks;
						break;
					}
					case MiddleClusterType.Time:
					{
						priceInfo.Value += info.Time;
						break;
					}
					default:
						throw new ArgumentOutOfRangeException();
				}

				if (Math.Abs(priceInfo.Value) > MaxValue)
				{
					MaxValue = Math.Abs(priceInfo.Value);
					TrueMaxValue = priceInfo.Value;
					MaxValuePrice = price;
				}
			}
		}

		public void AddTick(MarketDataArg tick)
		{
			if (tick.DataType != MarketDataType.Trade)
				return;

			var price = tick.Price;

			if (price < Low || Low == 0)
				Low = price;

			if (price > High)
				High = price;

			Volume += tick.Volume;
			var volume = tick.Volume;
			var bid = 0m;
			var ask = 0m;

			if (tick.Direction == TradeDirection.Buy)
				ask = volume;
			else if (tick.Direction == TradeDirection.Sell)
				bid = volume;

			if (!_allPrice.TryGetValue(price, out var priceInfo))
			{
				priceInfo = new PriceInfo(price);
				_allPrice.Add(price, priceInfo);
			}

			priceInfo.Volume += volume;

			if (priceInfo.Volume > _maxPriceInfo.Value)
			{
				_maxPrice = price;
				_maxPriceInfo = priceInfo;
			}

			switch (Type)
			{
				case MiddleClusterType.Bid:
				{
					priceInfo.Value += bid;
					break;
				}
				case MiddleClusterType.Ask:
				{
					priceInfo.Value += ask;
					break;
				}
				case MiddleClusterType.Delta:
				{
					priceInfo.Value += ask - bid;
					break;
				}
				case MiddleClusterType.Volume:
				{
					priceInfo.Value += volume;
					break;
				}
				case MiddleClusterType.Tick:
				{
					priceInfo.Value++;
					break;
				}
				case MiddleClusterType.Time:
				{
					priceInfo.Value++;
					break;
				}
				default:
					throw new ArgumentOutOfRangeException();
			}

			if (Math.Abs(priceInfo.Value) > MaxValue)
			{
				MaxValue = Math.Abs(priceInfo.Value);
				TrueMaxValue = priceInfo.Value;
				MaxValuePrice = price;
			}
		}

		public (decimal, decimal) GetValueArea(decimal tickSize, int valueAreaPercent, int valueAreaStep, int valueAreaDelay, bool useCache)
		{
			if (Volume == _cachedVol)
				return (_cachedVah, _cachedVal);

			if (useCache && _cachedVol != 0 && _cacheTs != 0 && Stopwatch.GetElapsedTime(_cacheTs).TotalMilliseconds < valueAreaDelay)
				return (_cachedVah, _cachedVal);
			
			var vah = 0m;
			var val = 0m;

			if (High != 0 && Low != 0)
			{
				var k = valueAreaPercent / 100.0m;
				vah = val = _maxPrice;
				var vol = _maxPriceInfo.Volume;
				var valueAreaVolume = Volume * k;

				var upperPrice = 0m;
				var lowerPrice = 0m;
				var upperIndex = 0;
				var lowerIndex = 0;

				while (vol <= valueAreaVolume)
				{
					if (vah >= High && val <= Low)
						break;

					var upperVol = 0m;
					var lowerVol = 0m;

					var newVah = upperPrice != vah;
					var newVal = lowerPrice != val;

					upperPrice = vah;
					lowerPrice = val;
					var c = valueAreaStep;

					var count = _allPrice.Count;

					if (newVah)
						upperIndex = _allPrice.IndexOfKey(upperPrice);

					var upLoopIdx = upperIndex;
					var upLoopPrice = upperPrice;

					for (var i = 0; i < c; i++)
					{
						if (upLoopIdx + 1 >= count)
							break;

						upLoopIdx++;

						var info = _allPrice.Values[upLoopIdx];
						upLoopPrice = info.Price;

						upperVol += info.Volume;
					}

					if (newVal)
						lowerIndex = _allPrice.IndexOfKey(lowerPrice);

					var downLoopIdx = lowerIndex;
					var downLoopPrice = lowerPrice;

					for (var i = 0; i < c; i++)
					{
						if (downLoopIdx - 1 < 0)
							break;

						downLoopIdx--;

						var info = _allPrice.Values[downLoopIdx];
						downLoopPrice = info.Price;

						lowerVol += info.Volume;
					}

					if (upperVol == lowerVol && upperVol == 0)
					{
						vah = Math.Min(upLoopPrice, High);
						val = Math.Max(downLoopPrice, Low);
					}
					else if (upperVol >= lowerVol)
					{
						vah = upLoopPrice;
						vol += upperVol;
					}
					else
					{
						val = downLoopPrice;
						vol += lowerVol;
					}

					if (vol >= valueAreaVolume)
						break;
				}
			}

			_cachedVol = Volume;
			_cachedVah = vah;
			_cachedVal = val;
			_cacheTs = Stopwatch.GetTimestamp();

			return (vah, val);
		}

		public void Clear()
		{
			_allPrice.Clear();
			MaxValue = High = Low = Volume = _cachedVol = _cachedVah = _cachedVal = _maxPrice = 0;
			_maxPriceInfo = new PriceInfo(0);
		}

		#endregion
	}

	#endregion

	[Obfuscation(Feature = "renaming", ApplyToMembers = true, Exclude = true)]
	[Serializable]
	public enum MiddleClusterType
	{
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Bid))]
		Bid,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Ask))]
		Ask,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Delta))]
		Delta,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Volume))]
		Volume,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Ticks))]
		Tick,

		[Browsable(false)]
		[Obsolete]
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Time))]
		Time
	}

	[Serializable]
	[Obfuscation(Feature = "renaming", ApplyToMembers = true, Exclude = true)]
	public enum Period
	{
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Hourly))]
		Hourly,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.H4))]
		H4,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Daily))]
		Daily,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Weekly))]
		Weekly,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Monthly))]
		Monthly,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.AllPeriodtxt))]
		All
	}

	[Serializable]
	[Obfuscation(Feature = "renaming", ApplyToMembers = true, Exclude = true)]
	public enum VolumeVizualizationType
	{
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.AtStart))]
		AtStart,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Accumulated))]
		Accumulated
	}

	#endregion

	#region Fields

	private readonly DynamicCandle _closedCandle = new();
	private readonly ValueDataSeries _dynamicLevels;
	private readonly object _syncRoot = new();

	private readonly RangeDataSeries _valueArea = new("ValueArea", "Value area")
	{
		RangeColor = CrossColor.FromArgb(30, 128, 0, 2),
		DescriptionKey = nameof(Strings.RangeAreaDescription)
	};

	private readonly ValueDataSeries _valueAreaBottom = new("ValueAreaBottom", "Value Area 2nd line") 
	{
		Color = System.Drawing.Color.Maroon.Convert(), 
		Width = 2,
        DescriptionKey = nameof(Strings.BottomChannelSettingsDescription)
    };

	private readonly ValueDataSeries _valueAreaTop = new("ValueAreaTop", "Value Area 1st line") 
	{ 
		Color = System.Drawing.Color.Maroon.Convert() ,
		Width = 2,
        DescriptionKey = nameof(Strings.TopChannelSettingsDescription)
    };

	private int _days;
	private decimal _filter;
	private int _lastAlertBar = -1;
	private decimal _lastApproximateLevel;
	private int _lastBar = -1;
	private int _lastCalculatedBar;

	private DrawingText _lastLabel;
	private int _lastPocAlert;

	private DateTime _lastTickTime;
	private DateTime _lastTime;
	private int _lastVahAlert;
	private int _lastValAlert;
	private decimal _lastValue;
	private Period _period = Period.Daily;
	private decimal _prevClose;

	private bool _showVolumes = true;
	private int _targetBar;
	private bool _tickBasedCalculation;

	private MiddleClusterType _type = MiddleClusterType.Volume;
	private VolumeVizualizationType _visualizationType = VolumeVizualizationType.Accumulated;
	private System.Drawing.Color _textColor = System.Drawing.Color.FromArgb(255, 75, 72, 72);

	#endregion

    #region Properties

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Calculation), Name = nameof(Strings.DaysLookBack), Order = int.MaxValue, Description = nameof(Strings.DaysLookBackDescription))]
    public int Days
	{
		get => _days;
		set
		{
			if (value < 0)
				return;

			_days = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Type), GroupName = nameof(Strings.Filters), Description = nameof(Strings.SourceTypeDescription), Order = 110)]
	public MiddleClusterType Type
	{
		get => _type;
		set
		{
			_type = value;
			_closedCandle.Type = value;
			RecalculateValues();
		}
	}

    [Parameter]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Filters), Description = nameof(Strings.PeriodTypeDescription), Order = 120)]
	public Period PeriodFrame
	{
		get => _period;
		set
		{
			_period = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.Filters), Description = nameof(Strings.MinimumFilterDescription), Order = 130)]
	public decimal Filter
	{
		get => _filter;
		set
		{
			_filter = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowVolume), GroupName = nameof(Strings.Other), Description = nameof(Strings.ShowVolumesDescription), Order = 200)]
	public bool ShowVolumes
	{
		get => _showVolumes;
		set
		{
			_showVolumes = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.VolumeVisualizationType), GroupName = nameof(Strings.Other), Description = nameof(Strings.CalculationModeDescription), Order = 210)]
	public VolumeVizualizationType VizualizationType
	{
		get => _visualizationType;
		set
		{
			_visualizationType = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ApproximationAlert), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.IsApproximationAlertDescription), Order = 300)]
	public bool UseApproximationAlert { get; set; }

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ApproximationFilter), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.ApproximationFilterDescription), Order = 310)]
	public int ApproximationFilter { get; set; } = 3;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.PocChangeAlert), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.UsePocChangeAlertDescription), Order = 320)]
	public bool UseAlerts { get; set; }

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.PocAlert), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.UsePocTouchAlertDescription), Order = 330)]
	public bool UsePocTouchAlert { get; set; }

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ValAlert), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.UseVALTouchAlertDescription), Order = 340)]
	public bool UseValTouchAlert { get; set; }

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.VahAlert), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.UseVAHTouchAlertDescription), Order = 350)]
	public bool UseVahTouchAlert { get; set; }

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFileDescription), Order = 360)]
	public string AlertFile { get; set; } = "alert1";

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.FontColor), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertTextColorDescription), Order = 370)]
	public CrossColor AlertForeColor { get; set; } = CrossColor.FromArgb(255, 247, 249, 249);

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BackGround), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFillColorDescription), Order = 380)]
	public CrossColor AlertBGColor { get; set; } = CrossColor.FromArgb(255, 75, 72, 72);

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TextColor), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.LabelTextColorDescription), Order = 600)]
	public CrossColor TextColor 
	{
		get=> _textColor.Convert();
		set
		{
			_textColor = value.Convert();

			foreach (var key in Labels.Keys)
				Labels[key].Textcolor = _textColor;
		}
	} 

	#endregion

	#region ctor

	public DynamicLevels()
		: base(true)
	{
		DenyToChangePanel = true;

		_days = 20;

		_dynamicLevels = (ValueDataSeries)DataSeries[0];
		_dynamicLevels.VisualType = VisualMode.Square;
		_dynamicLevels.Color = System.Drawing.Color.Orange.Convert();
		_dynamicLevels.Width = 2;
        _dynamicLevels.Name = "Dynamic levels";
		_dynamicLevels.DescriptionKey = nameof(Strings.POCLineSettingsDescription);

        _dynamicLevels.PropertyChanged += LevelsSeriesPropertyChanged;

		DataSeries.Add(_valueArea);
		DataSeries.Add(_valueAreaTop);
		DataSeries.Add(_valueAreaBottom);
	}

    #endregion

    #region Protected methods
    protected override void OnApplyDefaultColors()
    {
	    if (ChartInfo is null)
		    return;

	    var downColor = ChartInfo.ColorsStore.DownCandleColor;

	    var seriesColor = CrossColor.FromArgb(
		    (byte)(downColor.A / 4 * 3),
		    (byte)(downColor.R / 4 * 3),
		    (byte)(downColor.G / 4 * 3),
		    (byte)(downColor.B / 4 * 3)).Convert();

	    _valueAreaTop.Color = _valueAreaBottom.Color = seriesColor.Convert();
        _valueArea.RangeColor = seriesColor.SetTransparency(0.9m).Convert();
    }

    protected override void OnRecalculate()
	{
		lock (_syncRoot)
		{
			_tickBasedCalculation = false;
			_lastCalculatedBar = -1;
			_lastBar = -1;
			_closedCandle.Clear();
		}
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar == 0)
		{
			_lastPocAlert = 0;
			_lastValAlert = 0;
			_lastVahAlert = 0;
			_prevClose = GetCandle(CurrentBar - 1).Close;
			_lastTime = GetCandle(bar).Time;
			DataSeries.ForEach(x => x.Clear());

			_targetBar = 0;

			if (_days > 0)
			{
				var days = 0;

				for (var i = CurrentBar - 1; i >= 0; i--)
				{
					_targetBar = i;

					if (!IsNewSession(i))
						continue;

					days++;

					if (days == _days)
						break;
				}

				if (_targetBar > 0)
				{
					_dynamicLevels.SetPointOfEndLine(_targetBar - 1);
					_valueAreaTop.SetPointOfEndLine(_targetBar - 1);
					_valueAreaBottom.SetPointOfEndLine(_targetBar - 1);
				}
			}
		}

		if (bar < _targetBar)
			return;

		lock (_syncRoot)
		{
			if (_tickBasedCalculation)
				return;

			if (CheckUpdatePeriod(bar))
			{
				_closedCandle.Clear();
				_closedCandle.Type = Type;
			}

			var candle = GetCandle(bar);

			_lastTickTime = candle.LastTime;

			_closedCandle.AddCandle(candle, InstrumentInfo.TickSize);
			CalculateValues(bar);
		}
	}

	protected override void OnFinishRecalculate()
	{
		lock (_syncRoot)
			_tickBasedCalculation = true;
	}

	protected override void OnNewTrades(IEnumerable<MarketDataArg> trades)
	{
		if (ChartInfo is null)
			return;

		if (CurrentBar == 0)
			return;

		lock (_syncRoot)
		{
			if (!_tickBasedCalculation)
				return;

			var lastTime = GetCandle(_lastCalculatedBar).LastTime;

			foreach (var trade in trades)
			{
				try
				{
					if (trade.Time > lastTime)
					{
						CalculateValues(_lastCalculatedBar);

						if (_lastCalculatedBar < CurrentBar - 1)
						{
							_lastCalculatedBar++;
							lastTime = GetCandle(_lastCalculatedBar).LastTime;
						}
						else
						{
							this.LogError($"Last bar number is not exist C:{CurrentBar} L:{_lastCalculatedBar}");
#if DEBUG
							// if you are here please recall what you were doing
							// grab the local values of variables and
							// tell about this to @esper (telegram)
							Debugger.Break();
#else
								return;
#endif
						}
					}

					if (CheckUpdatePeriod(trade.Time))
					{
						_closedCandle.Clear();
						_closedCandle.Type = Type;
					}

					_closedCandle.AddTick(trade);

					_lastTickTime = trade.Time;
				}
				catch (Exception e)
				{
					this.LogError("Dynamic Levels error.", e);
				}
			}

			for (var i = _lastCalculatedBar; i <= CurrentBar - 1; i++)
			{
				if (!_tickBasedCalculation)
					return;

				CalculateValues(i);
			}
		}
	}

	#endregion

	#region Private methods

	private void LevelsSeriesPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is not "Color")
			return;

		foreach (var (labelKey, labelValue) in Labels)
		{
			if (Labels[labelKey] is null)
				continue;

			Labels[labelKey].FillColor = ((ValueDataSeries)sender).Color.Convert();
		}
	}

	private void CalculateValues(int i)
	{
		_lastCalculatedBar = i;

		var maxPrice = _closedCandle.MaxValuePrice;
		var value = _closedCandle.MaxValue;
		var valueString = ChartInfo.TryGetMinimizedVolumeString(value);

		if (Type == MiddleClusterType.Delta)
			valueString = ChartInfo.TryGetMinimizedVolumeString(_closedCandle.TrueMaxValue);

		var validFilter = value >= Filter;

        _dynamicLevels[i] = validFilter ? maxPrice : 0;

		if(!validFilter)
			_dynamicLevels.SetPointOfEndLine(i - 1);

        if (i == 0)
		{
			var close = GetCandle(0).Close;
			this[0] = _valueAreaTop[0] = _valueAreaBottom[0] = close;

			_valueArea[0] = new RangeValue
				{ Lower = close, Upper = close };

			return;
		}

		var prevPrice = this[i - 1];

		if (prevPrice > 0.000001m && Math.Abs(prevPrice - maxPrice) > InstrumentInfo.TickSize / 2 && value > Filter)
		{
			if (ShowVolumes)
			{
				var cl = System.Drawing.Color.FromArgb(_dynamicLevels.Color.A, _dynamicLevels.Color.R, _dynamicLevels.Color.G, _dynamicLevels.Color.B);

				_lastLabel = AddText(i.ToString(CultureInfo.InvariantCulture),
					valueString, prevPrice < maxPrice,
					i, maxPrice, 0, 0, _textColor,
					System.Drawing.Color.Black, cl, 11, DrawingText.TextAlign.Left);
			}

			if (UseAlerts && i > _lastAlertBar && i == CurrentBar - 1)
			{
				_lastAlertBar = i;
				AddAlert(AlertFile, InstrumentInfo.Instrument, $"Changed max level to {maxPrice}", AlertBGColor, AlertForeColor);
			}
		}
		else
		{
			if (ShowVolumes)
			{
				if (VizualizationType == VolumeVizualizationType.Accumulated)
				{
					if (_lastLabel != null && value != _lastValue)
					{
						_lastLabel.Text = ChartInfo.TryGetMinimizedVolumeString(value); 
						_lastValue = value;
					}
				}
			}
		}

		var va = _closedCandle.GetValueArea(InstrumentInfo.TickSize, PlatformSettings.ValueAreaPercent, PlatformSettings.ValueAreaStep, PlatformSettings.ValueAreaUpdateDelayMs, _tickBasedCalculation);

		_valueArea[i].Upper = va.Item1;
		_valueArea[i].Lower = va.Item2;
		_valueAreaTop[i] = va.Item1;
		_valueAreaBottom[i] = va.Item2;

		if (i != CurrentBar - 1)
			return;

		var candle = GetCandle(i);

		if (UseApproximationAlert)
		{
			if (maxPrice != _lastApproximateLevel && Math.Abs(candle.Close - maxPrice) / InstrumentInfo.TickSize <= ApproximationFilter)
			{
				_lastApproximateLevel = maxPrice;
				AddAlert(AlertFile, InstrumentInfo.Instrument, $"Approximate to max Level {maxPrice}", AlertBGColor, AlertForeColor);
			}
		}

		if (UsePocTouchAlert && _lastPocAlert != i)
		{
			if ((candle.Close >= _dynamicLevels[i] && _prevClose < _dynamicLevels[i])
			    ||
			    (candle.Close <= _dynamicLevels[i] && _prevClose > _dynamicLevels[i]))
			{
                AddAlert(AlertFile, InstrumentInfo.Instrument, $"Price reached POC level: {_dynamicLevels[i]}", AlertBGColor, AlertForeColor);

                _lastPocAlert = i;
            }
		}

		if (UseValTouchAlert && _lastValAlert != i)
		{
			if ((candle.Close >= _valueAreaBottom[i] && _prevClose < _valueAreaBottom[i])
			    ||
			    (candle.Close <= _valueAreaBottom[i] && _prevClose > _valueAreaBottom[i]))
			{
                AddAlert(AlertFile, InstrumentInfo.Instrument, $"Price reached VAL level: {_valueAreaBottom[i]}", AlertBGColor, AlertForeColor);

                _lastValAlert = i;
            }
		}

		if (UseVahTouchAlert && _lastVahAlert != i)
		{
			if ((candle.Close >= _valueAreaTop[i] && _prevClose < _valueAreaTop[i])
			    ||
			    (candle.Close <= _valueAreaTop[i] && _prevClose > _valueAreaTop[i]))
			{
                AddAlert(AlertFile, InstrumentInfo.Instrument, $"Price reached VAH level: {_valueAreaTop[i]}", AlertBGColor, AlertForeColor);

                _lastVahAlert = i;
            }
		}

		_prevClose = candle.Close;
	}

	private bool CheckUpdatePeriod(int i)
	{
		if (_lastBar == i)
			return false;

		_lastBar = i;

		if (i <= 0)
			return false;

		switch (PeriodFrame)
		{
			case Period.Daily when IsNewSession(i):
			case Period.Weekly when IsNewWeek(i):
			case Period.Monthly when IsNewMonth(i):
			case Period.Hourly when GetCandle(i).Time.Hour != GetCandle(i - 1).Time.Hour:
				return true;

			case Period.H4 when (GetCandle(i).Time - _lastTime).TotalHours >= 4:
				_lastTime = _lastTime.AddHours(4);
				return true;

			default:
				return false;
		}
	}

	private bool CheckUpdatePeriod(DateTime time)
	{
		switch (PeriodFrame)
		{
			case Period.Daily when DataProvider.IsNewSession(_lastTickTime, time):
			case Period.Weekly when DataProvider.IsNewSession(_lastTickTime, time):
			case Period.Monthly when DataProvider.IsNewMonth(_lastTickTime, time):
			case Period.Hourly when time.Hour != _lastTickTime.Hour:
				return true;

			case Period.H4 when (time - _lastTime).TotalHours >= 4:
				_lastTime = _lastTime.AddHours(4);
				return true;

			default:
				return false;
		}
	}

	#endregion
}