namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Concurrent;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Linq;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Context;
	using OFT.Rendering.Settings;
	using OFT.Rendering.Tools;

	[DisplayName("Open Line")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/23629-open-line")]
	public class OpenLine : Indicator
	{
		#region Fields

		private bool _customSessionStart;
		private int _days;

		private RenderFont _font = new("Arial", 8);
		private int _fontSize = 8;
		private ConcurrentBag<int> _sessions = new();

		private TimeSpan _startDate = new(9, 0, 0);
		private int _targetBar;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Days",
			GroupName = "Common",
			Order = 5)]
		public int Days
		{
			get => _days;
			set
			{
				if (value < 0)
					return;

				_days = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "CustomSessionStart",
			GroupName = "SessionTime",
			Order = 10)]
		public bool CustomSessionStart
		{
			get => _customSessionStart;
			set
			{
				_customSessionStart = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "StartTimeGmt",
			GroupName = "SessionTime",
			Order = 20)]
		public TimeSpan StartDate
		{
			get => _startDate;
			set
			{
				_startDate = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Text",
			GroupName = "TextSettings",
			Order = 30)]
		public string OpenCandleText { get; set; } = "Open Line";

		[Display(ResourceType = typeof(Resources), Name = "TextSize",
			GroupName = "TextSettings",
			Order = 40)]
		[Range(1, 100000)]

		public int FontSize
		{
			get => _fontSize;
			set
			{
				_fontSize = value;
				_font = new RenderFont("Arial", value);
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "OffsetY",
			GroupName = "TextSettings",
			Order = 50)]
		public int Offset { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "OpenLine",
			GroupName = "Drawing",
			Order = 50)]
		public PenSettings LinePen { get; set; } = new() { Color = Colors.SkyBlue, Width = 2 };

		#endregion

		#region ctor

		public OpenLine()
			: base(true)
		{
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.Final);

			_days = 20;

			DataSeries[0].IsHidden = true;
			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;

			DenyToChangePanel = true;
		}

		#endregion

		#region Protected methods

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			foreach (var session in _sessions)
			{
				if (session > LastVisibleBarNumber)
					continue;

				var x1 = ChartInfo.GetXByBar(session);

				var lastBar = _sessions
					.Where(x => x > session)
					.DefaultIfEmpty(-1)
					.Min();

				var x2 = lastBar == -1
					? ChartInfo.GetXByBar(LastVisibleBarNumber + 1)
					: ChartInfo.GetXByBar(lastBar);

				if (x2 < 0)
					continue;

				var candle = GetCandle(session);
				var y = ChartInfo.GetYByPrice(candle.Open, false);

				context.DrawLine(LinePen.RenderObject, x1, y, x2, y);

				if (lastBar != -1)
					continue;

				var stringSize = context.MeasureString(OpenCandleText, _font);
				context.DrawString(OpenCandleText, _font, LinePen.RenderObject.Color, x2 - stringSize.Width, y - stringSize.Height - Offset);
			}
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_sessions = new ConcurrentBag<int>
					{ 0 };
				_targetBar = 0;

				if (_days <= 0)
					return;

				var days = 0;

				for (var i = CurrentBar - 1; i >= 0; i--)
				{
					_targetBar = i;

					if (!IsNewSession(i))
						continue;

					days++;

					if (days == _days)
						break;
				}

				return;
			}

			if (bar < _targetBar)
				return;

			var isStart = _customSessionStart
				? IsNewCustomSession(bar)
				: IsNewSession(bar);

			if (isStart && !_sessions.Contains(bar))
				_sessions.Add(bar);
		}

		#endregion

		#region Private methods

		private bool IsNewCustomSession(int bar)
		{
			if (bar == 0)
				return true;

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			var candleStart = candle
				.Time.AddHours(InstrumentInfo.TimeZone)
				.TimeOfDay;

			var candleEnd = candle
				.LastTime.AddHours(InstrumentInfo.TimeZone)
				.TimeOfDay;

			var prevCandleEnd = prevCandle
				.LastTime.AddHours(InstrumentInfo.TimeZone)
				.TimeOfDay;

			return prevCandleEnd < _startDate && candleEnd >= _startDate || IsNewSession(bar) && prevCandle.Time.Date < candle.Time.Date && candleStart > _startDate;
		}

		#endregion
	}
}