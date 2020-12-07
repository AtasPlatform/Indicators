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
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/380-dynamic-levels")]
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

			private readonly Dictionary<decimal, PriceInfo> _allPrice = new Dictionary<decimal, PriceInfo>();
			private decimal _cachedVAH;
			private decimal _cachedVAL;

			private decimal _cachedVol;

			private decimal _maxPrice;

			private PriceInfo _maxPriceInfo = new PriceInfo();

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

			public void AddCandle(IndicatorCandle candle, decimal ticksize)
			{
				if (Open == 0)
					Open = candle.Open;
				Close = candle.Close;

				for (var price = candle.High; price >= candle.Low; price -= ticksize)
				{
					var info = candle.GetPriceVolumeInfo(price);
					if (info == null)
						continue;

					if (price > High)
						High = price;

					if (price < Low || Low == 0)
						Low = price;

					Volume += info.Volume;

					PriceInfo priceInfo;
					if (!_allPrice.TryGetValue(price, out priceInfo))
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

				PriceInfo priceInfo;
				if (!_allPrice.TryGetValue(price, out priceInfo))
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

			public decimal[] GetValueArea(decimal ticksize, int valueAreaPercent)
			{
				if (Volume == _cachedVol)
					return new[] { _cachedVAH, _cachedVAL };

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
						var upperprice = vah;
						var lowerPrice = val;
						var c = 2;

						for (var i = 0; i <= c; i++)
						{
							if (High >= upperprice + ticksize)
							{
								upperprice += ticksize;
								PriceInfo info;
								if (_allPrice.TryGetValue(upperprice, out info))
									upperVol += info.Volume;
							}
							else
								break;
						}

						for (var i = 0; i <= c; i++)
						{
							if (Low <= lowerPrice - ticksize)
							{
								lowerPrice -= ticksize;
								PriceInfo info;
								if (_allPrice.TryGetValue(lowerPrice, out info))
									lowerVol += info.Volume;
							}
							else
								break;
						}

						if (upperVol == lowerVol && upperVol == 0)
						{
							vah = Math.Min(upperprice, High);
							val = Math.Max(lowerPrice, Low);
						}
						else if (upperVol >= lowerVol)
						{
							vah = upperprice;
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
				_cachedVAH = vah;
				_cachedVAL = val;
				return new[] { vah, val };
			}

			public void Clear()
			{
				_allPrice.Clear();
				MaxValue = High = Low = Volume = _cachedVol = _cachedVAH = _cachedVAL = _maxPrice = 0;
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
		public enum VolumeVizualizationType
		{
			[Display(ResourceType = typeof(Resources), Name = "AtStart")]
			AtStart,

			[Display(ResourceType = typeof(Resources), Name = "Accumulated")]
			Accumulated
		}

		#endregion

		#region Fields

		private readonly DynamicCandle _closedcandle = new DynamicCandle();
		private readonly ValueDataSeries _dynamicLevels;
		private readonly object _syncRoot = new object();

		private readonly RangeDataSeries _valueArea = new RangeDataSeries("Value area") { RangeColor = Color.FromArgb(30, 128, 0, 2) };
		private readonly ValueDataSeries _valueAreaBottom = new ValueDataSeries("Value Area 2nd line") { Color = Colors.Maroon };
		private readonly ValueDataSeries _valueAreaTop = new ValueDataSeries("Value Area 1st line") { Color = Colors.Maroon };
		private decimal _lastAproximateLevel;
		private int _lastbar = -1;
		private int _lastcalculatedBar;

		private DrawingText _lastLabel;
		private decimal _lastvalue;

		private bool _showVolumes = true;
		private bool _tickBasedCalculation;
		private VolumeVizualizationType _vizualizationType = VolumeVizualizationType.Accumulated;
		private decimal filter;
		private int lastalertBar = -1;
		private Period per = Period.Daily;

		private MiddleClusterType type = MiddleClusterType.Volume;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Type", GroupName = "Filters")]
		public MiddleClusterType Type
		{
			get => type;
			set
			{
				type = value;
				_closedcandle.Type = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Filters")]
		public Period period
		{
			get => per;
			set
			{
				per = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Filter", GroupName = "Filters")]
		public decimal Filter
		{
			get => filter;
			set
			{
				filter = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ShowVolume", GroupName = "Other")]
		public bool ShowVolumes
		{
			get => _showVolumes;
			set
			{
				_showVolumes = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "VolumeVisualizationType", GroupName = "Other")]
		public VolumeVizualizationType VizualizationType
		{
			get => _vizualizationType;
			set
			{
				_vizualizationType = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ApproximationAlert", GroupName = "Alerts")]
		public bool UseApproximationAlert { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "ApproximationFilter", GroupName = "Alerts")]
		public int ApproximationFilter { get; set; } = 3;

		[Display(ResourceType = typeof(Resources), Name = "ChangingLevelAlert", GroupName = "Alerts")]
		public bool UseAlerts { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "AlertFile", GroupName = "Alerts")]
		public string AlertFile { get; set; } = "alert1";

		[Display(ResourceType = typeof(Resources), Name = "FontColor", GroupName = "Alerts")]
		public Color AlertForeColor { get; set; } = Color.FromArgb(255, 247, 249, 249);

		[Display(ResourceType = typeof(Resources), Name = "BackGround", GroupName = "Alerts")]
		public Color AlertBGColor { get; set; } = Color.FromArgb(255, 75, 72, 72);

		#endregion

		#region ctor

		public DynamicLevels()
			: base(true)
		{
			DenyToChangePanel = true;
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
				_lastcalculatedBar = -1;
				_lastbar = -1;
				_closedcandle.Clear();
			}
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			lock (_syncRoot)
			{
				CheckUpdatePeriod(bar);

				if (_tickBasedCalculation)
					return;

				_closedcandle.AddCandle(GetCandle(bar), InstrumentInfo.TickSize);
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

					_closedcandle.AddTick(arg);

					for (var i = _lastcalculatedBar; i <= CurrentBar - 1; i++)
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
			_lastcalculatedBar = i;

			var maxprice = _closedcandle.MaxValuePrice;
			var value = _closedcandle.MaxValue;
			var valuestring = "";

			valuestring = value.ToString();
			if (Type == MiddleClusterType.Delta)
				valuestring = _closedcandle.TrueMaxValue.ToString();

			_dynamicLevels[i] = value > Filter ? maxprice : 0;

			if (i == 0)
			{
				var close = GetCandle(0).Close;
				this[0] = _valueAreaTop[0] = _valueAreaBottom[0] = close;
				_valueArea[0] = new RangeValue
					{ Lower = close, Upper = close };

				return;
			}

			var prevPrice = this[i - 1];

			if (prevPrice > 0.0001m && Math.Abs(prevPrice - maxprice) > InstrumentInfo.TickSize / 2 && value > Filter)
			{
				if (ShowVolumes)
				{
					var cl = System.Drawing.Color.FromArgb(_dynamicLevels.Color.A, _dynamicLevels.Color.R, _dynamicLevels.Color.G, _dynamicLevels.Color.B);
					_lastLabel = AddText(i.ToString(CultureInfo.InvariantCulture),
						valuestring, prevPrice < maxprice,
						i, maxprice, 0, 0, System.Drawing.Color.Black,
						System.Drawing.Color.Black, cl, 10, DrawingText.TextAlign.Left);
				}

				if (UseAlerts && i > lastalertBar && i == CurrentBar - 1)
				{
					lastalertBar = i;
					AddAlert(AlertFile, InstrumentInfo.Instrument, $"Changed max level to {maxprice}", AlertBGColor, AlertForeColor);
				}
			}
			else
			{
				if (ShowVolumes)
				{
					if (VizualizationType == VolumeVizualizationType.Accumulated)
					{
						if (_lastLabel != null && value != _lastvalue)
						{
							_lastLabel.Text = value.ToString(CultureInfo.InvariantCulture);
							_lastvalue = value;
						}
					}
				}
			}

			var va = _closedcandle.GetValueArea(InstrumentInfo.TickSize, ValueAreaPercent);

			_valueArea[i].Upper = va[0];
			_valueArea[i].Lower = va[1];
			_valueAreaTop[i] = va[0];
			_valueAreaBottom[i] = va[1];

			if (UseApproximationAlert && i == CurrentBar - 1)
			{
				var candle = GetCandle(i);

				if (maxprice != _lastAproximateLevel && Math.Abs(candle.Close - maxprice) / InstrumentInfo.TickSize <= ApproximationFilter)
				{
					_lastAproximateLevel = maxprice;
					AddAlert(AlertFile, InstrumentInfo.Instrument, $"Approximate to max Level {maxprice}", AlertBGColor, AlertForeColor);
				}
			}
		}

		private void CheckUpdatePeriod(int i)
		{
			if (_lastbar != i)
			{
				_lastbar = i;

				if (i > 0)
				{
					if (period == Period.Daily)
					{
						if (IsNewSession(i))
						{
							_closedcandle.Clear();
							_closedcandle.Type = Type;
						}
					}
					else if (period == Period.Weekly)
					{
						if (IsNewWeek(i))
						{
							_closedcandle.Clear();
							_closedcandle.Type = Type;
						}
					}
					else if (period == Period.Hourly)
					{
						if (GetCandle(i).Time.Hour != GetCandle(i - 1).Time.Hour)
						{
							_closedcandle.Clear();
							_closedcandle.Type = Type;
						}
					}
					else if (period == Period.Monthly)
					{
						if (IsNewMonth(i))
						{
							_closedcandle.Clear();
							_closedcandle.Type = Type;
						}
					}
				}
			}
		}

		#endregion
	}
}