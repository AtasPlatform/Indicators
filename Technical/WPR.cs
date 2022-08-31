namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Context;
	using OFT.Rendering.Settings;
	using OFT.Rendering.Tools;

	using Color = System.Drawing.Color;

	[DisplayName("WPR")]
	[Description("Williams’ Percent Range")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/23626-wpr")]
	public class WPR : Indicator
	{
		#region Fields

		private readonly Highest _highest = new();
		private readonly Lowest _lowest = new();
		private int _period = 14;

		private LineSeries _line80 = new("-80") { Color = Colors.Gray, Width = 1, LineDashStyle = LineDashStyle.Dot, Value = -80, IsHidden = true };
		private LineSeries _line20 = new("-20") { Color = Colors.Gray, Width = 1, LineDashStyle = LineDashStyle.Dot, Value = -20, IsHidden = true };
		
        private bool _drawLines = true;

		#endregion

        #region Properties

        [Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "Period",
			GroupName = "Settings",
			Order = 20)]
		public int Period
		{
			get => _period;
			set
			{
				if (value <= 0)
					return;

				_period = _highest.Period = _lowest.Period = value;
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
					if(LineSeries.Contains(_line20))
						return;

					LineSeries.Add(_line20);
					LineSeries.Add(_line80);
				}
				else
				{
					LineSeries.Clear();
				}

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources),
			Name = "minus20",
			GroupName = "Line",
			Order = 30)]
		public LineSeries Line20
		{
			get => _line20;
			set => _line20 = value;
		}

		[Display(ResourceType = typeof(Resources),
			Name = "minus80",
			GroupName = "Line",
			Order = 30)]
		public LineSeries Line80
        {
			get => _line80;
			set => _line80 = value;
        }

		#endregion

		#region ctor

		public WPR()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			Period = 14;
			LineSeries.Add(_line20);
			LineSeries.Add(_line80);
		}

		#endregion

		#region Protected methods
		
		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			var highest = _highest.Calculate(bar, candle.High);
			var lowest = _lowest.Calculate(bar, candle.Low);

			if (highest - lowest != 0)
				this[bar] = -100 * (highest - candle.Close) / (highest - lowest);
			else
				this[bar] = bar > 0 ? this[bar - 1] : 0;
		}

		#endregion
	}
}