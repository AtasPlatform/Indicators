namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Sine-Wave Weighted Moving Average")]
	[FeatureId("NotReady")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45285-sine-wave-weighted-moving-average")]
	public class SWWMA : Indicator
	{
		#region Static and constants

		private const decimal _sinSum = 3.73205080757m;

		#endregion

		#region Fields

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);

		#endregion

		#region ctor

		public SWWMA()
		{
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar < 5)
				return;

			var valueSum = 0m;

			for (var i = 1; i <= 5; i++)
				valueSum += (decimal)Math.Sin(i * Math.PI / 6.0) * (decimal)SourceDataSeries[bar - i];

			_renderSeries[bar] = valueSum / _sinSum;
		}

		#endregion
	}
}