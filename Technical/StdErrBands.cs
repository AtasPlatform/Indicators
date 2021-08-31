namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Standard Error Bands")]
	[HelpLink("https://support.atas.net/ru/knowledge-bases/2/articles/45499-standard-error-bands")]
	public class StdErrBands : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _botSeries = new(Resources.BottomBand);
		private readonly LinearReg _linReg = new();
		private readonly SMA _sma = new();

		private readonly ValueDataSeries _topSeries = new(Resources.TopBand);
		private int _stdDev;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _sma.Period;
			set
			{
				if (value <= 0)
					return;

				_sma.Period = _linReg.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "StdDev", GroupName = "Settings", Order = 110)]
		public int StdDev
		{
			get => _stdDev;
			set
			{
				if (value <= 0)
					return;

				_stdDev = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public StdErrBands()
		{
			_sma.Period = _linReg.Period = 10;
			_stdDev = 1;
			_topSeries.Color = _botSeries.Color = Colors.DodgerBlue;
			DataSeries[0] = _topSeries;
			DataSeries.Add(_botSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_sma.Calculate(bar, value);
			_linReg.Calculate(bar, value);

			if (bar == 0)
				DataSeries.ForEach(x => x.Clear());

			if (bar < Period)
				return;

			var diffSum = 0m;
			var kSum = 0m;
			var kDiffSum = 0m;

			for (var i = bar - Period; i < bar; i++)
			{
				var diff = (decimal)SourceDataSeries[i] - _sma[i];
				diffSum += diff * diff;

				var k = i - (Period - 1) / 2m;
				kSum += k * k;

				kDiffSum += k * diff;
			}

			var sum = (double)((diffSum - kDiffSum * kDiffSum) / ((Period - 2) * kSum));

			var sqrt = Math.Round(Math.Sqrt(Math.Abs(sum)), 2);

			var se = (decimal)sqrt;
			_topSeries[bar] = decimal.Round(_linReg[bar] + _stdDev * se, 3);
			_botSeries[bar] = decimal.Round(_linReg[bar] - _stdDev * se, 3);
		}

		#endregion
	}
}