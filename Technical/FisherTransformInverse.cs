namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Inverse Fisher Transform")]
	[FeatureId("NotReady")]
	public class FisherTransformInverse : Indicator
	{
		#region Fields

		private readonly Highest _highest = new();
		private readonly ValueDataSeries _ift = new(Resources.Indicator);
		private readonly ValueDataSeries _iftSmoothed = new(Resources.SMA);
		private readonly Lowest _lowest = new();
		private readonly SMA _sma = new();
		private readonly WMA _wma = new();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "HighLow", GroupName = "Period", Order = 90)]
		public int HighLowPeriod
		{
			get => _highest.Period;
			set
			{
				if (value <= 0)
					return;

				_highest.Period = _lowest.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "WMA", GroupName = "Period", Order = 100)]
		public int WmaPeriod
		{
			get => _wma.Period;
			set
			{
				if (value <= 0)
					return;

				_wma.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SMA", GroupName = "Period", Order = 110)]
		public int SmaPeriod
		{
			get => _sma.Period;
			set
			{
				if (value <= 0)
					return;

				_sma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public FisherTransformInverse()
		{
			Panel = IndicatorDataProvider.NewPanel;

			_highest.Period = _lowest.Period = 10;

			_ift.Color = Colors.Red;
			_iftSmoothed.Color = Colors.Green;

			DataSeries[0] = _ift;
			DataSeries.Add(_iftSmoothed);
		}

		#endregion

		#region Protected methods

		#region Overrides of BaseIndicator

		protected override void OnRecalculate()
		{
			DataSeries.ForEach(x => x.Clear());
		}

		#endregion

		protected override void OnCalculate(int bar, decimal value)
		{
			var high = _highest.Calculate(bar, value);
			var low = _lowest.Calculate(bar, value);
			var eps = 0m;

			if (high != low)
				eps = 10m * (value - low) / (high - low) - 5;

			var epsSmoothed = _wma.Calculate(bar, eps);

			_ift[bar] = 0m;

			if (bar > 0)
			{
				if (high != low)
				{
					var expEps = (decimal)Math.Exp(Convert.ToDouble(2 * epsSmoothed));
					_ift[bar] = (expEps - 1) / (expEps + 1);
				}
				else
					_ift[bar] = _ift[bar - 1];
			}

			_iftSmoothed[bar] = _sma.Calculate(bar, _ift[bar]);
		}

		#endregion
	}
}