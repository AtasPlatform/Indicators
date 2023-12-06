namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;

	using OFT.Attributes;
    using OFT.Localization;

	[DisplayName("EMA")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.EMADescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602641")]
	public class EMA : Indicator
	{
		#region Fields

		private int _period = 10;
		private bool _onLine;
		private int _lastAlert;

		private ValueDataSeries _renderSeries = new("RenderSeries", "EMA");
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
		public int Period
		{
			get => _period;
			set
			{
				if (_period == value)
					return;

				if (value <= 0)
					return;

				_period = value;

				RaisePropertyChanged(nameof(Period));
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
		public Color BullishColor
		{
			get => _bullishColor.Convert();
			set
			{
				_bullishColor = value.Convert();
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BearlishColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.BearishColorDescription), Order = 220)]
		public Color BearishColor
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
            Order = 100)]
		public bool UseAlerts { get; set; }
		
		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.RepeatAlert),
			GroupName = nameof(Strings.ApproximationAlert),
            Description = nameof(Strings.RepeatAlertDescription),
            Order = 110)]
		[Range(0, 100000)]
		public bool RepeatAlert { get; set; }

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.ApproximationFilter),
			GroupName = nameof(Strings.ApproximationAlert),
            Description = nameof(Strings.ApproximationFilterDescription),
            Order = 120)]
		[Range(0, 100000)]
		public int AlertSensitivity { get; set; } = 1;

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.AlertFile),
			GroupName = nameof(Strings.ApproximationAlert),
            Description = nameof(Strings.AlertFileDescription),
            Order = 130)]
		public string AlertFile { get; set; } = "alert1";

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.FontColor),
			GroupName = nameof(Strings.ApproximationAlert),
            Description = nameof(Strings.AlertTextColorDescription),
            Order = 140)]
		public Color FontColor { get; set; } = Colors.White;

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.BackGround),
			GroupName = nameof(Strings.ApproximationAlert),
            Description = nameof(Strings.AlertFillColorDescription),
            Order = 150)]
		public Color BackgroundColor { get; set; } = Colors.DimGray;

		#endregion

		#region ctor

		public EMA()
		{
            DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_renderSeries[bar] = bar == 0
				? value
				: value * (2.0m / (1 + Period)) + (1 - 2.0m / (1 + Period)) * _renderSeries[bar - 1];

			if (ColoredDirection && bar != 0)
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
				AddAlert(AlertFile, InstrumentInfo.Instrument, $"EMA approximation alert: {_renderSeries[bar]:0.#####}", BackgroundColor, FontColor);
				_lastAlert = bar;
			}

			_onLine = onLine;
		}

		#endregion
	}
}