namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Concurrent;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Context;

	[DisplayName("Inside Bar")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45249-inside-or-equals-bar")]
	public class InsideEqualsBar : Indicator
	{
		#region Nested types

		public enum ToleranceMode
		{
			[Display(ResourceType = typeof(Resources), Name = "Ticks")]
			Ticks,

			[Display(ResourceType = typeof(Resources), Name = "AbsolutePrice")]
			Price,

			[Display(ResourceType = typeof(Resources), Name = "Percent")]
			Percent
		}

		#endregion

		#region Fields

		private Color _areaColor = Color.FromArgb(100, 200, 200, 0);
		private int _currentStart;
		private ConcurrentDictionary<int, int> _insideRanges;
		private int _lastBar;
		private decimal _tolerance;
		private ToleranceMode _toleranceType;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Mode", GroupName = "Tolerance", Order = 100)]
		[Range(0, 1000000)]
		public ToleranceMode ToleranceType
		{
			get => _toleranceType;
			set
			{
				_toleranceType = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Value", GroupName = "Tolerance", Order = 110)]
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

		[Display(ResourceType = typeof(Resources), Name = "AreaColor", GroupName = "Visualization", Order = 200)]
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

			var upperBody = Math.Max(candle.Open, candle.Close);
			var lowerBody = Math.Min(candle.Open, candle.Close);

			var tolerant = ToleranceType switch
			{
				ToleranceMode.Ticks =>
					(upperBody - startCandle.High) / InstrumentInfo.TickSize <= Tolerance
					&& (startCandle.Low - lowerBody) / InstrumentInfo.TickSize <= Tolerance,
				ToleranceMode.Price => upperBody - startCandle.High <= Tolerance
					&& startCandle.Low - lowerBody <= Tolerance,
				ToleranceMode.Percent => 100 * (upperBody - startCandle.High) / startCandle.High <= Tolerance
					&& 100 * (startCandle.Low - lowerBody) / startCandle.Low <= Tolerance,
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