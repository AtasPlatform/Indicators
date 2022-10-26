namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Cumulative Adjusted Value")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45492-cumulative-adjusted-value")]
	public class CAV : Indicator
	{
		#region Fields

		private readonly EMA _ema = new()
		{
			Period = 10
		};

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _ema.Period;
			set
			{
				_ema.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public CAV()
		{
			Panel = IndicatorDataProvider.NewPanel;
			LineSeries.Add(new LineSeries(Resources.ZeroValue) { Color = Colors.Gray, Value = 0 });
			
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var adjVal = value - _ema.Calculate(bar, value);

			if (bar == 0)
			{
				_renderSeries[bar] = adjVal;
				return;
			}

			_renderSeries[bar] = _renderSeries[bar - 1] + adjVal;
		}

		#endregion
	}
}