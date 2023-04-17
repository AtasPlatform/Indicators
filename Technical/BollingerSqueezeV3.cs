namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;
    using OFT.Attributes;

	[DisplayName("Bollinger Squeeze 3")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45182-bollinger-squeeze-3")]
	public class BollingerSqueezeV3 : Indicator
	{
		#region Fields

		private readonly ATR _atr = new();

		private readonly StdDev _stdDev = new();
		private decimal _atrMultiplier;
		private decimal _stdMultiplier;

		private System.Drawing.Color _negColor = DefaultColors.Red;
		private System.Drawing.Color _posColor = DefaultColors.Green;

		private ValueDataSeries _renderSeries = new(Resources.Visualization)
		{
			VisualType = VisualMode.Histogram,
			ShowZeroValue = false,
			UseMinimizedModeIfEnabled = true
		};

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Resources), Name = "Positive", GroupName = "Drawing", Order = 610)]
        public System.Windows.Media.Color PosColor
        {
	        get => _posColor.Convert();
	        set
	        {
		        _posColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Resources), Name = "Negative", GroupName = "Drawing", Order = 620)]
        public System.Windows.Media.Color NegColor
        {
	        get => _negColor.Convert();
	        set
	        {
		        _negColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "ATR", Order = 100)]
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

		[Display(ResourceType = typeof(Resources), Name = "Multiplier", GroupName = "ATR", Order = 110)]
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

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "StdDev", Order = 200)]
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

		[Display(ResourceType = typeof(Resources), Name = "Multiplier", GroupName = "StdDev", Order = 210)]
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