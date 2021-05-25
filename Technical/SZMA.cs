namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Simple Moving Average - Skip Zeros")]
	[FeatureId("NotReady")]
	public class SZMA : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private int _period;

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

		public SZMA()
		{
			_period = 10;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var sum = 0m;
			var nonZeroValues = 0;

			for (var i = Math.Max(0, bar - _period); i <= bar; i++)
			{
				if ((decimal)SourceDataSeries[i] == 0)
					continue;

				sum += (decimal)SourceDataSeries[i];
				nonZeroValues++;
			}

			_renderSeries[bar] = 0;

			if (nonZeroValues != 0)
				_renderSeries[bar] = sum / nonZeroValues;
		}

		#endregion
	}
}