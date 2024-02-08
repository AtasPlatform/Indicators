namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Volume Zone Oscillator")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.VolumeZoneIndDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602268")]
	public class VolumeZone : Indicator
	{
		#region Fields

		private readonly EMA _emaTv = new();
		private readonly EMA _emaVp = new();
		private LineSeries _overboughtLine1 = new("OverboughtLine1", Strings.Overbought1)
		{
			Value = 50,
			Color = Colors.LawnGreen,
			IsHidden = true,
			DescriptionKey = nameof(Strings.OverboughtLimitDescription)
		};
		private LineSeries _overboughtLine2 = new("OverboughtLine2", Strings.Overbought2)
		{
			Value = 75,
			Color = Colors.LimeGreen,
			IsHidden = true,
            DescriptionKey = nameof(Strings.OverboughtLimitDescription)
        };
		private LineSeries _overboughtLine3 = new("OverboughtLine3", Strings.Overbought3)
		{
			Value = 90,
			Color = Colors.DarkGreen,
			IsHidden = true,
            DescriptionKey = nameof(Strings.OverboughtLimitDescription)
        };
		private LineSeries _oversoldLine1 = new("OversoldLine1", Strings.Oversold1)
		{
			Value = -50,
			Color = Colors.IndianRed,
			IsHidden = true,
            DescriptionKey = nameof(Strings.OversoldLimitDescription)
        };
		private LineSeries _oversoldLine2 = new("OversoldLine2", Strings.Oversold2)
		{
			Value = -75,
			Color = Colors.Red,
			IsHidden = true,
            DescriptionKey = nameof(Strings.OversoldLimitDescription)
        };
		private LineSeries _oversoldLine3 = new("OversoldLine3", Strings.Oversold3)
		{
			Value = -90,
			Color = Colors.DarkRed,
			IsHidden = true,
            DescriptionKey = nameof(Strings.OversoldLimitDescription)
        };
		
		private bool _drawLines = true;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 10)]
		[Range(1, 10000)]
		public int Period
		{
			get => _emaVp.Period;
			set
			{
				_emaVp.Period = _emaTv.Period = value;
				RecalculateValues();
			}
		}
		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.Show),
			GroupName = nameof(Strings.Line),
			Order = 30)]
		public bool DrawLines
		{
			get => _drawLines;
			set
			{
				_drawLines = value;

				if (value)
				{
					if (LineSeries.Contains(_overboughtLine3))
						return;

					LineSeries.Add(_overboughtLine3);
					LineSeries.Add(_overboughtLine2);
					LineSeries.Add(_overboughtLine1);
					LineSeries.Add(_oversoldLine1);
					LineSeries.Add(_oversoldLine2);
					LineSeries.Add(_oversoldLine3);
				}
				else
				{
					LineSeries.Clear();
				}

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.Overbought3),
			GroupName = nameof(Strings.Line),
			Order = 40)]
		public LineSeries OverboughtLine3
		{
			get=> _overboughtLine3;
			set=> _overboughtLine3 = value;
		}

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.Overbought2),
			GroupName = nameof(Strings.Line),
			Order = 50)]
		public LineSeries OverboughtLine2
		{
			get=> _overboughtLine2;
			set=> _overboughtLine2 = value;
		}

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.Overbought1),
			GroupName = nameof(Strings.Line),
			Order = 60)]
		public LineSeries OverboughtLine1
		{
			get=> _overboughtLine1;
			set=> _overboughtLine1 = value;
		}

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.Oversold1),
			GroupName = nameof(Strings.Line),
			Order = 70)]
		public LineSeries OversoldLine1
        {
			get=> _oversoldLine1;
			set=> _oversoldLine1 = value;
		}

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.Oversold2),
			GroupName = nameof(Strings.Line),
			Order = 80)]
		public LineSeries OversoldLine2
        {
			get=> _oversoldLine2;
			set=> _oversoldLine2 = value;
		}

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.Oversold3),
			GroupName = nameof(Strings.Line),
			Order = 90)]
		public LineSeries OversoldLine3
        {
			get=> _oversoldLine3;
			set=> _oversoldLine3 = value;
		}

        #endregion

        #region ctor

        public VolumeZone()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			
            LineSeries.Add(_overboughtLine1);
			LineSeries.Add(_overboughtLine2);
			LineSeries.Add(_overboughtLine3);
			LineSeries.Add(_oversoldLine1);
			LineSeries.Add(_oversoldLine2);
			LineSeries.Add(_oversoldLine3);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			if (bar == 0)
			{
				_emaVp.Calculate(bar, candle.Volume);
				_emaTv.Calculate(bar, candle.Volume);
				return;
			}

			var prevCandle = GetCandle(bar - 1);

			var rVolume = candle.Close > prevCandle.Close ? candle.Volume : -candle.Volume;

			_emaVp.Calculate(bar, rVolume);
			_emaTv.Calculate(bar, candle.Volume);

			this[bar] = _emaTv[bar] != 0
				? _emaVp[bar] / _emaTv[bar] * 100
				: this[bar - 1];
		}

		#endregion
	}
}