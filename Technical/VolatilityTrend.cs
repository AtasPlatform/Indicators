namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Volatility Trend")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45449-volatility-trend")]
	public class VolatilityTrend : Indicator
	{
		#region Fields

		private readonly ATR _atr = new() { Period = 10 };
		private readonly ValueDataSeries _dirSeries = new("Dir");
		private readonly ValueDataSeries _dplSeries = new("DPL");
		private int _maxDynamicPeriod = 15;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
        public int Period
		{
			get => _atr.Period;
			set
			{
				_atr.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MaxDynamicPeriod", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
        public int MaxDynamicPeriod
		{
			get => _maxDynamicPeriod;
			set
			{
				_maxDynamicPeriod = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public VolatilityTrend()
		{
			DenyToChangePanel = true;
			Add(_atr);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				Clear();
				_dplSeries.Clear();
				return;
			}

			_dirSeries[bar] = value > this[bar - 1] ? 1 : -1;

			var dynamicPeriod = _dplSeries[bar - 1];
			var dynamicPeriod2 = _dirSeries[bar] == _dirSeries[bar - 1] ? dynamicPeriod : 0;
			var dynamicPeriod3 = dynamicPeriod2 < _maxDynamicPeriod ? dynamicPeriod2 + 1 : dynamicPeriod2;

			_dplSeries[bar] = dynamicPeriod3;

			if (_dirSeries[bar] == 1)
			{
				var max = SourceDataSeries.MAX((int)dynamicPeriod3, bar);
				this[bar] = max - _atr[bar];
			}
			else
			{
				var min = SourceDataSeries.MIN((int)dynamicPeriod3, bar);
				this[bar] = min - _atr[bar];
			}
		}

		#endregion
	}
}