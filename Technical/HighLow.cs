namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Highest High/Lowest Low Over N Bars")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45454-highest-highlowest-low-over-n-bars")]
	public class HighLow : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _highSeries = new("High");
		private readonly ValueDataSeries _lowSeries = new("Low");

		private readonly ValueDataSeries _maxSeries = new(Resources.Highest) { Color = DefaultColors.Green.Convert() };
        private readonly ValueDataSeries _minSeries = new(Resources.Lowest);
		private int _period = 15;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
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

		public HighLow()
			:base(true)
		{
			DenyToChangePanel = true;
			
			DataSeries[0] = _maxSeries;
			DataSeries.Add(_minSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			_highSeries[bar] = candle.High;
			_lowSeries[bar] = candle.Low;

			_maxSeries[bar] = _highSeries.MAX(_period, bar);
			_minSeries[bar] = _lowSeries.MIN(_period, bar);
		}

		#endregion
	}
}