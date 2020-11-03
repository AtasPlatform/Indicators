namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("VSA – WSD Histogram")]
	public class VsaWsd : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _avgVolume = new ValueDataSeries("AvgVolume");

		private readonly ValueDataSeries _dotsBuy = new ValueDataSeries("DotsBuy");
		private readonly ValueDataSeries _dotsNeutral = new ValueDataSeries("DotsNeutral");
		private readonly ValueDataSeries _dotsSell = new ValueDataSeries("DotsSell");

		private readonly EMA _ema = new EMA();
		private readonly ValueDataSeries _highLow = new ValueDataSeries("HighLow");
		private readonly ValueDataSeries _lowerWick = new ValueDataSeries("LowerWick");
		private readonly ValueDataSeries _upperWick = new ValueDataSeries("UpperWick");

		private decimal _tickSize;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Common", Order = 1)]
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

		public VsaWsd()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;

			_ema.Period = 100;

			_upperWick.Color = _dotsBuy.Color = Colors.Lime;
			_lowerWick.Color = _dotsSell.Color = Colors.Red;
			_highLow.Color = Colors.Blue;
			_avgVolume.Color = Colors.Goldenrod;
			_dotsNeutral.Color = Colors.Gray;

			_upperWick.VisualType = VisualMode.Histogram;
			_lowerWick.VisualType = VisualMode.Histogram;
			_highLow.VisualType = VisualMode.Histogram;
			_upperWick.Width = _lowerWick.Width = _highLow.Width = 2;
			_avgVolume.LineDashStyle = LineDashStyle.Dash;
			_dotsSell.LineDashStyle = _dotsBuy.LineDashStyle = _dotsNeutral.LineDashStyle = LineDashStyle.Dot;
			_dotsSell.VisualType = _dotsBuy.VisualType = _dotsNeutral.VisualType = VisualMode.Dots;
			_dotsSell.ShowZeroValue = _dotsBuy.ShowZeroValue = _dotsNeutral.ShowZeroValue = false;
			_dotsSell.Width = _dotsBuy.Width = _dotsNeutral.Width = 5;

			DataSeries[0] = _highLow;
			DataSeries.Add(_upperWick);
			DataSeries.Add(_lowerWick);
			DataSeries.Add(_avgVolume);
			DataSeries.Add(_dotsBuy);
			DataSeries.Add(_dotsSell);
			DataSeries.Add(_dotsNeutral);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_dotsBuy.Clear();
				_dotsSell.Clear();
				_dotsNeutral.Clear();
				_tickSize = ChartInfo.PriceChartContainer.Step;
			}

			var candle = GetCandle(bar);
			var result = candle.High - candle.Low;

			_highLow[bar] = result / _tickSize;

			decimal dResult1 = 0.0m, dResult2 = 0.0m;

			if (candle.Open > candle.Close)
				dResult1 = candle.High - candle.Open;
			else
				dResult1 = candle.High - candle.Close;

			if (candle.Open > candle.Close)
				dResult2 = candle.Low - candle.Close;
			else
				dResult2 = candle.Low - candle.Open;

			_upperWick[bar] = dResult1 / _tickSize;
			_lowerWick[bar] = dResult2 / _tickSize;

			var Volume = (candle.High - candle.Low) / _tickSize;
			_avgVolume[bar] = _ema.Calculate(bar, Volume);

			if (bar == 0)
				return;

			var prevCandle = GetCandle(bar - 1);

			if (candle.Close > prevCandle.Open && _highLow[bar] < _highLow[bar - 1])
			{
				_dotsBuy[bar] = _highLow[bar];
				_dotsSell[bar] = _dotsNeutral[bar] = 0;
			}
			else if (candle.Close < prevCandle.Open && _highLow[bar] < _highLow[bar - 1])
			{
				_dotsSell[bar] = _highLow[bar];
				_dotsBuy[bar] = _dotsNeutral[bar] = 0;
			}
			else
			{
				_dotsNeutral[bar] = _highLow[bar];
				_dotsBuy[bar] = _dotsSell[bar] = 0;
			}
		}

		#endregion
	}
}