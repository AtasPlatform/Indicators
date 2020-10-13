namespace ATAS.Indicators.Technical
{
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Linq;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	[DisplayName("ADR")]
	public class ADR : Indicator
	{
		#region Fields

		private readonly List<decimal> _ranges = new List<decimal>();
		private decimal _currentSessionHigh;
		private decimal _currentSessionLow;

		private float _fontSize;
		private int _lastBar;

		private LineTillTouch _lowerLine;

		private int _renderPeriods;
		private LineTillTouch _upperLine;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "FontSize")]
		public float FontSize
		{
			get => _fontSize;
			set
			{
				if (value <= 0)
					return;

				_fontSize = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "RenderPeriods")]
		public int RenderPeriods
		{
			get => _renderPeriods;
			set
			{
				if (value < 2)
					return;

				_renderPeriods = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public ADR()
			: base(true)
		{
			_lastBar = -1;
			DenyToChangePanel = true;
			_renderPeriods = 3;
			_fontSize = 12;

			DataSeries[0].PropertyChanged += (a, b) =>
			{
				var pen = GetPen();

				foreach (var lineTillTouch in HorizontalLinesTillTouch)
					lineTillTouch.Pen = pen;
			};
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				Labels.Clear();
				_ranges.Clear();
				_currentSessionHigh = _currentSessionLow = 0;
			}

			if (bar != _lastBar)
			{
				ProcessNewBar(bar);
				_lastBar = bar;
			}
			else
				ProcessNewTick(bar);
		}

		#endregion

		#region Private methods

		private Pen GetPen()
		{
			var series = (ValueDataSeries)DataSeries[0];
			return new Pen(series.Color.Convert(), series.Width);
		}

		private void ProcessNewBar(int bar)
		{
			var candle = GetCandle(bar);

			if (IsNewSession(bar) && _currentSessionHigh != 0)
			{
				_ranges.Add(_currentSessionHigh - _currentSessionLow);
				_currentSessionLow = _currentSessionHigh = 0;
				var avg = _ranges.Average();

				var pen = GetPen();

				_upperLine = new LineTillTouch(bar, candle.Open + avg / 2.0m, pen, 5);
				_lowerLine = new LineTillTouch(bar, candle.Open - avg / 2.0m, pen, 5);

				HorizontalLinesTillTouch.Add(_upperLine);
				HorizontalLinesTillTouch.Add(_lowerLine);

				var textNumber = $"{avg / ChartInfo.PriceChartContainer.Step:0.00}";

				AddText("Aver" + bar, $"AveR: {textNumber}", true, bar, candle.Open + avg / 2.0m, 0, 0, pen.Color, Color.Transparent, Color.Transparent,
					FontSize, DrawingText.TextAlign.Right);

				if (HorizontalLinesTillTouch.Count / 2 > RenderPeriods)
				{
					var firstBar = HorizontalLinesTillTouch
						.First().FirstBar;
					Labels.Remove("Aver" + firstBar);
					HorizontalLinesTillTouch.RemoveRange(0, 2);
				}
			}
			else
			{
				if (_upperLine != null)
					_upperLine.SecondBar = bar;

				if (_lowerLine != null)
					_lowerLine.SecondBar = bar;
			}

			UpdateCurrentSessionHighLow(candle);
		}

		private void ProcessNewTick(int bar)
		{
			var candle = GetCandle(bar);

			UpdateCurrentSessionHighLow(candle);
		}

		private void UpdateCurrentSessionHighLow(IndicatorCandle candle)
		{
			if (candle.High > _currentSessionHigh)
				_currentSessionHigh = candle.High;

			if (candle.Low < _currentSessionLow || _currentSessionLow == 0)
				_currentSessionLow = candle.Low;
		}

		#endregion
	}
}