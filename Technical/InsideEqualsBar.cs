namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Concurrent;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;

	using OFT.Attributes;
    using OFT.Localization;
    using OFT.Rendering.Context;

	[DisplayName("Inside Bar")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.InsideEqualsBarDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602245")]
	public class InsideEqualsBar : Indicator
	{
		#region Nested types

		public enum ToleranceMode
		{
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Ticks))]
			Ticks,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.AbsolutePrice))]
			Price,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Percent))]
			Percent
		}

		public enum CandleAreaMode
        {
            [Display(ResourceType = typeof(Strings), Name = nameof(Strings.HighLow))]
            HighLow,

            [Display(ResourceType = typeof(Strings), Name = nameof(Strings.CandleBodyHeight))]
            Body
		}

        #endregion

        #region Fields

        private Color _areaColor = Color.FromArgb(100, 200, 200, 0);
		private int _currentStart;
		private ConcurrentDictionary<int, int> _insideRanges;
		private int _lastBar;
		private decimal _tolerance;
		private ToleranceMode _toleranceType;
        private CandleAreaMode _candleArea;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Mode), GroupName = nameof(Strings.Tolerance), Description = nameof(Strings.ToleranceTypeDescription), Order = 100)]
		public ToleranceMode ToleranceType
		{
			get => _toleranceType;
			set
			{
				_toleranceType = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Value), GroupName = nameof(Strings.Tolerance), Description = nameof(Strings.ToleranceDescription), Order = 110)]
		[Range(0, 1000000)]
		public decimal Tolerance
		{
			get => _tolerance;
			set
			{
				_tolerance = _toleranceType is ToleranceMode.Ticks
					? Math.Round(value)
					: value;

				RecalculateValues();
			}
		}

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.CandleArea), GroupName = nameof(Strings.Tolerance), Description = nameof(Strings.CandleAreaModeDescription), Order = 120)]
        public CandleAreaMode CandleArea 
		{
			get => _candleArea;
			set
			{
                _candleArea = value;
				RecalculateValues();
            } 
		}

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AreaColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.AreaColorDescription), Order = 200)]
		public System.Windows.Media.Color AreaColor
		{
			get => _areaColor.Convert();
			set => _areaColor = value.Convert();
		}

		#endregion

		#region ctor

		public InsideEqualsBar()
			: base(true)
		{
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.Final);

			DenyToChangePanel = true;

			DataSeries[0].IsHidden = true;
			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
		}

		#endregion

		#region Protected methods

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			foreach (var range in _insideRanges)
			{
				if (range.Value < FirstVisibleBarNumber || range.Key > LastVisibleBarNumber)
					continue;

				var candle = GetCandle(range.Key);
				var y1 = ChartInfo.GetYByPrice(candle.High, false);
				var y2 = ChartInfo.GetYByPrice(candle.Low, false);
				var x1 = ChartInfo.GetXByBar(range.Key, false);
				var x2 = ChartInfo.GetXByBar(range.Value, false);

				var rect = new Rectangle(x1, y1, x2 - x1, y2 - y1);
				context.FillRectangle(_areaColor, rect);
			}
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_insideRanges = new ConcurrentDictionary<int, int>();
				_currentStart = -1;
				return;
			}

			if (_lastBar == bar || bar < 2)
				return;

			var candle = GetCandle(bar - 1);
			var startCandle = GetCandle(_currentStart == -1 ? bar - 2 : _currentStart);

			decimal upper, lower;

            switch (_candleArea)
            {
                case CandleAreaMode.Body:
					upper = Math.Max(candle.Open, candle.Close);
					lower = Math.Min(candle.Open, candle.Close);
                    break;
				default:
					upper = candle.High;
					lower = candle.Low;
					break;
            }

            var tolerant = ToleranceType switch
			{
				ToleranceMode.Ticks =>
					(upper - startCandle.High) / InstrumentInfo.TickSize <= Tolerance
					&& (startCandle.Low - lower) / InstrumentInfo.TickSize <= Tolerance,
				ToleranceMode.Price => upper - startCandle.High <= Tolerance
					&& startCandle.Low - lower <= Tolerance,
				ToleranceMode.Percent => 100 * (upper - startCandle.High) / startCandle.High <= Tolerance
					&& 100 * (startCandle.Low - lower) / startCandle.Low <= Tolerance,
				_ => throw new ArgumentOutOfRangeException()
			};

			if (tolerant)
			{
				if (_currentStart == -1)
					_currentStart = bar - 2;

				if (_insideRanges.ContainsKey(_currentStart))
					_insideRanges[_currentStart] = bar - 1;
				else
					_insideRanges.TryAdd(_currentStart, bar - 1);
			}
			else
				_currentStart = -1;

			_lastBar = bar;
        }

        #endregion
    }
}