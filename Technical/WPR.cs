namespace ATAS.Indicators.Technical
{
    using System;
    using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;
    using OFT.Rendering.Settings;

	[DisplayName("WPR")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.WPRDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602249")]
	public class WPR : Indicator
	{
		#region Fields

		private readonly Highest _highest = new() { Period = 14 };
		private readonly Lowest _lowest = new() { Period = 14 };
        
		private LineSeries _line80 = new("Line80", "-80") 
		{ 
			Color = System.Drawing.Color.Gray.Convert(),
			Width = 1,
			LineDashStyle = LineDashStyle.Dot,
			Value = -80,
			IsHidden = true 
		};

		private LineSeries _line20 = new("Line20", "-20") 
		{ 
			Color = System.Drawing.Color.Gray.Convert(),
			Width = 1, 
			LineDashStyle = LineDashStyle.Dot, 
			Value = -20, 
			IsHidden = true 
		};
		
        private bool _drawLines = true;

		#endregion

        #region Properties

        [Parameter]
		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.Period),
			GroupName = nameof(Strings.Settings),
            Description = nameof(Strings.PeriodDescription),
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

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.Show),
			GroupName = nameof(Strings.Line),
            Description = nameof(Strings.DrawLinesDescription),
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

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.minus20),
			GroupName = nameof(Strings.Line),
            Description = nameof(Strings.OverboughtLimitDescription),
            Order = 30)]
		public LineSeries Line20
		{
			get => _line20;
			set => _line20 = value;
		}

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.minus80),
			GroupName = nameof(Strings.Line),
            Description = nameof(Strings.OversoldLimitDescription),
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