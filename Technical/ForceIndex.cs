namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Force Index")]
	public class ForceIndex : Indicator
	{
		#region Fields

		private readonly EMA _ema = new();

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private bool _useEma;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "UseMA", GroupName = "Settings", Order = 100)]
		public bool UseEma
		{
			get => _useEma;
			set
			{
				_useEma = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SMAPeriod", GroupName = "Settings", Order = 110)]
		public int Period
		{
			get => _ema.Period;
			set
			{
				if (value <= 0)
					return;

				_ema.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public ForceIndex()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_ema.Period = 10;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_ema.Calculate(bar, 0);

			if (bar == 0)
				return;

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			var force = candle.Volume * (candle.Close - prevCandle.Close);

			if (_useEma)
				_renderSeries[bar] = _ema.Calculate(bar, force);
			else
				_renderSeries[bar] = force;
		}

		#endregion
	}
}