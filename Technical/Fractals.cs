namespace ATAS.Indicators.Technical
{
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;

    using ATAS.Indicators.Drawing;

    using OFT.Attributes;
    using OFT.Localization;
    using OFT.Rendering.Context.GDIPlus;
    using OFT.Rendering.Settings;

    using Utils.Common.Collections;

    using Pen = CrossPen;

    [DisplayName("Fractals")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.FractalsDescription))]
    [HelpLink("https://help.atas.net/support/solutions/articles/72000602388")]
	public class Fractals : Indicator
	{
		#region Nested types

		public enum ShowMode
		{
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.High))]
			High,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Low))]
			Low,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Any))]
			All,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.None))]
			None
		}

		#endregion

		#region Fields

		private readonly ValueDataSeries _fractalDown = new("FractalDown", "Fractal Down")
		{
			VisualType = VisualMode.Dots,
			ShowZeroValue = false,
			Width = 5
		};

		private readonly ValueDataSeries _fractalUp = new("FractalUp", "Fractal Up")
		{
			Color = System.Drawing.Color.LimeGreen.Convert(),
			VisualType = VisualMode.Dots,
			ShowZeroValue = false,
			Width = 5
		};

		private CrossPen _highPen;
		private CrossPen _lowPen;
		private ShowMode _mode = ShowMode.All;
        private bool _showLine;
		private int _period = 2;
		private bool _includeEqualHighLow;
		private decimal _tickSize;
		private int _lastBar;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.FractalPeriodDescription), Order = 10)]
		[Range(1, int.MaxValue)]
		public int Period
		{
			get => _period;
			set
			{
				_period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.IncludeEqualHighLow), GroupName = nameof(Strings.Settings), Description = nameof(Strings.IncludeEqualsValuesDescription), Order = 20)]
		public bool IncludeEqualHighLow
		{
			get => _includeEqualHighLow;
			set
			{
				_includeEqualHighLow = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Show), GroupName = nameof(Strings.Line), Description = nameof(Strings.IsNeedShowLinesDescription), Order = 100)]
		public bool ShowLine
		{
			get => _showLine;
			set
			{
				_showLine = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.High), GroupName = nameof(Strings.Line), Description = nameof(Strings.PenSettingsDescription), Order = 110)]
		public PenSettings HighPen { get; set; } = new() { Color = System.Drawing.Color.LimeGreen.Convert() };

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Low), GroupName = nameof(Strings.Line), Description = nameof(Strings.PenSettingsDescription), Order = 120)]
		public PenSettings LowPen { get; set; } = new() { Color = DefaultColors.Red.Convert() };

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.VisualMode), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.VisualModeDescription), Order = 200)]
		public ShowMode Mode
		{
			get => _mode;
			set
			{
				_mode = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Fractals()
			: base(true)
		{
			DenyToChangePanel = true;
			_highPen = HighPen.RenderObject.ToPen();
			_lowPen = LowPen.RenderObject.ToPen();

			HighPen.PropertyChanged += HighPenChanged;
			LowPen.PropertyChanged += LowPenChanged;

			DataSeries[0] = _fractalUp;
			DataSeries.Add(_fractalDown);
		}

		#endregion
		
		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_tickSize = ChartInfo.PriceChartContainer.Step;
				DataSeries.ForEach(x => x.Clear());
				ClearHorizontalLinesTillTouch();
			}

			if (Mode is ShowMode.None)
				return;

			if (bar < 2 * _period)
				return;

			var isNewBar = _lastBar != bar;
			_lastBar = bar;

			var fractalBar = bar - _period;
			var centerCandle = GetCandle(fractalBar);

			var isHighFractal = Mode is ShowMode.High or ShowMode.All && IsHighFractal(fractalBar, centerCandle.High);
			var isLowFractal = Mode is ShowMode.Low or ShowMode.All && IsLowFractal(fractalBar, centerCandle.Low);

			if (isHighFractal)
			{
				_fractalUp[fractalBar] = centerCandle.High + 3 * _tickSize;

				if (ShowLine)
				{
					var line = new LineTillTouch(fractalBar, centerCandle.High, _highPen) { Context = true };

					if (!isNewBar && IsCurrentLine(fractalBar, true))
					{
						ReplaceHorizontalLineTillTouch(HorizontalLinesTillTouch.Count - 1, line);
					}
					else
					{
						AddHorizontalLineTillTouch(line);
					}
				}
			}
			else
			{
				_fractalUp[fractalBar] = 0;

				if (ShowLine && !isNewBar)
				{
					if (IsCurrentLine(fractalBar, true))
						RemoveHorizontalLineTillTouchAt(HorizontalLinesTillTouch.Count - 1);
				}
			}

			if (isLowFractal)
			{
				_fractalDown[fractalBar] = centerCandle.Low - 3 * _tickSize;

				if (ShowLine)
				{
					var line = new LineTillTouch(fractalBar, centerCandle.Low, _lowPen) { Context = false };

					if (!isNewBar && IsCurrentLine(fractalBar, false))
					{
						ReplaceHorizontalLineTillTouch(HorizontalLinesTillTouch.Count - 1, line);
					}
					else
					{
						AddHorizontalLineTillTouch(line);
					}
				}
			}
			else
			{
				_fractalDown[fractalBar] = 0;

				if (ShowLine && !isNewBar)
				{
					if (IsCurrentLine(fractalBar, false))
						RemoveHorizontalLineTillTouchAt(HorizontalLinesTillTouch.Count - 1);
				}
			}
		}

        #endregion

        #region Private methods

        private bool IsHighFractal(int centerBar, decimal centerHigh)
        {
	        for (var i = 1; i <= _period; i++)
	        {
		        var left = GetCandle(centerBar - i).High;
		        var right = GetCandle(centerBar + i).High;

		        if (_includeEqualHighLow)
		        {
			        if (left > centerHigh || right > centerHigh)
				        return false;
		        }
		        else
		        {
			        if (left >= centerHigh || right >= centerHigh)
				        return false;
		        }
	        }

	        return true;
        }

        private bool IsLowFractal(int centerBar, decimal centerLow)
        {
	        for (var i = 1; i <= _period; i++)
	        {
		        var left = GetCandle(centerBar - i).Low;
		        var right = GetCandle(centerBar + i).Low;

		        if (_includeEqualHighLow)
		        {
			        if (left < centerLow || right < centerLow)
				        return false;
		        }
		        else
		        {
			        if (left <= centerLow || right <= centerLow)
				        return false;
		        }
	        }

	        return true;
        }

        private bool IsCurrentLine(int bar, bool isHighLine)
        {
	        return HorizontalLinesTillTouch.Count is not 0 &&
		        (bool)HorizontalLinesTillTouch[^1].Context == isHighLine &&
		        HorizontalLinesTillTouch[^1].FirstBar == bar;
        }

        private void HighPenChanged(object sender, PropertyChangedEventArgs e)
		{
			var highPen = HighPen.RenderObject.ToPen();

			HorizontalLinesTillTouch
				.Where(x => (bool)x.Context)
				.ForEach(x => x.Pen = highPen);

			_highPen = highPen;
		}

		private void LowPenChanged(object sender, PropertyChangedEventArgs e)
		{
			var lowPen = LowPen.RenderObject.ToPen();

			HorizontalLinesTillTouch
				.Where(x => !(bool)x.Context)
				.ForEach(x => x.Pen = lowPen);

			_lowPen = lowPen;
		}

		#endregion
	}
}