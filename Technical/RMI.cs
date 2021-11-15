namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Relative Momentum Index")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45174-relative-momentum-index")]
	public class RMI : Indicator
	{
		#region Fields

		private readonly SMMA _downSma = new();

		private readonly ValueDataSeries _rmiSeries = new("RMI");

		private readonly SMMA _upSma = new();

		private decimal _firstValue;

		private int _period;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), GroupName = "Settings", Name = "Period", Order = 100)]
		public int RmiLength
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

		[Display(ResourceType = typeof(Resources), GroupName = "Settings", Name = "SMAPeriod", Order = 110)]
		public int RmiMaLength
		{
			get => _upSma.Period;
			set
			{
				if (value <= 0)
					return;

				_upSma.Period = value;
				_downSma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public RMI()
		{
			Panel = IndicatorDataProvider.NewPanel;

			RmiLength = 3;
			RmiMaLength = 14;

			_rmiSeries.Color = Colors.Blue;
			_rmiSeries.Width = 2;
			_rmiSeries.ShowZeroValue = true;

			DataSeries[0] = _rmiSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_rmiSeries.Clear();
				_firstValue = value;
			}

			var periodBar = Math.Max(bar - RmiLength, 0);
			var upSma = _upSma.Calculate(bar, Math.Max(value - (decimal)SourceDataSeries[periodBar], 0));

			var downSma = _downSma.Calculate(bar,
				Math.Abs(Math.Min(value - (decimal)SourceDataSeries[periodBar], 0)));

			var rmi = downSma == 0
				? 100
				: upSma == 0
					? 0
					: 100 - 100 / (1 + upSma / downSma);

			_rmiSeries[bar] = rmi;
		}

		#endregion
	}
}