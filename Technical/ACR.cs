namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/53398-average-candle-range")]
	[DisplayName("Average Candle Range")]
	public class ACR : Indicator
	{
		#region Fields
		
		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private readonly ObjectDataSeries _rangeSeries = new("Ranges");
		private bool _ignoreWicks;
		private int _lastSession;

		#endregion

		[Display(ResourceType = typeof(Resources), Name = "IgnoreWicks", GroupName = "Settings", Order = 100)]
		public bool IgnoreWicks
		{
			get => _ignoreWicks;
			set
			{
				_ignoreWicks = value;
				RecalculateValues();
			}
		}

		#region ctor

		public ACR()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DenyToChangePanel = true;

			_renderSeries.VisualType = VisualMode.Histogram;

			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_renderSeries.Clear();
				_rangeSeries.Clear();
				_lastSession = 0;
			}

			if (IsNewSession(bar) && bar > 0)
				_lastSession = bar;

			var candle = GetCandle(bar);

			var range = IgnoreWicks
				? Math.Abs(candle.Open - candle.Close)
				: candle.High - candle.Low;

			_rangeSeries[bar] = range;

			_renderSeries[bar] = _rangeSeries.CalcAverage(bar - _lastSession + 1, bar);
		}

		#endregion
	}
}