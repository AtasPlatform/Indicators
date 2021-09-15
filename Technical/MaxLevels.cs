namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Reflection;

	using OFT.Attributes;
	using OFT.Localization;
	using OFT.Rendering.Context;
	using OFT.Rendering.Tools;

	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/421-maximum-levels")]
	[DisplayName("Maximum Levels")]
	[Category("Clusters, Profiles, Levels")]
	public class MaxLevels : Indicator
	{
		#region Nested types

		[Obfuscation(Feature = "renaming", ApplyToMembers = true, Exclude = true)]
		public enum MaxLevelType
		{
			[Display(ResourceType = typeof(Strings), Name = "Bid")]
			Bid,

			[Display(ResourceType = typeof(Strings), Name = "Ask")]
			Ask,

			[Display(ResourceType = typeof(Strings), Name = "PositiveDelta")]
			PositiveDelta,

			[Display(ResourceType = typeof(Strings), Name = "NegativeDelta")]
			NegativeDelta,

			[Display(ResourceType = typeof(Strings), Name = "Volume")]
			Volume,

			[Display(ResourceType = typeof(Strings), Name = "Ticks")]
			Tick,

			[Display(ResourceType = typeof(Strings), Name = "Time")]
			Time
		}

		#endregion

		#region Fields

		private RenderFont _axisFont = new("Arial", 8F, FontStyle.Regular, GraphicsUnit.Point, 204);
		private Color _axisTextColor = System.Drawing.Color.White;
		private IndicatorCandle _candle;
		private bool _candleRequested;
		private string _description = "Current Day";
		private RenderFont _font = new("Arial", 8);
		private int _length;
		private Color _lineColor = System.Drawing.Color.CornflowerBlue;
		private FixedProfilePeriods _period = FixedProfilePeriods.CurrentDay;
		private RenderPen _renderPen = new(System.Drawing.Color.CornflowerBlue, 2);

		private RenderStringFormat _stringRightFormat = new()
		{
			Alignment = StringAlignment.Far,
			LineAlignment = StringAlignment.Center,
			Trimming = StringTrimming.EllipsisCharacter,
			FormatFlags = StringFormatFlags.NoWrap
		};

		private Color _textColor = System.Drawing.Color.Black;
		private int _width = 2;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Strings), GroupName = "Calculation", Name = "Period", Order = 10)]
		public FixedProfilePeriods Period
		{
			get => _period;
			set
			{
				_period = value;
				_description = GetPeriodDescription(_period);
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), GroupName = "Calculation", Name = "Type", Order = 20)]
		public MaxLevelType Type { get; set; } = MaxLevelType.Volume;

		[Display(ResourceType = typeof(Strings), GroupName = "Visualization", Name = "Color", Order = 30)]
		public System.Windows.Media.Color Color
		{
			get => _lineColor.Convert();
			set
			{
				_lineColor = value.Convert();
				_renderPen = new RenderPen(_lineColor, _width);
			}
		}

		[Display(ResourceType = typeof(Strings), GroupName = "Visualization", Name = "Width", Order = 40)]
		public int Width
		{
			get => _width;
			set
			{
				_width = Math.Max(1, value);
				_renderPen = new RenderPen(_lineColor, _width);
			}
		}

		[Display(ResourceType = typeof(Strings), GroupName = "Visualization", Name = "Length", Order = 45)]
		public int Length
		{
			get => _length;
			set => _length = Math.Max(1, value);
		}

		[Display(ResourceType = typeof(Strings), GroupName = "Visualization", Name = "AxisTextColor", Order = 50)]
		public System.Windows.Media.Color AxisTextColor
		{
			get => _axisTextColor.Convert();
			set => _axisTextColor = value.Convert();
		}

		[Display(ResourceType = typeof(Strings), GroupName = "Text", Name = "Show", Order = 50)]
		public bool ShowText { get; set; } = true;

		[Display(ResourceType = typeof(Strings), GroupName = "Text", Name = "Color", Order = 60)]
		public System.Windows.Media.Color TextColor
		{
			get => _textColor.Convert();
			set => _textColor = value.Convert();
		}

		[Display(ResourceType = typeof(Strings), GroupName = "Text", Name = "Size", Order = 70)]
		public int FontSize
		{
			get => (int)_font.Size;
			set => _font = new RenderFont("Arial", Math.Max(7, value));
		}

		#endregion

		#region ctor

		public MaxLevels()
			: base(true)
		{
			_length = 300;
			DataSeries[0].IsHidden = true;
			DenyToChangePanel = true;
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.LatestBar | DrawingLayouts.Historical);
			DrawAbovePrice = true;
			Width = Width;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				_candleRequested = false;

			if (!_candleRequested && bar == CurrentBar - 1)
			{
				_candleRequested = true;
				GetFixedProfile(new FixedProfileRequest(Period));
			}
		}

		protected override void OnFixedProfilesResponse(IndicatorCandle fixedProfile, FixedProfilePeriods period)
		{
			_candle = fixedProfile;
			RedrawChart();
		}

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			if (_candle == null)
				return;

			var priceInfo = GetPriceVolumeInfo(_candle, Type);

			if (priceInfo == null)
				return;

			var y = ChartInfo.GetYByPrice(priceInfo.Price);
			var firstX = ChartInfo.PriceChartContainer.Region.Width - _length;
			var secondX = ChartInfo.PriceChartContainer.Region.Width;

			context.DrawLine(_renderPen, firstX, y, secondX, y);

			if (ShowText)
			{
				var size = context.MeasureString(_description, _font);

				var textRect = new Rectangle(new Point(ChartInfo.PriceChartContainer.Region.Width - size.Width - 20, y - size.Height - Width / 2),
					new Size(size.Width + 20, size.Height));

				context.SetTextRenderingHint(RenderTextRenderingHint.Aliased);
				context.DrawString(_description, _font, _textColor, textRect, _stringRightFormat);
				context.SetTextRenderingHint(RenderTextRenderingHint.AntiAlias);
			}

			this.DrawLabelOnPriceAxis(context, string.Format(ChartInfo.StringFormat, priceInfo.Price), y, _axisFont, _lineColor, _axisTextColor);
		}

		#endregion

		#region Private methods

		private string GetPeriodDescription(FixedProfilePeriods period)
		{
			switch (period)
			{
				case FixedProfilePeriods.CurrentDay:
					return "Current day";
				case FixedProfilePeriods.LastDay:
					return "Last day";
				case FixedProfilePeriods.CurrentWeek:
					return "Current week";
				case FixedProfilePeriods.LastWeek:
					return "Last week";
				case FixedProfilePeriods.CurrentMonth:
					return "Current month";
				case FixedProfilePeriods.LastMonth:
					return "Last month";
				case FixedProfilePeriods.Contract:
					return "Contract";
				default:
					throw new ArgumentOutOfRangeException(nameof(period), period, null);
			}
		}

		private PriceVolumeInfo GetPriceVolumeInfo(IndicatorCandle candle, MaxLevelType levelType)
		{
			switch (Type)
			{
				case MaxLevelType.Bid:
				{
					return _candle.MaxBidPriceInfo;
				}
				case MaxLevelType.Ask:
				{
					return _candle.MaxAskPriceInfo;
				}
				case MaxLevelType.PositiveDelta:
				{
					return _candle.MaxPositiveDeltaPriceInfo;
				}
				case MaxLevelType.NegativeDelta:
				{
					return _candle.MaxNegativeDeltaPriceInfo;
				}
				case MaxLevelType.Volume:
				{
					return _candle.MaxVolumePriceInfo;
				}
				case MaxLevelType.Tick:
				{
					return _candle.MaxTickPriceInfo;
				}
				case MaxLevelType.Time:
				{
					return _candle.MaxTimePriceInfo;
				}
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		#endregion
	}
}