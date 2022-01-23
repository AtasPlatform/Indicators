namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Delta Divergence")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/49348-delta-divergence")]
	public class DeltaDivergence : Indicator
	{
		#region Overrides of BaseIndicator

		private readonly ValueDataSeries _posSeries = new(Resources.Up);
		private readonly ValueDataSeries _negSeries = new(Resources.Down);
		private int _lastBar;

		public DeltaDivergence()
			: base(true)
		{
			DenyToChangePanel = true;

			_posSeries.Color = Colors.Green;
			_negSeries.Color = Colors.Red;

			_posSeries.VisualType = VisualMode.UpArrow;
			_negSeries.VisualType = VisualMode.DownArrow;

			DataSeries[0] = _posSeries;
			DataSeries.Add(_negSeries);
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar < 2)
				return;

			if(_lastBar == bar)
				return;

			_lastBar = bar;

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);
			var prev2Candle = GetCandle(bar - 2);

			if (prevCandle.Close - prevCandle.Open > 0
				&& prev2Candle.Close - prev2Candle.Open > 0
				&& candle.Close - candle.Open < 0
				&& candle.High >= prevCandle.High
				&& candle.Delta < 0)
				_negSeries[bar] = candle.High + InstrumentInfo.TickSize * 2;
			else
				_negSeries[bar] = 0;

			if (prevCandle.Close - prevCandle.Open < 0
				&& prev2Candle.Close - prev2Candle.Open < 0
				&& candle.Close - candle.Open > 0
				&& candle.Low <= prevCandle.Low
				&& candle.Delta > 0)
				_posSeries[bar] = candle.Low - InstrumentInfo.TickSize * 2;
			else
				_posSeries[bar] = 0;
		}

		#endregion
	}
}