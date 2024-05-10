namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Drawing;

	using OFT.Attributes;
    using OFT.Localization;
    using Color = System.Drawing.Color;

#if CROSS_PLATFORM
    using CrossColor = System.Drawing.Color;
#else
    using CrossColor = System.Windows.Media.Color;
#endif

    [DisplayName("Squeeze Momentum")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.SqueezeMomentumDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602637")]
	public class SqueezeMomentum : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _dotsSeries = new("DotsSeries", "Dots")
		{
			Color = Color.Gray.Convert(),
			VisualType = VisualMode.Dots,
			Width = 2,
			Digits = 6,
			ShowTooltip = false,
			ShowZeroValue = true,
			UseMinimizedModeIfEnabled = true,
			IsHidden = true,
			IgnoredByAlerts = true
		};
		
		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Values)
		{
			VisualType = VisualMode.Histogram,
            Digits = 6,
            ShowZeroValue = false,
            UseMinimizedModeIfEnabled = true
		};

        private readonly Highest _highest = new() { Period = 20 };
        private readonly LinearReg _linRegr = new() { Period = 20 };
        private readonly Lowest _lowest = new() { Period = 20 };
		private readonly SMA _smaBb = new() { Period = 20 };
		private readonly SMA _smaKc = new() { Period = 20 };
		private readonly SMA _smaKcRange = new() { Period = 20 };
		private readonly StdDev _stdDev = new() { Period = 20 };
		
		private decimal _bbMultFactor = 2.0m;
        private decimal _kcMultFactor = 1.5m;
        private bool _useTrueRange;
        private Color _upperColor = DefaultColors.Lime;
        private Color _upColor = DefaultColors.Green;
        private Color _lowColor = DefaultColors.DarkRed;
        private Color _lowerColor = DefaultColors.Red;
        private Color _nullColor = DefaultColors.Blue;
        private Color _falseColor = DefaultColors.Gray;
        private Color _trueColor = Color.Black;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BBPeriod), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription))]
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

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BBMultFactor), GroupName = nameof(Strings.Settings), Description = nameof(Strings.MultiplierDescription))]
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

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.KCPeriod), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription))]
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

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.KCMultFactor), GroupName = nameof(Strings.Settings), Description = nameof(Strings.MultiplierDescription))]
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

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.UseTrueRangeKc), GroupName = nameof(Strings.Settings), Description = nameof(Strings.UseTrueRangeDescription))]
        public bool UseTrueRange
        {
            get => _useTrueRange;
            set
            {
                _useTrueRange = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Upper), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.UpperPositiveValueColorDescription), Order = 610)]
        public CrossColor UpperColor
        {
	        get => _upperColor.Convert();
	        set
	        {
		        _upperColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Up), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.PositiveValueColorDescription), Order = 620)]
        public CrossColor UpColor
        {
	        get => _upColor.Convert();
	        set
	        {
		        _upColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Low), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.NegativeValueColorDescription), Order = 630)]
        public CrossColor LowColor
        {
	        get => _lowColor.Convert();
	        set
	        {
		        _lowColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Lower), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.LowerNegativeValueColorDescription), Order = 640)]
        public CrossColor LowerColor
        {
	        get => _lowerColor.Convert();
	        set
	        {
		        _lowerColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(Name = "Dots True", GroupName = nameof(Strings.Drawing), Description = nameof(Strings.ColorDescription), Order = 650)]
        public CrossColor TrueColor
        {
	        get => _trueColor.Convert();
	        set
	        {
                _trueColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(Name = "Dots False", GroupName = nameof(Strings.Drawing), Description = nameof(Strings.ColorDescription), Order = 660)]
        public CrossColor FalseColor
        {
	        get => _falseColor.Convert();
	        set
	        {
		        _falseColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(Name = "Dots Null", GroupName = nameof(Strings.Drawing), Description = nameof(Strings.ColorDescription), Order = 670)]
        public CrossColor NullColor
        {
	        get => _nullColor.Convert();
	        set
	        {
		        _nullColor = value.Convert();
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