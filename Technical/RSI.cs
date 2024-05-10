namespace ATAS.Indicators.Technical
{
    using System;
    using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;
    using OFT.Rendering.Settings;

#if CROSS_PLATFORM
    using Color = System.Drawing.Color;
#else
    using Color = System.Windows.Media.Color;
#endif

    [DisplayName("RSI")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.RSIDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602531")]
	public class RSI : Indicator
	{
		#region Fields

		private readonly Color _black = System.Drawing.Color.Black.Convert();
		private readonly SMMA _negative = new() { Period = 10 };
        private readonly SMMA _positive = new() { Period = 10 };

        private LineSeries _upLine = new("UpLine", "Up")
        {
            Color = System.Drawing.Color.Orange.Convert(),
            LineDashStyle = LineDashStyle.Dash,
            Value = 70,
            Width = 1,
            DescriptionKey = nameof(Strings.OverboughtLimitDescription)
        };

        private LineSeries _downLine = new("DownLine", "Down")
		{
			Color = System.Drawing.Color.Orange.Convert(),
			LineDashStyle = LineDashStyle.Dash,
			Value = 30,
			Width = 1,
            DescriptionKey = nameof(Strings.OversoldLimitDescription)
        };

		private int _lastDownAlert;

		private int _lastUpAlert;
		private decimal _lastValue;

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
			get => _positive.Period;
			set
			{
				_positive.Period = _negative.Period = value;
				RecalculateValues();
			}
		}

        [Display(ResourceType = typeof(Strings),
           Name = nameof(Strings.UpAlert),
           GroupName = nameof(Strings.Alerts),
           Description = nameof(Strings.UpAlertFileFilterDescription),
           Order = 100)]
        public FilterString UpAlertFilter { get; set; }

        [Display(ResourceType = typeof(Strings),
          Name = nameof(Strings.DownAlert),
          GroupName = nameof(Strings.Alerts),
          Description = nameof(Strings.DownAlertFileFilterDescription),
          Order = 110)]
        public FilterString DownAlertFilter { get; set; }

        #region Hidden

        [Browsable(false)]
		[Obsolete]
		public bool UseUpAlert 
		{
			get => UpAlertFilter.Enabled;
			set => UpAlertFilter.Enabled = value;
        }

        [Browsable(false)]
        [Obsolete]
        public string UpAlertFile
		{
            get => UpAlertFilter.Value;
            set => UpAlertFilter.Value = value;
        }

        [Browsable(false)]
        [Obsolete]
        public bool UseDownAlert 
		{
            get => DownAlertFilter.Enabled;
            set => DownAlertFilter.Enabled = value;
        }

        [Browsable(false)]
        [Obsolete]
        public string DownAlertFile 
		{
            get => DownAlertFilter.Value;
            set => DownAlertFilter.Value = value;
        }

        #endregion

        #endregion

        #region ctor

        public RSI()
		{
			Panel = IndicatorDataProvider.NewPanel;

			LineSeries.Add(_downLine);
			LineSeries.Add(_upLine);

			UpAlertFilter = new(true) { Value = "alert1" };
            DownAlertFilter = new(true) { Value = "alert1" };
        }

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				this[bar] = 0;
				_lastUpAlert = _lastDownAlert = 0;
				return;
			}

			var diff = (decimal)SourceDataSeries[bar] - (decimal)SourceDataSeries[bar - 1];
			var pos = _positive.Calculate(bar, diff > 0 ? diff : 0);
			var neg = _negative.Calculate(bar, diff < 0 ? -diff : 0);

			if (neg != 0)
			{
				var div = pos / neg;

				this[bar] = div == 1
					? 0m
					: 100m - 100m / (1m + div);
			}
			else
				this[bar] = 100m;

			if (bar == CurrentBar - 1 && _lastValue != 0)
			{
				if (UpAlertFilter.Enabled)
				{
					if ((_lastValue < _upLine.Value && this[bar] >= _upLine.Value || _lastValue > _upLine.Value && this[bar] <= _upLine.Value)
						&& _lastUpAlert != bar)
					{
						AddAlert(UpAlertFilter.Value, InstrumentInfo.Instrument, $"Up value alert {this[bar]:0.#####}", _black, _upLine.Color);
						_lastUpAlert = bar;
					}
				}

				if (DownAlertFilter.Enabled)
				{
					if ((_lastValue < _downLine.Value && this[bar] >= _downLine.Value || _lastValue > _downLine.Value && this[bar] <= _downLine.Value)
					    && _lastDownAlert != bar)
					{
						AddAlert(DownAlertFilter.Value, InstrumentInfo.Instrument, $"Down value alert {this[bar]:0.#####}", _black, _downLine.Color);
						_lastDownAlert = bar;
					}
				}
			}

			_lastValue = this[bar];
		}

		#endregion
	}
}