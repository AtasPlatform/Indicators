namespace ATAS.Indicators.Technical
{
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Linq;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Average Daily Range")]
	public class ADR : Indicator
	{
		#region Nested types

		public enum ControlPoint
		{
			[Display(ResourceType = typeof(Resources), Name = "OpenSession")]
			OpenSession,

			[Display(ResourceType = typeof(Resources), Name = "LowSession")]
			LowSession,

			[Display(ResourceType = typeof(Resources), Name = "HighSession")]
			HighSession
		}

		#endregion

		#region Fields

		private readonly List<decimal> _ranges = new List<decimal>();
		private ControlPoint _atStart;
		private decimal _avg;
		private decimal _currentSessionHigh;
		private decimal _currentSessionLow;
		private DrawingText _currentText;

		private float _fontSize;
		private int _lastBar;

		private LineTillTouch _lowerLine;

		private int _period;
		private LineTillTouch _upperLine;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "CalculationMode")]
		public ControlPoint CalculationMode
		{
			get => _atStart;
			set
			{
				_atStart = value;
				RecalculateValues();
			}
		}

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

		[Display(ResourceType = typeof(Resources), Name = "Period")]
		public int Period
		{
			get => _period;
			set
			{
				if (value < 2)
					return;

				_period = value;
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
			_period = 3;
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
				var candle = GetCandle(bar);
				HorizontalLinesTillTouch.Clear();
				Labels.Clear();
				_ranges.Clear();
				_currentSessionHigh = candle.High;
				_currentSessionLow = candle.Low;
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

				if (_ranges.Count > Period)
					_ranges.RemoveAt(0);
				_avg = _ranges.Average();

				var pen = GetPen();

				switch (CalculationMode)
				{
					case ControlPoint.OpenSession:
						_upperLine = new LineTillTouch(bar, candle.Open + _avg / 2.0m, pen, 5);
						_lowerLine = new LineTillTouch(bar, candle.Open - _avg / 2.0m, pen, 5);
						break;

					case ControlPoint.HighSession:
						_upperLine = new LineTillTouch(bar, candle.High, pen, 5);
						_lowerLine = new LineTillTouch(bar, candle.High - _avg, pen, 5);
						break;

					case ControlPoint.LowSession:
						_upperLine = new LineTillTouch(bar, candle.Low + _avg, pen, 5);
						_lowerLine = new LineTillTouch(bar, candle.Low, pen, 5);
						break;
				}

				_currentSessionLow = _currentSessionHigh = 0;
				HorizontalLinesTillTouch.Add(_upperLine);
				HorizontalLinesTillTouch.Add(_lowerLine);

				AddOrEditText(true);
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

			switch (CalculationMode)
			{
				case ControlPoint.HighSession:
					if (candle.High
						>
						_upperLine.FirstPrice)
					{
						_upperLine.FirstPrice =
							_upperLine.SecondPrice = candle.High;

						_lowerLine.FirstPrice =
							_lowerLine.SecondPrice = candle.High - _avg;

						AddOrEditText(false);
					}

					break;

				case ControlPoint.LowSession:
					if (candle.Low
						<
						_lowerLine.FirstPrice)
					{
						_upperLine.FirstPrice =
							_upperLine.SecondPrice = candle.Low + _avg;

						_lowerLine.FirstPrice =
							_lowerLine.SecondPrice = candle.Low;

						AddOrEditText(false);
					}

					break;
			}
		}

		private void AddOrEditText(bool add)
		{
			var textNumber = $"{_avg / ChartInfo.PriceChartContainer.Step:0.00}";

			if (add)
			{
				var pen = GetPen();

				var firstBar = _upperLine.FirstBar;

				_currentText = AddText("Aver" + firstBar, $"AveR: {textNumber}", true, firstBar, _upperLine.FirstPrice, pen.Color, Color.Transparent,
					Color.Transparent,
					FontSize, DrawingText.TextAlign.Right);
			}
			else
			{
				_currentText.Text = $"AveR: {textNumber}";
				_currentText.TextPrice = _upperLine.FirstPrice;
			}
		}

		#endregion
	}
}