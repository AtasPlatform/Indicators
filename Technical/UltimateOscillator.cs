namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Ultimate Oscillator")]
	public class UltimateOscillator : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _closeLowSeries = new ValueDataSeries("CloseLow");
		private readonly ValueDataSeries _highLowSeries = new ValueDataSeries("HighLow");
		private readonly ValueDataSeries _renderSeries = new ValueDataSeries(Resources.Visualization);
		private int _period1;
		private int _period2;

		private int _period3;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period1", GroupName = "Settings", Order = 100)]
		public int Period1
		{
			get => _period1;
			set
			{
				if (value <= 0)
					return;

				_period1 = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Period2", GroupName = "Settings", Order = 110)]
		public int Period2
		{
			get => _period2;
			set
			{
				if (value <= 0)
					return;

				_period2 = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Period3", GroupName = "Settings", Order = 120)]
		public int Period3
		{
			get => _period3;
			set
			{
				if (value <= 0)
					return;

				_period3 = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public UltimateOscillator()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;

			_period1 = 5;
			_period2 = 10;
			_period3 = 15;

			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				return;
			
			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			var th = candle.High > prevCandle.Close || bar == 0
				? candle.High
				: prevCandle.Close;

			var tl = candle.Low < prevCandle.Close || bar == 0
				? candle.Low
				: prevCandle.Close;

			_closeLowSeries[bar] = candle.Close - tl;
			_highLowSeries[bar] = th - tl;

			var closeLow1 = _closeLowSeries.CalcSum(_period1, bar);
			var highLow1 = _highLowSeries.CalcSum(_period1, bar);

			var closeLow2 = _closeLowSeries.CalcSum(_period2, bar);
			var highLow2 = _highLowSeries.CalcSum(_period2, bar);

			var closeLow3 = _closeLowSeries.CalcSum(_period3, bar);
			var highLow3 = _highLowSeries.CalcSum(_period3, bar);

			_renderSeries[bar] = 100 / 7m * (4 * closeLow1 / highLow1 + 2 * closeLow2 / highLow2 + closeLow3 / highLow3);
		}

		#endregion
	}
}