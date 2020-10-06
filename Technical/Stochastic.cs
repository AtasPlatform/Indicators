namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using Utils.Common.Attributes;
	using Utils.Common.Localization;

	[DisplayName("Stochastic")]
	[LocalizedDescription(typeof(Resources), "Stochastic")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/8594-stochastic")]
	public class Stochastic : Indicator
	{
		#region Fields

		private readonly Highest _highest = new Highest();
		private readonly ValueDataSeries _k = new ValueDataSeries("K");
		private readonly SMA _ksma = new SMA();
		private readonly Lowest _lowest = new Lowest();
		private readonly SMA _sma = new SMA();

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "Period",
			GroupName = "Common",
			Order = 20)]
		public int Period
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

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "Smooth",
			GroupName = "Common",
			Order = 20)]
		public int Smooth
		{
			get => _ksma.Period;
			set
			{
				if (value <= 0)
					return;

				_ksma.Period = value;
				RecalculateValues();
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "Period",
			GroupName = "AveragePeriod",
			Order = 20)]
		public int AveragePeriod
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

		public Stochastic()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;

			((ValueDataSeries)DataSeries[0]).Color = Colors.Blue;

			DataSeries.Add(new ValueDataSeries("%D")
			{
				VisualType = VisualMode.Line,
				LineDashStyle = LineDashStyle.Dash,
				Color = Colors.Red
			});

			LineSeries.Add(new LineSeries("Down")
			{
				Color = Colors.Orange,
				LineDashStyle = LineDashStyle.Dash,
				Value = 20,
				Width = 1
			});
			LineSeries.Add(new LineSeries("Up")
			{
				Color = Colors.Orange,
				LineDashStyle = LineDashStyle.Dash,
				Value = 80,
				Width = 1
			});
			Smooth = 3;
			Period = 10;
			AveragePeriod = 3;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			var highest = _highest.Calculate(bar, candle.High);
			var lowest = _lowest.Calculate(bar, candle.Low);

			decimal k = 50;
			if (highest - lowest == 0)
			{
				if (bar > 0)
					k = _k[bar - 1];
			}
			else
				k = (candle.Close - lowest) / (highest - lowest) * 100;

			_k[bar] = k;
			var ksma = _ksma.Calculate(bar, k);
			var d = _sma.Calculate(bar, ksma);

			this[bar] = ksma;
			DataSeries[1][bar] = d;
		}

		#endregion
	}
}