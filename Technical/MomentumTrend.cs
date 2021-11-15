namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Momentum Trend")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45299-momentum-trend")]
	public class MomentumTrend : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _downSeries = new(Resources.Down);
		private readonly Momentum _momentum = new();

		private readonly ValueDataSeries _upSeries = new(Resources.Up);

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 20)]
		public int Period
		{
			get => _momentum.Period;
			set
			{
				if (value <= 0)
					return;

				_momentum.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public MomentumTrend()
		{
			DenyToChangePanel = true;

			_momentum.Period = 10;

			_upSeries.VisualType = _downSeries.VisualType = VisualMode.Dots;
			_upSeries.Width = _downSeries.Width = 3;
			_upSeries.Color = Colors.Green;
			_downSeries.Color = Colors.Red;
			DataSeries[0] = _upSeries;
			DataSeries.Add(_downSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_momentum.Calculate(bar, value);

			if (bar == 0)
			{
				DataSeries.ForEach(x => x.Clear());
				return;
			}

			var candle = GetCandle(bar);

			if (_momentum[bar] > _momentum[bar - 1])
				_upSeries[bar] = candle.High;
			else
				_downSeries[bar] = candle.Low;
		}

		#endregion
	}
}