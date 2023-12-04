namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Inertia V2")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.InertiaV2IndDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602405")]
	public class Inertia2 : Indicator
	{
		#region Fields

		private readonly LinearReg _linReg = new() { Period = 14 };

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);
		private readonly RVI2 _rvi = new();
		private readonly StdDev _stdDev = new();
		private readonly ValueDataSeries _stdDown = new("StdDown");
		private readonly ValueDataSeries _stdUp = new("StdUp");
		private int _rviPeriod = 10;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.RVI), GroupName = nameof(Strings.Period), Description = nameof(Strings.RVIPeriodDescription), Order = 100)]
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

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LinearReg), GroupName = nameof(Strings.Period), Description = nameof(Strings.LinearRegPeriodDescription), Order = 110)]
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

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.StdDev), GroupName = nameof(Strings.Period), Description = nameof(Strings.StdDevPeriodDescription), Order = 120)]
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