namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("DeMarker")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/43365-demarker")]
	public class DeMarker : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private readonly SMA _smaMax = new() { Period = 10 };
		private readonly SMA _smaMin = new() { Period = 10 };

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _smaMax.Period;
			set
			{
				_smaMax.Period = _smaMin.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DeMarker()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_smaMax.Calculate(bar, 0);
				_smaMin.Calculate(bar, 0);
				return;
			}

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			var deMax = Math.Max(0, candle.High - prevCandle.High);
			var deMin = Math.Min(0, prevCandle.Low - candle.Low);

			_smaMax.Calculate(bar, deMax);
			_smaMin.Calculate(bar, deMin);

			if (_smaMax[bar] + _smaMin[bar] != 0)
				_renderSeries[bar] = _smaMax[bar] / (_smaMax[bar] + _smaMin[bar]);
			else
				_renderSeries[bar] = _renderSeries[bar - 1];
		}

		#endregion
	}
}