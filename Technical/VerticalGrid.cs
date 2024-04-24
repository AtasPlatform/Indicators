namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;

using ATAS.Indicators.Drawing;

using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using OFT.Rendering.Tools;

[DisplayName("Vertical Grid")]
public class VerticalGrid : Indicator
{
	#region Nested types

	public enum PeriodMode
	{
		Seconds,
		Minutes,
		Hours,
		Days
	}

	#endregion

	#region Fields

	private readonly RenderFont _font = new("Arial", 9);

	private RenderStringFormat _format = new()
	{
		Alignment = StringAlignment.Center,
		LineAlignment = StringAlignment.Center
	};

	private List<int> _gridBars = new();
	private object _gridLocker = new();
	private int _period = 10;
	private TimeSpan _periodTime;
	private PeriodMode _periodType = PeriodMode.Minutes;

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.SubPeriodsMode), GroupName = nameof(Strings.Settings), Order = 100)]
	public PeriodMode PeriodType
	{
		get => _periodType;
		set
		{
			_periodType = value;
			CalcGridPeriod();
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings),
		Description = nameof(Strings.PeriodDescription), Order = 110)]
	[Range(1, 10000)]
	public int Period
	{
		get => _period;
		set
		{
			_period = value;
			CalcGridPeriod();
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.GridStyle), GroupName = nameof(Strings.VisualSettings), Order = 200)]
	public PenSettings GridPen { get; set; } = new()
	{
		Color = DefaultColors.Gray.Convert()
	};

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TextColor), GroupName = nameof(Strings.VisualSettings), Order = 210)]
	public Color TextColor { get; set; } = DefaultColors.Black;

	#endregion

	#region ctor

	public VerticalGrid()
		: base(true)
	{
		DenyToChangePanel = true;
		EnableCustomDrawing = true;
		SubscribeToDrawingEvents(DrawingLayouts.Historical | DrawingLayouts.LatestBar);

		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
		DataSeries[0].IsHidden = true;

		DrawAbovePrice = false;
	}

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
		lock (_gridLocker)
		{
			if (bar == 0)
			{
				_gridBars.Clear();
				_gridBars.Add(bar);
				return;
			}

			var lastGridBar = _gridBars[^1];

			if (lastGridBar == bar)
				return;

			var candle = GetCandle(bar);
			var lastGridCandle = GetCandle(lastGridBar);

			var timeDiff = candle.LastTime - lastGridCandle.Time;

			if (timeDiff < _periodTime && !IsNewSession(bar))
				return;

			_gridBars.Add(bar);
		}
	}

	protected override void OnRender(RenderContext g, DrawingLayouts layout)
	{
		if (ChartInfo is null)
			return;

		var yTop = Container.Region.Y;
		var yBot = Container.Region.Bottom;

		lock (_gridLocker)
		{
			foreach (var bar in _gridBars)
			{
				if (bar < FirstVisibleBarNumber || bar > LastVisibleBarNumber)
					continue;

				var x = ChartInfo.GetXByBar(bar, false);
				g.DrawLine(GridPen.RenderObject, x, yTop, x, yBot);
			}

			var lastLabelX = 0;

			foreach (var bar in _gridBars)
			{
				if (bar < FirstVisibleBarNumber || bar > LastVisibleBarNumber)
					continue;

				var x = ChartInfo.GetXByBar(bar, false);
				var timeStr = GetCandle(bar).Time.ToString("HH:mm:ss");

				var strSize = g.MeasureString(timeStr, _font);

				var leftX = x - strSize.Width / 2;

				if (leftX < lastLabelX)
					continue;

				var rect = new Rectangle(leftX - 3, yBot - strSize.Height, strSize.Width + 6, strSize.Height);

				g.FillRectangle(GridPen.Color.Convert(), rect, 3);
				g.DrawString(timeStr, _font, TextColor, rect, _format);

				lastLabelX = rect.Right;
			}
		}
	}

	#endregion

	#region Private methods

	private void CalcGridPeriod()
	{
		_periodTime = PeriodType switch
		{
			PeriodMode.Seconds => TimeSpan.FromSeconds(_period),
			PeriodMode.Minutes => TimeSpan.FromMinutes(_period),
			PeriodMode.Hours => TimeSpan.FromHours(_period),
			_ => TimeSpan.FromDays(_period)
		};
	}

	#endregion
}