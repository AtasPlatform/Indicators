namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Swing High and Low")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45337-swing-high-and-low")]
	public class SwingHighLow : Indicator
	{
		#region Fields

		private readonly Highest _highest = new();
		private readonly Lowest _lowest = new();

		private readonly ValueDataSeries _shSeries = new(Resources.Highest);
		private readonly ValueDataSeries _slSeries = new(Resources.Lowest);
		private bool _includeEqual;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _highest.Period;
			set
			{
				if (value <= 0)
					return;

				_highest.Period = _lowest.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "IncludeEqualHighLow", GroupName = "Settings", Order = 110)]
		public bool IncludeEqual
		{
			get => _includeEqual;
			set
			{
				_includeEqual = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public SwingHighLow() 
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_highest.Period = _lowest.Period = 10;

			_shSeries.Color = Colors.Green;
			_slSeries.Color = Colors.Red;

			_includeEqual = true;

			DataSeries[0] = _shSeries;
			DataSeries.Add(_slSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				DataSeries.ForEach(x => x.Clear());

			var candle = GetCandle(bar);
			_highest.Calculate(bar, candle.High);
			_lowest.Calculate(bar, candle.Low);
			
			if (bar < Period * 2)
				return;

			var calcBar = bar - Period;
			var calcCandle = GetCandle(calcBar);

			if (_includeEqual)
			{
				if (calcCandle.High < (decimal)_highest.DataSeries[0][bar - Period - 1]
					||
					calcCandle.High < (decimal)_highest.DataSeries[0][bar])
					_shSeries[calcBar] = 0;
				else
					_shSeries[calcBar] = 1;

				if (calcCandle.Low > (decimal)_lowest.DataSeries[0][bar - Period - 1]
					||
					calcCandle.Low > (decimal)_lowest.DataSeries[0][bar])
					_slSeries[calcBar] = 0;
				else
					_slSeries[calcBar] = 1;
			}
			else
			{
				if (calcCandle.High <= (decimal)_highest.DataSeries[0][bar - Period - 1]
					||
					calcCandle.High <= (decimal)_highest.DataSeries[0][bar])
					_shSeries[calcBar] = 0;
				else
					_shSeries[calcBar] = 1;

				if (calcCandle.Low >= (decimal)_lowest.DataSeries[0][bar - Period - 1]
					||
					calcCandle.Low >= (decimal)_lowest.DataSeries[0][bar])
					_slSeries[calcBar] = 0;
				else
					_slSeries[calcBar] = 1;
			}
		}

		#endregion
	}
}