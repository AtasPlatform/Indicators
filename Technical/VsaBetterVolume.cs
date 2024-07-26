﻿namespace ATAS.Indicators.Technical
{
    using System;
    using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Drawing;

	using OFT.Attributes;
    using OFT.Localization;
    using Color = System.Drawing.Color;
	
    [DisplayName("VSA Better Volume")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.VsaBetterVolumeDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602502")]
	public class VsaBetterVolume : Indicator
	{
        #region Fields

        private int _period = 14;
		private decimal _tickSize;
		
		private readonly Highest _highestAbs = new() { Period = 20 };
		private readonly Highest _highestComp = new() { Period = 20 };

		private readonly Lowest _lowest = new() { Period = 20 };
		private readonly Highest _lowestComp = new() { Period = 20 };

		private readonly ValueDataSeries _volume = new("Volume");
        private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Volume)
		{
			Color = System.Drawing.Color.DodgerBlue.Convert(),
			Width = 2,
			VisualType = VisualMode.Histogram,
			ShowZeroValue = false,
			UseMinimizedModeIfEnabled = true,
			ResetAlertsOnNewBar = true,
            DescriptionKey = nameof(Strings.VolumeHistSettingsDescription)
        };

		private readonly ValueDataSeries _v4Series = new("V4Series", "V4")
		{
			Color = System.Drawing.Color.LightSeaGreen.Convert(),
			Width = 1,
			VisualType = VisualMode.Line,
			UseMinimizedModeIfEnabled = true,
			IgnoredByAlerts = true,
			DescriptionKey = nameof(Strings.AvgVolumeLineSettingsDescription)
		};

		private Color _yellowColor = Color.Orange;
		private Color _whiteColor = Color.LightGray;
		private Color _redColor = DefaultColors.DarkRed;
		private Color _magentaColor = Color.DarkMagenta;
		private Color _greenColor = DefaultColors.Green;
		private Color _blueColor = Color.DodgerBlue;

		#endregion

        #region Properties

        [Display(ResourceType = typeof(Strings), Name = "Blue", GroupName = nameof(Strings.Drawing), Description = nameof(Strings.ColorDescription), Order = 610)]
        public CrossColor BlueColor
        {
	        get => _blueColor.Convert();
	        set
	        {
		        _blueColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Strings), Name = "Green", GroupName = nameof(Strings.Drawing), Description = nameof(Strings.ColorDescription), Order = 620)]
        public CrossColor GreenColor
        {
	        get => _greenColor.Convert();
	        set
	        {
		        _greenColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Strings), Name = "Magenta", GroupName = nameof(Strings.Drawing), Description = nameof(Strings.ColorDescription), Order = 625)]
        public CrossColor MagentaColor
        {
	        get => _magentaColor.Convert();
	        set
	        {
		        _magentaColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Strings), Name = "Red", GroupName = nameof(Strings.Drawing), Description = nameof(Strings.ColorDescription), Order = 630)]
        public CrossColor RedColor
        {
	        get => _redColor.Convert();
	        set
	        {
		        _redColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Strings), Name = "White", GroupName = nameof(Strings.Drawing), Description = nameof(Strings.ColorDescription), Order = 650)]
        public CrossColor WhiteColor
        {
	        get => _whiteColor.Convert();
	        set
	        {
		        _whiteColor = value.Convert();
		        RecalculateValues();
	        }
        }
        [Display(ResourceType = typeof(Strings), Name = "Yellow", GroupName = nameof(Strings.Drawing), Description = nameof(Strings.ColorDescription), Order = 660)]
        public CrossColor YellowColor
        {
	        get => _yellowColor.Convert();
	        set
	        {
		        _yellowColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 0)]
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

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.RetrospectiveAnalysis), GroupName = nameof(Strings.Settings), Description = nameof(Strings.HighestLowestPeriodDescription), Order = 1)]
		[Range(1, 10000)]
        public int LookBack
		{
			get => _highestAbs.Period;
			set
			{
				_highestAbs.Period = _highestComp.Period = value;
				_lowestComp.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public VsaBetterVolume()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			
			DataSeries[0] = _renderSeries;
			DataSeries.Add(_v4Series);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				DataSeries.ForEach(x => x.Clear());
				_tickSize = ChartInfo.PriceChartContainer.Step;
			}

			var candle = GetCandle(bar);
			_volume[bar] = candle.Volume;

			var volLowest = _lowest.Calculate(bar, candle.Volume);
			_renderSeries[bar] = candle.Volume;

			_renderSeries.Colors[bar] = candle.Volume == volLowest ? _yellowColor : _blueColor;
			
			var range = (candle.High - candle.Low) / _tickSize;
			var value2 = candle.Volume * range;

			var value3 = 0.0m;

			if (range != 0)
				value3 = candle.Volume / range;

			var sumVolume = _volume.CalcSum(Period, bar);

			_v4Series[bar] = sumVolume / Period;

			var hiValue2 = _highestAbs.Calculate(bar, value2);

			if (value2 != 0)
				_highestComp.Calculate(bar, value3);

			if (value2 == hiValue2 && candle.Close > (candle.High + candle.Low) / 2.0m && candle.Close >= candle.Open)
				_renderSeries.Colors[bar] = _redColor;

			if (value3 == _highestComp[bar])
				_renderSeries.Colors[bar] = _greenColor;

			if (value2 == hiValue2 && value3 == _highestComp[bar])
				_renderSeries.Colors[bar] = _magentaColor;

			if (value2 == hiValue2 && candle.Close <= (candle.High + candle.Low) / 2.0m && candle.Close <= candle.Open)
				_renderSeries.Colors[bar] = _whiteColor;
		}

		#endregion
	}
}