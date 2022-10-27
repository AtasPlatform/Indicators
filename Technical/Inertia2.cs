namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Inertia V2")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45247-inertia-v2")]
	public class Inertia2 : Indicator
	{
		#region Fields

		private readonly LinearReg _linReg = new() { Period = 14 };

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private readonly RVI2 _rvi = new();
		private readonly StdDev _stdDev = new();
		private readonly ValueDataSeries _stdDown = new("StdDown");
		private readonly ValueDataSeries _stdUp = new("StdUp");
		private int _rviPeriod = 10;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Resources), Name = "RVI", GroupName = "Period", Order = 100)]
		[Range(1, 10000)]
        public int RviPeriod
		{
			get => _rviPeriod;
			set
			{
				_rviPeriod = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LinearReg", GroupName = "Period", Order = 110)]
		[Range(1, 10000)]
        public int LinearRegPeriod
		{
			get => _linReg.Period;
			set
			{
				_linReg.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "StdDev", GroupName = "Period", Order = 110)]
		[Range(1, 10000)]
        public int StdDevPeriod
		{
			get => _stdDev.Period;
			set
			{
				_stdDev.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Inertia2()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			
			Add(_rvi);
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			_stdDev.Calculate(bar, candle.Close);

			if (bar == 0)
			{
				_stdUp.Clear();
				_stdDown.Clear();
				_renderSeries.Clear();
				return;
			}

			var prevCandle = GetCandle(bar - 1);

			var rviUp = 0m;
			var rviDown = 0m;

			if (candle.Close > prevCandle.Close)
				rviUp = _stdDev[bar];
			else
				rviDown = _stdDev[bar];

			_stdUp[bar] = (_stdUp[bar - 1] * (_rviPeriod - 1) + rviUp) / _rviPeriod;
			_stdDown[bar] = (_stdDown[bar - 1] * (_rviPeriod - 1) + rviDown) / _rviPeriod;

			var rvix = 0m;

			if (_stdUp[bar] + _stdDown[bar] != 0)
				rvix = 100m * _stdUp[bar] / (_stdUp[bar] + _stdDown[bar]);

			_renderSeries[bar] = _linReg.Calculate(bar, rvix);
		}

		#endregion
	}
}