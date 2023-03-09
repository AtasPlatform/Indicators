namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Color = System.Drawing.Color;

	[DisplayName("Squeeze Momentum")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/38307-squeeze-momentum")]
	public class SqueezeMomentum : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _dotsSeries = new("Dots")
		{
			Color = Colors.Gray,
			VisualType = VisualMode.Dots,
			Width = 2,
			Digits = 6,
			ShowTooltip = false,
			ShowZeroValue = true,
			UseMinimizedModeIfEnabled = true,
			IsHidden = true,
			IgnoredByAlerts = true
		};
		
		private readonly Highest _highest = new() { Period = 20 };
		private readonly LinearReg _linRegr = new() { Period = 20 };
		private readonly ValueDataSeries _renderSeries = new(Resources.Values)
		{
			VisualType = VisualMode.Histogram,
            Digits = 6,
            ShowZeroValue = false,
            UseMinimizedModeIfEnabled = true
		};
		
		private readonly Lowest _lowest = new() { Period = 20 };
		private readonly SMA _smaBb = new() { Period = 20 };
		private readonly SMA _smaKc = new() { Period = 20 };
		private readonly SMA _smaKcRange = new() { Period = 20 };
		private readonly StdDev _stdDev = new() { Period = 20 };
		
		private decimal _bbMultFactor = 2.0m;
        private decimal _kcMultFactor = 1.5m;
        private bool _useTrueRange;
        private Color _upperColor = Color.Lime;
        private Color _upColor = Color.Green;
        private Color _lowColor = Color.DarkRed;
        private Color _lowerColor = Color.Red;
        private Color _nullColor = Color.Blue;
        private Color _falseColor = Color.Gray;
        private Color _trueColor = Color.Black;

        #endregion

        #region Properties
		
        [Display(ResourceType = typeof(Resources), Name = "Upper", GroupName = "Drawing", Order = 610)]
        public System.Windows.Media.Color UpperColor
        {
	        get => _upperColor.Convert();
	        set
	        {
		        _upperColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Resources), Name = "Up", GroupName = "Drawing", Order = 620)]
        public System.Windows.Media.Color UpColor
        {
	        get => _upColor.Convert();
	        set
	        {
		        _upColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Resources), Name = "Low", GroupName = "Drawing", Order = 630)]
        public System.Windows.Media.Color LowColor
        {
	        get => _lowColor.Convert();
	        set
	        {
		        _lowColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Resources), Name = "Lower", GroupName = "Drawing", Order = 640)]
        public System.Windows.Media.Color LowerColor
        {
	        get => _lowerColor.Convert();
	        set
	        {
		        _lowerColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(Name = "Dots true", GroupName = "Drawing", Order = 650)]
        public System.Windows.Media.Color TrueColor
        {
	        get => _trueColor.Convert();
	        set
	        {
                _trueColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(Name = "Dots false", GroupName = "Drawing", Order = 660)]
        public System.Windows.Media.Color FalseColor
        {
	        get => _falseColor.Convert();
	        set
	        {
		        _falseColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(Name = "Dots null", GroupName = "Drawing", Order = 670)]
        public System.Windows.Media.Color NullColor
        {
	        get => _nullColor.Convert();
	        set
	        {
		        _nullColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Resources), Name = "BBPeriod", Order = 10)]
		[Range(1, 10000)]
		public int BBPeriod
		{
			get => _smaBb.Period;
			set
			{
				_smaBb.Period = value;
				_stdDev.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "BBMultFactor", Order = 11)]
		[Range(1, 10000)]
        public decimal BBMultFactor
		{
			get => _bbMultFactor;
			set
			{
				_bbMultFactor = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "KCPeriod", Order = 20)]
		[Range(1, 10000)]
        public int KCPeriod
		{
			get => _smaKc.Period;
			set
			{
				_smaKc.Period = value;
				_smaKcRange.Period = value;
				_linRegr.Period = value;
				_highest.Period = value;
				_lowest.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "KCMultFactor", Order = 21)]
		[Range(0.00000001, 100000000)]
        public decimal KCMultFactor
		{
			get => _kcMultFactor;
			set
			{
				_kcMultFactor = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "UseTrueRangeKc", Order = 22)]
		public bool UseTrueRange
		{
			get => _useTrueRange;
			set
			{
				_useTrueRange = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public SqueezeMomentum()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;

			DataSeries[0] = _renderSeries;
			DataSeries.Add(_dotsSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			var basis = _smaBb.Calculate(bar, candle.Close);
			var dev = BBMultFactor * _stdDev.Calculate(bar, candle.Close);

			var upperBB = basis + dev;
			var lowerBB = basis - dev;

			var ma = _smaKc.Calculate(bar, candle.Close);
			var range = candle.High - candle.Low;

			if (UseTrueRange && bar > 0)
			{
				var trueRange = Math.Max(range, Math.Abs(candle.High - GetCandle(bar - 1).Close));

				range = Math.Max(trueRange, Math.Abs(candle.Low - GetCandle(bar - 1).Close));
			}

			var rangeSma = _smaKcRange.Calculate(bar, range);

			var upperKC = ma + rangeSma * KCMultFactor;
			var lowerKC = ma - rangeSma * KCMultFactor;

			var sqzOn = lowerBB > lowerKC && upperBB < upperKC;
			var sqzOff = lowerBB < lowerKC && upperBB > upperKC;
			var noSqz = !sqzOff && !sqzOn;

			var high = _highest.Calculate(bar, candle.High);
			var low = _lowest.Calculate(bar, candle.Low);

			var val = _linRegr.Calculate(bar, candle.Close - ((high + low) / 2.0m + ma) / 2.0m);

			if (bar == 0)
				return;

			var lastValue = _renderSeries[bar - 1];

			_renderSeries[bar] = val;
			_renderSeries.Colors[bar] = val > 0
				? val > lastValue
					? _upperColor
					: _upColor
				: val < lastValue
					? _lowerColor
					: _lowColor;

			_dotsSeries[bar] = 0.000000001m;
			_dotsSeries.Colors[bar] = noSqz
				? _nullColor
				: sqzOn
					? _trueColor
					: _falseColor;
		}

		#endregion
	}
}