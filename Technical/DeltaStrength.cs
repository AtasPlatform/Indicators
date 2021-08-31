namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Delta Strength")]
	[HelpLink("https://support.atas.net/ru/knowledge-bases/2/articles/45992-delta-strength")]
	public class DeltaStrength : Indicator
	{
		#region Nested types

		public enum Filter
		{
			[Display(ResourceType = typeof(Resources), Name = "Bullish")]
			Bull,

			[Display(ResourceType = typeof(Resources), Name = "Bearlish")]
			Bear,

			[Display(ResourceType = typeof(Resources), Name = "Any")]
			All
		}

		#endregion

		#region Fields

		private Filter _negFilter;

		private ValueDataSeries _negSeries = new(Resources.Negative);
		private int _percentage;
		private Filter _posFilter;
		private ValueDataSeries _posSeries = new(Resources.Positive);

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Percent", GroupName = "Settings", Order = 100)]
		public int Percentage
		{
			get => _percentage;
			set
			{
				if (value is < 0 or > 100)
					return;

				_percentage = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "PositiveDelta", GroupName = "Filter", Order = 200)]
		public Filter PosFilter
		{
			get => _posFilter;
			set
			{
				_posFilter = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "NegativeDelta", GroupName = "Filter", Order = 210)]
		public Filter NegFilter
		{
			get => _negFilter;
			set
			{
				_negFilter = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DeltaStrength()
			: base(true)
		{
			DenyToChangePanel = true;
			_posFilter = _negFilter = Filter.All;
			_percentage = 98;
			_posSeries.Color = Colors.Green;
			_negSeries.Color = Colors.Red;
			_posSeries.VisualType = _negSeries.VisualType = VisualMode.Dots;
			_posSeries.Width = _negSeries.Width = 4;
			DataSeries[0] = _posSeries;
			DataSeries.Add(_negSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				DataSeries.ForEach(x => x.Clear());

			var candle = GetCandle(bar);

			if (candle.Delta < 0 && candle.MinDelta < 0 && candle.Delta <= candle.MinDelta * 0.01m * _percentage)
			{
				if (_negFilter == Filter.All
					|| _negFilter == Filter.Bull && candle.Close > candle.Open
					|| _negFilter == Filter.Bear && candle.Close < candle.Open)
					_negSeries[bar] = candle.High + 2 * InstrumentInfo.TickSize;
				else
					_negSeries[bar] = 0;
			}
			else
				_negSeries[bar] = 0;

			if (candle.Delta > 0 && candle.MaxDelta > 0 && candle.Delta >= candle.MaxDelta * 0.01m * _percentage)
			{
				if (_posFilter == Filter.All
					|| _posFilter == Filter.Bull && candle.Close > candle.Open
					|| _posFilter == Filter.Bear && candle.Close < candle.Open)
					_posSeries[bar] = candle.Low - 2 * InstrumentInfo.TickSize;
				else
					_posSeries[bar] = 0;
			}
			else
				_posSeries[bar] = 0;
		}

		#endregion
	}
}