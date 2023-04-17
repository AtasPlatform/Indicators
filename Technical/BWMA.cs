namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Bill Williams Moving Average")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/43438-bill-williams-moving-average")]
	public class BWMA : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private int _period = 10;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _period;
			set
			{
				_period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public BWMA()
		{
			_renderSeries.Color = DefaultColors.Blue.Convert();
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