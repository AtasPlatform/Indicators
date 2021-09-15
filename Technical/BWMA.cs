namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using OFT.Attributes;
	using OFT.Localization;

	[DisplayName("Bill Williams Moving Average")]
	[HelpLink("https://support.atas.net/ru/knowledge-bases/2/articles/43438-bill-williams-moving-average")]
	public class BWMA : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new(Strings.Visualization);
		private int _period;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Strings), Name = "Period", GroupName = "Settings", Order = 100)]
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

		public BWMA()
		{
			_period = 10;
			_renderSeries.Color = Colors.Blue;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_renderSeries.Clear();
				_renderSeries[bar] = value;
				return;
			}

			_renderSeries[bar] = (1m - 1m / _period) * _renderSeries[bar - 1] + value / _period;
		}

		#endregion
	}
}