namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Settings;

	using Utils.Common.Localization;

	[DisplayName("Stochastic")]
	[LocalizedDescription(typeof(Resources), "Stochastic")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/8594-stochastic")]
	public class Stochastic : Indicator
	{
		#region Fields

		private readonly Highest _highest = new() { Period = 10 };
		private readonly ValueDataSeries _k = new("K");
		private readonly SMA _ksma = new() { Period = 3 };
		private readonly Lowest _lowest = new() { Period = 10 };
		private readonly SMA _sma = new() { Period = 3 };
		private bool _drawLines = true;

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "Period",
			GroupName = "Common",
			Order = 20)]
		[Range(1, 10000)]
		public int Period
		{
			get => _highest.Period;
			set
			{
				_highest.Period = _lowest.Period = value;
				RecalculateValues();
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "Smooth",
			GroupName = "Common",
			Order = 20)]
		[Range(1, 10000)]
        public int Smooth
		{
			get => _ksma.Period;
			set
			{
				_ksma.Period = value;
				RecalculateValues();
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "Period",
			GroupName = "AveragePeriod",
			Order = 20)]
		[Range(1, 10000)]
        public int AveragePeriod
		{
			get => _sma.Period;
			set
			{
				_sma.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources),
			Name = "Show",
			GroupName = "Line",
			Order = 30)]
		public bool DrawLines
		{
			get => _drawLines;
			set
			{
				_drawLines = value;

				if (value)
				{
					if (LineSeries.Contains(UpLine))
						return;

					LineSeries.Add(UpLine);
					LineSeries.Add(DownLine);
				}
				else
				{
					LineSeries.Clear();
				}

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources),
			Name = "Up",
			GroupName = "Line",
			Order = 30)]
		public LineSeries UpLine { get; set; } = new("Up")
		{
			Color = Colors.Orange,
			LineDashStyle = LineDashStyle.Dash,
			Value = 80,
			Width = 1,
			IsHidden = true
		};

		[Display(ResourceType = typeof(Resources),
			Name = "Down",
			GroupName = "Line",
			Order = 30)]

		public LineSeries DownLine { get; set; } = new("Down")
		{
			Color = Colors.Orange,
			LineDashStyle = LineDashStyle.Dash,
			Value = 20,
			Width = 1,
			IsHidden = true
		};

        #endregion

        #region ctor

        public Stochastic()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;

			((ValueDataSeries)DataSeries[0]).Color = DefaultColors.Blue.Convert();

			DataSeries.Add(new ValueDataSeries("%D")
			{
				VisualType = VisualMode.Line,
				LineDashStyle = LineDashStyle.Dash,
				Color = DefaultColors.Red.Convert(),
				IgnoredByAlerts = true
			});

			LineSeries.Add(UpLine);
			LineSeries.Add(DownLine);
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