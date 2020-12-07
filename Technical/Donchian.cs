namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Donchian Channel")]
	[Description("Donchian Channel")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/21698-donchian-channel")]
	public class Donchian : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _average = new ValueDataSeries("Average");

		private readonly ValueDataSeries _highSeries = new ValueDataSeries("High");
		private readonly ValueDataSeries _lowSeries = new ValueDataSeries("Low");
		private int _period = 20;
		private bool _showAverage;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "ShowAverage")]
		public bool ShowAverage
		{
			get => _showAverage;
			set
			{
				_showAverage = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Period")]
		public int Period
		{
			get => _period;
			set
			{
				if (value <= 0)
					return;

				_period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Donchian()
			: base(true)
		{
			_highSeries.Width = 1;
			_highSeries.Color = Colors.Red;
			_lowSeries.Width = 1;
			_lowSeries.Color = Colors.Green;
			_average.Width = 1;
			_average.Color = Colors.Blue;
			DataSeries[0].IsHidden = true;
			DataSeries.Add(_highSeries);
			DataSeries.Add(_lowSeries);
			DataSeries.Add(_average);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var high = decimal.MinValue;
			var low = decimal.MaxValue;

			for (var i = bar; i >= bar - (bar < _period ? bar : _period); i--)
			{
				var candle = GetCandle(i);
				var cHigh = candle.High != 0 ? candle.High : Math.Max(candle.Open, candle.Close);
				var cLow = candle.Low != 0 ? candle.Low : Math.Min(candle.Open, candle.Close);

				if (cHigh > high)
					high = cHigh;

				if (cLow < low)
					low = cLow;
			}

			_highSeries[bar] = high;
			_lowSeries[bar] = low;

			if (_showAverage)
				_average[bar] = (high + low) / 2;
		}

		#endregion
	}
}