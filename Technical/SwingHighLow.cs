namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Swing High and Low")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.SwingHighLowDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602483")]
	public class SwingHighLow : Indicator
	{
		#region Fields

		private int _lastHighAlert;
		private int _lastLowAlert;

		private Color _fontColor = Colors.White;
		private Color _backgroundColor = Colors.Black;

		private readonly Highest _highest = new() { Period = 10 };
		private readonly Lowest _lowest = new() { Period = 10 };

		private readonly ValueDataSeries _shSeries = new("ShSeries", Strings.Highest)
		{
			Color = DefaultColors.Green.Convert(),
			VisualType = VisualMode.DownArrow
		};
		private readonly ValueDataSeries _slSeries = new("SlSeries", Strings.Lowest)
		{
			Color = DefaultColors.Red.Convert(),
			VisualType = VisualMode.UpArrow
		};
        private bool _includeEqual = true;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
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

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.IncludeEqualHighLow), GroupName = nameof(Strings.Settings), Description = nameof(Strings.IncludeEqualsValuesDescription), Order = 110)]
		public bool IncludeEqual
		{
			get => _includeEqual;
			set
			{
				_includeEqual = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), 
			Name = nameof(Strings.UseAlert), 
			GroupName = nameof(Strings.ApproximationAlert), 
			Description = nameof(Strings.UseAlertDescription), 
			Order = 200)]
		public bool UseAlerts { get; set; }


		[Display(ResourceType = typeof(Strings), 
			Name = nameof(Strings.AlertFile), 
			GroupName = nameof(Strings.ApproximationAlert), 
			Description = nameof(Strings.AlertFileDescription), 
			Order = 210)]
		public string AlertFile { get; set; } = "alert1";

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.FontColor),
			GroupName = nameof(Strings.ApproximationAlert),
			Description = nameof(Strings.AlertTextColorDescription),
			Order = 340)]
		public System.Drawing.Color FontColor
		{
			get => _fontColor.Convert();
			set => _fontColor = value.Convert();
		}

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.BackGround),
			GroupName = nameof(Strings.ApproximationAlert),
			Description = nameof(Strings.AlertFillColorDescription),
			Order = 350)]
		public System.Drawing.Color BackgroundColor
		{
			get => _backgroundColor.Convert();
			set => _backgroundColor = value.Convert();
		}

        #endregion

        #region ctor

        public SwingHighLow() 
			: base(true)
		{
			DenyToChangePanel = true;
			
			DataSeries[0] = _shSeries;
			DataSeries.Add(_slSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				DataSeries.ForEach(x => x.Clear());

			var candle = GetCandle(bar);
			_highest.Calculate(bar, candle.High);
			_lowest.Calculate(bar, candle.Low);
			
			if (bar < Period * 2)
				return;

			var calcBar = bar - Period;
			var calcCandle = GetCandle(calcBar);

			if (_includeEqual)
			{
				if (calcCandle.High < (decimal)_highest.DataSeries[0][bar - Period - 1]
					||
					calcCandle.High < (decimal)_highest.DataSeries[0][bar])
					_shSeries[calcBar] = 0;
				else
					_shSeries[calcBar] = calcCandle.High + InstrumentInfo.TickSize * 2;

				if (calcCandle.Low > (decimal)_lowest.DataSeries[0][bar - Period - 1]
					||
					calcCandle.Low > (decimal)_lowest.DataSeries[0][bar])
					_slSeries[calcBar] = 0;
				else
					_slSeries[calcBar] = calcCandle.Low - InstrumentInfo.TickSize * 2;
            }
			else
			{
				if (calcCandle.High <= (decimal)_highest.DataSeries[0][bar - Period - 1]
					||
					calcCandle.High <= (decimal)_highest.DataSeries[0][bar])
					_shSeries[calcBar] = 0;
				else
					_shSeries[calcBar] = calcCandle.High + InstrumentInfo.TickSize * 2;

                if (calcCandle.Low >= (decimal)_lowest.DataSeries[0][bar - Period - 1]
					||
					calcCandle.Low >= (decimal)_lowest.DataSeries[0][bar])
					_slSeries[calcBar] = 0;
				else
					_slSeries[calcBar] = calcCandle.Low - InstrumentInfo.TickSize * 2;
            }

			if(!UseAlerts || bar < CurrentBar - 1)
				return;

			if (_slSeries[calcBar] is not 0 && _lastLowAlert != calcBar)
			{
				_lastLowAlert = calcBar;
				AddAlert(AlertFile, InstrumentInfo.Instrument, $"Low swing triggered at {candle.Close}", _backgroundColor, _fontColor);
			}

			if (_shSeries[calcBar] is not 0 && _lastHighAlert != calcBar)
			{
				_lastHighAlert = calcBar;
				AddAlert(AlertFile, InstrumentInfo.Instrument, $"High swing triggered at {candle.Close}", _backgroundColor, _fontColor);
			}
        }

		#endregion
	}
}