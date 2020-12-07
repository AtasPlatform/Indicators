namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Resources;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Color = System.Drawing.Color;

	[DisplayName("Open Line")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/23629-open-line")]
	public class OpenLine : Indicator
	{
		#region Fields

		private bool _customSessionStart;
		private int _fontSize = 10;
		private readonly ValueDataSeries _line;

		private int _offset;
		private string _openCandleText = "Open Line";
		private decimal _openValue = decimal.Zero;
		private TimeSpan _startDate = new TimeSpan(9, 0, 0);

		#endregion

		#region Properties

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
		public string OpenCandleText
		{
			get => _openCandleText;
			set
			{
				_openCandleText = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "TextSize",
			GroupName = "TextSettings",
			Order = 40)]
		public int FontSize
		{
			get => _fontSize;
			set
			{
				_fontSize = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "OffsetY",
			GroupName = "TextSettings",
			Order = 50)]
		public int Offset
		{
			get => _offset;
			set
			{
				_offset = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public OpenLine()
			: base(true)
		{
			_line = (ValueDataSeries)DataSeries[0];
			_line.VisualType = VisualMode.Square;
			_line.Color = Colors.SkyBlue;
			_line.Width = 2;
			DenyToChangePanel = true;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_openValue = decimal.Zero;
				return;
			}

			var candle = GetCandle(bar);
			var isStart = _customSessionStart ? candle.Time.TimeOfDay >= _startDate && GetCandle(bar - 1).Time.TimeOfDay < _startDate : IsNewSession(bar);

			if (isStart)
			{
				_openValue = candle.Open;
				_line.SetPointOfEndLine(bar - 1);
			}

			if (_openValue != decimal.Zero)
			{
				_line[bar] = _openValue;
				var penColor = Color.FromArgb(_line.Color.A, _line.Color.R, _line.Color.G, _line.Color.B);

				if (bar == CurrentBar - 1)
				{
					AddText("OpenLine", _openCandleText, true, CurrentBar, _openValue, -_offset, 0, penColor
						, Color.Transparent, Color.Transparent, _fontSize,
						DrawingText.TextAlign.Left);
				}
			}
		}

		#endregion
	}
}