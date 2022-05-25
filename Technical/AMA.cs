namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Adaptive Moving Average")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45283-adaptive-moving-average")]
	public class AMA : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _diffSeries = new("Diff");

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private decimal _fastConstant;
		private int _period;
		private decimal _slowConstant;

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

		[Display(ResourceType = typeof(Resources), Name = "FastConst", GroupName = "Settings", Order = 110)]
		public decimal FastConstant
		{
			get => _fastConstant;
			set
			{
				if (value <= 0)
					return;

				_fastConstant = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SlowConst", GroupName = "Settings", Order = 110)]
		public decimal SlowConstant
		{
			get => _slowConstant;
			set
			{
				if (value <= 0)
					return;

				_slowConstant = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public AMA()
		{
			_period = 15;
			_fastConstant = 3;
			_slowConstant = 20;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				return;

			_diffSeries[bar] = Math.Abs(value - (decimal)SourceDataSeries[bar - 1]);

			if (bar < _period)
				return;

			var dir = value - (decimal)SourceDataSeries[bar - _period];
			var vol = _diffSeries.CalcSum(_period, bar);
			vol = vol == 0 ? 0.000001m : vol;
			var c = Math.Abs(dir / vol) * (2 / (_fastConstant + 1) - 2 / (_slowConstant + 1)) + 2 / (_slowConstant + 1);
			c = c * c;

			if (_renderSeries[bar - 1] != 0)
				_renderSeries[bar] = _renderSeries[bar - 1] + c * (value - _renderSeries[bar - 1]);
			else
				_renderSeries[bar] = (decimal)SourceDataSeries[bar - 1] + c * (value - (decimal)SourceDataSeries[bar - 1]);
		}

		#endregion
	}
}