namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
	using OFT.Localization;

	[DisplayName("Triangular Moving Average")]
	[HelpLink("https://support.atas.net/ru/knowledge-bases/2/articles/45292-triangular-moving-average")]
	public class TMA : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new(Strings.Visualization);
		private readonly ValueDataSeries _sma1 = new("Sma1");
		private readonly ValueDataSeries _sma2 = new("Sma2");
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

		public TMA()
		{
			_period = 10;
			_renderSeries.ShowZeroValue = false;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var n1 = (int)Math.Ceiling(_period / 2m);
			var n2 = _period % 2 == 1 ? n1 : n1 + 1;

			_sma1[bar] = DynamicSma(bar, n1, SourceDataSeries);
			_sma2[bar] = DynamicSma(bar, n2, _sma1);
			_renderSeries[bar] = _sma2[bar];
		}

		#endregion

		#region Private methods

		private decimal DynamicSma(int bar, int period, IDataSeries series)
		{
			var sum = 0m;

			for (var i = Math.Max(0, bar - period); i < bar; i++)
				sum += (decimal)series[i];

			return sum / period;
		}

		#endregion
	}
}