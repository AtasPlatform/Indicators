namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Drawing;
    using OFT.Attributes;
    using OFT.Localization;
	
    [DisplayName("SMA")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.SMADescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602468")]
	public class SMA : Indicator
	{
		#region Fields

		private int _lastBar = -1;
		private int _period = 10;
		private decimal _sum;
		private bool _onLine;
		private int _lastAlert;

		private ValueDataSeries _renderSeries = new("RenderSeries", "SMA");
		private System.Drawing.Color _bullishColor = DefaultColors.Green;
		private System.Drawing.Color _bearishColor = DefaultColors.Red;
		private bool _coloredDirection = true;

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
			get => _period;
			set
			{
				_period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ColoredDirection), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.ColoredDirectionDescription), Order = 200)]
		[Range(1, 10000)]
		public bool ColoredDirection
		{
			get => _coloredDirection;
			set
			{
				_coloredDirection = value;

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BullishColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.BullishColorDescription), Order = 210)]
		public CrossColor BullishColor
		{
			get => _bullishColor.Convert();
			set
			{
				_bullishColor = value.Convert();
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BearlishColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.BearishColorDescription), Order = 220)]
		public CrossColor BearishColor
		{
			get => _bearishColor.Convert();
			set
			{
				_bearishColor = value.Convert();
				RecalculateValues();
			}
		}

        [Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.UseAlerts),
			GroupName = nameof(Strings.ApproximationAlert),
            Description = nameof(Strings.UseAlertsDescription),
            Order = 300)]
		public bool UseAlerts { get; set; }


		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.RepeatAlert),
			GroupName = nameof(Strings.ApproximationAlert),
            Description = nameof(Strings.RepeatAlertDescription),
            Order = 310)]
		[Range(0, 100000)]
		public bool RepeatAlert { get; set; }

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.ApproximationFilter),
			GroupName = nameof(Strings.ApproximationAlert),
            Description = nameof(Strings.ApproximationFilterDescription),
            Order = 320)]
		[Range(0, 100000)]
		public int AlertSensitivity { get; set; } = 1;

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.AlertFile),
			GroupName = nameof(Strings.ApproximationAlert),
            Description = nameof(Strings.AlertFileDescription),
            Order = 330)]
		public string AlertFile { get; set; } = "alert1";

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.FontColor),
			GroupName = nameof(Strings.ApproximationAlert),
            Description = nameof(Strings.AlertTextColorDescription),
            Order = 340)]
		public CrossColor FontColor { get; set; } = System.Drawing.Color.White.Convert();

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.BackGround),
			GroupName = nameof(Strings.ApproximationAlert),
            Description = nameof(Strings.AlertFillColorDescription),
            Order = 350)]
		public CrossColor BackgroundColor { get; set; } = System.Drawing.Color.DimGray.Convert();

        #endregion

        #region ctor

        public SMA()
        {
            DataSeries[0] = _renderSeries;
        }

        #endregion

        #region Protected methods

        protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_onLine = false;
				_sum = 0;
				_renderSeries[bar] = value;
				return;
			}

			if (bar != _lastBar)
			{
				_lastBar = bar;
				_sum += (decimal)SourceDataSeries[bar - 1];

				if (bar >= Period)
					_sum -= (decimal)SourceDataSeries[bar - Period];
			}

			var sum = _sum + value;
			_renderSeries[bar] = sum / Math.Min(Period, bar + 1);

			if (ColoredDirection)
			{
				_renderSeries.Colors[bar] = _renderSeries[bar] > _renderSeries[bar - 1]
					? _bullishColor
					: _bearishColor;
			}

			if (bar != CurrentBar - 1 || !UseAlerts)
				return;

			if (_lastAlert == bar && !RepeatAlert)
				return;

            var close = GetCandle(bar).Close;
			var onLine = Math.Abs(_renderSeries[bar] - close) / InstrumentInfo.TickSize <= AlertSensitivity;

			if (onLine && !_onLine)
			{
				AddAlert(AlertFile, InstrumentInfo.Instrument, $"SMA approximation alert: {_renderSeries[bar]:0.#####}", BackgroundColor, FontColor);
				_lastAlert = bar;
			}

			_onLine = onLine;
		}

		#endregion
	}
}