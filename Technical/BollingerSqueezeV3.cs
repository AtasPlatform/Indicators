namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Drawing;
    using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Bollinger Squeeze 3")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.BollingerSqueezeV3Description))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602338")]
	public class BollingerSqueezeV3 : Indicator
	{
		#region Fields

		private readonly ATR _atr = new();

		private readonly StdDev _stdDev = new();
		private decimal _atrMultiplier;
		private decimal _stdMultiplier;

		private System.Drawing.Color _negColor = DefaultColors.Red;
		private System.Drawing.Color _posColor = DefaultColors.Green;

		private ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization)
		{
			VisualType = VisualMode.Histogram,
			ShowZeroValue = false,
			UseMinimizedModeIfEnabled = true
		};

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Positive), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.PositiveValueColorDescription), Order = 610)]
        public CrossColor PosColor
        {
	        get => _posColor.Convert();
	        set
	        {
		        _posColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Negative), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.NegativeValueColorDescription), Order = 620)]
        public CrossColor NegColor
        {
	        get => _negColor.Convert();
	        set
	        {
		        _negColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.ATR), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(1, 1000000)]
		public int AtrPeriod
		{
			get => _atr.Period;
			set
			{
				_atr.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Multiplier), GroupName = nameof(Strings.ATR), Description = nameof(Strings.ATRMultiplierDescription), Order = 110)]
		[Range(0.000001, 1000000)]
		public decimal AtrMultiplier
		{
			get => _atrMultiplier;
			set
			{
				_atrMultiplier = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.StdDev), Description = nameof(Strings.PeriodDescription), Order = 200)]
		[Range(1, 1000000)]
		public int StdDevPeriod
		{
			get => _stdDev.Period;
			set
			{
				_stdDev.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Multiplier), GroupName = nameof(Strings.StdDev), Description = nameof(Strings.MultiplierDescription), Order = 210)]
		[Range(0.000001, 1000000)]
		public decimal StdMultiplier
		{
			get => _stdMultiplier;
			set
			{
				_stdMultiplier = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public BollingerSqueezeV3()
		{
			Panel = IndicatorDataProvider.NewPanel;

			_atr.Period = 10;
			_stdDev.Period = 10;
			
			_stdMultiplier = 1;
			_atrMultiplier = 1;
			Add(_atr);

			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnRecalculate()
		{
			DataSeries.ForEach(x => x.Clear());
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			var ratio = 0m;
			var stdValue = _stdDev.Calculate(bar, value);

			if (_atr[bar] != 0)
				ratio = _stdMultiplier * stdValue / (_atrMultiplier * _atr[bar]);

			_renderSeries[bar] = ratio;
			_renderSeries.Colors[bar] = ratio >= 1 ? _posColor : _negColor;
		}

		#endregion
	}
}