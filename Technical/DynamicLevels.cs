namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Globalization;
	using System.Reflection;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Logging;

	[DisplayName("Dynamic Levels")]
	[Category("Clusters, Profiles, Levels")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/380-dynamic-levels")]
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

				public decimal Volume { get; set; }

				public decimal Value { get; set; }

				#endregion
			}

			#endregion

			#region Fields

			private readonly Dictionary<decimal, PriceInfo> _allPrice = new();
			private decimal _cachedVah;
			private decimal _cachedVal;

			private decimal _cachedVol;

			private decimal _maxPrice;

			private PriceInfo _maxPriceInfo = new();

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
						priceInfo = new PriceInfo();
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
					priceInfo = new PriceInfo();
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

			public decimal[] GetValueArea(decimal tickSize, int valueAreaPercent)
			{
				if (Volume == _cachedVol)
					return new[] { _cachedVah, _cachedVal };

				var vah = 0m;
				var val = 0m;

				if (High != 0 && Low != 0)
				{
					var k = valueAreaPercent / 100.0m;
					vah = val = _maxPrice;
					var vol = _maxPriceInfo.Volume;
					var valueAreaVolume = Volume * k;

					while (vol <= valueAreaVolume)
					{
						if (vah >= High && val <= Low)
							break;

						var upperVol = 0m;
						var lowerVol = 0m;
						var upperPrice = vah;
						var lowerPrice = val;
						var c = 2;

						for (var i = 0; i <= c; i++)
						{
							if (High >= upperPrice + tickSize)
							{
								upperPrice += tickSize;

								if (_allPrice.TryGetValue(upperPrice, out var info))
									upperVol += info.Volume;
							}
							else
								break;
						}

						for (var i = 0; i <= c; i++)
						{
							if (Low <= lowerPrice - tickSize)
							{
								lowerPrice -= tickSize;

								if (_allPrice.TryGetValue(lowerPrice, out var info))
									lowerVol += info.Volume;
							}
							else
								break;
						}

						if (upperVol == lowerVol && upperVol == 0)
						{
							vah = Math.Min(upperPrice, High);
							val = Math.Max(lowerPrice, Low);
						}
						else if (upperVol >= lowerVol)
						{
							vah = upperPrice;
							vol += upperVol;
						}
						else
						{
							val = lowerPrice;
							vol += lowerVol;
						}

						if (vol >= valueAreaVolume)
							break;
					}
				}

				_cachedVol = Volume;
				_cachedVah = vah;
				_cachedVal = val;
				return new[] { vah, val };
			}

			public void Clear()
			{
				_allPrice.Clear();
				MaxValue = High = Low = Volume = _cachedVol = _cachedVah = _cachedVal = _maxPrice = 0;
				_maxPriceInfo = new PriceInfo();
			}

			#endregion
		}

		#endregion

		[Obfuscation(Feature = "renaming", ApplyToMembers = true, Exclude = true)]
		[Serializable]
		public enum MiddleClusterType
		{
			[Display(ResourceType = typeof(Resources), Name = "Bid")]
			Bid,

			[Display(ResourceType = typeof(Resources), Name = "Ask")]
			Ask,

			[Display(ResourceType = typeof(Resources), Name = "Delta")]
			Delta,

			[Display(ResourceType = typeof(Resources), Name = "Volume")]
			Volume,

			[Display(ResourceType = typeof(Resources), Name = "Ticks")]
			Tick,

			[Display(ResourceType = typeof(Resources), Name = "Time")]
			Time
		}

		[Serializable]
		[Obfuscation(Feature = "renaming", ApplyToMembers = true, Exclude = true)]
		public enum Period
		{
			[Display(ResourceType = typeof(Resources), Name = "Hourly")]
			Hourly,

			[Display(ResourceType = typeof(Resources), Name = "H4")]
			H4,

			[Display(ResourceType = typeof(Resources), Name = "Daily")]
			Daily,

			[Display(ResourceType = typeof(Resources), Name = "Weekly")]
			Weekly,

			[Display(ResourceType = typeof(Resources), Name = "Monthly")]
			Monthly,

			[Display(ResourceType = typeof(Resources), Name = "AllPeriodtxt")]
			All
		}

		[Serializable]
		[Obfuscation(Feature = "renaming", ApplyToMembers = true, Exclude = true)]
		public enum VolumeVisualizationType
		{
			[Display(ResourceType = typeof(Resources), Name = "AtStart")]
			AtStart,

			[Display(ResourceType = typeof(Resources), Name = "Accumulated")]
			Accumulated
		}

		#endregion

		#region Fields

		private readonly DynamicCandle _closedCandle = new();
		private readonly ValueDataSeries _dynamicLevels;
		private readonly object _syncRoot = new();

		private readonly RangeDataSeries _valueArea = new("Value area") { RangeColor = Color.FromArgb(30, 128, 0, 2) };
		private readonly ValueDataSeries _valueAreaBottom = new("Value Area 2nd line") { Color = Colors.Maroon };
		private readonly ValueDataSeries _valueAreaTop = new("Value Area 1st line") { Color = Colors.Maroon };
		private int _days;
		private decimal _filter;
		private int _lastAlertBar = -1;
		private decimal _lastApproximateLevel;
		private int _lastBar = -1;
		private int _lastCalculatedBar;

		private DrawingText _lastLabel;
		private int _lastPocAlert;
		private DateTime _lastTime;
		private int _lastVahAlert;
		private int _lastValAlert;
		private decimal _lastValue;
		private Period _period = Period.Daily;
		private decimal _prevClose;

		private bool _showVolumes = true;
		private int _targetBar;
		private bool _tickBasedCalculation;
		private VolumeVisualizationType _visualizationType = VolumeVisualizationType.Accumulated;

		private MiddleClusterType _type = MiddleClusterType.Volume;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Days", GroupName = "Filters", Order = 100)]
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

		[Display(ResourceType = typeof(Resources), Name = "Type", GroupName = "Filters", Order = 110)]
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

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Filters", Order = 120)]
		public Period PeriodFrame
		{
			get => _period;
			set
			{
				_period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Filter", GroupName = "Filters", Order = 130)]
		public decimal Filter
		{
			get => _filter;
			set
			{
				_filter = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ShowVolume", GroupName = "Other", Order = 200)]
		public bool ShowVolumes
		{
			get => _showVolumes;
			set
			{
				_showVolumes = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "VolumeVisualizationType", GroupName = "Other", Order = 210)]
		public VolumeVisualizationType VisualizationType
		{
			get => _visualizationType;
			set
			{
				_visualizationType = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ApproximationAlert", GroupName = "Alerts", Order = 300)]
		public bool UseApproximationAlert { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "ApproximationFilter", GroupName = "Alerts", Order = 310)]
		public int ApproximationFilter { get; set; } = 3;

		[Display(ResourceType = typeof(Resources), Name = "PocChangeAlert", GroupName = "Alerts", Order = 320)]
		public bool UsePocAlert { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "PocTouchAlert", GroupName = "Alerts", Order = 330)]
		public bool UsePocTouchAlert { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "ValTouchAlert", GroupName = "Alerts", Order = 340)]
		public bool UseValTouchAlert { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "VahTouchAlert", GroupName = "Alerts", Order = 350)]
		public bool UseVahTouchAlert { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "AlertFile", GroupName = "Alerts", Order = 360)]
		public string AlertFile { get; set; } = "alert1";

		[Display(ResourceType = typeof(Resources), Name = "FontColor", GroupName = "Alerts", Order = 370)]
		public Color AlertForeColor { get; set; } = Color.FromArgb(255, 247, 249, 249);

		[Display(ResourceType = typeof(Resources), Name = "BackGround", GroupName = "Alerts", Order = 380)]
		public Color AlertBgColor { get; set; } = Color.FromArgb(255, 75, 72, 72);

		#endregion

		#region ctor

		public DynamicLevels()
			: base(true)
		{
			DenyToChangePanel = true;

			_days = 20;

			_dynamicLevels = (ValueDataSeries)DataSeries[0];
			_dynamicLevels.VisualType = VisualMode.Square;
			_dynamicLevels.Color = Colors.Aqua;
			_dynamicLevels.Name = "Dynamic levels";

			DataSeries.Add(_valueArea);
			DataSeries.Add(_valueAreaTop);
			DataSeries.Add(_valueAreaBottom);
		}

		#endregion

		#region Protected methods

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
				CheckUpdatePeriod(bar);

				if (_tickBasedCalculation)
					return;

				_closedCandle.AddCandle(GetCandle(bar), InstrumentInfo.TickSize);
				CalculateValues(bar);
			}
		}

		protected override void OnFinishRecalculate()
		{
			lock (_syncRoot)
				_tickBasedCalculation = true;
		}

		protected override void OnNewTrade(MarketDataArg arg)
		{
			lock (_syncRoot)
			{
				try
				{
					if (!_tickBasedCalculation)
						return;

					_closedCandle.AddTick(arg);

					for (var i = _lastCalculatedBar; i <= CurrentBar - 1; i++)
					{
						if (!_tickBasedCalculation)
							return;

						CalculateValues(i);
					}
				}
				catch (Exception e)
				{
					this.LogError("Dynamic Levels error.", e);
				}
			}
		}

		#endregion

		#region Private methods

		private void CalculateValues(int i)
		{
			_lastCalculatedBar = i;

			var maxPrice = _closedCandle.MaxValuePrice;
			var value = _closedCandle.MaxValue;
			var valueString = value.ToString(CultureInfo.InvariantCulture);

			if (Type == MiddleClusterType.Delta)
				valueString = _closedCandle.TrueMaxValue.ToString(CultureInfo.InvariantCulture);

			_dynamicLevels[i] = value > Filter ? maxPrice : 0;

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
						i, maxPrice, 0, 0, System.Drawing.Color.Black,
						System.Drawing.Color.Black, cl, 10, DrawingText.TextAlign.Left);
				}

				if (UsePocAlert && i > _lastAlertBar && i == CurrentBar - 1)
				{
					_lastAlertBar = i;
					AddAlert(AlertFile, InstrumentInfo.Instrument, $"Changed max level to {maxPrice}", AlertBgColor, AlertForeColor);
				}
			}
			else
			{
				if (ShowVolumes)
				{
					if (VisualizationType == VolumeVisualizationType.Accumulated)
					{
						if (_lastLabel != null && value != _lastValue)
						{
							_lastLabel.Text = value.ToString(CultureInfo.InvariantCulture);
							_lastValue = value;
						}
					}
				}
			}

			var va = _closedCandle.GetValueArea(InstrumentInfo.TickSize, PlatformSettings.ValueAreaPercent);

			_valueArea[i].Upper = va[0];
			_valueArea[i].Lower = va[1];
			_valueAreaTop[i] = va[0];
			_valueAreaBottom[i] = va[1];

			if (i != CurrentBar - 1)
				return;

			var candle = GetCandle(i);

			if (UseApproximationAlert)
			{
				if (maxPrice != _lastApproximateLevel && Math.Abs(candle.Close - maxPrice) / InstrumentInfo.TickSize <= ApproximationFilter)
				{
					_lastApproximateLevel = maxPrice;
					AddAlert(AlertFile, InstrumentInfo.Instrument, $"Approximate to max Level {maxPrice}", AlertBgColor, AlertForeColor);
				}
			}

			if (UsePocTouchAlert && _lastPocAlert != i)
			{
				if (candle.Close >= _dynamicLevels[i] && _prevClose < _dynamicLevels[i]
				    ||
				    candle.Close <= _dynamicLevels[i] && _prevClose > _dynamicLevels[i])
					AddAlert(AlertFile, InstrumentInfo.Instrument, $"Price reached POC level: {_dynamicLevels[i]}", AlertBgColor, AlertForeColor);
			}

			if (UseValTouchAlert && _lastValAlert != i)
			{
				if (candle.Close >= _valueAreaBottom[i] && _prevClose < _valueAreaBottom[i]
				    ||
				    candle.Close <= _valueAreaBottom[i] && _prevClose > _valueAreaBottom[i])
					AddAlert(AlertFile, InstrumentInfo.Instrument, $"Price reached VAL level: {_valueAreaBottom[i]}", AlertBgColor, AlertForeColor);
			}

			if (UseVahTouchAlert && _lastVahAlert != i)
			{
				if (candle.Close >= _valueAreaTop[i] && _prevClose < _valueAreaTop[i]
				    ||
				    candle.Close <= _valueAreaTop[i] && _prevClose > _valueAreaTop[i])
					AddAlert(AlertFile, InstrumentInfo.Instrument, $"Price reached VAH level: {_valueAreaTop[i]}", AlertBgColor, AlertForeColor);
			}

			_prevClose = candle.Close;
		}

		private void CheckUpdatePeriod(int i)
		{
			if (_lastBar != i)
			{
				_lastBar = i;

				if (i > 0)
				{
					if (PeriodFrame == Period.Daily)
					{
						if (IsNewSession(i))
						{
							_closedCandle.Clear();
							_closedCandle.Type = Type;
						}
					}
					else if (PeriodFrame == Period.Weekly)
					{
						if (IsNewWeek(i))
						{
							_closedCandle.Clear();
							_closedCandle.Type = Type;
						}
					}
					else if (PeriodFrame == Period.Hourly)
					{
						if (GetCandle(i).Time.Hour != GetCandle(i - 1).Time.Hour)
						{
							_closedCandle.Clear();
							_closedCandle.Type = Type;
						}
					}
					else if (PeriodFrame == Period.H4)
					{
						if ((GetCandle(i).Time - _lastTime).TotalHours >= 4)
						{
							_lastTime = _lastTime.AddHours(4);
							_closedCandle.Clear();
							_closedCandle.Type = Type;
						}
					}
					else if (PeriodFrame == Period.Monthly)
					{
						if (IsNewMonth(i))
						{
							_closedCandle.Clear();
							_closedCandle.Type = Type;
						}
					}
				}
			}
		}

		#endregion
	}
}